using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using PPE_Detection_App.Api.Models;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PPE_Detection_App.Api.Services
{
    public class CameraStreamService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CameraStreamService> _logger;
        private readonly WebSocketManagerService _webSocketManager;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeCameras = new();
        private readonly List<string> _violationLabels = new List<string> { "NO-Gloves", "NO-Goggles", "NO-Hardhat", "NO-Mask", "NO-Safety Vest" };
        private readonly string _outputDirectory;
        private readonly Font _font;

        private readonly ConcurrentDictionary<string, DateTime> _lastDetectionTimestamps = new();
        private const int ViolationCooldownSeconds = 15;

        public CameraStreamService(IServiceProvider serviceProvider, ILogger<CameraStreamService> logger, IWebHostEnvironment env, WebSocketManagerService webSocketManager)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _webSocketManager = webSocketManager;
            _outputDirectory = Path.Combine(env.WebRootPath, "violations");
            if (!Directory.Exists(_outputDirectory))
                Directory.CreateDirectory(_outputDirectory);

            try
            {
                _font = SystemFonts.CreateFont("Arial", 16, FontStyle.Bold);
            }
            catch
            {
                _logger.LogWarning("Arial font not found. Falling back to a default system font.");
                _font = SystemFonts.Families.Any()
                    ? new Font(SystemFonts.Families.First(), 16)
                    : throw new Exception("No fonts found on the system. Cannot draw bounding boxes.");
            }
        }

        public bool IsProcessing(int cameraId) => _activeCameras.ContainsKey(cameraId);

        public void StartProcessing(int cameraId)
        {
            if (IsProcessing(cameraId))
            {
                _logger.LogWarning($"Processing for camera {cameraId} is already running.");
                return;
            }

            _lastDetectionTimestamps.Clear();

            var cts = new CancellationTokenSource();
            if (_activeCameras.TryAdd(cameraId, cts))
            {
                Task.Run(() => ProcessCameraFeed(cameraId, cts.Token), cts.Token);
                _logger.LogInformation($"Started processing for camera {cameraId}.");
            }
        }

        public void StopProcessing(int cameraId)
        {
            if (_activeCameras.TryRemove(cameraId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _logger.LogInformation($"Stopped processing for camera {cameraId}.");
            }
        }

        private async Task ProcessCameraFeed(int cameraId, CancellationToken cancellationToken)
        {
            using var capture = new VideoCapture(cameraId);
            if (!capture.IsOpened())
            {
                _logger.LogError($"Error: Could not open camera {cameraId}.");
                _activeCameras.TryRemove(cameraId, out _);
                return;
            }

            capture.FrameWidth = 1280;
            capture.FrameHeight = 720;

            using var frame = new Mat();
            using var rgbaFrame = new Mat();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    await Task.Delay(10, cancellationToken); 
                    continue;
                }

                Image<Rgba32>? imageForProcessing = null;
                try
                {
                    Cv2.CvtColor(frame, rgbaFrame, ColorConversionCodes.BGR2RGBA);
                    imageForProcessing = ConvertMatToImageSharp(rgbaFrame);

                    using var scope = _serviceProvider.CreateScope();
                    var yoloProcessor = scope.ServiceProvider.GetRequiredService<YoloV8Processor>();
                    var violationRepo = scope.ServiceProvider.GetRequiredService<ViolationRepository>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var faceService = scope.ServiceProvider.GetRequiredService<FaceRecognitionService>();
                    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                    var faceMatcherService = scope.ServiceProvider.GetRequiredService<FaceMatcherService>();

                    var detections = yoloProcessor.ProcessImage(imageForProcessing);
                    var allViolationDetections = detections.Where(d => _violationLabels.Contains(d.Label)).ToList();

                    var eligibleDetections = new List<DetectionResult>();
                    if (allViolationDetections.Any())
                    {
                        var now = DateTime.UtcNow;
                        foreach (var detection in allViolationDetections)
                        {
                            if (!_lastDetectionTimestamps.TryGetValue(detection.Label, out var lastTime) ||
                                (now - lastTime).TotalSeconds > ViolationCooldownSeconds)
                            {
                                eligibleDetections.Add(detection);
                                _lastDetectionTimestamps[detection.Label] = now;
                            }
                        }
                    }

                    // Vẽ TẤT CẢ các đối tượng (Người, Mũ, Kính...) để hiển thị trên stream giúp bạn dễ theo dõi
                    foreach (var detection in detections)
                    {
                        bool isViolation = _violationLabels.Contains(detection.Label);
                        bool isOnCooldown = isViolation && !eligibleDetections.Contains(detection);
                        DrawBoundingBox(imageForProcessing, detection, isViolation, isOnCooldown);
                    }

                    if (eligibleDetections.Any())
                    {
                        await HandleViolations(eligibleDetections, detections.ToList(), imageForProcessing, violationRepo, emailService, configuration, faceService, dbService, faceMatcherService);
                    }

                    if (_webSocketManager.GetConnectionCount() > 0)
                    {
                        var dataUri = ConvertImageToDataUri(imageForProcessing);
                        await _webSocketManager.BroadcastMessage(dataUri);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing frame from camera {cameraId}.");
                }
                finally
                {
                    imageForProcessing?.Dispose();
                }

                // Bỏ delay ở cuối vòng lặp để xử lý frame nhanh nhất có thể.
                // FPS sẽ được giới hạn bởi tốc độ xử lý và tốc độ của camera.
                await Task.Yield(); // Cho phép các tác vụ khác chạy
            }
        }

        private static Image<Rgba32> ConvertMatToImageSharp(Mat rgbaMat)
        {
            int width = rgbaMat.Width;
            int height = rgbaMat.Height;
            int stride = (int)rgbaMat.Step();
            int totalBytes = stride * height;

            var rawBytes = new byte[totalBytes];
            Marshal.Copy(rgbaMat.Data, rawBytes, 0, totalBytes);

            var image = new Image<Rgba32>(width, height);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var rowSpan = accessor.GetRowSpan(y);
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int offset = rowOffset + x * 4;
                        rowSpan[x] = new Rgba32(
                            r: rawBytes[offset],
                            g: rawBytes[offset + 1],
                            b: rawBytes[offset + 2],
                            a: rawBytes[offset + 3]
                        );
                    }
                }
            });

            return image;
        }

        private static string ConvertImageToDataUri(Image image)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 75 });
            var base64String = Convert.ToBase64String(ms.ToArray());
            return $"data:image/jpeg;base64,{base64String}";
        }

        private void DrawBoundingBox(Image image, DetectionResult detection, bool isViolation = false, bool isOnCooldown = false)
        {
            var box = detection.Box;
            var label = $"{detection.Label} ({detection.Confidence:P0})";
            
            // Màu Xanh lá cho đối tượng bình thường, Đỏ cho vi phạm mới, Vàng cho vi phạm đang cooldown
            var color = isViolation ? (isOnCooldown ? Color.Yellow : Color.Red) : Color.LimeGreen;
            var rect = new RectangleF((float)box.Left, (float)box.Top, (float)box.Width, (float)box.Height);

            image.Mutate(x =>
            {
                x.Draw(color, 2f, rect);

                var textSize = TextMeasurer.MeasureSize(label, new TextOptions(_font));
                var textLocation = new PointF(rect.Left, rect.Top - textSize.Height - 5);

                if (textLocation.Y < 0)
                    textLocation.Y = rect.Top + 5;

                var textBackground = new RectangleF(textLocation.X, textLocation.Y, textSize.Width + 4, textSize.Height + 2);
                x.Fill(Color.Black, textBackground);
                x.DrawText(label, _font, color, new PointF(textLocation.X + 2, textLocation.Y + 1));
            });
        }

        private async Task HandleViolations(List<DetectionResult> violations, List<DetectionResult> allDetections, Image<Rgba32> image, ViolationRepository repo, EmailService emailService, IConfiguration config, FaceRecognitionService faceService, DatabaseService dbService, FaceMatcherService faceMatcherService)
        {
            if (!violations.Any()) return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var randomSuffix = Path.GetRandomFileName().Split('.')[0].Substring(0, 4);
            var fileName = $"violation_{timestamp}_{randomSuffix}.jpg";
            var imagePath = Path.Combine(_outputDirectory, fileName);
            var relativePath = $"/violations/{fileName}";

            await image.SaveAsJpegAsync(imagePath);

            foreach (var detection in violations)
            {
                int? matchedEmployeeId = null;
                try
                {
                    // 1. Tìm BoundingBox của 'Person' chứa lỗi này (dựa vào tâm của lỗi)
                    var centerX = detection.Box.Left + detection.Box.Width / 2;
                    var centerY = detection.Box.Top + detection.Box.Height / 2;

                    bool hasFaceBox = false;
                    BoundingBox targetFaceBox = detection.Box; // Khởi tạo giá trị mặc định

                    foreach (var p in allDetections)
                    {
                        if (p.Label == "Person" && 
                            centerX >= p.Box.Left && centerX <= p.Box.Right && 
                            centerY >= p.Box.Top && centerY <= p.Box.Bottom)
                        {
                            // 2. Ước lượng vùng khuôn mặt: Lấy khoảng 25% phía trên của Person box
                            float headWidth = p.Box.Width * 0.4f;  // Chiều rộng đầu khoảng 40% chiều rộng người
                            float headHeight = p.Box.Height * 0.25f; // Chiều cao đầu khoảng 25% chiều cao người
                            float headX = p.Box.Left + (p.Box.Width - headWidth) / 2; 
                            float headY = p.Box.Top; 
                            
                            targetFaceBox = new BoundingBox(headX, headY, headWidth, headHeight);
                            hasFaceBox = true;
                            _logger.LogInformation($"[FaceRec] Tìm thấy 'Person' chứa lỗi {detection.Label}, đang cắt vùng đầu...");
                            break; // Tìm thấy Person chứa lỗi thì dừng loop luôn
                        }
                    }

                    if (!hasFaceBox && (detection.Label == "NO-Hardhat" || detection.Label == "NO-Mask" || detection.Label == "NO-Goggles"))
                    {
                        // Lỗi ở vùng đầu (không tìm thấy dáng người), lấy luôn box của lỗi làm Face Box
                        targetFaceBox = detection.Box;
                        hasFaceBox = true;
                        _logger.LogInformation($"[FaceRec] Không thấy 'Person', lấy box của lỗi {detection.Label} làm vùng khuôn mặt.");
                    }

                    // Nếu có vùng nghi ngờ là khuôn mặt thì mới cắt để chạy nhận diện
                    if (hasFaceBox)
                    {
                        int x = (int)Math.Max(0, targetFaceBox.Left);
                        int y = (int)Math.Max(0, targetFaceBox.Top);
                        int width = (int)Math.Min(image.Width - x, targetFaceBox.Width);
                        int height = (int)Math.Min(image.Height - y, targetFaceBox.Height);

                        if (width > 0 && height > 0)
                        {
                            var cropRect = new Rectangle(x, y, width, height);
                            using var cropImage = image.Clone(ctx => ctx.Crop(cropRect));
                            
                            // --- LƯU ẢNH CROP ĐỂ DEBUG XEM AI CÓ NHÌN THẤY MẶT KHÔNG ---
                            var debugCropPath = Path.Combine(_outputDirectory, $"debug_face_{timestamp}_{randomSuffix}.jpg");
                            await cropImage.SaveAsJpegAsync(debugCropPath);
                            _logger.LogInformation($"[FaceRec] Đã lưu ảnh khuôn mặt trích xuất tại: /violations/debug_face_{timestamp}_{randomSuffix}.jpg");

                            using var rgbCrop = cropImage.CloneAs<Rgb24>();
                            
                            // Trích xuất vector đặc trưng
                            var embedding = faceService.GetFaceEmbedding(rgbCrop);
                            
                            // Gọi "Tổ đội trưởng" FaceMatcherService để xác định nhân viên
                            matchedEmployeeId = await faceMatcherService.IdentifyEmployeeAsync(embedding);
                        }
                    }
                    else 
                    {
                        _logger.LogWarning($"[FaceRec] Không xác định được vùng đầu cho lỗi {detection.Label}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Lỗi trích xuất khuôn mặt: {ex.Message}");
                }

                var log = new ViolationLog
                {
                    Category_Id = detection.Label,
                    Image_Path = relativePath,
                    Confidence_Score = detection.Confidence,
                    Box_X = detection.Box.Left,
                    Box_Y = detection.Box.Top,
                    Box_W = detection.Box.Width,
                    Box_H = detection.Box.Height,
                    Detected_Time = DateTime.Now,
                    Status = 0,
                    Employee_Id = matchedEmployeeId // <--- Lưu ID thực tế bắt được
                };
                await repo.InsertViolationLogAsync(log);
            }

            _logger.LogInformation($"{violations.Count} new violations logged. Image saved to {imagePath}");

            try 
            {
                string adminEmail = config["EmailSettings:AdminEmail"] ?? "vun197276@gmail.com"; 
                emailService.SendViolationEmail(adminEmail, imagePath, $"Camera Detection (ID: {violations.First().Label})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert email.");
            }
        }

        public void Dispose()
        {
            foreach (var key in _activeCameras.Keys.ToList())
                StopProcessing(key);
        }
    }
}
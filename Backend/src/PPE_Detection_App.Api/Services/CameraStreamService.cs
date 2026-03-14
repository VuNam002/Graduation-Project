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
                    await Task.Delay(10, cancellationToken); // Chờ một chút nếu không có frame
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
                        await HandleViolations(eligibleDetections, imageForProcessing, violationRepo, emailService, configuration);
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

        private async Task HandleViolations(List<DetectionResult> detections, Image<Rgba32> image, ViolationRepository repo, EmailService emailService, IConfiguration config)
        {
            if (!detections.Any()) return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var randomSuffix = Path.GetRandomFileName().Split('.')[0].Substring(0, 4);
            var fileName = $"violation_{timestamp}_{randomSuffix}.jpg";
            var imagePath = Path.Combine(_outputDirectory, fileName);
            var relativePath = $"/violations/{fileName}";

            await image.SaveAsJpegAsync(imagePath);

            foreach (var detection in detections)
            {
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
                    Status = 0
                };
                await repo.InsertViolationLogAsync(log);
            }

            _logger.LogInformation($"{detections.Count} new violations logged. Image saved to {imagePath}");

            try 
            {
                string adminEmail = config["EmailSettings:AdminEmail"] ?? "vun197276@gmail.com"; 
                emailService.SendViolationEmail(adminEmail, imagePath, $"Camera Detection (ID: {detections.First().Label})");
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
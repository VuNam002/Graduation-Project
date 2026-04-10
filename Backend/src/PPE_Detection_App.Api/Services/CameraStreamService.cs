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
            
            DateTime lastConfigCheck = DateTime.MinValue;
            float currentConf = YoloV8Processor.DefaultConfidenceThreshold;
            float currentNms = YoloV8Processor.DefaultNmsThreshold;

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

                    // Cập nhật cấu hình từ DB mỗi 10 giây thay vì truy vấn liên tục mỗi khung hình
                    if ((DateTime.UtcNow - lastConfigCheck).TotalSeconds > 10)
                    {
                        var confStr = await dbService.GetSystemConfigAsync("ConfidenceThreshold");
                        if (float.TryParse(confStr, out float parsedConf))
                        {
                            currentConf = parsedConf;
                        }

                        var nmsStr = await dbService.GetSystemConfigAsync("NmsThreshold");
                        if (float.TryParse(nmsStr, out float parsedNms))
                        {
                            currentNms = parsedNms;
                        }
                        
                        lastConfigCheck = DateTime.UtcNow;
                    }

                    var detections = yoloProcessor.ProcessImageWithThresholds(imageForProcessing, currentConf, currentNms);
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

                    var drawnTextRects = new List<RectangleF>();
                    foreach (var detection in detections)
                    {
                        bool isViolation = _violationLabels.Contains(detection.Label);
                        bool isOnCooldown = isViolation && !eligibleDetections.Contains(detection);
                        DrawBoundingBox(imageForProcessing, detection, isViolation, isOnCooldown, drawnTextRects);
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

                await Task.Yield(); 
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

        private void DrawBoundingBox(Image image, DetectionResult detection, bool isViolation, bool isOnCooldown, List<RectangleF> drawnTextRects)
        {
            var box = detection.Box;
            var label = $"{detection.Label} ({detection.Confidence:P0})";
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
                
                // Cơ chế thông minh: Tránh việc các nhãn chữ (label) bị in đè lên nhau
                bool isOverlapping = true;
                int attempts = 0;
                while (isOverlapping && attempts < 10)
                {
                    isOverlapping = false;
                    foreach (var drawnRect in drawnTextRects)
                    {
                        if (textBackground.IntersectsWith(drawnRect))
                        {
                            isOverlapping = true;
                            textLocation.Y -= (textSize.Height + 2); 
                            if (textLocation.Y < 0)
                            {
                                textLocation.Y = rect.Top + 5 + (attempts + 1) * (textSize.Height + 2); 
                            }
                            textBackground.Y = textLocation.Y;
                            break;
                        }
                    }
                    attempts++;
                }

                drawnTextRects.Add(textBackground);

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
            var detectedEmployeeNames = new HashSet<string>();
            var groupedViolations = new List<List<DetectionResult>>();

            foreach (var v in violations)
            {
                bool isAddedToGroup = false;
                var vCx = v.Box.Left + v.Box.Width / 2; 
                var vCy = v.Box.Top + v.Box.Height / 2; 

                foreach (var group in groupedViolations)
                {
                    var baseBox = group.First().Box;
                    var bCx = baseBox.Left + baseBox.Width / 2;
                    var bCy = baseBox.Top + baseBox.Height / 2;
                    var distance = Math.Sqrt(Math.Pow(vCx - bCx, 2) + Math.Pow(vCy - bCy, 2));
                    if (distance < Math.Max(baseBox.Width, baseBox.Height) * 3) 
                    {
                        group.Add(v);
                        isAddedToGroup = true;
                        break;
                    }
                }
                if (!isAddedToGroup)
                {
                    groupedViolations.Add(new List<DetectionResult> { v });
                }
            }
            foreach (var group in groupedViolations)
            {
                var primaryDetection = group.First(); 
                string combinedLabels = string.Join(", ", group.Select(g => g.Label).Distinct());
                float avgConfidence = group.Average(g => g.Confidence);
                float minX = group.Min(g => g.Box.Left);
                float minY = group.Min(g => g.Box.Top);
                float maxX = group.Max(g => g.Box.Right);
                float maxY = group.Max(g => g.Box.Bottom);

                int? matchedEmployeeId = null;
                try
                {
                    var centerX = primaryDetection.Box.Left + primaryDetection.Box.Width / 2;
                    var centerY = primaryDetection.Box.Top + primaryDetection.Box.Height / 2;

                    bool hasFaceBox = false;
                    BoundingBox targetFaceBox = primaryDetection.Box;

                    foreach (var p in allDetections)
                    {
                        if (p.Label == "Person" &&
                            centerX >= p.Box.Left && centerX <= p.Box.Right &&
                            centerY >= p.Box.Top && centerY <= p.Box.Bottom)
                        {
                            float headWidth = p.Box.Width * 0.4f;
                            float headHeight = p.Box.Height * 0.25f;
                            float headX = p.Box.Left + (p.Box.Width - headWidth) / 2;
                            float headY = p.Box.Top;

                            targetFaceBox = new BoundingBox(headX, headY, headWidth, headHeight);
                            hasFaceBox = true;
                            _logger.LogInformation($"[FaceRec] Tim thay 'Person' chua loi {combinedLabels}, đang cat vung dau...");
                            break;
                        }
                    }

                    if (!hasFaceBox && (primaryDetection.Label == "NO-Hardhat" || primaryDetection.Label == "NO-Mask" || primaryDetection.Label == "NO-Goggles"))
                    {
                        float estHeadWidth = primaryDetection.Box.Width * 2.8f;
                        float estHeadHeight = primaryDetection.Box.Height * 2.8f;
                        float estCx = primaryDetection.Box.Left + primaryDetection.Box.Width / 2f;
                        float estCy = primaryDetection.Box.Top + primaryDetection.Box.Height / 2f;

                        if (primaryDetection.Label == "NO-Mask")
                            estCy -= primaryDetection.Box.Height * 0.8f;
                        else if (primaryDetection.Label == "NO-Hardhat")
                            estCy += primaryDetection.Box.Height * 0.8f;

                        targetFaceBox = new BoundingBox(estCx - estHeadWidth / 2f, estCy - estHeadHeight / 2f, estHeadWidth, estHeadHeight);
                        hasFaceBox = true;
                        _logger.LogInformation($"[FaceRec] Khong thay 'Person', mo rong chuc nang vung dau de cum loi {combinedLabels}.");
                    }

                    if (hasFaceBox)
                    {
                        float cx = targetFaceBox.Left + targetFaceBox.Width / 2f;
                        float cy = targetFaceBox.Top + targetFaceBox.Height / 2f;

                        float squareSize = Math.Max(targetFaceBox.Width, targetFaceBox.Height) * 1.2f;

                        int x = (int)Math.Max(0, cx - squareSize / 2f);
                        int y = (int)Math.Max(0, cy - squareSize / 2f);
                        int width = (int)Math.Min(image.Width - x, squareSize);
                        int height = (int)Math.Min(image.Height - y, squareSize);

                        if (width > 0 && height > 0)
                        {
                            var cropRect = new Rectangle(x, y, width, height);
                            using var cropImage = image.Clone(ctx => ctx.Crop(cropRect));

                            var debugCropPath = Path.Combine(_outputDirectory, $"debug_face_{timestamp}_{randomSuffix}.jpg");
                            await cropImage.SaveAsJpegAsync(debugCropPath);

                            using var rgbCrop = cropImage.CloneAs<Rgb24>();
                            var embedding = faceService.GetFaceEmbedding(rgbCrop);
                            var matchResult = await faceMatcherService.IdentifyEmployeeAsync(embedding);
                            if (matchResult.Id != null)
                            {
                                matchedEmployeeId = matchResult.Id;
                                if (!string.IsNullOrEmpty(matchResult.Name))
                                {
                                    detectedEmployeeNames.Add(matchResult.Name);
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"[FaceRec] khong xac dinh vung dau cho nhom loi {combinedLabels}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Loi trich xuat khuon mat: {ex.Message}");
                }

                var distinctLabels = group.Select(g => g.Label).Distinct().ToList();
                foreach (var label in distinctLabels)
                {
                    var specificDetection = group.First(g => g.Label == label);
                    var log = new ViolationLog
                    {
                        Category_Id = label, 
                        Image_Path = relativePath,
                        Confidence_Score = specificDetection.Confidence,
                        Box_X = specificDetection.Box.Left,
                        Box_Y = specificDetection.Box.Top,
                        Box_W = specificDetection.Box.Width,
                        Box_H = specificDetection.Box.Height,
                        Detected_Time = DateTime.Now,
                        Status = 0,
                        Employee_Id = matchedEmployeeId
                    };
                    await repo.InsertViolationLogAsync(log);
                }
            }

            _logger.LogInformation($"{groupedViolations.Count} nhom vi pham da duoc ghi nhan. Anh luu lai {imagePath}");

            try
            {
                string adminEmail = config["EmailSettings:AdminEmail"] ?? "vun197276@gmail.com";
                string namesStr = detectedEmployeeNames.Any() ? string.Join(", ", detectedEmployeeNames) : "Khong xac ding";
                string allLabels = string.Join(" | ", groupedViolations.Select(g => string.Join(",", g.Select(x => x.Label).Distinct())));
                emailService.SendViolationEmail(adminEmail, imagePath, $"Camera Detection (Loi: {allLabels})", namesStr);
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
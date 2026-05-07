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

        private readonly List<string> _violationLabels = new List<string>
        {
            "Fall-Detected", "Gloves", "Goggles", "Hardhat", "Ladder",
            "NO-Gloves", "NO-Goggles", "NO-Hardhat", "NO-Mask", "NO-Safety Vest",
            "Person", "Safety Cone", "Safety Vest"
        };

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

            Mat latestFrame = new Mat();
            object frameLock = new object();
            bool isNewFrameAvailable = false;
            bool isProcessing = false;

            DateTime lastConfigCheck = DateTime.MinValue;
            float currentConf = YoloV8Processor.DefaultConfidenceThreshold;
            float currentNms = YoloV8Processor.DefaultNmsThreshold;
            string currentModel = "YOLOv8";

            var captureTask = Task.Run(() =>
            {
                using var tempFrame = new Mat();
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (capture.Read(tempFrame) && !tempFrame.Empty())
                    {
                        lock (frameLock)
                        {
                            tempFrame.CopyTo(latestFrame);
                            isNewFrameAvailable = true;
                        }
                    }
                    else
                    {
                        Task.Delay(100, cancellationToken).Wait(cancellationToken);
                    }
                }
                _logger.LogInformation($"Luồng đọc camera {cameraId} đã dừng.");
            }, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                Mat? frameToProcess = null;
                lock (frameLock)
                {
                    if (isNewFrameAvailable && !isProcessing)
                    {
                        frameToProcess = latestFrame.Clone();
                        isNewFrameAvailable = false;
                        isProcessing = true;
                    }
                }

                if (frameToProcess != null)
                {
                    Image<Rgba32>? imageForProcessing = null;
                    try
                    {
                        using var rgbaFrame = new Mat();
                        Cv2.CvtColor(frameToProcess, rgbaFrame, ColorConversionCodes.BGR2RGBA);
                        imageForProcessing = ConvertMatToImageSharp(rgbaFrame);

                        using var scope = _serviceProvider.CreateScope();
                        var yoloProcessor = scope.ServiceProvider.GetRequiredService<YoloV8Processor>();
                        var violationRepo = scope.ServiceProvider.GetRequiredService<ViolationRepository>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        var faceService = scope.ServiceProvider.GetRequiredService<FaceRecognitionService>();
                        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                        if ((DateTime.UtcNow - lastConfigCheck).TotalSeconds > 10)
                        {
                            var confStr = await dbService.GetSystemConfigAsync("ConfidenceThreshold");
                            if (float.TryParse(confStr, out float parsedConf)) currentConf = parsedConf;

                            var nmsStr = await dbService.GetSystemConfigAsync("NmsThreshold");
                            if (float.TryParse(nmsStr, out float parsedNms)) currentNms = parsedNms;

                            var modelStr = await dbService.GetSystemConfigAsync("ActiveModel");
                            if (!string.IsNullOrEmpty(modelStr)) currentModel = modelStr;

                            _logger.LogInformation($"[AI Config] Đang dùng Model: {currentModel} | Conf: {currentConf} | NMS: {currentNms}");
                            lastConfigCheck = DateTime.UtcNow;
                        }

                        var allDetections = yoloProcessor.ProcessImageWithThresholds(imageForProcessing, currentConf, currentNms, currentModel).ToList();

                        // --- START Tăng cường nhận diện PPE bị thiếu ---
                        // Nếu AI chỉ bắt được Person mà không có box PPE đè lên, tự động nội suy ra lỗi không mặc PPE
                        var persons = allDetections.Where(d => d.Label == "Person").ToList();
                        var requiredPPEs = new[] { 
                            ("Hardhat", "NO-Hardhat"), 
                            ("Safety Vest", "NO-Safety Vest"),
                            ("Mask", "NO-Mask")
                        };
                        
                        var inferredDetections = new List<DetectionResult>();

                        foreach (var person in persons)
                        {
                            foreach (var (posLabel, negLabel) in requiredPPEs)
                            {
                                bool hasPPE = allDetections.Any(d => 
                                    (d.Label == posLabel || d.Label == negLabel) && 
                                    !(d.Box.Left > person.Box.Right || 
                                      d.Box.Right < person.Box.Left || 
                                      d.Box.Top > person.Box.Bottom || 
                                      d.Box.Bottom < person.Box.Top));

                                if (!hasPPE)
                                {
                                    float estX = (float)person.Box.Left;
                                    float estY = (float)person.Box.Top;
                                    float estW = (float)person.Box.Width;
                                    float estH = (float)person.Box.Height;
                                    
                                    if (negLabel == "NO-Hardhat")
                                    {
                                        estH = estH * 0.2f;
                                    }
                                    else if (negLabel == "NO-Mask")
                                    {
                                        estY = estY + estH * 0.1f;
                                        estH = estH * 0.2f;
                                    }
                                    else if (negLabel == "NO-Safety Vest")
                                    {
                                        estY = estY + estH * 0.2f;
                                        estH = estH * 0.5f;
                                    }

                                    var inferredBox = new BoundingBox(estX, estY, estW, estH);
                                    inferredDetections.Add(new DetectionResult(negLabel, person.Confidence * 0.8f, inferredBox));
                                }
                            }
                        }
                        
                        if (inferredDetections.Any())
                        {
                            allDetections.AddRange(inferredDetections);
                        }
                        // --- END Tăng cường nhận diện PPE bị thiếu ---

                        var allViolationDetections = allDetections.Where(d => _violationLabels.Contains(d.Label)).ToList();

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
                        foreach (var detection in allViolationDetections)
                        {
                            bool isOnCooldown = !eligibleDetections.Contains(detection);
                            DrawBoundingBox(imageForProcessing, detection, isViolation: true, isOnCooldown: isOnCooldown, drawnTextRects);
                        }

                        if (eligibleDetections.Any())
                        {
                            await HandleViolations(eligibleDetections, allDetections, imageForProcessing, violationRepo, emailService, configuration, faceService, dbService);
                        }

                        if (_webSocketManager.GetConnectionCount() > 0)
                        {
                            var dataUri = ConvertImageToDataUri(imageForProcessing);
                            await _webSocketManager.BroadcastMessage(dataUri);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Lỗi khi xử lý frame từ camera {cameraId}.");
                    }
                    finally
                    {
                        imageForProcessing?.Dispose();
                        frameToProcess.Dispose();
                        isProcessing = false;
                    }
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }

            _logger.LogInformation($"Luồng xử lý AI cho camera {cameraId} đã dừng.");
            await captureTask;
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
            var color = isOnCooldown ? Color.Yellow : Color.Red;
            var rect = new RectangleF((float)box.Left, (float)box.Top, (float)box.Width, (float)box.Height);

            image.Mutate(x =>
            {
                x.Draw(color, 2f, rect);

                var textSize = TextMeasurer.MeasureSize(label, new TextOptions(_font));
                var textLocation = new PointF(rect.Left, rect.Top - textSize.Height - 5);

                if (textLocation.Y < 0)
                    textLocation.Y = rect.Top + 5;

                var textBackground = new RectangleF(textLocation.X, textLocation.Y, textSize.Width + 4, textSize.Height + 2);

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
                                textLocation.Y = rect.Top + 5 + (attempts + 1) * (textSize.Height + 2);
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

        private async Task HandleViolations(
            List<DetectionResult> violations,
            List<DetectionResult> allDetections,
            Image<Rgba32> image,
            ViolationRepository repo,
            EmailService emailService,
            IConfiguration config,
            FaceRecognitionService faceService,
            DatabaseService dbService)
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
                    groupedViolations.Add(new List<DetectionResult> { v });
            }

            foreach (var group in groupedViolations)
            {
                var primaryDetection = group.First();
                string combinedLabels = string.Join(", ", group.Select(g => g.Label).Distinct());

                int? matchedEmployeeId = null;
                try
                {
                    var centerX = primaryDetection.Box.Left + primaryDetection.Box.Width / 2;
                    var centerY = primaryDetection.Box.Top + primaryDetection.Box.Height / 2;

                    BoundingBox targetBox = primaryDetection.Box;
                    bool hasTargetBox = false;

                    foreach (var p in allDetections)
                    {
                        if (p.Label == "Person" &&
                            centerX >= p.Box.Left && centerX <= p.Box.Right &&
                            centerY >= p.Box.Top && centerY <= p.Box.Bottom)
                        {
                            targetBox = p.Box;
                            hasTargetBox = true;
                            _logger.LogInformation($"[FaceRec] Tìm thấy 'Person' chứa lỗi {combinedLabels}, dùng toàn bộ box người.");
                            break;
                        }
                    }

                    if (!hasTargetBox)
                    {
                        float estCx = primaryDetection.Box.Left + primaryDetection.Box.Width / 2f;
                        float estCy = primaryDetection.Box.Top + primaryDetection.Box.Height / 2f;
                        float estBodyWidth = primaryDetection.Box.Width * 3.0f;
                        float estBodyHeight = primaryDetection.Box.Height * 4.0f;

                        switch (primaryDetection.Label)
                        {
                            case "NO-Hardhat":
                                estCy += primaryDetection.Box.Height * 2.0f;
                                break;

                            case "NO-Mask":
                            case "NO-Goggles":
                                estCy += primaryDetection.Box.Height * 1.0f;
                                break;

                            case "NO-Safety Vest":
   
                                break;

                            case "NO-Gloves":
                                estCy -= primaryDetection.Box.Height * 1.0f;
                                break;

                            case "Fall-Detected":
                                estBodyWidth = primaryDetection.Box.Width * 2.0f;
                                estBodyHeight = primaryDetection.Box.Height * 2.0f;
                                break;
                        }

                        targetBox = new BoundingBox(
                            estCx - estBodyWidth / 2f,
                            estCy - estBodyHeight / 2f,
                            estBodyWidth,
                            estBodyHeight
                        );
                        hasTargetBox = true;
                        _logger.LogInformation($"[FaceRec] Không tìm thấy 'Person', ước tính toàn thân cho lỗi {combinedLabels}.");
                    }

                    float squareSize = Math.Max(targetBox.Width, targetBox.Height);
                    float cropCx = targetBox.Left + targetBox.Width / 2f;
                    float cropCy = targetBox.Top + targetBox.Height / 2f;

                    int cropX = (int)Math.Max(0, cropCx - squareSize / 2f);
                    int cropY = (int)Math.Max(0, cropCy - squareSize / 2f);
                    int cropW = (int)Math.Min(image.Width - cropX, squareSize);
                    int cropH = (int)Math.Min(image.Height - cropY, squareSize);

                    if (cropW > 0 && cropH > 0)
                    {
                        var cropRect = new Rectangle(cropX, cropY, cropW, cropH);
                        using var cropImage = image.Clone(ctx => ctx.Crop(cropRect));

                        image.Mutate(ctx =>
                        {
                            ctx.Draw(Color.Cyan, 3f, cropRect);
                            ctx.DrawText("Body-Crop AI", _font, Color.Cyan, new PointF(cropX, Math.Max(0, cropY - 20)));
                        });

                        var debugCropPath = Path.Combine(_outputDirectory, $"debug_body_{timestamp}_{randomSuffix}.jpg");
                        await cropImage.SaveAsJpegAsync(debugCropPath);

                        using var rgbCrop = cropImage.CloneAs<Rgb24>();
                        var embedding = faceService.GetFaceEmbedding(rgbCrop);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[FaceRec] Lỗi trích xuất khuôn mặt: {ex.Message}");
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

            _logger.LogInformation($"{groupedViolations.Count} nhóm vi phạm đã được ghi nhận. Ảnh lưu tại {imagePath}");

            try
            {
                string adminEmail = config["EmailSettings:AdminEmail"] ?? "vun197276@gmail.com";
                string namesStr = detectedEmployeeNames.Any() ? string.Join(", ", detectedEmployeeNames) : "Không xác định";
                string allLabels = string.Join(" | ", groupedViolations.Select(g => string.Join(", ", g.Select(x => x.Label).Distinct())));
                emailService.SendViolationEmail(adminEmail, imagePath, $"Camera Detection (Lỗi: {allLabels})", namesStr);
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
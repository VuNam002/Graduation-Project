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
            "NO-Hardhat", "NO-Mask", "NO-Safety Vest", "Fall-Detected",
            "NO-Gloves", "NO-Goggles", "NO_helmet", "NO_Vest", "NO_goggles", "No_SafetyShoes", "Slippers"
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

                        using var cleanImageForSave = imageForProcessing.Clone();

                        var drawnTextRects = new List<RectangleF>();
                        foreach (var detection in allDetections)
                        {
                            bool isViolation = _violationLabels.Contains(detection.Label);
                            bool isOnCooldown = isViolation && !eligibleDetections.Contains(detection);
                            DrawBoundingBox(imageForProcessing, detection, isViolation: isViolation, isOnCooldown: isOnCooldown, drawnTextRects);
                        }

                        if (eligibleDetections.Any())
                        {
                            await HandleViolations(eligibleDetections, allDetections, cleanImageForSave, violationRepo, emailService, configuration, faceService, dbService);
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
            var color = isViolation ? (isOnCooldown ? Color.Yellow : Color.Red) : Color.SpringGreen;
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
            Image<Rgba32> cleanImage,
            ViolationRepository repo,
            EmailService emailService,
            IConfiguration config,
            FaceRecognitionService faceService,
            DatabaseService dbService)
        {
            if (!violations.Any()) return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
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

            using var annotatedFullImage = cleanImage.Clone();
            var globalTextRects = new List<RectangleF>();
            foreach (var d in allDetections)
            {
                DrawBoundingBox(annotatedFullImage, d, isViolation: _violationLabels.Contains(d.Label), isOnCooldown: false, globalTextRects);
            }

            foreach (var group in groupedViolations)
            {
                string combinedLabels = string.Join(", ", group.Select(g => g.Label).Distinct());
                int? matchedEmployeeId = null;
                string relativePath = "";
                try
                {
                    float minX = group.Min(g => g.Box.Left);
                    float minY = group.Min(g => g.Box.Top);
                    float maxX = group.Max(g => g.Box.Right);
                    float maxY = group.Max(g => g.Box.Bottom);
                    float cx = (minX + maxX) / 2f;
                    float cy = (minY + maxY) / 2f;

                    var personBox = allDetections
                        .Where(d => d.Label == "Person")
                        .FirstOrDefault(p => cx >= p.Box.Left && cx <= p.Box.Right && cy >= p.Box.Top && cy <= p.Box.Bottom)?.Box;

                    float cropX, cropY, cropR, cropB;
                    if (personBox != null)
                    {
                        float expandX = (float)personBox.Width * 0.2f;
                        float expandY = (float)personBox.Height * 0.2f;
                        cropX = (float)personBox.Left - expandX;
                        cropY = (float)personBox.Top - expandY;
                        cropR = (float)personBox.Right + expandX;
                        cropB = (float)personBox.Bottom + expandY;
                    }
                    else
                    {
                        float width = maxX - minX;
                        float height = maxY - minY;
                        float expandX = Math.Max(width * 1.5f, cleanImage.Width * 0.15f);
                        float expandY = Math.Max(height * 2.0f, cleanImage.Height * 0.25f);
                        cropX = minX - expandX;
                        cropY = minY - expandY;
                        cropR = maxX + expandX;
                        cropB = maxY + expandY;
                    }

                    int finalCropX = (int)Math.Max(0, cropX);
                    int finalCropY = (int)Math.Max(0, cropY);
                    int finalCropR = (int)Math.Min(cleanImage.Width, cropR);
                    int finalCropB = (int)Math.Min(cleanImage.Height, cropB);
                    int finalCropW = finalCropR - finalCropX;
                    int finalCropH = finalCropB - finalCropY;

                    if (finalCropW > 0 && finalCropH > 0)
                    {
                        var cropRect = new Rectangle(finalCropX, finalCropY, finalCropW, finalCropH);
                        using var groupImage = annotatedFullImage.Clone(ctx => ctx.Crop(cropRect));

                        var randomSuffix = Path.GetRandomFileName().Split('.')[0].Substring(0, 4);
                        var fileName = $"violation_{timestamp}_{randomSuffix}.jpg";
                        var imagePath = Path.Combine(_outputDirectory, fileName);
                        relativePath = $"/violations/{fileName}";

                        await groupImage.SaveAsJpegAsync(imagePath);

                        try
                        {
                            string adminEmail = config["EmailSettings:AdminEmail"] ?? "vun197276@gmail.com";
                            emailService.SendViolationEmail(adminEmail, imagePath, $"Camera Detection (Lỗi: {combinedLabels})", "Không xác định");
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error processing crop: {ex.Message}");
                }

                if (string.IsNullOrEmpty(relativePath)) continue;

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
        }

        public void Dispose()
        {
            foreach (var key in _activeCameras.Keys.ToList())
                StopProcessing(key);
        }
    }
}
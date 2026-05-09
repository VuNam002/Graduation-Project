using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;
using PPE_Detection_App.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectionController : ControllerBase
    {
        private readonly YoloV8Processor _processor;
        private readonly DatabaseService _dbService;
        private readonly IWebHostEnvironment _env;
        private readonly FaceRecognitionService _faceService;

        public DetectionController(YoloV8Processor processor, DatabaseService dbService, IWebHostEnvironment env, FaceRecognitionService faceService)
        {
            _processor = processor;
            _dbService = dbService;
            _env = env;
            _faceService = faceService;
        }

        [HttpGet("health")]
        public IActionResult HealthCheck() => Ok(new { status = "healthy", classes = _processor.GetClassLabels() });

        [HttpGet("classes")]
        public IActionResult GetClasses() => Ok(new { totalClasses = _processor.GetClassLabels().Length, classes = _processor.GetClassLabels() });

        [HttpPost("detect")]
        public async Task<IActionResult> DetectObjects(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { success = false, error = "No image file found" });

            try
            {
                using var stream = file.OpenReadStream();
                using var image = await Image.LoadAsync(stream);

                var detections = _processor.ProcessImage(image).ToList();

                var targetViolations = new[] { 
                    "NO-Hardhat", "NO-Mask", "Fall-Detected",
                    "NO-Gloves", "NO-Goggles", "NO_Vest", "NO_goggles" 
                };

                var safetyIssues = detections.Where(d => targetViolations.Contains(d.Label)).ToList();
                var equipment = detections.Where(d => !d.Label.StartsWith("NO-") && !d.Label.StartsWith("NO_", StringComparison.OrdinalIgnoreCase) && d.Label != "Person" && d.Label != "Fall-Detected" && !targetViolations.Contains(d.Label)).ToList();
                var persons = detections.Where(d => d.Label == "Person").ToList();
                var falls = detections.Where(d => d.Label == "Fall-Detected").ToList();

                var allViolations = new List<DetectionResult>(safetyIssues);

                var savedViolations = new List<string>();
                var validCategories = await _dbService.GetAllCategoriesAsync();

                if (allViolations.Any())
                {
                    string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string uploadFolder = Path.Combine(webRootPath, "violations");

                    if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                    string fileName = $"violation_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 4)}.jpg";
                    string fullPath = Path.Combine(uploadFolder, fileName);
                    string dbImagePath = $"/violations/{fileName}";

                    await image.SaveAsJpegAsync(fullPath);
                    
                    var validCategoryIds = new HashSet<string>(validCategories.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);
                    foreach (var issue in allViolations)
                    {
                        if (validCategoryIds.Contains(issue.Label))
                        {
                            int? matchedEmployeeId = null;
                            try
                            {
                                float targetWidth = issue.Box.Width;
                                float targetHeight = issue.Box.Height;
                                float cx = issue.Box.X + issue.Box.Width / 2f;
                                float cy = issue.Box.Y + issue.Box.Height / 2f;

                                // Ước lượng lại vùng đầu nếu Box chỉ bao quanh một phần nhỏ (Khẩu trang / Mũ)
                                if (issue.Label == "NO-Mask" || issue.Label == "NO-Hardhat" || issue.Label == "NO-Goggles")
                                {
                                    targetWidth *= 2.8f;
                                    targetHeight *= 2.8f;
                                    
                                    if (issue.Label == "NO-Mask") cy -= issue.Box.Height * 0.8f;
                                    else if (issue.Label == "NO-Hardhat") cy += issue.Box.Height * 0.8f;
                                }
                                
                                float squareSize = Math.Max(targetWidth, targetHeight) * 1.2f;

                                int x = (int)Math.Max(0, cx - squareSize / 2f);
                                int y = (int)Math.Max(0, cy - squareSize / 2f);
                                int width = (int)Math.Min(image.Width - x, squareSize);
                                int height = (int)Math.Min(image.Height - y, squareSize);

                                if (width > 0 && height > 0)
                                {
                                    var cropRect = new Rectangle(x, y, width, height);
                                    using var cropImage = image.Clone(ctx => ctx.Crop(cropRect));
                                    using var rgbCrop = cropImage.CloneAs<Rgb24>();
                                    var embedding = _faceService.GetFaceEmbedding(rgbCrop);
                                }
                            }
                            catch
                            {
                                // Bỏ qua lỗi nếu trích xuất/nhận diện khuôn mặt thất bại, tiếp tục lưu vi phạm
                            }

                            var log = new ViolationLog
                            {
                                Category_Id = issue.Label,
                                Image_Path = dbImagePath,
                                Confidence_Score = issue.Confidence,
                                Box_X = issue.Box.X,
                                Box_Y = issue.Box.Y,
                                Box_W = issue.Box.Width,
                                Box_H = issue.Box.Height,
                                Detected_Time = DateTime.Now,
                                Status = 0,
                                Employee_Id = matchedEmployeeId
                            };

                            await _dbService.InsertViolationLogAsync(log);
                            savedViolations.Add(issue.Label);
                        }

                    }
                }

                return Ok(new
                {
                    success = true,
                    fileName = file.FileName,
                    summary = new
                    {
                        totalDetections = detections.Count,
                        personsDetected = persons.Count,
                        safetyIssuesDetected = safetyIssues.Count,
                        equipmentDetected = equipment.Count,
                        fallsDetected = falls.Count
                    },
                    detections = detections.Select(d => new
                    {
                        label = d.Label,
                        confidence = Math.Round(d.Confidence * 100, 2),
                        isSafetyIssue = d.Label.StartsWith("NO-") || d.Label == "Fall-Detected",
                        isFallDetected = d.Label == "Fall-Detected",
                        box = new { x = Math.Round(d.Box.X, 2), y = Math.Round(d.Box.Y, 2), width = Math.Round(d.Box.Width, 2), height = Math.Round(d.Box.Height, 2) }
                    }).OrderByDescending(d => d.confidence),
                    processedAt = DateTime.UtcNow,
                    debug_allViolationLabels = allViolations.Select(v => v.Label).ToList(),
                    debug_availableCategories = validCategories.Select(c => c.Id).ToList(),
                    debug_savedViolationLabels = savedViolations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message, title = "Image processing failed" });
            }
        }
    }
}
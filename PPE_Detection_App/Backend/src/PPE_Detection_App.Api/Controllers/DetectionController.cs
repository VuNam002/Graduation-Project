using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;
using PPE_Detection_App.Api.Models;
using SixLabors.ImageSharp;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectionController : ControllerBase
    {
        private readonly YoloV8Processor _processor;
        private readonly DatabaseService _dbService;
        private readonly IWebHostEnvironment _env;

        public DetectionController(YoloV8Processor processor, DatabaseService dbService, IWebHostEnvironment env)
        {
            _processor = processor;
            _dbService = dbService;
            _env = env;
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

                var safetyIssues = detections.Where(d => d.Label.StartsWith("NO-")).ToList();
                var equipment = detections.Where(d => !d.Label.StartsWith("NO-") && d.Label != "Person" && d.Label != "Fall-Detected").ToList();
                var persons = detections.Where(d => d.Label == "Person").ToList();
                var falls = detections.Where(d => d.Label == "Fall-Detected").ToList();

                var allViolations = new List<DetectionResult>();
                allViolations.AddRange(safetyIssues);
                allViolations.AddRange(falls);

                if (allViolations.Any())
                {
                    string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string uploadFolder = Path.Combine(webRootPath, "violations");

                    if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                    string fileName = $"violation_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 4)}.jpg";
                    string fullPath = Path.Combine(uploadFolder, fileName);
                    string dbImagePath = $"/violations/{fileName}";

                    await image.SaveAsJpegAsync(fullPath);

                    foreach (var issue in allViolations)
                    {
                        var log = new ViolationLog
                        {
                            Category_Id = issue.Label,
                            Image_Path = dbImagePath,
                            Confidence_Score = Math.Round(issue.Confidence, 3),
                            Box_X = Math.Round(issue.Box.X, 2),
                            Box_Y = Math.Round(issue.Box.Y, 2),
                            Box_W = Math.Round(issue.Box.Width, 2),
                            Box_H = Math.Round(issue.Box.Height, 2)
                        };
                        await _dbService.InsertViolationLogAsync(log);
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
                        isSafetyIssue = d.Label.StartsWith("NO-"),
                        isFallDetected = d.Label == "Fall-Detected",
                        box = new { x = Math.Round(d.Box.X, 2), y = Math.Round(d.Box.Y, 2), width = Math.Round(d.Box.Width, 2), height = Math.Round(d.Box.Height, 2) }
                    }).OrderByDescending(d => d.confidence),
                    processedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message, title = "Image processing failed" });
            }
        }
    }
}
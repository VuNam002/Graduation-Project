﻿﻿﻿using Microsoft.AspNetCore.Mvc;
using Models.DTO;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Models.DTO;
using PPE_Detection_App.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        private readonly FaceRecognitionService _faceRecognitionService;
        private readonly DatabaseService _databaseService;
        private readonly IWebHostEnvironment _env;

        public EmployeeController(FaceRecognitionService faceRecognitionService, DatabaseService databaseService, IWebHostEnvironment env)
        {
            _faceRecognitionService = faceRecognitionService;
            _databaseService = databaseService;
            _env = env;
        }

        /// <summary>
        /// API Đăng ký khuôn mặt nhân viên (eKYC)
        /// </summary>
        [HttpPost("enroll")]
        public async Task<IActionResult> EnrollEmployee([FromForm] string employeeCode, [FromForm] string fullName, [FromForm] string department, [FromForm] List<IFormFile> faceImages)
        {
            if (faceImages == null || !faceImages.Any())
                return BadRequest("Vui lòng tải lên ít nhất 1 ảnh khuôn mặt (khuyến nghị 3-5 ảnh ở các góc khác nhau)!");

            try
            {
                var faceVectors = new List<float[]>();
                var employeeFacesDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "employee_faces");
                if (!Directory.Exists(employeeFacesDir)) Directory.CreateDirectory(employeeFacesDir);

                foreach (var file in faceImages)
                {
                    if (file.Length == 0) continue;
                    using var stream = file.OpenReadStream();
                    using var image = await Image.LoadAsync<Rgb24>(stream);

                    float[] vector = _faceRecognitionService.GetFaceEmbedding(image);
                    if (vector.Length > 0)
                    {
                        faceVectors.Add(vector);
                        
                        // Lưu ảnh backup
                        var fileName = $"{employeeCode}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.jpg";
                        await image.SaveAsJpegAsync(Path.Combine(employeeFacesDir, fileName));
                    }
                }

                if (!faceVectors.Any())
                    return BadRequest("Không thể trích xuất đặc trưng từ các ảnh này. Vui lòng thử ảnh rõ mặt hơn.");

                // Lưu danh sách vector thành JSON (Mảng 2 chiều: float[][])
                string vectorJsonString = JsonSerializer.Serialize(faceVectors);

                var newEmployee = new Employee
                {
                    Employee_Code = employeeCode,
                    Full_Name = fullName,
                    Department = department,
                    Face_Vector = vectorJsonString
                };

                await _databaseService.AddEmployeeAsync(newEmployee);

                return Ok(new
                {
                    Message = "Đăng ký khuôn mặt đa góc độ thành công",
                    EmployeeName = fullName,
                    TotalAnglesEnrolled = faceVectors.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi he thong: {ex.Message}");
            }
        }
    }
}
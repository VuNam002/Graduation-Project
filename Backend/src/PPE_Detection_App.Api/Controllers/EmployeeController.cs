using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
﻿﻿﻿using Microsoft.AspNetCore.Mvc;
using Models.DTO;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Models.DTO;
using PPE_Detection_App.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

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
        [HttpGet]
        public async Task<IActionResult> GetAllEmployees()
        {
            try
            {
                var employees = await _databaseService.GetAllEmployeesAsync();
                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi he thong: {ex.Message}");
            }
        }
        [HttpGet("{Employee_Id}")]
        public async Task<IActionResult> GetEmployeeById(int Employee_Id)
        {
            try
            {
                var employee = await _databaseService.GetEmployeeByIdAsync(Employee_Id);
                if (employee == null) return NotFound($"Khong tim thay nhan vien voi ID {Employee_Id}");
                return Ok(employee);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi he thong: {ex.Message}");
            }
        }
        [HttpDelete("{Employee_Id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEmployee(int Employee_Id)
        {
            try
            {
                var success = await _databaseService.DeleteEmployeeAsync(Employee_Id);
                if (!success) return NotFound($"Khong tim thay nhan vien voi ID {Employee_Id}");
                return Ok($"Da xoa nhan vien voi ID {Employee_Id} thanh cong");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi he thong: {ex.Message}");
            }
        }
        [HttpPatch("{Employee_Id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEmployee(int Employee_Id, [FromBody] UpdateEmployee dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest("Du lieu khong hop le");
                }

                var success = await _databaseService.UpdateEmployee(Employee_Id, dto);

                if (!success)
                {
                    return NotFound($"Khong tim thay nhan vien voi ID {Employee_Id}");
                }

                return Ok($"Da cap nhat thanh cong nhan vien voi ID {Employee_Id}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi khi cap nhat nhan vien: {ex.Message}");
            }
        }
        /// <summary>
        /// API Đăng ký khuôn mặt nhân viên (eKYC)
        /// </summary> 
        [HttpPost("enroll")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EnrollEmployee([FromForm] string employeeCode, [FromForm] string fullName, [FromForm] string department, [FromForm] List<IFormFile> faceImages)
        {
            if (faceImages == null || !faceImages.Any())
                return BadRequest("Vui long tai it nhat 1 anh (Khuyen nghi 3 den 5 anh)!");

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
                    return BadRequest("Khong the trich xuat anh, vui long de ro mat hon");

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
                    Message = "Dang ky khuon mat da goc do thanh cong",
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
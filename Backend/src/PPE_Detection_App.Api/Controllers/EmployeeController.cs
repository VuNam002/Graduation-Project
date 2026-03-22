﻿using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> EnrollEmployee([FromForm] EnrollEmployeeRequest request)
        {
            if (request.FaceImage == null || request.FaceImage.Length == 0)
                return BadRequest("Vui long tai anh len khuon mat!");

            try
            {
                using var stream = request.FaceImage.OpenReadStream();
                using var image = await Image.LoadAsync<Rgb24>(stream);

                float[] faceVector = _faceRecognitionService.GetFaceEmbedding(image);

                if (faceVector.Length == 0)
                    return BadRequest("Khong the trich xuat dac trung tu anh nay");

                string vectorJsonString = JsonSerializer.Serialize(faceVector);

                // Tạo mục lưu trữ hình ảnh định danh nhân viên (eKYC)
                var employeeFacesDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "employee_faces");
                if (!Directory.Exists(employeeFacesDir))
                {
                    Directory.CreateDirectory(employeeFacesDir);
                }
                
                var fileName = $"{request.EmployeeCode}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                var filePath = Path.Combine(employeeFacesDir, fileName);
                await image.SaveAsJpegAsync(filePath);

                var newEmployee = new Employee
                {
                    Employee_Code = request.EmployeeCode,
                    Full_Name = request.FullName,
                    Department = request.Department,
                    Face_Vector = vectorJsonString
                };

                await _databaseService.AddEmployeeAsync(newEmployee);

                return Ok(new
                {
                    Message = "Dang ky khuon mat thanh cong",
                    EmployeeName = request.FullName,
                    VectorLength = faceVector.Length
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loi he thong: {ex.Message}");
            }
        }
    }
}
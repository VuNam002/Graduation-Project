using PPE_Detection_App.Api.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PPE_Detection_App.Api.Services
{
    public class FaceMatcherService
    {
        private readonly DatabaseService _databaseService;
        private readonly FaceRecognitionService _faceRecognitionService;
        private readonly ILogger<FaceMatcherService> _logger;

        // Ngưỡng quyết định. Hạ ngưỡng nhận diện xuống khoảng 0.35 để dễ bắt khuôn mặt từ camera hơn.
        private const double MatchThreshold = 0.35;

        // Tiêm DatabaseService, FaceRecognitionService và Logger vào đây
        public FaceMatcherService(DatabaseService databaseService, FaceRecognitionService faceRecognitionService, ILogger<FaceMatcherService> logger)
        {
            _databaseService = databaseService;
            _faceRecognitionService = faceRecognitionService;
            _logger = logger;
        }

        /// <summary>
        /// Tìm kiếm xem khuôn mặt vi phạm thuộc về nhân viên nào trong CSDL
        /// </summary>
        public async Task<int?> IdentifyEmployeeAsync(float[] unknownFaceVector)
        {
            if (unknownFaceVector == null || unknownFaceVector.Length == 0)
                return null;

            var allEmployees = await _databaseService.GetAllEmployeesAsync();
            if (allEmployees == null || !allEmployees.Any())
                return null;

            int? bestMatchEmployeeId = null;
            double highestSimilarity = -1.0;
            string bestMatchName = "";

            foreach (var emp in allEmployees)
            {
                if (string.IsNullOrEmpty(emp.Face_Vector)) continue;

                try
                {
                    float[]? knownFaceVector = JsonSerializer.Deserialize<float[]>(emp.Face_Vector);
                    if (knownFaceVector == null || knownFaceVector.Length != unknownFaceVector.Length)
                        continue;

                    double similarity = _faceRecognitionService.CalculateCosineSimilarity(unknownFaceVector, knownFaceVector);
                    _logger.LogInformation($"[FaceMatch] So sanh voi {emp.Full_Name}: Độ giống {similarity:P2}");

                    if (similarity > highestSimilarity)
                    {
                        highestSimilarity = similarity;
                        bestMatchEmployeeId = emp.Employee_Id;
                        bestMatchName = emp.Full_Name;
                    }
                }
                catch
                {
                    continue; 
                }
            }

            if (highestSimilarity >= MatchThreshold)
            {
                _logger.LogInformation($"[FaceMatch] Chot nguoi vi pham {bestMatchName} (ID: {bestMatchEmployeeId}) - Max Sim: {highestSimilarity:P2}");
                return bestMatchEmployeeId;
            }

            _logger.LogInformation($"[FaceMatch] Khong xac dinh duoc nhan vien (Duoi nguong {MatchThreshold:P0}). Diem cao nhat: {highestSimilarity:P2}");
            return null;
        }
    }
}
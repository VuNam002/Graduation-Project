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

        // Nâng ngưỡng quyết định lên 0.75 (75%) để khắt khe hơn, tránh nhận diện bừa bãi
        private const double MatchThreshold = 0.75;
        public FaceMatcherService(DatabaseService databaseService, FaceRecognitionService faceRecognitionService, ILogger<FaceMatcherService> logger)
        {
            _databaseService = databaseService;
            _faceRecognitionService = faceRecognitionService;
            _logger = logger;
        }

        /// <summary>
        /// Tìm kiếm xem khuôn mặt vi phạm thuộc về nhân viên nào trong CSDL
        /// </summary>
        public async Task<(int? Id, string? Name)> IdentifyEmployeeAsync(float[] unknownFaceVector)
        {
            if (unknownFaceVector == null || unknownFaceVector.Length == 0)
                return (null, null);

            var allEmployees = await _databaseService.GetAllEmployeesAsync();
            if (allEmployees == null || !allEmployees.Any())
                return (null, null);

            int? bestMatchEmployeeId = null;
            double highestSimilarity = -1.0;
            string bestMatchName = "";

            foreach (var emp in allEmployees)
            {
                if (string.IsNullOrEmpty(emp.Face_Vector)) continue;

                try
                {
                    float[][]? knownFaceVectors = null;
                    try 
                    {
                        knownFaceVectors = JsonSerializer.Deserialize<float[][]>(emp.Face_Vector);
                    }
                    catch
                    {
                        var singleVector = JsonSerializer.Deserialize<float[]>(emp.Face_Vector);
                        if (singleVector != null) knownFaceVectors = new float[][] { singleVector };
                    }

                    if (knownFaceVectors == null || knownFaceVectors.Length == 0) continue;
                    
                    double maxSimForThisEmployee = -1.0;
                    
                    foreach (var knownVector in knownFaceVectors)
                    {
                        if (knownVector.Length != unknownFaceVector.Length) continue;

                        double similarity = _faceRecognitionService.CalculateCosineSimilarity(unknownFaceVector, knownVector);
                        
                        if (similarity > maxSimForThisEmployee)
                        {
                            maxSimForThisEmployee = similarity;
                        }

                        if (similarity > highestSimilarity)
                        {
                            highestSimilarity = similarity;
                            bestMatchEmployeeId = emp.Employee_Id;
                            bestMatchName = emp.Full_Name;
                        }
                    }
                    
                    _logger.LogInformation($"[FaceMatch] So sanh voi {emp.Full_Name}: Do giong {maxSimForThisEmployee:P2}");
                }
                catch
                {
                    continue; 
                }
            }

            if (highestSimilarity >= MatchThreshold)
            {
                _logger.LogInformation($"[FaceMatch] Chot nguoi vi pham {bestMatchName} (ID: {bestMatchEmployeeId}) - Max Sim: {highestSimilarity:P2}");
                return (bestMatchEmployeeId, bestMatchName);
            }

            _logger.LogInformation($"[FaceMatch] Khong xac dinh duoc nhan vien (Duoi nguong {MatchThreshold:P0}). Diem cao nhat: {highestSimilarity:P2}");
            return (null, null);
        }
    }
}
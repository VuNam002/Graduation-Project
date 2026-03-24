using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Models.DTO;

namespace PPE_Detection_App.Api.Services
{
    public class SystemService
    {
        private readonly DatabaseService _databaseService;

        public SystemService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<SystemConfigDto> GetConfigsAsync()
        {
            var confStr = await _databaseService.GetSystemConfigAsync("ConfidenceThreshold");
            var nmsStr = await _databaseService.GetSystemConfigAsync("NmsThreshold");

            return new SystemConfigDto
            {
                ConfidenceThreshold = float.TryParse(confStr, out float parsedConf) ? parsedConf : YoloV8Processor.DefaultConfidenceThreshold,
                NmsThreshold = float.TryParse(nmsStr, out float parsedNms) ? parsedNms : YoloV8Processor.DefaultNmsThreshold
            };
        }

        public async Task UpdateConfigsAsync(float confidenceThreshold, float nmsThreshold)
        {
            await _databaseService.UpdateSystemConfigAsync("ConfidenceThreshold", confidenceThreshold.ToString("0.00"), "Ngưỡng độ tin cậy AI (0-1)");
            await _databaseService.UpdateSystemConfigAsync("NmsThreshold", nmsThreshold.ToString("0.00"), "Ngưỡng triệt tiêu hộp nhiễu NMS (0-1)");
        }
    }
}
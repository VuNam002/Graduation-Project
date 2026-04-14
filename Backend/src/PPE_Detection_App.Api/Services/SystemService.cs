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
            var activeModel = await _databaseService.GetSystemConfigAsync("ActiveModel") ?? "YOLOv8";

            return new SystemConfigDto
            {
                ConfidenceThreshold = float.TryParse(confStr, out float parsedConf) ? parsedConf : YoloV8Processor.DefaultConfidenceThreshold,
                NmsThreshold = float.TryParse(nmsStr, out float parsedNms) ? parsedNms : YoloV8Processor.DefaultNmsThreshold,
                ActiveModel = activeModel
            };
        }

        public async Task UpdateConfigsAsync(SystemConfigDto config)
        {
            await _databaseService.UpdateSystemConfigAsync("ConfidenceThreshold", config.ConfidenceThreshold.ToString("0.00"), "Ngưỡng độ tin cậy AI (0-1)");
            await _databaseService.UpdateSystemConfigAsync("NmsThreshold", config.NmsThreshold.ToString("0.00"), "Ngưỡng triệt tiêu hộp nhiễu NMS (0-1)");
            if (!string.IsNullOrEmpty(config.ActiveModel)) {
                await _databaseService.UpdateSystemConfigAsync("ActiveModel", config.ActiveModel, "Mô hình AI đang sử dụng (YOLOv8 hoặc YOLOv11)");
            }
        }
    }
}
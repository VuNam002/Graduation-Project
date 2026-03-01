using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly CameraStreamService _cameraStreamService;
        private readonly ILogger<CameraController> _logger;

        public CameraController(CameraStreamService cameraStreamService, ILogger<CameraController> logger)
        {
            _cameraStreamService = cameraStreamService;
            _logger = logger;
        }

        [HttpPost("{cameraId}/start")]
        public IActionResult StartCamera(int cameraId)
        {
            if (cameraId < 0)
            {
                return BadRequest("Camera ID không hợp lệ.");
            }

            if (_cameraStreamService.IsProcessing(cameraId))
            {
                return Conflict($"Camera {cameraId} đã được xử lý.");
            }

            _logger.LogInformation($"Yêu cầu bắt đầu xử lý cho camera {cameraId}.");
            _cameraStreamService.StartProcessing(cameraId);

            return Ok($"Bắt đầu xử lý cho camera {cameraId}.");
        }

        [HttpPost("{cameraId}/stop")]
        public IActionResult StopCamera(int cameraId)
        {
            if (!_cameraStreamService.IsProcessing(cameraId))
            {
                return NotFound($"Không tìm thấy quá trình xử lý nào cho camera {cameraId}.");
            }

            _logger.LogInformation($"Yêu cầu dừng xử lý cho camera {cameraId}.");
            _cameraStreamService.StopProcessing(cameraId);

            return Ok($"Đã dừng xử lý cho camera {cameraId}.");
        }

        [HttpGet("{cameraId}/status")]
        public IActionResult GetCameraStatus(int cameraId)
        {
            var isRunning = _cameraStreamService.IsProcessing(cameraId);
            return Ok(new { cameraId, isRunning });
        }
    }
}

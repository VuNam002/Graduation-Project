using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebSocketController : ControllerBase
    {
        private readonly CameraStreamService _cameraService;
        private readonly WebSocketManagerService _wsManager;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(
            CameraStreamService cameraService,
            WebSocketManagerService wsManager,
            ILogger<WebSocketController> logger)
        {
            _cameraService = cameraService;
            _wsManager = wsManager;
            _logger = logger;
        }

        [HttpPost("start/{cameraId}")]
        public IActionResult StartCamera(int cameraId)
        {
            try
            {
                if (_cameraService.IsProcessing(cameraId))
                {
                    return BadRequest(new { error = $"Camera {cameraId} is already running" });
                }

                _cameraService.StartProcessing(cameraId);
                _logger.LogInformation($"Camera {cameraId} started");

                return Ok(new
                {
                    success = true,
                    message = $"Camera {cameraId} started successfully",
                    cameraId,
                    timestamp = DateTime.Now,
                    activeConnections = _wsManager.GetConnectionCount()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start camera {cameraId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("stop/{cameraId}")]
        public IActionResult StopCamera(int cameraId)
        {
            try
            {
                if (!_cameraService.IsProcessing(cameraId))
                {
                    return BadRequest(new { error = $"Camera {cameraId} is not running" });
                }

                _cameraService.StopProcessing(cameraId);
                _logger.LogInformation($"Camera {cameraId} stopped");

                return Ok(new
                {
                    success = true,
                    message = $"Camera {cameraId} stopped successfully",
                    cameraId,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to stop camera {cameraId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status/{cameraId}")]
        public IActionResult GetStatus(int cameraId)
        {
            var isProcessing = _cameraService.IsProcessing(cameraId);
            return Ok(new
            {
                cameraId,
                isProcessing,
                status = isProcessing ? "running" : "stopped",
                activeConnections = _wsManager.GetConnectionCount(),
                timestamp = DateTime.Now
            });
        }

        [HttpGet("connections")]
        public IActionResult GetConnections()
        {
            return Ok(new
            {
                count = _wsManager.GetConnectionCount(),
                connections = _wsManager.GetActiveConnections(),
                timestamp = DateTime.Now
            });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;
using System;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController : ControllerBase
    {
        private readonly CameraStreamService _cameraService;

        public StreamController(CameraStreamService cameraService)
        {
            _cameraService = cameraService;
        }

        [HttpPost("start/{cameraId}")]
        public IActionResult StartCamera(int cameraId)
        {
            try
            {
                _cameraService.StartProcessing(cameraId);
                return Ok(new { message = "Camera started", cameraId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("stop/{cameraId}")]
        public IActionResult StopCamera(int cameraId)
        {
            try
            {
                _cameraService.StopProcessing(cameraId);
                return Ok(new { message = "Camera stopped", cameraId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

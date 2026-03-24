using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Models.DTO;
using PPE_Detection_App.Api.Services;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class SystemController : ControllerBase
    {
        private readonly SystemService _systemService;

        public SystemController(SystemService systemService)
        {
            _systemService = systemService;
        }

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _systemService.GetConfigsAsync();
            return Ok(new { success = true, data = config });
        }

        [HttpPost("config")]
        public async Task<IActionResult> UpdateConfig([FromBody] SystemConfigDto request)
        {
            if (request.ConfidenceThreshold <= 0 || request.ConfidenceThreshold > 1)
                return BadRequest(new { success = false, message = "ConfidenceThreshold phai nam trong khoang (0, 1]." });
            
            if (request.NmsThreshold <= 0 || request.NmsThreshold > 1)
                return BadRequest(new { success = false, message = "NmsThreshold phai nam trong khoang (0, 1]." });

            await _systemService.UpdateConfigsAsync(request.ConfidenceThreshold, request.NmsThreshold);
            
            return Ok(new { success = true, message = "Cap nhat cau hinh AI thanh cong." });
        }
    }
}

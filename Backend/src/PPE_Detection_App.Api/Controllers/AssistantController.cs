using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssistantController : ControllerBase
    {
        private readonly AIAssistantService _aiAssistantService;

        public AssistantController(AIAssistantService aiAssistantService)
        {
            _aiAssistantService = aiAssistantService;
        }

        public class ChatRequest { public string Question { get; set; } = string.Empty; }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { success = false, message = "Cau hoi khong duoc de trong." });
            var answer = await _aiAssistantService.ChatWithDataAsync(request.Question);

            return Ok(new
            {
                success = true,
                reply = answer
            });
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Services;
using System.Threading.Tasks;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AuthService _authService;

        public AdminController(AuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var token = await _authService.LoginAsync(loginRequest);

            if (token == null)
            {
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
            }

            return Ok(new { token });
        }

        [HttpPost("create-account")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message) = await _authService.CreateAccountAsync(request);

            if (!success)
            {
                return Conflict(new { message });
            }

            return Ok(new { message });
        }

        [AllowAnonymous]
        [HttpGet("hash-password/{password}")]
        public IActionResult HashPassword(string password)
        {
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            return Ok(new { password, hashedPassword });
        }
        [HttpGet("accounts")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var accounts = await _authService.GetAllAsync();
            return Ok(accounts);
        }
    } 
}

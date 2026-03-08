using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Services;
using PPE_Detection_App.Api.Models.DTO;

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

        [HttpDelete("delete-account/{username}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAccount(string username)
        {
            var (success, message) = await _authService.DeleteAccountAsync(username);
            if (!success)
            {
                return NotFound(new { message });
            }
            return Ok(new { message });
        }

        [HttpPatch("status-account")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatusAccount([FromBody] UpdateStatusRequests request)
        {
            var result = await _authService.UpdateStatusAccountAsync(request.Username, request.Status);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }
            return Ok(new
            {
                success = true,
                message = result.Message
            });
        }

        [AllowAnonymous]
        [HttpGet("hash-password/{password}")]
        public IActionResult HashPassword(string password)
        {
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            return Ok(new { password, hashedPassword });
        }
        [HttpGet("accounts")]
        public async Task<IActionResult> GetAll()
        {
            var accounts = await _authService.GetAllAsync(); 
            return Ok(accounts);
        }
    } 
}

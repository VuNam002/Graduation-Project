using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Models.DTO;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace PPE_Detection_App.Api.Services
{
    public class AuthService
    {
        private readonly DatabaseService _databaseService;
        private readonly IConfiguration _config;

        public AuthService(DatabaseService databaseService, IConfiguration config)
        {
            _databaseService = databaseService;
            _config = config;
        }

        public async Task<string?> LoginAsync(LoginRequest loginRequest)
        {
            var user = await _databaseService.GetAdminUserByUsernameAsync(loginRequest.Username);

            if (user == null)
            {
                return null; 
            }

            bool passwordIsValid;
            try
            {
                passwordIsValid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password_Hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                passwordIsValid = loginRequest.Password == user.Password_Hash;

                if (passwordIsValid)
                {
                    var newHash = BCrypt.Net.BCrypt.HashPassword(loginRequest.Password);
                    await _databaseService.UpdateUserPasswordHashAsync(user.Username, newHash);
                }
            }

            if (!passwordIsValid)
            {
                return null; 
            }

            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(AdminUser user)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var secretKey = jwtSettings["SecretKey"] ?? throw new Exception("Thiếu Jwt:SecretKey trong cấu hình.");
            var issuer = jwtSettings["Issuer"] ?? throw new Exception("Thiếu Jwt:Issuer trong cấu hình.");
            var audience = jwtSettings["Audience"] ?? throw new Exception("Thiếu Jwt:Audience trong cấu hình.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(ClaimTypes.Name, user.Full_Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddHours(8), 
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<(bool Success, string Message)> CreateAccountAsync(CreateAccountRequest request)
        {
            var existingUser = await _databaseService.GetAdminUserByUsernameAsync(request.Username);
            if (existingUser != null)
            {
                return (false, "Tên đăng nhập đã tồn tại.");
            }

            var newUser = new AdminUser
            {
                Username = request.Username,
                Password_Hash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Full_Name = request.FullName,
                Role = request.Role,
                Status = request.Status,
                Is_Deleted = false
            };

            await _databaseService.CreateAdminUserAsync(newUser);

            return (true, "Tạo tài khoản thành công.");
        }
        public async Task<(bool Success, string Message)> DeleteAccountAsync(string username)
        {
            var user = await _databaseService.GetAdminUserByUsernameAsync(username);
            if (user == null)
            {
                return (false, "Tài khoản không tồn tại.");
            }
            await _databaseService.DeleteAdminUserAsync(username);
            return (true, "Xóa tài khoản thành công.");
        }
        public async Task<(bool Success, string Message)> UpdateStatusAccountAsync(string username, byte status)
        {
            var user = await _databaseService.GetAdminUserByUsernameAsync(username);
            if (user == null)
            {
                return (false, "Tài khoản không tồn tại");
            }
            await _databaseService.UpdateStatusAdminUserAsync(username, status);
            return (true, "Cập nhật trạng thái tài khoản thành công");
        }
        public async Task<IEnumerable<AdminUserResponse>> GetAllAsync()
        {
            var users = await _databaseService.GetAllAdminUsersAsync();
            return users.Select(u => new AdminUserResponse
            {
                Username = u.Username,
                FullName = u.Full_Name,
                Role = u.Role,
                Status = u.Status
            });
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace PPE_Detection_App.Api.Models
{
    public class CreateAccountRequest
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [MinLength(4, ErrorMessage = "Tên đăng nhập phải có ít nhất 4 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = "User"; // Mặc định là User
        public byte Status { get; set; }
    }
}

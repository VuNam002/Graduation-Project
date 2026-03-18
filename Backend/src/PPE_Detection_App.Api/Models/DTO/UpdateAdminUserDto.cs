namespace PPE_Detection_App.Api.Models.DTO
{
    public class UpdateAdminUserDto
    {
        public string Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }
}
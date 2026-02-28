namespace PPE_Detection_App.Api.Models
{
    public class AdminUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password_Hash { get; set; } = string.Empty;
        public string Full_Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Is_Deleted { get; set; }
    }
}
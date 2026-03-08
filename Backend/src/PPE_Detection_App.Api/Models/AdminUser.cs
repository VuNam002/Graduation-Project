public class AdminUser
{
    public string Username { get; set; }
    public string Password_Hash { get; set; }
    public string Full_Name { get; set; }
    public string Role { get; set; }
    public bool Is_Deleted { get; set; }
    public byte Status { get; set; }  // Thêm dòng này
}
namespace PPE_Detection_App.Api.Models
{
    public class ViolationCategory
    {
        public string Id { get; set; } = string.Empty;
        public string Display_Name { get; set; } = string.Empty;
        public int Severity_Level { get; set; }
        public string? Color_Code { get; set; }
        public bool Is_Deleted { get; set; }
    }
}
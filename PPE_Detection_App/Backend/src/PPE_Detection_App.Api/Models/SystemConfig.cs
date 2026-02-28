namespace PPE_Detection_App.Api.Models
{
    public class SystemConfig
    {
        public string Config_Key { get; set; } = string.Empty;
        public string Config_Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
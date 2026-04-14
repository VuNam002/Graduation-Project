namespace PPE_Detection_App.Api.Models.DTO
{
    public class SystemConfigDto
    {
        public float ConfidenceThreshold { get; set; }
        public float NmsThreshold { get; set; }
        public string? ActiveModel { get; set; }
    }
}
namespace PPE_Detection_App.Api.Models.DTO
{
    public class ViolationNotificationDto
    {
        public string Message { get; set; }
        public string ImageUrl { get; set; }
        public DateTime Timestamp { get; set; }
        public string ViolationType { get; set; }
    }
}

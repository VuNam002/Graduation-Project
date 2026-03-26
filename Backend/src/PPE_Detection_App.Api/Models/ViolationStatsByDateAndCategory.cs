namespace PPE_Detection_App.Api.Models
{
    public class ViolationStatsByDateAndCategory
    {
        public DateTime Date { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public int? SeverityLevel { get; set; }
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ViewedCount { get; set; }
        public int FalseAlertCount { get; set; }
    }
}
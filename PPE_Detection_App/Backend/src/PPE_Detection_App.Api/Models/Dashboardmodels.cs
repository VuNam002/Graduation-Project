

namespace PPE_Detection_App.Api.Models
{
    /// <summary>
    /// Thống kê vi phạm theo ngày
    /// </summary>
    public class ViolationStatsByDate
    {
        public DateTime Date { get; set; }
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ViewedCount { get; set; }
        public int FalseAlertCount { get; set; }
    }

    /// <summary>
    /// Thống kê vi phạm theo loại (category)
    /// </summary>
    public class ViolationStatsByCategory
    {
        public string Category_Id { get; set; } = string.Empty;
        public string Display_Name { get; set; } = string.Empty;
        public int Severity_Level { get; set; }
        public string? Color_Code { get; set; }
        public int Count { get; set; }
        public double AvgConfidence { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Thống kê vi phạm theo giờ trong ngày
    /// </summary>
    public class ViolationStatsByHour
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Tổng quan Dashboard (summary cards)
    /// </summary>
    public class DashboardSummary
    {
        public int TotalViolations { get; set; }
        public int NewViolations { get; set; }
        public int ViewedViolations { get; set; }
        public int FalseAlerts { get; set; }
        public double AvgConfidence { get; set; }
        public string? TopCategory { get; set; }
    }

    /// <summary>
    /// Xu hướng vi phạm so với kỳ trước
    /// </summary>
    public class ViolationTrend
    {
        public int CurrentPeriodCount { get; set; }
        public int PreviousPeriodCount { get; set; }
        public decimal ChangePercentage { get; set; }
        public bool IsIncreasing { get; set; }
    }

    /// <summary>
    /// Thống kê tổng hợp cho Dashboard
    /// (Model tổng hợp - có thể không cần thiết nếu dùng anonymous objects)
    /// </summary>
    public class DashboardStatistics
    {
        public DashboardSummary? TodaySummary { get; set; }
        public List<ViolationStatsByDate>? ViolationsByDate { get; set; }
        public List<ViolationStatsByCategory>? ViolationsByCategory { get; set; }
        public List<ViolationStatsByCategory>? TopViolations { get; set; }
        public List<ViolationStatsByHour>? PeakHours { get; set; }
        public ViolationTrend? Trend { get; set; }
    }
}
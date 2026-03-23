namespace Models.DTO
{
    public class ViolationDetailReportDto
    {
        public string Employee_Code { get; set; }
        public string Full_Name { get; set; }
        public string Department { get; set; }
        public string Display_Name { get; set; }
        public string Severity_Level { get; set; }
        public decimal Confidence_Score { get; set; }
        public DateTime Detected_Time { get; set; }
        public string Status { get; set; }
    }
}
    
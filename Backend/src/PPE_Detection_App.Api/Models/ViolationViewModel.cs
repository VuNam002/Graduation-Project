namespace Models
{
    public class ViolationViewModel
    {
        public long Id { get; set; }
        public string Category_Id { get; set; } = string.Empty;
        public string? Category_DisplayName { get; set; }
        public int Severity_Level { get; set; }
        public string? Color_Code { get; set; }
        public string Image_Path { get; set; } = string.Empty;
        public double Confidence_Score { get; set; }
        public DateTime Detected_Time { get; set; }
        public double Box_X { get; set; }
        public double Box_Y { get; set; }
        public double Box_W { get; set; }
        public double Box_H { get; set; }
        public byte Status { get; set; }
        public bool Is_Deleted { get; set; }
        public int? Employee_Id { get; set; }
        public string? Employee_Name { get; set; }
        public string? Employee_Code { get; set; }
    }
}

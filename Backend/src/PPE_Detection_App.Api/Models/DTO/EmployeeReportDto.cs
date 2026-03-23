namespace Models.DTO
{
    public class EmployeeReportDto
    {
        public int Employee_Id { get; set; }
        public string Employee_Code { get; set; } = string.Empty;
        public string Full_Name { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string Face_Vector { get; set; } = string.Empty;
        public DateTime Created_At { get; set; } = DateTime.Now;
        public bool Is_Deleted { get; set; }

        public int Total_Violations { get; set; }
        public string? Most_Common_Violation { get; set; }
    }
}

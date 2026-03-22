namespace Models.DTO
{
    public class EnrollEmployeeRequest
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public IFormFile? FaceImage { get; set; }
    }
}

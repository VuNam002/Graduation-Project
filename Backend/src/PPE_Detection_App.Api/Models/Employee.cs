namespace PPE_Detection_App.Api.Models
{
    public class Employee
    {
        public int Employee_Id { get; set; }
        public string Employee_Code { get; set; } = string.Empty;
        public string Full_Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Face_Vector { get; set; } = string.Empty;
        public DateTime? Created_At { get; set; }
        public bool Is_Deleted { get; set; }
    }
}
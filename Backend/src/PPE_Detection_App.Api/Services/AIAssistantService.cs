using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PPE_Detection_App.Api.Services
{
    public class AIAssistantService
    {
        private readonly string _connectionString;
        private readonly string _geminiApiKey = "AIzaSyDEOtnh2zunW0PEKQ1PSkIF1MjB6fleRmM"; 
        private readonly HttpClient _httpClient;

        public AIAssistantService(IConfiguration config, HttpClient httpClient)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _httpClient = httpClient;
        }

        public async Task<string> ChatWithDataAsync(string userQuestion)
        {
            try
            {
                string schemaContext = @"
                    Bạn là một chuyên gia cơ sở dữ liệu SQL Server. Nhiệm vụ của bạn là chuyển câu hỏi của người dùng thành câu lệnh truy vấn SQL.
                    Dưới đây là cấu trúc Database của tôi:
                    - Bảng Employee(Employee_Id INT, Employee_Code VARCHAR, Full_Name NVARCHAR, Department NVARCHAR, Is_Deleted BIT)
                    - Bảng Violation_Log(Id INT, Category_Id VARCHAR, Employee_Id INT, Detected_Time DATETIME, Confidence_Score FLOAT, Status TINYINT, Is_Deleted BIT)
                    - Bảng Violation_Category(Id VARCHAR, Display_Name NVARCHAR, Severity_Level INT)
                    
                    Quy tắc bắt buộc:
                    1. Chỉ trả về DUY NHẤT câu lệnh SQL, KHÔNG giải thích, KHÔNG có thẻ markdown (```sql).
                    2. Khi cần thông tin nhân viên: Nối bảng Violation_Log.Employee_Id = Employee.Employee_Id.
                    3. Khi cần tên lỗi: Nối bảng Violation_Log.Category_Id = Violation_Category.Id.
                    4. Luôn thêm điều kiện Is_Deleted = 0 ở các bảng để không lấy dữ liệu đã xóa.
                    5. Hàm GETDATE() để lấy ngày hiện tại.
                ";

                string prompt1 = $"{schemaContext}\nCâu hỏi của sếp: '{userQuestion}'\nViết câu lệnh SQL:";
                string sqlQuery = await CallGeminiApi(prompt1);

                sqlQuery = sqlQuery.Replace("```sql", "").Replace("```", "").Trim();

                using var connection = new SqlConnection(_connectionString);
                var rawData = await connection.QueryAsync(sqlQuery);
                
                // In câu lệnh SQL ra Terminal để tiện debug
                Console.WriteLine($"[AI Generated SQL]: {sqlQuery}");

                if (!rawData.Any()) return "Dạ sếp, em không tìm thấy dữ liệu nào phù hợp với yêu cầu ạ.";

                // Ép kiểu dynamic của Dapper sang Dictionary để JSON hiểu và lấy được data thực
                var dictData = rawData.Select(row => (IDictionary<string, object>)row);
                string jsonData = JsonSerializer.Serialize(dictData);
                
                string prompt2 = $@"
                    Bạn là Trợ lý ảo An toàn lao động ngoan ngoãn. Sếp vừa hỏi bạn: '{userQuestion}'.
                    Bạn đã tra cứu cơ sở dữ liệu và có kết quả dạng JSON sau: {jsonData}.
                    Hãy tổng hợp và báo cáo lại sếp một cách ngắn gọn, tự nhiên, lịch sự bằng tiếng Việt. KHÔNG nhắc đến cấu trúc JSON hay SQL.
                ";

                string finalAnswer = await CallGeminiApi(prompt2);
                return finalAnswer;
            }
            catch (Exception ex)
            {
                return $"Xin lỗi sếp, em gặp lỗi khi tra cứu dữ liệu: {ex.Message}";
            }
        }

        private async Task<string> CallGeminiApi(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_geminiApiKey))
            {
                throw new Exception("Bạn chưa nhập Gemini API Key trong code!");
            }
            string requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_geminiApiKey}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUri, content);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi từ Google AI: {response.StatusCode} - {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        }
    }
}
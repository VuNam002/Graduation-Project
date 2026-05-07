﻿﻿﻿﻿﻿using ClosedXML.Excel;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Models.DTO;
using OpenCvSharp;
using PPE_Detection_App.Api.Models;
using PPE_Detection_App.Api.Models.DTO;

namespace PPE_Detection_App.Api.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Thiếu ConnectionString trong appsettings.json");
        }


        public async Task InsertViolationLogAsync(ViolationLog log)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                INSERT INTO Violation_Log 
                (Category_Id, Image_Path, Confidence_Score, Box_X, Box_Y, Box_W, Box_H, Detected_Time, Employee_Id) 
                VALUES 
                (@Category_Id, @Image_Path, @Confidence_Score, @Box_X, @Box_Y, @Box_W, @Box_H, GETDATE(), @Employee_Id)";
            await connection.ExecuteAsync(sql, log);
        }

        /// <summary>
        /// Lấy danh sách violations với filter và phân trang
        /// </summary>
        public async Task<(IEnumerable<ViolationLog> Data, int TotalCount)> GetViolationsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? categoryId = null,
            byte? status = null,
            int page = 1,
            int pageSize = 20)
        {
            using var connection = new SqlConnection(_connectionString);

            // Build WHERE clause động
            var conditions = new List<string> { "vl.Is_Deleted = 0" };
            var parameters = new DynamicParameters();

            if (fromDate.HasValue)
            {
                conditions.Add("vl.Detected_Time >= @FromDate");
                parameters.Add("FromDate", fromDate.Value);
            }

            if (toDate.HasValue)
            {
                conditions.Add("vl.Detected_Time <= @ToDate");
                parameters.Add("ToDate", toDate.Value.AddDays(1).AddSeconds(-1)); 
            }

            if (!string.IsNullOrEmpty(categoryId))
            {
                conditions.Add("vl.Category_Id = @CategoryId");
                parameters.Add("CategoryId", categoryId);
            }

            if (status.HasValue)
            {
                conditions.Add("vl.Status = @Status");
                parameters.Add("Status", status.Value);
            }

            string whereClause = string.Join(" AND ", conditions);

            string countSql = $"SELECT COUNT(*) FROM Violation_Log vl WHERE {whereClause}";
            int totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            string dataSql = $@"
                SELECT 
                    vl.Id,
                    vl.Category_Id,
                    vc.Display_Name AS Category_DisplayName,
                    vc.Severity_Level,
                    vc.Color_Code,
                    vl.Image_Path,
                    vl.Confidence_Score,
                    vl.Detected_Time,
                    vl.Box_X,
                    vl.Box_Y,
                    vl.Box_W,
                    vl.Box_H,
                    vl.Status,
                    vl.Is_Deleted,
                    vl.Employee_Id
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                ORDER BY vl.Detected_Time DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            var data = await connection.QueryAsync<ViolationLog>(dataSql, parameters);

            return (data, totalCount);
        }

        public async Task<ViolationLog?> GetViolationByIdAsync(long id)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT 
                    vl.*,
                    vc.Display_Name AS Category_DisplayName,
                    vc.Severity_Level,
                    vc.Color_Code
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE vl.Id = @Id AND vl.Is_Deleted = 0";

            return await connection.QueryFirstOrDefaultAsync<ViolationLog>(sql, new { Id = id });
        }

        /// <summary>
        /// Cập nhật trạng thái violation (0: Mới, 1: Đã xem, 2: Báo động giả)
        /// </summary>
        public async Task<bool> UpdateViolationStatusAsync(long id, byte status)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                UPDATE Violation_Log 
                SET Status = @Status 
                WHERE Id = @Id AND Is_Deleted = 0";

            int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rowsAffected > 0;
        }

 
        public async Task<bool> DeleteViolationAsync(long id)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                UPDATE Violation_Log 
                SET Is_Deleted = 1 
                WHERE Id = @Id";

            int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }


        public async Task<IEnumerable<ViolationStatsByDate>> GetViolationStatsByDateAsync(
            DateTime startDate,
            DateTime endDate)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT 
                    CAST(Detected_Time AS DATE) AS Date,
                    COUNT(*) AS TotalCount,
                    SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewCount,
                    SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS ViewedCount,
                    SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS FalseAlertCount
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate 
                  AND Detected_Time < @EndDate
                  AND Is_Deleted = 0
                GROUP BY CAST(Detected_Time AS DATE)
                ORDER BY Date DESC";

            return await connection.QueryAsync<ViolationStatsByDate>(sql, new
            {
                StartDate = startDate,
                EndDate = endDate.AddDays(1)
            });
        }

        public async Task<IEnumerable<ViolationStatsByCategory>> GetViolationStatsByCategoryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var connection = new SqlConnection(_connectionString);

            var conditions = new List<string> { "vl.Is_Deleted = 0" };
            var parameters = new DynamicParameters();

            if (startDate.HasValue)
            {
                conditions.Add("vl.Detected_Time >= @StartDate");
                parameters.Add("StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("vl.Detected_Time < @EndDate");
                parameters.Add("EndDate", endDate.Value.AddDays(1));
            }

            string whereClause = string.Join(" AND ", conditions);

            string sql = $@"
                SELECT 
                    vl.Category_Id,
                    vc.Display_Name,
                    vc.Severity_Level,
                    vc.Color_Code,
                    COUNT(*) AS Count,
                    AVG(vl.Confidence_Score) AS AvgConfidence,
                    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS DECIMAL(5,2)) AS Percentage
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                GROUP BY vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code
                ORDER BY Count DESC";

            return await connection.QueryAsync<ViolationStatsByCategory>(sql, parameters);
        }

        public async Task<IEnumerable<ViolationStatsByCategory>> GetTopViolationsAsync(
            int topCount = 5,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var connection = new SqlConnection(_connectionString);

            var conditions = new List<string> { "vl.Is_Deleted = 0" };
            var parameters = new DynamicParameters();
            parameters.Add("TopCount", topCount);

            if (startDate.HasValue)
            {
                conditions.Add("vl.Detected_Time >= @StartDate");
                parameters.Add("StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("vl.Detected_Time < @EndDate");
                parameters.Add("EndDate", endDate.Value.AddDays(1));
            }

            string whereClause = string.Join(" AND ", conditions);

            string sql = $@"
                SELECT TOP (@TopCount)
                    vl.Category_Id,
                    vc.Display_Name,
                    vc.Severity_Level,
                    vc.Color_Code,
                    COUNT(*) AS Count,
                    AVG(vl.Confidence_Score) AS AvgConfidence
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                GROUP BY vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code
                ORDER BY Count DESC";

            return await connection.QueryAsync<ViolationStatsByCategory>(sql, parameters);
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync(DateTime date)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"
                DECLARE @StartDate DATETIME = @Date;
                DECLARE @EndDate DATETIME = DATEADD(DAY, 1, @Date);

                SELECT 
                    COUNT(*) AS TotalViolations,
                    SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewViolations,
                    SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS ViewedViolations,
                    SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS FalseAlerts,
                    AVG(Confidence_Score) AS AvgConfidence,
                    (
                        SELECT TOP 1 Category_Id 
                        FROM Violation_Log 
                        WHERE Detected_Time >= @StartDate 
                          AND Detected_Time < @EndDate 
                          AND Is_Deleted = 0
                        GROUP BY Category_Id 
                        ORDER BY COUNT(*) DESC
                    ) AS TopCategory
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate 
                  AND Detected_Time < @EndDate
                  AND Is_Deleted = 0";

            return await connection.QueryFirstOrDefaultAsync<DashboardSummary>(sql, new { Date = date })
                   ?? new DashboardSummary();
        }

        public async Task<IEnumerable<ViolationStatsByHour>> GetViolationStatsByHourAsync(
            DateTime startDate,
            DateTime endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"
                SELECT 
                    DATEPART(HOUR, Detected_Time) AS Hour,
                    COUNT(*) AS Count
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate 
                  AND Detected_Time < @EndDate
                  AND Is_Deleted = 0
                GROUP BY DATEPART(HOUR, Detected_Time)
                ORDER BY Hour";

            return await connection.QueryAsync<ViolationStatsByHour>(sql, new
            {
                StartDate = startDate,
                EndDate = endDate.AddDays(1)
            });
        }

        /// <summary>
        /// Lấy xu hướng vi phạm (so sánh với kỳ trước)
        /// </summary>
        public async Task<ViolationTrend> GetViolationTrendAsync(
            DateTime currentStartDate,
            DateTime currentEndDate)
        {
            using var connection = new SqlConnection(_connectionString);

            var daysDiff = (currentEndDate - currentStartDate).Days;
            var previousStartDate = currentStartDate.AddDays(-daysDiff);
            var previousEndDate = currentStartDate.AddSeconds(-1);

            string sql = @"
                SELECT 
                    (SELECT COUNT(*) 
                     FROM Violation_Log 
                     WHERE Detected_Time >= @CurrentStartDate 
                       AND Detected_Time < @CurrentEndDate
                       AND Is_Deleted = 0) AS CurrentPeriodCount,
                    (SELECT COUNT(*) 
                     FROM Violation_Log 
                     WHERE Detected_Time >= @PreviousStartDate 
                       AND Detected_Time < @PreviousEndDate
                       AND Is_Deleted = 0) AS PreviousPeriodCount";

            var result = await connection.QueryFirstAsync<dynamic>(sql, new
            {
                CurrentStartDate = currentStartDate,
                CurrentEndDate = currentEndDate.AddDays(1),
                PreviousStartDate = previousStartDate,
                PreviousEndDate = previousEndDate.AddDays(1)
            });

            int current = result.CurrentPeriodCount;
            int previous = result.PreviousPeriodCount;

            decimal changePercentage = previous > 0
                ? Math.Round((decimal)(current - previous) / previous * 100, 2)
                : 0;

            return new ViolationTrend
            {
                CurrentPeriodCount = current,
                PreviousPeriodCount = previous,
                ChangePercentage = changePercentage,
                IsIncreasing = changePercentage > 0
            };
        }

        public async Task<List<AdminUser>> GetAllAdminUsersAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM Admin_User WHERE Is_Deleted = 0 ORDER BY Username";
            var result = await connection.QueryAsync<AdminUser>(sql);
            return result.ToList();
        }

        public async Task<IEnumerable<ViolationCategory>> GetAllCategoriesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM Violation_Category WHERE Is_Deleted = 0 ORDER BY Severity_Level DESC";
            return await connection.QueryAsync<ViolationCategory>(sql);
        }

        public async Task<AdminUser?> GetAdminUserByUsernameAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM Admin_User WHERE Username = @Username AND Is_Deleted = 0";
            return await connection.QueryFirstOrDefaultAsync<AdminUser>(sql, new { Username = username });
        }

                public async Task CreateAdminUserAsync(AdminUser user)

                {

                    using var connection = new SqlConnection(_connectionString);

                    string sql = @"

                        INSERT INTO Admin_User (Username, Password_Hash, Full_Name, Role, Is_Deleted)

                        VALUES (@Username, @Password_Hash, @Full_Name, @Role, 0)";

                    await connection.ExecuteAsync(sql, user);

                }

                public async Task DeleteAdminUserAsync(string username)
                {
                    using var connection = new SqlConnection(_connectionString);
                    string sql = @"
                        UPDATE Admin_User 
                        SET Is_Deleted = 1 
                        WHERE Username = @Username";
                    await connection.ExecuteAsync(sql, new { Username = username });
                }

        public async Task UpdateStatusAdminUserAsync(string username, byte status)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                UPDATE Admin_User
                SET Status = @Status
                WHERE Username = @Username
                AND Is_Deleted = 0";
            await connection.ExecuteAsync(sql, new
            {
                Username = username,
                Status = status  
            });
        }

        public async Task<byte[]> ExportViolationReportToExcelAsync(DateTime startDate, DateTime endDate)
        {
            var data = (await GetAllViolationDetailsAsync(startDate, endDate)).ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Báo Cáo Vi Phạm");

            int totalCols = 4;

            ws.Cell(1, 1).Value = $"BÁO CÁO VI PHẠM  |  {startDate:dd/MM/yyyy} – {endDate:dd/MM/yyyy}";
            ws.Range(1, 1, 1, totalCols).Merge()
              .Style.Font.SetBold(true).Font.SetFontSize(14)
              .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
              .Fill.SetBackgroundColor(XLColor.FromHtml("#2F5496"))
              .Font.SetFontColor(XLColor.White);

            string[] headers = {
                "Loại Vi Phạm", "Mức Độ", "Thời Gian Phát Hiện", "Trạng Thái"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(2, i + 1);
                cell.Value = headers[i];
                cell.Style
                    .Font.SetBold(true)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#4472C4"))
                    .Font.SetFontColor(XLColor.White)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            }

            int row = 3;
            bool isAlt = false;

            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.Display_Name;
                ws.Cell(row, 2).Value = item.Severity_Level;
                ws.Cell(row, 3).Value = item.Detected_Time;
                ws.Cell(row, 3).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm:ss";
                
                string statusText = Convert.ToString(item.Status) switch
                {
                    "0" => "Mới",
                    "1" => "Đã xem",
                    "2" => "Báo động giả",
                    _ => "Không xác định"
                };
                ws.Cell(row, 4).Value = statusText;

                var rowRange = ws.Range(row, 1, row, totalCols);
                if (isAlt)
                {
                    rowRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1"));
                }
                
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                isAlt = !isAlt;
                row++;
            }

            ws.Cell(row, 1).Value = $"Tổng: {data.Count} vi phạm";
            ws.Range(row, 1, row, totalCols).Merge()
              .Style.Font.SetBold(true)
              .Fill.SetBackgroundColor(XLColor.FromHtml("#E2EFDA"))
              .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(2);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        // Method lấy dữ liệu chi tiết (dùng nội bộ cho export)
        private async Task<IEnumerable<ViolationDetailReportDto>> GetAllViolationDetailsAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"
        SELECT
            vc.Display_Name,
            vc.Severity_Level,
            vl.Confidence_Score,
            vl.Detected_Time,
            vl.Status
        FROM Violation_Log vl
        LEFT JOIN Violation_Category vc
            ON vl.Category_Id = vc.Id
            AND vc.Is_Deleted = 0
        WHERE vl.Is_Deleted = 0
          AND vl.Detected_Time >= @StartDate
          AND vl.Detected_Time <= @EndDate
        ORDER BY vl.Detected_Time DESC";

            return await connection.QueryAsync<ViolationDetailReportDto>(sql, new
            {
                StartDate = startDate,
                EndDate = endDate.AddDays(1).AddSeconds(-1)
            });
        }
        public async Task<AdminUser?> DetailAccountAsync(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT Username, Password_Hash, Full_Name, Role, Status, Is_Deleted
                FROM Admin_User
                WHERE Username = @Username
                AND Is_Deleted = 0";
            return await connection.QueryFirstOrDefaultAsync<AdminUser>(sql, new { Username = username });
        }

        public async Task UpdateAccountAsync(UpdateAdminUserDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            var setClauses = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("Username", dto.Username);

            if (!string.IsNullOrEmpty(dto.PasswordHash))
            {
                setClauses.Add("Password_Hash = @PasswordHash");
                parameters.Add("PasswordHash", dto.PasswordHash);
            }

            if (!string.IsNullOrEmpty(dto.FullName))
            {
                setClauses.Add("Full_Name = @FullName");
                parameters.Add("FullName", dto.FullName);
            }

            if (!string.IsNullOrEmpty(dto.Role))
            {
                setClauses.Add("Role = @Role");
                parameters.Add("Role", dto.Role);
            }

            if (!setClauses.Any()) return; 

            string sql = $@"
                UPDATE Admin_User
                SET {string.Join(", ", setClauses)}
                WHERE Username = @Username
                AND Is_Deleted = 0";

            await connection.ExecuteAsync(sql, parameters);
        }

        public async Task UpdateUserPasswordHashAsync(string username, string passwordHash)
                {
                    using var connection = new SqlConnection(_connectionString);
                    string sql = @"
                        UPDATE Admin_User 
                        SET Password_Hash = @PasswordHash 
                        WHERE Username = @Username";
                    await connection.ExecuteAsync(sql, new { Username = username, PasswordHash = passwordHash });
                }

        public async Task AddEmployeeAsync(Employee employee)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                INSERT INTO Employee (Employee_Code, Full_Name, Department, Face_Vector)
                VALUES (@Employee_Code, @Full_Name, @Department, @Face_Vector)";
            await connection.ExecuteAsync(sql, employee);
        }
        public async Task<Employee?> GetEmployeeByCodeAsync(string employeeCode)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                string sql = @"SELECT * 
                       FROM Employee 
                       WHERE Employee_Code = @Employee_Code 
                       AND Is_Deleted = 0";

                return await connection.QueryFirstOrDefaultAsync<Employee>(
                    sql,
                    new { Employee_Code = employeeCode }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy employee: " + ex.Message);
                return null; 
            }
        }
        public async Task<Employee?> GetEmployeeByIdAsync(int employeeId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                string sql = "SELECT * FROM Employee WHERE Employee_Id = @Employee_Id AND Is_Deleted = 0";
                return await connection.QueryFirstOrDefaultAsync<Employee>(sql, new { Employee_Id = employeeId });
            } catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy employee: " + ex.Message);
                return null;    
            }
            
        }

        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                string sql = "SELECT * FROM Employee WHERE Is_Deleted = 0";
                var result = await connection.QueryAsync<Employee>(sql);
                return result.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy danh sách employee: " + ex.Message);
                return new List<Employee>();
            }
        }
        public async Task<bool> DeleteEmployeeAsync(int employeeId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                string sql = "UPDATE Employee SET Is_Deleted = 1 WHERE Employee_Id = @Employee_Id";
                int rowsAffected = await connection.ExecuteAsync(sql, new { Employee_Id = employeeId });
                return rowsAffected > 0;
            } catch (Exception ex)
            {
                Console.WriteLine("Loi khi xoa employee: " + ex.Message);
                return false;
            }
        }
        public async Task<bool> UpdateEmployee(int id, UpdateEmployee dto)
        {
            using var connection = new SqlConnection(_connectionString);

            string sql = @"
            UPDATE Employee
            SET 
                Employee_Code = @Employee_Code,
                Full_Name = @Full_Name,
                Department = @Department
            WHERE Employee_Id = @Employee_Id
            ";

            var result = await connection.ExecuteAsync(sql, new
            {
                Employee_Id = id,
                dto.Employee_Code,
                dto.Full_Name,
                dto.Department
            });

            return result > 0;
        }

        public async Task<string?> GetSystemConfigAsync(string key)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT Config_Value FROM System_Config WHERE Config_Key = @Key";
            return await connection.QueryFirstOrDefaultAsync<string>(sql, new { Key = key });
        }

        public async Task UpdateSystemConfigAsync(string key, string value, string? description = null)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                IF EXISTS (SELECT 1 FROM System_Config WHERE Config_Key = @Key)
                    UPDATE System_Config SET Config_Value = @Value, Description = ISNULL(@Description, Description) WHERE Config_Key = @Key
                ELSE
                    INSERT INTO System_Config (Config_Key, Config_Value, Description) VALUES (@Key, @Value, @Description)";
            
            await connection.ExecuteAsync(sql, new { Key = key, Value = value, Description = description });
        }
    }
}

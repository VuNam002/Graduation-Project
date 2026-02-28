using Dapper;
using Microsoft.Data.SqlClient;
using PPE_Detection_App.Api.Models;

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
                (Category_Id, Image_Path, Confidence_Score, Box_X, Box_Y, Box_W, Box_H, Detected_Time) 
                VALUES 
                (@Category_Id, @Image_Path, @Confidence_Score, @Box_X, @Box_Y, @Box_W, @Box_H, GETDATE())";
            await connection.ExecuteAsync(sql, log);
        }

        public async Task<(IEnumerable<ViolationLog> Data, int TotalCount)> GetViolationsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? categoryId = null,
            byte? status = null,
            int page = 1,
            int pageSize = 20)
        {
            using var connection = new SqlConnection(_connectionString);

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
                    vl.Is_Deleted
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

        public async Task<IEnumerable<ViolationCategory>> GetAllCategoriesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM Violation_Category WHERE Is_Deleted = 0 ORDER BY Severity_Level DESC";
            return await connection.QueryAsync<ViolationCategory>(sql);
        }
    }

    // ==================== DTO MODELS ====================

    public class ViolationStatsByDate
    {
        public DateTime Date { get; set; }
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ViewedCount { get; set; }
        public int FalseAlertCount { get; set; }
    }

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

    public class ViolationStatsByHour
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    public class DashboardSummary
    {
        public int TotalViolations { get; set; }
        public int NewViolations { get; set; }
        public int ViewedViolations { get; set; }
        public int FalseAlerts { get; set; }
        public double AvgConfidence { get; set; }
        public string? TopCategory { get; set; }
    }

    public class ViolationTrend
    {
        public int CurrentPeriodCount { get; set; }
        public int PreviousPeriodCount { get; set; }
        public decimal ChangePercentage { get; set; }
        public bool IsIncreasing { get; set; }
    }
}
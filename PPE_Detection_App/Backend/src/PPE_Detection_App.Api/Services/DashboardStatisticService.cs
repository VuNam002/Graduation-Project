using Dapper;
using Microsoft.Data.SqlClient;
using PPE_Detection_App.Api.Models;

namespace PPE_Detection_App.Api.Services
{
    public class DashboardStatisticService
    {
        private readonly string _connectionString;

        public DashboardStatisticService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Thiếu ConnectionString trong appsettings.json");
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
                        WHERE Detected_Time >= @StartDate AND Detected_Time < @EndDate AND Is_Deleted = 0
                        GROUP BY Category_Id ORDER BY COUNT(*) DESC
                    ) AS TopCategory
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate AND Detected_Time < @EndDate AND Is_Deleted = 0";

            return await connection.QueryFirstOrDefaultAsync<DashboardSummary>(sql, new { Date = date })
                   ?? new DashboardSummary();
        }

        public async Task<IEnumerable<ViolationStatsByDate>> GetViolationStatsByDateAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT 
                    CAST(Detected_Time AS DATE) AS Date, COUNT(*) AS TotalCount,
                    SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewCount,
                    SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS ViewedCount,
                    SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS FalseAlertCount
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate AND Detected_Time < @EndDate AND Is_Deleted = 0
                GROUP BY CAST(Detected_Time AS DATE)
                ORDER BY Date DESC";

            return await connection.QueryAsync<ViolationStatsByDate>(sql, new { StartDate = startDate, EndDate = endDate.AddDays(1) });
        }

        public async Task<IEnumerable<ViolationStatsByCategory>> GetViolationStatsByCategoryAsync(DateTime? startDate = null, DateTime? endDate = null)
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
                    vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code,
                    COUNT(*) AS Count, AVG(vl.Confidence_Score) AS AvgConfidence,
                    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS DECIMAL(5,2)) AS Percentage
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                GROUP BY vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code
                ORDER BY Count DESC";

            return await connection.QueryAsync<ViolationStatsByCategory>(sql, parameters);
        }

        public async Task<IEnumerable<ViolationStatsByCategory>> GetTopViolationsAsync(int topCount = 5, DateTime? startDate = null, DateTime? endDate = null)
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
                    vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code,
                    COUNT(*) AS Count, AVG(vl.Confidence_Score) AS AvgConfidence
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                GROUP BY vl.Category_Id, vc.Display_Name, vc.Severity_Level, vc.Color_Code
                ORDER BY Count DESC";

            return await connection.QueryAsync<ViolationStatsByCategory>(sql, parameters);
        }

        public async Task<IEnumerable<ViolationStatsByHour>> GetViolationStatsByHourAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT DATEPART(HOUR, Detected_Time) AS Hour, COUNT(*) AS Count
                FROM Violation_Log
                WHERE Detected_Time >= @StartDate AND Detected_Time < @EndDate AND Is_Deleted = 0
                GROUP BY DATEPART(HOUR, Detected_Time)
                ORDER BY Hour";

            return await connection.QueryAsync<ViolationStatsByHour>(sql, new { StartDate = startDate, EndDate = endDate.AddDays(1) });
        }

        public async Task<ViolationTrend> GetViolationTrendAsync(DateTime currentStartDate, DateTime currentEndDate)
        {
            using var connection = new SqlConnection(_connectionString);
            var daysDiff = (currentEndDate - currentStartDate).Days;
            var previousStartDate = currentStartDate.AddDays(-daysDiff);
            var previousEndDate = currentStartDate.AddSeconds(-1);

            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM Violation_Log WHERE Detected_Time >= @CurrentStartDate AND Detected_Time < @CurrentEndDate AND Is_Deleted = 0) AS CurrentPeriodCount,
                    (SELECT COUNT(*) FROM Violation_Log WHERE Detected_Time >= @PreviousStartDate AND Detected_Time < @PreviousEndDate AND Is_Deleted = 0) AS PreviousPeriodCount";

            var result = await connection.QueryFirstAsync<dynamic>(sql, new
            {
                CurrentStartDate = currentStartDate,
                CurrentEndDate = currentEndDate.AddDays(1),
                PreviousStartDate = previousStartDate,
                PreviousEndDate = previousEndDate.AddDays(1)
            });

            int current = result.CurrentPeriodCount ?? 0;
            int previous = result.PreviousPeriodCount ?? 0;

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
    }
}
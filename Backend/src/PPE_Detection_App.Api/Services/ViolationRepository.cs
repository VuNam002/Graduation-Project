using Dapper;
using Microsoft.Data.SqlClient;
using PPE_Detection_App.Api.Models;

namespace PPE_Detection_App.Api.Services
{
    public class ViolationRepository
    {
        private readonly string _connectionString;

        public ViolationRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Thiếu ConnectionString trong appsettings.json");
        }

        #region --- Quản lý Violation Log ---

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

        /// <summary>
        /// Lấy danh sách violations với filter và phân trang
        /// </summary>
        public async Task<(IEnumerable<ViolationLog> Data, int TotalCount)> GetViolationsAsync(
            DateTime? fromDate = null, DateTime? toDate = null,
            string? categoryId = null, byte? status = null,
            int page = 1, int pageSize = 20)
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
                    vl.Id, vl.Category_Id, vc.Display_Name AS Category_DisplayName,
                    vc.Severity_Level, vc.Color_Code, vl.Image_Path, vl.Confidence_Score,
                    vl.Detected_Time, vl.Box_X, vl.Box_Y, vl.Box_W, vl.Box_H,
                    vl.Status, vl.Is_Deleted
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE {whereClause}
                ORDER BY vl.Detected_Time DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var data = await connection.QueryAsync<ViolationLog>(dataSql, parameters);
            return (data, totalCount);
        }

        public async Task<ViolationLog?> GetViolationByIdAsync(long id)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"
                SELECT 
                    vl.*, vc.Display_Name AS Category_DisplayName,
                    vc.Severity_Level, vc.Color_Code
                FROM Violation_Log vl
                LEFT JOIN Violation_Category vc ON vl.Category_Id = vc.Id
                WHERE vl.Id = @Id AND vl.Is_Deleted = 0";

            return await connection.QueryFirstOrDefaultAsync<ViolationLog>(sql, new { Id = id });
        }

        /// <summary>
        /// Cập nhật trạng thái (0: Mới, 1: Đã xem, 2: Báo động giả)
        /// </summary>
        public async Task<bool> UpdateViolationStatusAsync(long id, byte status)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"UPDATE Violation_Log SET Status = @Status WHERE Id = @Id AND Is_Deleted = 0";
            int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteViolationAsync(long id)
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = @"UPDATE Violation_Log SET Is_Deleted = 1 WHERE Id = @Id";
            int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        #endregion

        #region --- Quản lý Violation Category ---

        public async Task<IEnumerable<ViolationCategory>> GetAllCategoriesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            string sql = "SELECT * FROM Violation_Category WHERE Is_Deleted = 0 ORDER BY Severity_Level DESC";
            return await connection.QueryAsync<ViolationCategory>(sql);
        }

        #endregion
    }
}
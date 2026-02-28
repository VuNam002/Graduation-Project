using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;
using PPE_Detection_App.Api.Models;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ViolationController : ControllerBase
    {
        private readonly DatabaseService _dbService;
        private readonly ILogger<ViolationController> _logger;

        public ViolationController(DatabaseService dbService, ILogger<ViolationController> logger)
        {
            _dbService = dbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetViolations(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? categoryId = null,
            [FromQuery] byte? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var (data, totalCount) = await _dbService.GetViolationsAsync(
                    fromDate, toDate, categoryId, status, page, pageSize);

                return Ok(new
                {
                    success = true,
                    data = data.Select(v => new
                    {
                        id = v.Id,
                        categoryId = v.Category_Id,
                        displayName = v.Category_DisplayName,
                        severityLevel = v.Severity_Level,
                        colorCode = v.Color_Code,
                        imagePath = v.Image_Path,
                        confidence = Math.Round(v.Confidence_Score * 100, 2),
                        detectedTime = v.Detected_Time,
                        box = new
                        {
                            x = Math.Round(v.Box_X, 2),
                            y = Math.Round(v.Box_Y, 2),
                            width = Math.Round(v.Box_W, 2),
                            height = Math.Round(v.Box_H, 2)
                        },
                        status = v.Status,
                        statusText = v.Status switch
                        {
                            0 => "Mới",
                            1 => "Đã xem",
                            2 => "Báo động giả",
                            _ => "Không xác định"
                        }
                    }),
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalRecords = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting violations");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy danh sách vi phạm" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetViolationById(long id)
        {
            try
            {
                var violation = await _dbService.GetViolationByIdAsync(id);

                if (violation == null)
                {
                    return NotFound(new { success = false, error = "Không tìm thấy vi phạm" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = violation.Id,
                        categoryId = violation.Category_Id,
                        displayName = violation.Category_DisplayName,
                        severityLevel = violation.Severity_Level,
                        colorCode = violation.Color_Code,
                        imagePath = violation.Image_Path,
                        confidence = Math.Round(violation.Confidence_Score * 100, 2),
                        detectedTime = violation.Detected_Time,
                        box = new
                        {
                            x = Math.Round(violation.Box_X, 2),
                            y = Math.Round(violation.Box_Y, 2),
                            width = Math.Round(violation.Box_W, 2),
                            height = Math.Round(violation.Box_H, 2)
                        },
                        status = violation.Status,
                        statusText = violation.Status switch
                        {
                            0 => "Mới",
                            1 => "Đã xem",
                            2 => "Báo động giả",
                            _ => "Không xác định"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting violation {Id}", id);
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy thông tin vi phạm" });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateViolationStatus(long id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                if (request.Status < 0 || request.Status > 2)
                {
                    return BadRequest(new { success = false, error = "Trạng thái không hợp lệ. Cho phép: 0 (Mới), 1 (Đã xem), 2 (Báo động giả)" });
                }

                var result = await _dbService.UpdateViolationStatusAsync(id, request.Status);

                if (!result)
                {
                    return NotFound(new { success = false, error = "Không tìm thấy vi phạm hoặc đã bị xóa" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công",
                    data = new
                    {
                        id = id,
                        newStatus = request.Status,
                        statusText = request.Status switch
                        {
                            0 => "Mới",
                            1 => "Đã xem",
                            2 => "Báo động giả",
                            _ => "Không xác định"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating violation status {Id}", id);
                return StatusCode(500, new { success = false, error = "Lỗi khi cập nhật trạng thái" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteViolation(long id)
        {
            try
            {
                var result = await _dbService.DeleteViolationAsync(id);

                if (!result)
                {
                    return NotFound(new { success = false, error = "Không tìm thấy vi phạm" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Xóa vi phạm thành công",
                    data = new { id = id }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting violation {Id}", id);
                return StatusCode(500, new { success = false, error = "Lỗi khi xóa vi phạm" });
            }
        }

        [HttpGet("statistics/by-date")]
        public async Task<IActionResult> GetStatisticsByDate(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var end = endDate ?? DateTime.Today;
                var start = startDate ?? end.AddDays(-6);

                var stats = await _dbService.GetViolationStatsByDateAsync(start, end);

                return Ok(new
                {
                    success = true,
                    period = new { startDate = start, endDate = end },
                    data = stats.Select(s => new
                    {
                        date = s.Date.ToString("yyyy-MM-dd"),
                        totalCount = s.TotalCount,
                        newCount = s.NewCount,
                        viewedCount = s.ViewedCount,
                        falseAlertCount = s.FalseAlertCount
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics by date");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy thống kê" });
            }
        }

        /// Thống kê vi phạm theo loại (cho biểu đồ pie chart)
        [HttpGet("statistics/by-category")]
        public async Task<IActionResult> GetStatisticsByCategory(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var stats = await _dbService.GetViolationStatsByCategoryAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    data = stats.Select(s => new
                    {
                        categoryId = s.Category_Id,
                        displayName = s.Display_Name,
                        severityLevel = s.Severity_Level,
                        colorCode = s.Color_Code,
                        count = s.Count,
                        avgConfidence = Math.Round(s.AvgConfidence * 100, 2),
                        percentage = s.Percentage
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics by category");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy thống kê theo loại" });
            }
        }

        /// Top N vi phạm nhiều nhất
        [HttpGet("statistics/top-violations")]
        public async Task<IActionResult> GetTopViolations(
            [FromQuery] int top = 5,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                if (top < 1 || top > 20) top = 5;

                var stats = await _dbService.GetTopViolationsAsync(top, startDate, endDate);

                return Ok(new
                {
                    success = true,
                    data = stats.Select(s => new
                    {
                        categoryId = s.Category_Id,
                        displayName = s.Display_Name,
                        severityLevel = s.Severity_Level,
                        colorCode = s.Color_Code,
                        count = s.Count,
                        avgConfidence = Math.Round(s.AvgConfidence * 100, 2)
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top violations");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy top vi phạm" });
            }
        }

        /// Thống kê vi phạm theo giờ (Peak hours - Heatmap)
        [HttpGet("statistics/peak-hours")]
        public async Task<IActionResult> GetPeakHours(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Mặc định: 7 ngày gần nhất
                var end = endDate ?? DateTime.Today;
                var start = startDate ?? end.AddDays(-6);

                var stats = await _dbService.GetViolationStatsByHourAsync(start, end);

                return Ok(new
                {
                    success = true,
                    period = new { startDate = start, endDate = end },
                    data = stats.Select(s => new
                    {
                        hour = s.Hour,
                        timeRange = $"{s.Hour:D2}:00 - {s.Hour:D2}:59",
                        count = s.Count
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting peak hours");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy giờ cao điểm" });
            }
        }

        /// Xu hướng vi phạm (so sánh với kỳ trước)
        [HttpGet("statistics/trend")]
        public async Task<IActionResult> GetViolationTrend(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Mặc định: 7 ngày gần nhất
                var end = endDate ?? DateTime.Today;
                var start = startDate ?? end.AddDays(-6);

                var trend = await _dbService.GetViolationTrendAsync(start, end);

                return Ok(new
                {
                    success = true,
                    currentPeriod = new { startDate = start, endDate = end },
                    data = new
                    {
                        currentCount = trend.CurrentPeriodCount,
                        previousCount = trend.PreviousPeriodCount,
                        changePercentage = trend.ChangePercentage,
                        isIncreasing = trend.IsIncreasing,
                        trendText = trend.IsIncreasing
                            ? $"Tăng {Math.Abs(trend.ChangePercentage)}% so với kỳ trước"
                            : trend.ChangePercentage < 0
                                ? $"Giảm {Math.Abs(trend.ChangePercentage)}% so với kỳ trước"
                                : "Không thay đổi so với kỳ trước"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting violation trend");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy xu hướng" });
            }
        }

        /// Tổng quan hôm nay (Dashboard summary)
        [HttpGet("statistics/today-summary")]
        public async Task<IActionResult> GetTodaySummary()
        {
            try
            {
                var today = DateTime.Today;
                var summary = await _dbService.GetDashboardSummaryAsync(today);

                string? topCategoryName = null;
                if (!string.IsNullOrEmpty(summary.TopCategory))
                {
                    var categories = await _dbService.GetAllCategoriesAsync();
                    topCategoryName = categories.FirstOrDefault(c => c.Id == summary.TopCategory)?.Display_Name;
                }

                return Ok(new
                {
                    success = true,
                    date = today.ToString("yyyy-MM-dd"),
                    data = new
                    {
                        totalViolations = summary.TotalViolations,
                        newViolations = summary.NewViolations,
                        viewedViolations = summary.ViewedViolations,
                        falseAlerts = summary.FalseAlerts,
                        avgConfidence = Math.Round(summary.AvgConfidence * 100, 2),
                        topCategory = new
                        {
                            id = summary.TopCategory,
                            displayName = topCategoryName
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today summary");
                return StatusCode(500, new { success = false, error = "Lỗi khi lấy tổng quan hôm nay" });
            }
        }
    }

    public class UpdateStatusRequest
    {
        public byte Status { get; set; }
    }
}
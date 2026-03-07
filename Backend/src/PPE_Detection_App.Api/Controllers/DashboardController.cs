using Microsoft.AspNetCore.Mvc;
using PPE_Detection_App.Api.Services;

namespace PPE_Detection_App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardStatisticService _dashboardService;
        private readonly ViolationRepository _violationRepo;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            DashboardStatisticService dashboardService,
            ViolationRepository violationRepo,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _violationRepo = violationRepo;
            _logger = logger;
        }

        /// <summary>
        /// Lấy toàn bộ dữ liệu cho Dashboard (1 endpoint duy nhất)
        /// Frontend chỉ cần gọi endpoint này để có đủ dữ liệu hiển thị Dashboard
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetDashboardOverview(
            [FromQuery] int daysRange = 7)
        {
            try
            {
                var today = DateTime.Today;
                var startDate = today.AddDays(-daysRange + 1);

                _logger.LogInformation("Fetching dashboard data from {StartDate} to {EndDate}", startDate, today);

                var todaySummaryTask = _dashboardService.GetDashboardSummaryAsync(today);
                var violationsByDateTask = _dashboardService.GetViolationStatsByDateAsync(startDate, today);
                var violationsByCategoryTask = _dashboardService.GetViolationStatsByCategoryAsync(startDate, today);
                var topViolationsTask = _dashboardService.GetTopViolationsAsync(5, startDate, today);
                var peakHoursTask = _dashboardService.GetViolationStatsByHourAsync(startDate, today);
                var trendTask = _dashboardService.GetViolationTrendAsync(startDate, today);

                await Task.WhenAll(
                    todaySummaryTask,
                    violationsByDateTask,
                    violationsByCategoryTask,
                    topViolationsTask,
                    peakHoursTask,
                    trendTask
                );

                var todaySummary = await todaySummaryTask;
                var violationsByDate = await violationsByDateTask;
                var violationsByCategory = await violationsByCategoryTask;
                var topViolations = await topViolationsTask;
                var peakHours = await peakHoursTask;
                var trend = await trendTask;

                string? topCategoryName = null;
                if (!string.IsNullOrEmpty(todaySummary.TopCategory))
                {
                    var categories = await _violationRepo.GetAllCategoriesAsync();
                    topCategoryName = categories.FirstOrDefault(c => c.Id == todaySummary.TopCategory)?.Display_Name;
                }

                // ==================== BUILD RESPONSE ====================
                return Ok(new
                {
                    success = true,
                    generatedAt = DateTime.UtcNow,
                    period = new
                    {
                        startDate = startDate.ToString("yyyy-MM-dd"),
                        endDate = today.ToString("yyyy-MM-dd"),
                        days = daysRange
                    },

                    // TỔNG QUAN HÔM NAY (CARDS)
                    todaySummary = new
                    {
                        date = today.ToString("yyyy-MM-dd"),
                        totalViolations = todaySummary.TotalViolations,
                        newViolations = todaySummary.NewViolations,
                        viewedViolations = todaySummary.ViewedViolations,
                        falseAlerts = todaySummary.FalseAlerts,
                        avgConfidence = Math.Round(todaySummary.AvgConfidence * 100, 2),
                        topCategory = new
                        {
                            id = todaySummary.TopCategory,
                            displayName = topCategoryName
                        }
                    },

                    // BIỂU ĐỒ XU HƯỚNG (LINE CHART)
                    violationsTrend = new
                    {
                        labels = violationsByDate.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray(),
                        datasets = new[]
                        {
                            new
                            {
                                label = "Tổng vi phạm",
                                data = violationsByDate.Select(d => d.TotalCount).ToArray(),
                                borderColor = "#3b82f6",
                                backgroundColor = "rgba(59, 130, 246, 0.1)"
                            },
                            new
                            {
                                label = "Vi phạm mới",
                                data = violationsByDate.Select(d => d.NewCount).ToArray(),
                                borderColor = "#ef4444",
                                backgroundColor = "rgba(239, 68, 68, 0.1)"
                            },
                            new
                            {
                                label = "Đã xử lý",
                                data = violationsByDate.Select(d => d.ViewedCount).ToArray(),
                                borderColor = "#10b981",
                                backgroundColor = "rgba(16, 185, 129, 0.1)"
                            }
                        },
                        rawData = violationsByDate.Select(d => new
                        {
                            date = d.Date.ToString("yyyy-MM-dd"),
                            total = d.TotalCount,
                            new_count = d.NewCount,
                            viewed = d.ViewedCount,
                            falseAlert = d.FalseAlertCount
                        })
                    },

                    // PHÂN BỐ THEO LOẠI (PIE CHART)
                    violationsByCategory = new
                    {
                        labels = violationsByCategory.Select(c => c.Display_Name).ToArray(),
                        datasets = new[]
                        {
                            new
                            {
                                data = violationsByCategory.Select(c => c.Count).ToArray(),
                                backgroundColor = violationsByCategory.Select(c => c.Color_Code ?? "#6b7280").ToArray()
                            }
                        },
                        rawData = violationsByCategory.Select(c => new
                        {
                            categoryId = c.Category_Id,
                            displayName = c.Display_Name,
                            count = c.Count,
                            percentage = c.Percentage,
                            avgConfidence = Math.Round(c.AvgConfidence * 100, 2),
                            severityLevel = c.Severity_Level,
                            colorCode = c.Color_Code
                        })
                    },

                    // TOP 5 VI PHẠM NHIỀU NHẤT
                    topViolations = topViolations.Select(t => new
                    {
                        categoryId = t.Category_Id,
                        displayName = t.Display_Name,
                        count = t.Count,
                        avgConfidence = Math.Round(t.AvgConfidence * 100, 2),
                        severityLevel = t.Severity_Level,
                        colorCode = t.Color_Code
                    }),

                    // GIỜ CAO ĐIỂM (HEATMAP / BAR CHART)
                    peakHours = new
                    {
                        labels = peakHours.Select(h => $"{h.Hour:D2}:00").ToArray(),
                        datasets = new[]
                        {
                            new
                            {
                                label = "Số vi phạm",
                                data = peakHours.Select(h => h.Count).ToArray(),
                                backgroundColor = "#f59e0b"
                            }
                        },
                        rawData = peakHours.Select(h => new
                        {
                            hour = h.Hour,
                            timeRange = $"{h.Hour:D2}:00 - {h.Hour:D2}:59",
                            count = h.Count
                        })
                    },

                    // XU HƯỚNG SO VỚI KỲ TRƯỚC
                    trend = new
                    {
                        currentPeriod = new
                        {
                            startDate = startDate.ToString("yyyy-MM-dd"),
                            endDate = today.ToString("yyyy-MM-dd"),
                            count = trend.CurrentPeriodCount
                        },
                        previousPeriod = new
                        {
                            startDate = startDate.AddDays(-daysRange).ToString("yyyy-MM-dd"),
                            endDate = startDate.AddDays(-1).ToString("yyyy-MM-dd"),
                            count = trend.PreviousPeriodCount
                        },
                        change = new
                        {
                            percentage = trend.ChangePercentage,
                            isIncreasing = trend.IsIncreasing,
                            direction = trend.IsIncreasing ? "up" : (trend.ChangePercentage < 0 ? "down" : "stable"),
                            text = trend.IsIncreasing
                                ? $"Tăng {Math.Abs(trend.ChangePercentage)}%"
                                : trend.ChangePercentage < 0
                                    ? $"Giảm {Math.Abs(trend.ChangePercentage)}%"
                                    : "Không đổi",
                            color = trend.IsIncreasing ? "#ef4444" : "#10b981"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard overview");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Lỗi khi lấy dữ liệu dashboard",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy dữ liệu real-time (polling mỗi 30s-60s)
        /// </summary>
        [HttpGet("realtime")]
        public async Task<IActionResult> GetRealtimeData()
        {
            try
            {
                var today = DateTime.Today;
                var last30Minutes = DateTime.Now.AddMinutes(-30);

                // Lấy vi phạm trong 30 phút gần nhất (Sử dụng _violationRepo)
                var (recentViolations, _) = await _violationRepo.GetViolationsAsync(
                    fromDate: last30Minutes,
                    toDate: DateTime.Now,
                    status: 0,
                    page: 1,
                    pageSize: 10
                );

                // Sử dụng _dashboardService
                var todaySummary = await _dashboardService.GetDashboardSummaryAsync(today);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    summary = new
                    {
                        totalToday = todaySummary.TotalViolations,
                        newCount = todaySummary.NewViolations,
                        last30Minutes = recentViolations.Count()
                    },
                    recentViolations = recentViolations.Select(v => new
                    {
                        id = v.Id,
                        categoryId = v.Category_Id,
                        displayName = v.Category_DisplayName,
                        imagePath = v.Image_Path,
                        confidence = Math.Round(v.Confidence_Score * 100, 2),
                        detectedTime = v.Detected_Time,
                        timeAgo = GetTimeAgo(v.Detected_Time)
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching realtime data");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Lỗi khi lấy dữ liệu realtime"
                });
            }
        }

        /// <summary>
        /// Lấy thống kê theo tháng (cho báo cáo tháng)
        /// </summary>
        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlyStats(
            [FromQuery] int year = 0,
            [FromQuery] int month = 0)
        {
            try
            {
                if (year == 0) year = DateTime.Now.Year;
                if (month == 0) month = DateTime.Now.Month;

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Sử dụng _dashboardService
                var violationsByDate = await _dashboardService.GetViolationStatsByDateAsync(startDate, endDate);
                var violationsByCategory = await _dashboardService.GetViolationStatsByCategoryAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    period = new
                    {
                        year = year,
                        month = month,
                        monthName = startDate.ToString("MMMM yyyy"),
                        startDate = startDate.ToString("yyyy-MM-dd"),
                        endDate = endDate.ToString("yyyy-MM-dd")
                    },
                    summary = new
                    {
                        totalViolations = violationsByDate.Sum(d => d.TotalCount),
                        avgPerDay = Math.Round(violationsByDate.Average(d => (double)d.TotalCount), 2),
                        peakDay = violationsByDate.OrderByDescending(d => d.TotalCount).FirstOrDefault()?.Date.ToString("yyyy-MM-dd"),
                        peakDayCount = violationsByDate.OrderByDescending(d => d.TotalCount).FirstOrDefault()?.TotalCount ?? 0
                    },
                    dailyStats = violationsByDate.Select(d => new
                    {
                        date = d.Date.ToString("yyyy-MM-dd"),
                        dayOfWeek = d.Date.DayOfWeek.ToString(),
                        total = d.TotalCount,
                        new_count = d.NewCount,
                        viewed = d.ViewedCount,
                        falseAlert = d.FalseAlertCount
                    }),
                    categoryStats = violationsByCategory.Select(c => new
                    {
                        categoryId = c.Category_Id,
                        displayName = c.Display_Name,
                        count = c.Count,
                        percentage = c.Percentage,
                        avgConfidence = Math.Round(c.AvgConfidence * 100, 2)
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching monthly stats");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Lỗi khi lấy thống kê tháng"
                });
            }
        }

        /// <summary>
        /// Lấy thống kê tùy chỉnh theo khoảng thời gian
        /// </summary>
        [HttpGet("custom-range")]
        public async Task<IActionResult> GetCustomRangeStats(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate == default || endDate == default)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Vui lòng cung cấp startDate và endDate (yyyy-MM-dd)"
                    });
                }

                if (startDate > endDate)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "startDate phải nhỏ hơn hoặc bằng endDate"
                    });
                }

                // Sử dụng _dashboardService
                var violationsByDate = await _dashboardService.GetViolationStatsByDateAsync(startDate, endDate);
                var violationsByCategory = await _dashboardService.GetViolationStatsByCategoryAsync(startDate, endDate);
                var peakHours = await _dashboardService.GetViolationStatsByHourAsync(startDate, endDate);

                var totalDays = (endDate - startDate).Days + 1;

                return Ok(new
                {
                    success = true,
                    period = new
                    {
                        startDate = startDate.ToString("yyyy-MM-dd"),
                        endDate = endDate.ToString("yyyy-MM-dd"),
                        totalDays = totalDays
                    },
                    summary = new
                    {
                        totalViolations = violationsByDate.Sum(d => d.TotalCount),
                        avgPerDay = Math.Round(violationsByDate.Sum(d => d.TotalCount) / (double)totalDays, 2),
                        newViolations = violationsByDate.Sum(d => d.NewCount),
                        resolvedViolations = violationsByDate.Sum(d => d.ViewedCount),
                        falseAlerts = violationsByDate.Sum(d => d.FalseAlertCount)
                    },
                    dailyTrend = violationsByDate.Select(d => new
                    {
                        date = d.Date.ToString("yyyy-MM-dd"),
                        total = d.TotalCount
                    }),
                    categoryBreakdown = violationsByCategory.Select(c => new
                    {
                        categoryId = c.Category_Id,
                        displayName = c.Display_Name,
                        count = c.Count,
                        percentage = c.Percentage
                    }),
                    peakHours = peakHours.Select(h => new
                    {
                        hour = h.Hour,
                        count = h.Count
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching custom range stats");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Lỗi khi lấy thống kê"
                });
            }
        }

        /// <summary>
        /// Lấy thống kê cho widget nhỏ (mini cards)
        /// </summary>
        [HttpGet("widgets")]
        public async Task<IActionResult> GetWidgets()
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var last7Days = today.AddDays(-6);
                var last30Days = today.AddDays(-29);

                // Sử dụng _dashboardService
                var todaySummary = await _dashboardService.GetDashboardSummaryAsync(today);
                var yesterdaySummary = await _dashboardService.GetDashboardSummaryAsync(yesterday);
                var last7DaysStats = await _dashboardService.GetViolationStatsByDateAsync(last7Days, today);
                var last30DaysStats = await _dashboardService.GetViolationStatsByDateAsync(last30Days, today);

                return Ok(new
                {
                    success = true,
                    widgets = new
                    {
                        // Widget 1: Hôm nay
                        today = new
                        {
                            value = todaySummary.TotalViolations,
                            label = "Vi phạm hôm nay",
                            change = todaySummary.TotalViolations - yesterdaySummary.TotalViolations,
                            changePercent = yesterdaySummary.TotalViolations > 0
                                ? Math.Round((double)(todaySummary.TotalViolations - yesterdaySummary.TotalViolations) / yesterdaySummary.TotalViolations * 100, 2)
                                : 0,
                            trend = todaySummary.TotalViolations > yesterdaySummary.TotalViolations ? "up" : "down",
                            icon = "alert-circle",
                            color = "#ef4444"
                        },

                        // Widget 2: Vi phạm mới (chưa xem)
                        newViolations = new
                        {
                            value = todaySummary.NewViolations,
                            label = "Chưa xem",
                            percentage = todaySummary.TotalViolations > 0
                                ? Math.Round((double)todaySummary.NewViolations / todaySummary.TotalViolations * 100, 2)
                                : 0,
                            icon = "bell",
                            color = "#f59e0b"
                        },

                        // Widget 3: Tuần này
                        thisWeek = new
                        {
                            value = last7DaysStats.Sum(d => d.TotalCount),
                            label = "7 ngày gần đây",
                            avgPerDay = Math.Round(last7DaysStats.Average(d => (double)d.TotalCount), 1),
                            icon = "calendar",
                            color = "#3b82f6"
                        },

                        // Widget 4: Tháng này
                        thisMonth = new
                        {
                            value = last30DaysStats.Sum(d => d.TotalCount),
                            label = "30 ngày gần đây",
                            avgPerDay = Math.Round(last30DaysStats.Average(d => (double)d.TotalCount), 1),
                            icon = "trending-up",
                            color = "#8b5cf6"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching widgets");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Lỗi khi lấy dữ liệu widgets"
                });
            }
        }

        // ==================== HELPER METHODS ====================

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Vừa xong";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} phút trước";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} giờ trước";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} ngày trước";

            return dateTime.ToString("dd/MM/yyyy HH:mm");
        }
    }
}
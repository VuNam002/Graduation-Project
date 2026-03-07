"use client"

import * as React from "react"
import { Area, AreaChart, CartesianGrid, XAxis } from "recharts"
import { fetchDashboardMonthly } from "@/lib/api"

import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "./ui/skeleton"

export function ChartAreaInteractive() {
  const [selectedMonth, setSelectedMonth] = React.useState<string>(String(new Date().getMonth() + 1))
  const [selectedYear, setSelectedYear] = React.useState<string>(String(new Date().getFullYear()))
  const [chartData, setChartData] = React.useState<any[] | null>(null)
  const [chartConfig, setChartConfig] = React.useState<ChartConfig | null>(null)
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState<string | null>(null)
  const [periodDescription, setPeriodDescription] = React.useState("Đang tải...");

  // Tạo danh sách năm cố định để không phải tính toán lại mỗi lần render
  const years = React.useMemo(() => {
    const currentYear = new Date().getFullYear();
    return Array.from({ length: 21 }, (_, i) => currentYear - 10 + i);
  }, []);

  React.useEffect(() => {
    const fetchData = async () => {
      setLoading(true)
      setError(null)
      try {
        const response = await fetchDashboardMonthly({
          month: parseInt(selectedMonth),
          year: parseInt(selectedYear)
        })

        if (response && response.success) {
          const { dailyStats, period } = response;
          if (!dailyStats || !period) {
            throw new Error("Cấu trúc dữ liệu API không hợp lệ.");
          }
          
          const year = parseInt(selectedYear);
          const month = parseInt(selectedMonth);
          const daysInMonth = new Date(year, month, 0).getDate();
          const completeData = [];

          for (let day = 1; day <= daysInMonth; day++) {
            const dateStr = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
            const existingStat = dailyStats.find((stat) => stat.date === dateStr);
            
            if (existingStat) {
              completeData.push(existingStat);
            } else {
              completeData.push({
                date: dateStr,
                total: 0,
              });
            }
          }
          
          setChartData(completeData);
          
          const newChartConfig: ChartConfig = {
            total: {
              label: "Tổng số vi phạm",
              color: "hsl(var(--chart-1))",
            },
          };
          setChartConfig(newChartConfig);

          setPeriodDescription(`Tháng ${period.month} năm ${period.year}`);
        } else {
           throw new Error( "Không thể lấy dữ liệu báo cáo tháng");
        }
      } catch (err) {
        if (err instanceof Error && err.message.includes('validation errors')) {
            setError(`Lỗi xác thực: ${err.message}`);
        } else {
            setError(err instanceof Error ? err.message : "Đã xảy ra lỗi không xác định");
        }
      } finally {
        setLoading(false)
      }
    }

    fetchData()
  }, [selectedMonth, selectedYear])

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardTitle>Xu hướng vi phạm</CardTitle>
        <CardDescription>
          {periodDescription}
        </CardDescription>
        <CardAction className="flex gap-2">
          <Select value={selectedMonth} onValueChange={setSelectedMonth} disabled={loading}>
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="Tháng" />
            </SelectTrigger>
            <SelectContent>
              {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                <SelectItem key={m} value={String(m)}>
                  Tháng {m}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={selectedYear} onValueChange={setSelectedYear} disabled={loading}>
            <SelectTrigger className="w-[100px]">
              <SelectValue placeholder="Năm" />
            </SelectTrigger>
            <SelectContent className="max-h-[200px]">
              {years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardAction>
      </CardHeader>
      <CardContent className="px-2 pt-4 sm:px-6 sm:pt-6">
        {loading ? (
          <div className="aspect-auto h-[250px] w-full">
             <Skeleton className="h-full w-full" />
          </div>
        ) : error ? (
          <div className="flex items-center justify-center aspect-auto h-[250px] w-full text-red-500">
            {error}
          </div>
        ) : chartData && chartConfig ? (
          <ChartContainer
            config={chartConfig}
            className="aspect-auto h-[250px] w-full"
          >
            <AreaChart data={chartData}>
              <defs>
                {Object.keys(chartConfig).map((key) => (
                    <linearGradient key={key} id={`fill${key}`} x1="0" y1="0" x2="0" y2="1">
                        <stop
                        offset="5%"
                        stopColor={`var(--color-${key})`}
                        stopOpacity={0.8}
                        />
                        <stop
                        offset="95%"
                        stopColor={`var(--color-${key})`}
                        stopOpacity={0.1}
                        />
                    </linearGradient>
                ))}
              </defs>
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                minTickGap={32}
                tickFormatter={(value) => {
                  const [y, m, d] = value.split('-').map(Number);
                  const date = new Date(y, m - 1, d);
                  return date.toLocaleDateString("vi-VN", {
                    day: "numeric",
                    month: "numeric",
                  })
                }}
              />
              <ChartTooltip
                cursor={false}
                content={
                  <ChartTooltipContent
                    labelFormatter={(value) => {
                      const [y, m, d] = value.split('-').map(Number);
                      const date = new Date(y, m - 1, d);
                      return date.toLocaleDateString("vi-VN", {
                        weekday: "short",
                        day: "numeric",
                        month: "long",
                      })
                    }}
                    indicator="dot"
                  />
                }
              />
              {Object.keys(chartConfig).map((key) => (
                <Area
                  key={key}
                  dataKey={key}
                  type="natural"
                  fill={`url(#fill${key})`}
                  stroke={`var(--color-${key})`}
                  stackId="a"
                />
              ))}
            </AreaChart>
          </ChartContainer>
        ) : (
          <div className="flex items-center justify-center aspect-auto h-[250px] w-full">
            Không có dữ liệu.
          </div>
        )}
      </CardContent>
    </Card>
  )
}

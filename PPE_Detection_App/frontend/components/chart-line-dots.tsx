"use client"

import * as React from "react"
import { TrendingUp } from "lucide-react"
import { CartesianGrid, Line, LineChart, XAxis } from "recharts"
import { fetchRealtimeViolations } from "@/lib/api"

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"
import { Skeleton } from "@/components/ui/skeleton"

export function ChartLineDots() {
  const [chartData, setChartData] = React.useState<any[]>([])
  const [summary, setSummary] = React.useState<any>(null)
  const [loading, setLoading] = React.useState(true)

  React.useEffect(() => {
    const loadData = async () => {
      try {
        const response = await fetchRealtimeViolations()
        if (response && response.success) {
          setSummary(response.summary)
          
          const violations = response.recentViolations || []
          
          if (violations.length > 0) {
             // Group violations by time (HH:mm)
             const groupedData = violations.reduce((acc: any, curr: any) => {
                const date = new Date(curr.timestamp)
                const time = date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
                if (!acc[time]) {
                    acc[time] = { time, count: 0 }
                }
                acc[time].count += 1
                return acc
             }, {})
             
             const formattedData = Object.values(groupedData).sort((a: any, b: any) => a.time.localeCompare(b.time))
             setChartData(formattedData as any[])
          } else {
             setChartData([])
          }
        }
      } catch (error) {
        console.error("Failed to fetch realtime violations:", error)
      } finally {
        setLoading(false)
      }
    }
    loadData()
    
    const interval = setInterval(loadData, 30000) 
    return () => clearInterval(interval)
  }, [])

  const chartConfig = {
    count: {
      label: "Vi phạm",
      color: "hsl(var(--chart-1))",
    },
  } satisfies ChartConfig

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <Skeleton className="h-6 w-32 mb-2" />
          <Skeleton className="h-4 w-48" />
        </CardHeader>
        <CardContent>
           <Skeleton className="h-[200px] w-full" />
        </CardContent>
        <CardFooter>
           <Skeleton className="h-4 w-full" />
        </CardFooter>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Vi phạm theo thời gian thực</CardTitle>
        <CardDescription>Cập nhật mới nhất</CardDescription>
      </CardHeader>
      <CardContent>
        {chartData.length > 0 ? (
        <ChartContainer config={chartConfig}>
          <LineChart
            accessibilityLayer
            data={chartData}
            margin={{
              left: 12,
              right: 12,
              top: 12,
              bottom: 12
            }}
          >
            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="time"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
            />
            <ChartTooltip
              cursor={false}
              content={<ChartTooltipContent hideLabel />}
            />
            <Line
              dataKey="count"
              type="natural"
              stroke="var(--color-count)"
              strokeWidth={2}
              dot={{
                fill: "var(--color-count)",
              }}
              activeDot={{
                r: 6,
              }}
            />
          </LineChart>
        </ChartContainer>
        ) : (
            <div className="flex items-center justify-center h-[200px] text-muted-foreground">
                Chưa có dữ liệu vi phạm gần đây
            </div>
        )}
      </CardContent>
      <CardFooter className="flex-col items-start gap-2 text-sm">
        {summary && (
            <>
                <div className="flex gap-2 leading-none font-medium">
                Hôm nay: {summary.totalToday} vi phạm <TrendingUp className="h-4 w-4" />
                </div>
                <div className="leading-none text-muted-foreground">
                {summary.newCount} vi phạm mới, {summary.last30Minutes} trong 30 phút qua
                </div>
            </>
        )}
      </CardFooter>
    </Card>
  )
}

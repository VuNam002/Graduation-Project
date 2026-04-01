"use client"

import * as React from "react"
import { Bar, BarChart, CartesianGrid, XAxis } from "recharts"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

import {
  Card,
  CardContent,
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
import { fetchDashboardOverview } from "@/lib/api"
import { Skeleton } from "@/components/ui/skeleton"

export function ChartBarDefault() {
  const [chartData, setChartData] = React.useState<any[]>([])
  const [trend, setTrend] = React.useState<any>(null)
  const [loading, setLoading] = React.useState(true)
  const [startDate, setStartDate] = React.useState("")
  const [endDate, setEndDate] = React.useState("")
  const [fetchTrigger, setFetchTrigger] = React.useState(0)

  React.useEffect(() => {
    const loadData = async () => {
      setLoading(true)
      try {
        const params: any = startDate && endDate ? { startDate, endDate } : { daysRange: 7 }
        const response = await fetchDashboardOverview(params)
        if (response && response.success) {
          const { peakHours, trend } = response
          
          if (peakHours && peakHours.rawData) {
             const formattedData = peakHours.rawData.map((item: any) => ({
                hour: `${String(item.hour).padStart(2, '0')}:00`,
                count: item.count,
             }))
             formattedData.sort((a: any, b: any) => a.hour.localeCompare(b.hour))
             setChartData(formattedData)
          }
          setTrend(trend)
        }
      } catch (error) {
        console.error("Failed to fetch bar chart data:", error)
      } finally {
        setLoading(false)
      }
    }
    loadData()
  }, [fetchTrigger, startDate, endDate])

  const chartConfig = {
    count: {
      label: "Số vi phạm",
      color: "#0099FF",
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
      <CardHeader className="flex flex-col 2xl:flex-row 2xl:items-center justify-between pb-2 gap-4 space-y-0">
        <div className="flex flex-col gap-1">
          <CardTitle>Giờ cao điểm</CardTitle>
        </div>
        <div className="flex flex-wrap items-center gap-1">
          <Input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="h-8 w-[120px] text-xs"
          />
          <span className="text-muted-foreground text-xs">-</span>
          <Input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="h-8 w-[120px] text-xs"
          />
          <Button size="sm" variant="secondary" className="h-8" onClick={() => setFetchTrigger(prev => prev + 1)}>
            Lọc
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <ChartContainer config={chartConfig}>
          <BarChart accessibilityLayer data={chartData}>
            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="hour"
              tickLine={false}
              tickMargin={10}
              axisLine={false}
            />
            <ChartTooltip
              cursor={false}
              content={<ChartTooltipContent hideLabel />}
            />
            <Bar dataKey="count" fill="var(--color-count)" radius={8} />
          </BarChart>
        </ChartContainer>
      </CardContent>
      <CardFooter className="flex-col items-start gap-2 text-sm">
        <div className="leading-none text-muted-foreground">
          Hiển thị các khung giờ có số lượng vi phạm cao nhất
        </div>
      </CardFooter>
    </Card>
  )
}

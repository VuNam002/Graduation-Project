"use client"

import * as React from "react"
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Card,
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
import { Skeleton } from "@/components/ui/skeleton"
import { fetchDashboardOverview } from "@/lib/api"

const chartConfig = {
  value: {
    label: "Số lượng",
    color: "#0099FF",
  },
} satisfies ChartConfig

interface ChartDataItem {
  name: string
  value: number
}

export function WidgetsBarChart() {
  const [data, setData] = React.useState<ChartDataItem[]>([])
  const [loading, setLoading] = React.useState(true)
  const [startDate, setStartDate] = React.useState("")
  const [endDate, setEndDate] = React.useState("")
  const [fetchTrigger, setFetchTrigger] = React.useState(0)

  React.useEffect(() => {
    const fetchData = async () => {
      setLoading(true)
      try {
        const params: any = {}
        if (startDate && endDate) {
          params.startDate = startDate
          params.endDate = endDate
        } else {
          params.daysRange = 7
        }
        const result = await fetchDashboardOverview(params)
        
        if (result.success) {
          let total = 0, newCount = 0, viewed = 0, falseAlert = 0
          if (result.violationsTrend?.rawData) {
            result.violationsTrend.rawData.forEach((item: any) => {
              total += item.total || 0
              newCount += item.new_count || 0
              viewed += item.viewed || 0
              falseAlert += item.falseAlert || 0
            })
          }
          const chartData = [
            { name: "Tổng số", value: total },
            { name: "Mới", value: newCount },
            { name: "Đã xử lý", value: viewed },
            { name: "Báo giả", value: falseAlert },
          ]
          setData(chartData)
        }
      } catch (error) {
        console.error("Failed to fetch bar chart data:", error)
      } finally {
        setLoading(false)
      }
    }
    fetchData()
  }, [fetchTrigger])

  if (loading) {
    return (
      <Card className="flex flex-col h-full">
        <CardHeader>
          <Skeleton className="h-6 w-32 mb-2" />
          <Skeleton className="h-4 w-48" />
        </CardHeader>
        <CardContent className="flex-1">
          <Skeleton className="h-[250px] w-full" />
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="flex flex-col h-full">
      <CardHeader className="flex flex-col 2xl:flex-row 2xl:items-center justify-between pb-2 gap-4 space-y-0">
        <div className="flex flex-col gap-1">
          <CardTitle>So sánh trạng thái xử lý</CardTitle>
          <CardDescription>Các chỉ số theo thời gian</CardDescription>
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
      <CardContent className="flex-1">
        <ChartContainer config={chartConfig} className="h-[250px] w-full">
          <BarChart data={data}>
            <CartesianGrid vertical={false} />
            <XAxis 
              dataKey="name" 
              tickLine={false}
              axisLine={false}
              tickMargin={8}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              tickMargin={8}
            />
            <ChartTooltip content={<ChartTooltipContent />} />
            <Bar dataKey="value" fill="var(--color-value)" radius={4} />
          </BarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}
"use client"

import * as React from "react"
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts"
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
import { fetchDashboardWidgets } from "@/lib/api"

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

  React.useEffect(() => {
    const fetchData = async () => {
      try {
        const result = await fetchDashboardWidgets()
        
        if (result.success) {
          const chartData = [
            { name: "Hôm nay", value: result.widgets.today.value },
            { name: "Chưa xem", value: result.widgets.newViolations.value },
            { name: "7 ngày", value: result.widgets.thisWeek.value },
            { name: "30 ngày", value: result.widgets.thisMonth.value },
          ]
          setData(chartData)
        }
      } catch (error) {
        console.error("Failed to fetch widgets data:", error)
      } finally {
        setLoading(false)
      }
    }
    fetchData()
  }, [])

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
      <CardHeader>
        <CardTitle>So sánh thống kê</CardTitle>
        <CardDescription>Các chỉ số vi phạm</CardDescription>
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
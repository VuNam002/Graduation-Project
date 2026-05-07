"use client"

import * as React from "react"
import { Label, Pie, PieChart } from "recharts"
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
import { fetchDashboardOverview } from "@/lib/api"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

const chartConfig = {
  count: {
    label: "Số lượng",
  },
  new: {
    label: "Mới",
    color: "#0099FF",
  },
  viewed: {
    label: "Đã xem",
    color: "#FFFF33",
  },
  falseAlert: {
    label: "Báo giả",
    color: "#ef4444",
  },
} satisfies ChartConfig

export function NewViolationsRadial() {
  const [data, setData] = React.useState({ new: 0, viewed: 0, falseAlert: 0, total: 0 })
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
          let newCount = 0
          let viewedCount = 0
          let falseAlertCount = 0
          let totalCount = 0

          if (result.violationsTrend?.rawData) {
            result.violationsTrend.rawData.forEach((item: any) => {
              newCount += item.new_count || 0
              viewedCount += item.viewed || 0
              falseAlertCount += item.falseAlert || 0
              totalCount += item.total || 0
            })
          }

          setData({
            new: newCount,
            viewed: viewedCount,
            falseAlert: falseAlertCount,
            total: totalCount,
          })
        }
      } catch (error) {
        console.error("Failed to fetch widgets data:", error)
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
        <CardContent className="flex-1 flex items-center justify-center">
          <Skeleton className="h-40 w-40 rounded-full" />
        </CardContent>
      </Card>
    )
  }

  const chartData = [
    { status: "new", count: data.new, fill: "var(--color-new)" },
    { status: "viewed", count: data.viewed, fill: "var(--color-viewed)" },
    { status: "falseAlert", count: data.falseAlert, fill: "var(--color-falseAlert)" },
  ]

  return (
    <Card className="flex flex-col h-full">
      <CardHeader className="flex flex-col 2xl:flex-row 2xl:items-center justify-between pb-2 gap-4 space-y-0">
        <div className="flex flex-col gap-1">
          <CardTitle>Trạng thái Thống kê xử lý</CardTitle>
          <CardDescription>Thống kê xử lý</CardDescription>
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
      <CardContent className="flex-1 pb-0">
        <ChartContainer
          config={chartConfig}
          className="mx-auto aspect-square max-h-[250px]"
        >
          <PieChart>
            <ChartTooltip
              cursor={false}
              content={<ChartTooltipContent hideLabel />}
            />
            <Pie
              data={chartData}
              dataKey="count"
              nameKey="status"
              innerRadius={60}
              strokeWidth={5}
            >
              <Label
                content={({ viewBox }) => {
                  if (viewBox && "cx" in viewBox && "cy" in viewBox) {
                    return (
                      <text
                        x={viewBox.cx}
                        y={viewBox.cy}
                        textAnchor="middle"
                        dominantBaseline="central"
                      >
                        <tspan
                          x={viewBox.cx}
                          y={viewBox.cy}
                          className="fill-foreground text-3xl font-bold"
                        >
                          {data.total}
                        </tspan>
                        <tspan
                          x={viewBox.cx}
                          y={(viewBox.cy || 0) + 24}
                          className="fill-muted-foreground"
                        >
                          Tổng cộng
                        </tspan>
                      </text>
                    )
                  }
                }}
              />
            </Pie>
          </PieChart>
        </ChartContainer>
      </CardContent>
      <CardFooter className="flex-col gap-2 text-sm">
        <div className="leading-none text-muted-foreground">
          Hiển thị phân bố trạng thái xử lý
        </div>
      </CardFooter>
    </Card>
  )
}
"use client"

import * as React from "react"
import { Label, PolarRadiusAxis, RadialBar, RadialBarChart } from "recharts"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  type ChartConfig,
} from "@/components/ui/chart"
import { Skeleton } from "@/components/ui/skeleton"
import { fetchDashboardWidgets } from "@/lib/api"


const chartConfig = {
  percentage: {
    label: "Tỷ lệ",
    color: "hsl(var(--chart-2))",
  },
} satisfies ChartConfig

interface WidgetData {
  percentage: number
  value: number
  total: number
}

export function NewViolationsRadial() {
  const [data, setData] = React.useState<WidgetData>({ percentage: 0, value: 0, total: 0 })
  const [loading, setLoading] = React.useState(true)

  React.useEffect(() => {
    const fetchData = async () => {
      try {
        const result = await fetchDashboardWidgets()
        
        if (result.success) {
          setData({
            percentage: result.widgets.newViolations.percentage,
            value: result.widgets.newViolations.value,
            total: result.widgets.today.value
          })
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
        <CardContent className="flex-1 flex items-center justify-center">
          <Skeleton className="h-40 w-40 rounded-full" />
        </CardContent>
      </Card>
    )
  }

  const chartData = [{ name: "Chưa xem", percentage: data.percentage }]

  return (
    <Card className="flex flex-col h-full">
      <CardHeader>
        <CardTitle>Vi phạm chưa xem</CardTitle>
        <CardDescription>Tỷ lệ chưa xử lý</CardDescription>
      </CardHeader>
      <CardContent className="flex-1 flex items-center justify-center pb-0">
        <ChartContainer config={chartConfig} className="mx-auto aspect-square max-h-[250px]">
          <RadialBarChart 
            data={chartData} 
            startAngle={90} 
            endAngle={90 + (data.percentage / 100) * 360}
            innerRadius={80} 
            outerRadius={110}
          >
            <PolarRadiusAxis tick={false} tickLine={false} axisLine={false}>
              <Label
                content={({ viewBox }) => {
                  if (viewBox && "cx" in viewBox && "cy" in viewBox) {
                    return (
                      <text x={viewBox.cx} y={viewBox.cy} textAnchor="middle">
                        <tspan 
                          x={viewBox.cx} 
                          y={viewBox.cy} 
                          className="fill-foreground text-4xl font-bold"
                        >
                          {data.value}
                        </tspan>
                        <tspan 
                          x={viewBox.cx} 
                          y={(viewBox.cy || 0) + 24} 
                          className="fill-muted-foreground"
                        >
                          Chưa xem ({data.percentage.toFixed(1)}%)
                        </tspan>
                      </text>
                    )
                  }
                }}
              />
            </PolarRadiusAxis>
            <RadialBar 
              dataKey="percentage" 
              fill="#f59e0b" 
              cornerRadius={10}
              background
            />
          </RadialBarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}
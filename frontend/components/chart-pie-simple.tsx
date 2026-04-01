"use client"

import * as React from "react"
import { Pie, PieChart } from "recharts"
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

export function ChartPieSimple() {
  const [chartData, setChartData] = React.useState<any[]>([])
  const [chartConfig, setChartConfig] = React.useState<ChartConfig>({})
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
          const { violationsByCategory, trend } = response
          
          if (violationsByCategory && violationsByCategory.rawData) {
             // Map rawData to chart format
             const formattedData = violationsByCategory.rawData.map((item: any) => ({
                category: item.categoryId,
                count: item.count,
                fill: item.colorCode,
                name: item.displayName
             }))
             setChartData(formattedData)

             // Create dynamic config based on categories
             const newConfig: ChartConfig = {
                count: { label: "Số lượng" }
             }
             violationsByCategory.rawData.forEach((item: any) => {
                newConfig[item.categoryId] = {
                    label: item.displayName,
                    color: item.colorCode
                }
             })
             setChartConfig(newConfig)
          }
          setTrend(trend)
        }
      } catch (error) {
        console.error("Failed to fetch pie chart data:", error)
      } finally {
        setLoading(false)
      }
    }
    loadData()
  }, [fetchTrigger, startDate, endDate])

  if (loading) {
    return (
      <Card className="flex flex-col">
        <CardHeader className="items-center pb-0">
          <Skeleton className="h-6 w-32 mb-2" />
          <Skeleton className="h-4 w-48" />
        </CardHeader>
        <CardContent className="flex-1 pb-0">
          <div className="mx-auto aspect-square max-h-[250px] flex items-center justify-center">
             <Skeleton className="h-40 w-40 rounded-full" />
          </div>
        </CardContent>
        <CardFooter className="flex-col gap-2 text-sm">
           <Skeleton className="h-4 w-full" />
        </CardFooter>
      </Card>
    )
  }

  return (
    <Card className="flex flex-col">
      <CardHeader className="flex flex-col 2xl:flex-row 2xl:items-start justify-between pb-2 gap-4 space-y-0">
        <div className="flex flex-col gap-1">
          <CardTitle>Phân loại vi phạm</CardTitle>
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
        {chartData.length > 0 ? (
          <ChartContainer
            config={chartConfig}
            className="mx-auto aspect-square max-h-[250px]"
          >
            <PieChart>
              <ChartTooltip
                cursor={false}
                content={<ChartTooltipContent nameKey="category" />}
              />
              <Pie
                data={chartData}
                dataKey="count"
                nameKey="category"
                innerRadius={60}
                strokeWidth={5}
              />
            </PieChart>
          </ChartContainer>
        ) : (
           <div className="flex items-center justify-center h-[250px] text-muted-foreground">
             Không có dữ liệu
           </div>
        )}
      </CardContent>
      <CardFooter className="flex-col gap-2 text-sm">
        <div className="leading-none text-muted-foreground">
          Hiển thị phân bố các loại vi phạm
        </div>
      </CardFooter>
    </Card>
  )
}
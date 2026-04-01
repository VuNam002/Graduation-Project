"use client"

import * as React from "react"
import { TrendingUp } from "lucide-react"
import { CartesianGrid, Line, LineChart, XAxis } from "recharts"
import { fetchDashboardMultiline } from "@/lib/api"
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
import { Skeleton } from "@/components/ui/skeleton"

export const description = "Biểu đồ đa đường hiển thị vi phạm theo thời gian"

export function ChartLineMultiple() {
  const [chartData, setChartData] = React.useState<any[]>([])
  const [chartConfig, setChartConfig] = React.useState<ChartConfig>({})
  const [lines, setLines] = React.useState<{ key: string; color: string }[]>([])
  const [period, setPeriod] = React.useState<{ startDate: string; endDate: string; days: number } | null>(null)
  const [loading, setLoading] = React.useState(true)
  const [startDate, setStartDate] = React.useState("")
  const [endDate, setEndDate] = React.useState("")
  const [fetchTrigger, setFetchTrigger] = React.useState(0)

  React.useEffect(() => {
    const loadData = async () => {
      setLoading(true)
      try {
        const params: any = { startDate, endDate }
        const response = await fetchDashboardMultiline(params)
        
        if (response && response.success && response.chartData) {
          const { labels, datasets } = response.chartData
          
          const formattedData = labels.map((label, index) => {
            const dataPoint: any = { date: label }
            datasets.forEach((dataset) => {
              dataPoint[dataset.categoryId] = dataset.data[index]
            })
            return dataPoint
          })
          setChartData(formattedData)
          const newConfig: ChartConfig = {}
          const activeLines: { key: string; color: string }[] = []

          datasets.forEach((dataset) => {
            newConfig[dataset.categoryId] = {
              label: dataset.label,
              color: dataset.borderColor,
            }
            activeLines.push({
              key: dataset.categoryId,
              color: dataset.borderColor,
            })
          })

          setChartConfig(newConfig)
          setLines(activeLines)
          setPeriod(response.period)
        }
      } catch (error) {
        console.error("Failed to fetch multiline chart data:", error)
      } finally {
        setLoading(false)
      }
    }
    
    loadData()
  }, [fetchTrigger, startDate, endDate])

  return (
    <Card>
      <CardHeader className="flex flex-col 2xl:flex-row 2xl:items-center justify-between pb-2 gap-4 space-y-0">
        <div className="flex flex-col gap-1">
          <CardTitle>Biểu đồ vi phạm theo loại</CardTitle>
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
        {loading ? (
          <Skeleton className="h-[250px] w-full" />
        ) : chartData.length > 0 ? (
          <ChartContainer config={chartConfig} className="h-[250px] w-full">
            <LineChart
              accessibilityLayer
              data={chartData}
              margin={{ left: 12, right: 12, top: 12, bottom: 12 }}
            >
              <CartesianGrid vertical={false} strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                tickFormatter={(value) => {
                  const [y, m, d] = value.split('-');
                  return `${d}/${m}`; 
                }}
              />
              <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
              {lines.map((line) => (
                <Line
                  key={line.key}
                  dataKey={line.key}
                  type="monotone"
                  stroke={`var(--color-${line.key})`}
                  strokeWidth={2}
                  dot={{ fill: `var(--color-${line.key})` }}
                  activeDot={{ r: 6 }}
                />
              ))}
            </LineChart>
          </ChartContainer>
        ) : (
          <div className="flex items-center justify-center h-[250px] text-muted-foreground">
            Không có dữ liệu
          </div>
        )}
      </CardContent>
      <CardFooter>
        <div className="flex w-full items-start gap-2 text-sm">
          <div className="grid gap-2">
            <div className="flex items-center gap-2 leading-none font-medium">
              Xu hướng vi phạm các loại <TrendingUp className="h-4 w-4 text-primary" />
            </div>
          </div>
        </div>
      </CardFooter>
    </Card>
  )
}

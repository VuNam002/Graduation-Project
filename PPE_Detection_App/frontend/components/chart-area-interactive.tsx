"use client"

import * as React from "react"
import { Area, AreaChart, CartesianGrid, XAxis } from "recharts"
import { fetchDashboardOverview } from "@/lib/api"
import { type DashboardResponse } from "@/lib/types"

import { useIsMobile } from "@/hooks/use-mobile"
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
import {
  ToggleGroup,
  ToggleGroupItem,
} from "@/components/ui/toggle-group"
import { Skeleton } from "./ui/skeleton"

export function ChartAreaInteractive() {
  const isMobile = useIsMobile()
  const [timeRange, setTimeRange] = React.useState("30d")
  const [chartData, setChartData] = React.useState<any[] | null>(null)
  const [chartConfig, setChartConfig] = React.useState<ChartConfig | null>(null)
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState<string | null>(null)
  const [periodDescription, setPeriodDescription] = React.useState("Last 30 days");

  React.useEffect(() => {
    if (isMobile) {
      setTimeRange("7d")
    }
  }, [isMobile])

  React.useEffect(() => {
    const fetchData = async () => {
      setLoading(true)
      setError(null)
      try {
        const days = parseInt(timeRange.replace("d", ""), 10)
        if (isNaN(days)) {
          throw new Error("Invalid time range specified.");
        }
        
        const response = await fetchDashboardOverview({
          daysRange: days,
        })

        if (response && response.success) {
          const { violationsTrend, period } = response;
          if (!violationsTrend || !period) {
            throw new Error("Invalid data structure in API response.");
          }
          const newChartData = violationsTrend.labels.map((label, index) => {
            const dataPoint: { [key: string]: any } = { date: label };
            violationsTrend.datasets.forEach(dataset => {
              const key = dataset.label.replace(/[^a-zA-Z0-9]/g, '');
              dataPoint[key] = dataset.data[index];
            });
            return dataPoint;
          });
          setChartData(newChartData);
          
          const chartColors = ["--chart-1", "--chart-2", "--chart-3", "--chart-4", "--chart-5"];
          const newChartConfig: ChartConfig = violationsTrend.datasets.reduce((config, dataset, index) => {
            const key = dataset.label.replace(/[^a-zA-Z0-9]/g, '');
            config[key] = {
              label: dataset.label.charAt(0).toUpperCase() + dataset.label.slice(1),
              color: `hsl(var(${chartColors[index % chartColors.length]}))`,
            };
            return config;
          }, {} as ChartConfig);
          setChartConfig(newChartConfig);

          setPeriodDescription(`Last ${period.days} days`);
        } else {
           throw new Error( (response as any).error || "Failed to fetch dashboard overview");
        }
      } catch (err) {
        if (err instanceof Error && err.message.includes('validation errors')) {
            setError(`Validation Error: ${err.message}`);
        } else {
            setError(err instanceof Error ? err.message : "An unknown error occurred");
        }
      } finally {
        setLoading(false)
      }
    }

    fetchData()
  }, [timeRange])

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardTitle>Violations Trend</CardTitle>
        <CardDescription>
          <span className="hidden @[540px]/card:block">
            Total violations for the {periodDescription.toLowerCase()}
          </span>
          <span className="@[540px]/card:hidden">{periodDescription}</span>
        </CardDescription>
        <CardAction>
          <ToggleGroup
            type="single"
            value={timeRange}
            onValueChange={setTimeRange}
            variant="outline"
            className="hidden *:data-[slot=toggle-group-item]:px-4! @[767px]/card:flex"
            disabled={loading}
          >
            <ToggleGroupItem value="30d">30 days</ToggleGroupItem>
            <ToggleGroupItem value="7d">7 days</ToggleGroupItem>
            <ToggleGroupItem value="3d">3 days</ToggleGroupItem>
            <ToggleGroupItem value="1d">1 day</ToggleGroupItem>
          </ToggleGroup>
          <Select value={timeRange} onValueChange={setTimeRange} disabled={loading}>
            <SelectTrigger
              className="flex w-40 **:data-[slot=select-value]:block **:data-[slot=select-value]:truncate @[767px]/card:hidden"
              size="sm"
              aria-label="Select a value"
            >
              <SelectValue placeholder="30 days" />
            </SelectTrigger>
            <SelectContent className="rounded-xl">
              <SelectItem value="30d" className="rounded-lg">
                30 days
              </SelectItem>
              <SelectItem value="7d" className="rounded-lg">
                7 days
              </SelectItem>
              <SelectItem value="3d" className="rounded-lg">
                3 days
              </SelectItem>
              <SelectItem value="1d" className="rounded-lg">
                1 day
              </SelectItem>
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
                  const date = new Date(value)
                  return date.toLocaleDateString("en-US", {
                    month: "short",
                    day: "numeric",
                  })
                }}
              />
              <ChartTooltip
                cursor={false}
                content={
                  <ChartTooltipContent
                    labelFormatter={(value) => {
                      return new Date(value).toLocaleDateString("en-US", {
                        month: "short",
                        day: "numeric",
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
            No data available.
          </div>
        )}
      </CardContent>
    </Card>
  )
}

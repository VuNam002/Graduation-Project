"use client"

import * as React from "react"
import { IconTrendingDown, IconTrendingUp, IconMinus } from "@tabler/icons-react"
import { fetchDashboardOverview } from "@/lib/api"
import { DashboardResponse } from "@/lib/types"

import { Badge } from "@/components/ui/badge"
import {
  Card,
  CardAction,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "./ui/skeleton"

export function SectionCards() {
  const [data, setData] = React.useState<DashboardResponse | null>(null)
  const [loading, setLoading] = React.useState(true)

  React.useEffect(() => {
    const loadData = async () => {
      try {
        const response = await fetchDashboardOverview({ daysRange: 7 })
        if (response && response.success) {
          setData(response)
        }
      } catch (error) {
        console.error("Failed to fetch dashboard overview:", error)
      } finally {
        setLoading(false)
      }
    }
    loadData()
  }, [])

  if (loading) {
    return (
      <div className="grid grid-cols-1 gap-4 px-4 lg:px-6 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Card key={i} className="@container/card">
            <CardHeader>
              <Skeleton className="h-4 w-1/2 mb-2" />
              <Skeleton className="h-8 w-1/3" />
            </CardHeader>
            <CardFooter>
              <Skeleton className="h-4 w-2/3" />
            </CardFooter>
          </Card>
        ))}
      </div>
    )
  }

  if (!data) {
    return null
  }

  const { trend, todaySummary, topViolations } = data
  
  const getTrendIcon = () => {
    if (trend.change.direction === 'increasing') return IconTrendingUp
    if (trend.change.direction === 'decreasing') return IconTrendingDown
    return IconMinus
  }
  
  const getTrendColor = () => {
    if (trend.change.direction === 'increasing') return "text-red-500"
    if (trend.change.direction === 'decreasing') return "text-green-500"
    return "text-gray-500"
  }
  
  const TrendIcon = getTrendIcon()
  const trendColor = getTrendColor()

  return (
    <div className="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 sm:grid-cols-2 lg:grid-cols-3 dark:*:data-[slot=card]:bg-card">
      <Card className="@container/card">
        <CardHeader>
          <CardDescription>Tổng số vi phạm (7 ngày)</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            {trend.currentPeriod.count}
          </CardTitle>
          <CardAction >
            <Badge variant="outline" className={trendColor} >
              <TrendIcon className="mr-1 size-3" />
              {trend.change.percentage}%
            </Badge>
          </CardAction>
        </CardHeader>
        <CardFooter className="flex-col items-start gap-1.5 text-sm">
          <div className="line-clamp-1 flex gap-2 font-medium items-center">
            {trend.change.text} 
            <TrendIcon className={`size-4 ${trendColor}`} />
          </div>
          <div className="text-muted-foreground">
            So với kỳ trước ({trend.previousPeriod.count})
          </div>
        </CardFooter>
      </Card>
      
      <Card className="@container/card">
        <CardHeader>
          <CardDescription>Vi phạm hôm nay</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            {todaySummary.totalViolations || 0}
          </CardTitle>
          <CardAction>
            <Badge variant="outline" className="text-green-400">
              +{todaySummary.newViolations} Mới
            </Badge>
          </CardAction>
        </CardHeader>
        <CardFooter className="flex-col items-start gap-1.5 text-sm">
          <div className="line-clamp-1 flex gap-2 font-medium">
            Đã xử lý: {todaySummary.viewedViolations}
          </div>
          <div className="text-muted-foreground">
            Ngày {new Date(todaySummary.date || "").toLocaleDateString('vi-VN')}
          </div>
        </CardFooter>
      </Card>
      
      <Card className="@container/card">
        <CardHeader>
          <CardDescription>Vi phạm phổ biến nhất</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl truncate" title={topViolations[0]?.displayName || "N/A"}>
            {topViolations[0]?.count || 0}
          </CardTitle>
          <CardAction>
            <Badge variant="outline" className="text-red-400">
              Top 1
            </Badge>
          </CardAction>
        </CardHeader>
        <CardFooter className="flex-col items-start gap-1.5 text-sm">
          <div className="line-clamp-1 flex gap-2 font-medium truncate w-full">
            {topViolations[0]?.displayName || "Không có dữ liệu"}
          </div>
          <div className="text-muted-foreground">Trong 7 ngày qua</div>
        </CardFooter>
      </Card>
    </div>
  )
}
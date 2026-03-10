"use client"

import * as React from "react"
import {
  IconEye,
  IconTrash,
  IconAlertTriangle,
  IconFilter,
  IconChevronLeft,
  IconChevronRight,
  IconPhoto,
} from "@tabler/icons-react"
import { toast } from "sonner"
import { useRouter } from "next/navigation"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  fetchViolations,
  fetchUpdateViolationStatus,
  fetchDeleteViolation,
  fetchCategories,
  getBackendUrl,
} from "@/lib/api"
import { ViolationLog, ViolationCategory } from "@/lib/types"

const STATUS_OPTIONS = [
  { value: "all", label: "Tất cả" },
  { value: "0", label: "Mới" },
  { value: "1", label: "Đã xem" },
  { value: "2", label: "Báo động giả" },
]

function StatusBadge({ status }: { status: number }) {
  if (status === 0)
    return (
      <Badge className="bg-blue-600 hover:bg-blue-700 text-sm px-3 py-1">
        Mới
      </Badge>
    )
  if (status === 1)
    return (
      <Badge className="bg-green-600 hover:bg-green-700 text-sm px-3 py-1">
        Đã xem
      </Badge>
    )
  return (
    <Badge variant="outline" className="text-sm px-3 py-1 text-muted-foreground">
      Báo động giả
    </Badge>
  )
}

function SeverityBadge({ level }: { level?: number }) {
  if (level === 3)
    return <Badge variant="destructive" className="text-xs">Cao</Badge>
  if (level === 2)
    return <Badge className="bg-orange-500 hover:bg-orange-600 text-xs">Trung bình</Badge>
  return <Badge variant="secondary" className="text-xs">Thấp</Badge>
}

const PAGE_SIZE = 10

export function ViolationsTable() {
  const router = useRouter()
  const [violations, setViolations] = React.useState<ViolationLog[]>([])
  const [categories, setCategories] = React.useState<ViolationCategory[]>([])
  const [totalCount, setTotalCount] = React.useState(0)
  const [loading, setLoading] = React.useState(true)
  const [updating, setUpdating] = React.useState<number | null>(null)
  const [previewImage, setPreviewImage] = React.useState<string | null>(null)

  const [fromDate, setFromDate] = React.useState("")
  const [toDate, setToDate] = React.useState("")
  const [categoryId, setCategoryId] = React.useState("all")
  const [status, setStatus] = React.useState("all")
  const [page, setPage] = React.useState(1)

  const totalPages = Math.ceil(totalCount / PAGE_SIZE)

  const loadData = React.useCallback(async (resetPage = false) => {
    if (resetPage) {
      setPage(1);
    }
    setLoading(true)
    try {
      const currentPage = resetPage ? 1 : page;
      const params = { page: currentPage, pageSize: PAGE_SIZE, fromDate, toDate, categoryId, status }

      const res = await fetchViolations(params)
      setViolations(res.data)
      setTotalCount(res.pagination.totalRecords)
    } catch {
      toast.error("Không thể tải danh sách vi phạm")
    } finally {
      setLoading(false)
    }
  }, [page, fromDate, toDate, categoryId, status])

  // Load categories once
  React.useEffect(() => {
    fetchCategories()
      .then(setCategories)
      .catch(() => {})
  }, [])

  React.useEffect(() => {
    loadData()
  }, [page]) 

  const handleFilter = () => {
    loadData(true); 
  }

  const handleStatusChange = async (id: number, currentStatus: number) => {
    if (updating !== null) return
    const nextStatus = ((currentStatus + 1) % 3) as 0 | 1 | 2
    setUpdating(id)
    try {
      const result = await fetchUpdateViolationStatus(id, nextStatus)
      if (result.success !== false) {
        setViolations((prev) =>
          prev.map((v) => (v.id === id ? { ...v, status: nextStatus } : v))
        )
        toast.success("Đã cập nhật trạng thái vi phạm")
      } else {
        toast.error(result.message || "Không thể cập nhật trạng thái")
      }
    } catch {
      toast.error("Lỗi kết nối đến server")
    } finally {
      setUpdating(null)
    }
  }

  const handleDelete = async (id: number) => {
    if (!confirm("Bạn có chắc chắn muốn xóa vi phạm này không?")) return
    try {
      const result = await fetchDeleteViolation(id)
      if (result.success !== false) {
        setViolations((prev) => prev.filter((v) => v.id !== id))
        setTotalCount((c) => c - 1)
        toast.success("Xóa vi phạm thành công")
      } else {
        toast.error(result.message || "Không thể xóa vi phạm")
      }
    } catch {
      toast.error("Đã xảy ra lỗi khi xóa vi phạm")
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <IconFilter className="size-4" />
            Bộ lọc
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-3 items-end">
            <div className="flex flex-col gap-1">
              <label className="text-sm text-muted-foreground">Từ ngày</label>
              <Input
                type="date"
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="w-40"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-sm text-muted-foreground">Đến ngày</label>
              <Input
                type="date"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="w-40"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-sm text-muted-foreground">Loại vi phạm</label>
              <Select value={categoryId} onValueChange={setCategoryId}>
                <SelectTrigger className="w-44">
                  <SelectValue placeholder="Tất cả" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Tất cả</SelectItem>
                  {categories.map((cat) => (
                    <SelectItem key={cat.id} value={cat.id}>
                      {cat.displayName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-sm text-muted-foreground">Trạng thái</label>
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger className="w-40">
                  <SelectValue placeholder="Tất cả" />
                </SelectTrigger>
                <SelectContent>
                  {STATUS_OPTIONS.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button onClick={handleFilter} className="self-end">
              Lọc
            </Button>
            <Button
              variant="outline"
              className="self-end"
              onClick={() => {
                setFromDate("")
                setToDate("")
                setCategoryId("all")
                setStatus("all")
                handleFilter();
              }}
            >
              Xóa bộ lọc
            </Button>
          </div>
        </CardContent>
      </Card>

      {loading ? (
        <Card>
          <CardHeader>
            <Skeleton className="h-8 w-48 mb-2" />
            <Skeleton className="h-4 w-64" />
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <IconAlertTriangle className="size-5 text-orange-500" />
              Danh sách vi phạm
            </CardTitle>
            <CardDescription>
              Tổng cộng <strong>{totalCount}</strong> vi phạm được ghi nhận
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="text-base py-4">ID</TableHead>
                  <TableHead className="text-base py-4">Loại vi phạm</TableHead>
                  <TableHead className="text-base py-4">Mức độ</TableHead>
                  <TableHead className="text-base py-4">Thời gian</TableHead>
                  <TableHead className="text-base py-4">Trạng thái</TableHead>
                  <TableHead className="text-base py-4">Hành động</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {violations.length > 0 ? (
                  violations.map((v) => (
                    <TableRow key={v.id}>
                      <TableCell className="font-medium text-base py-4">#{v.id}</TableCell>
                      <TableCell className="text-base py-4">
                        <div className="flex items-center gap-2">
                          {v.colorCode && (
                            <span
                              className="inline-block size-3 rounded-full flex-shrink-0"
                              style={{ backgroundColor: v.colorCode }}
                            />
                          )}
                          {v.displayName ?? v.categoryId}
                        </div>
                      </TableCell>
                      <TableCell className="py-4">
                        <SeverityBadge level={v.severityLevel} />
                      </TableCell>
                      
                      <TableCell className="text-base py-4">
                        {new Date(v.detectedTime).toLocaleString("vi-VN")}
                      </TableCell>
                      <TableCell className="py-4">
                        <div
                          className={`inline-flex cursor-pointer hover:opacity-80 transition-opacity ${
                            updating === v.id ? "opacity-50 pointer-events-none" : ""
                          }`}
                          onClick={() => handleStatusChange(v.id, v.status)}
                          title="Click để đổi trạng thái"
                        >
                          <StatusBadge status={v.status} />
                        </div>
                      </TableCell>
                      <TableCell className="py-4">
                        <div className="flex items-center gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Xem chi tiết"
                            onClick={() => router.push(`/violations/${v.id}`)}
                          >
                            <IconEye className="size-5 text-blue-500" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Xem ảnh gốc"
                            onClick={() => {
                              const backendUrl = getBackendUrl();
                              const fullUrl = v.imagePath.startsWith('http') 
                                ? v.imagePath 
                                : `${backendUrl}${v.imagePath.startsWith('/') ? '' : '/'}${v.imagePath}`;
                              setPreviewImage(fullUrl);
                            }}
                          >
                            <IconPhoto className="size-5 text-green-600" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Xóa vi phạm"
                            onClick={() => handleDelete(v.id)}
                          >
                            <IconTrash className="size-5 text-red-500" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                ) : (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center text-muted-foreground">
                      Không có dữ liệu vi phạm.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>

            {totalPages > 1 && (
              <div className="flex items-center justify-between mt-4">
                <span className="text-sm text-muted-foreground">
                  Trang {page} / {totalPages}
                </span>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="icon"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => p - 1)}
                  >
                    <IconChevronLeft className="size-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="icon"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    <IconChevronRight className="size-4" />
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={!!previewImage} onOpenChange={(open) => !open && setPreviewImage(null)}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Ảnh gốc</DialogTitle>
          </DialogHeader>
          <div className="flex items-center justify-center bg-black/5 rounded-md p-2">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img 
              src={previewImage || ""} 
              alt="Preview" 
              className="max-w-full max-h-[80vh] object-contain rounded-md"
            />
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

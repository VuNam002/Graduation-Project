"use client"

import * as React from "react"
import {
  IconUsers,
  IconUserPlus,
  IconSearch,
  IconIdBadge2,
  IconTrash,
} from "@tabler/icons-react"
import { toast } from "sonner"
import { useRouter } from "next/navigation"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"

import { Employee } from "@/lib/types"

import {fetchGetAllEmployee, fetchDeleteEmployee } from "@/lib/api"
import { ca } from "zod/locales"

const PAGE_SIZE = 6

export function EmployeesTable() {
  const router = useRouter()
  const [employees, setEmployees] = React.useState<Employee[]>([])
  const [loading, setLoading] = React.useState(true)
  const [searchTerm, setSearchTerm] = React.useState("")
  const [page, setPage] = React.useState(1)

  const loadData = React.useCallback(async () => {
    setLoading(true)
    try {
      const data = await fetchGetAllEmployee()
      const formattedData = data.map((item: any) => ({
        ...item,
        face_Vector: item.face_Vector || "",
        created_At: item.created_At || null,
        is_Deleted: item.is_Deleted || false
      })) as Employee[]
      setEmployees(formattedData)
    } catch (error) {
      toast.error("Không thể tải danh sách nhân viên")
      console.error(error)
    } finally {
      setLoading(false)
    }
  }, [])

  const handleDelete = async (Employee_Id : number) => {
    if(!confirm("Bạn có chắc muốn xóa nhân viên này?")) return;
    try {
      const result = await fetchDeleteEmployee(Employee_Id )
      if(result && result.success !== false) {
        toast.success(`Xóa nhân viên (ID: ${Employee_Id}) thành công`)
        const remainingItems = employees.filter(e => e.employee_Id !== Employee_Id).length;
        const newTotalPages = Math.ceil(remainingItems / PAGE_SIZE);
        if (page > newTotalPages && newTotalPages > 0) {
          setPage(newTotalPages);
        }
        
        loadData()
      } else {
        toast.error(result?.message || "Xóa nhân viên thất bại")
      }
    }catch (error) {  
        toast.error("Đã xảy ra lỗi khi kết nối hoặc xử lý xóa nhân viên")
        console.error(error)
    }
  }

  React.useEffect(() => {
    loadData()
  }, [loadData])

  const filteredEmployees = employees.filter(
    (e) =>
      e.full_Name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      e.employee_Code.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const totalCount = filteredEmployees.length
  const totalPages = Math.ceil(totalCount / PAGE_SIZE)
  const currentEmployees = filteredEmployees.slice(
    (page - 1) * PAGE_SIZE,
    page * PAGE_SIZE
  )

  React.useEffect(() => {
    setPage(1)
  }, [searchTerm])

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <IconSearch className="size-4" />
            Tìm kiếm nhân viên
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-3 items-end justify-between">
            <div className="flex flex-col gap-1 w-full max-w-sm">
              <Input
                placeholder="Nhập tên hoặc mã nhân viên..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <Button className="flex items-center gap-2 bg-primary" onClick={() => router.push('/employee/created')}>
              <IconUserPlus className="size-4" />
              Đăng ký mới (eKYC)
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
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <IconUsers className="size-5 text-blue-500" />
              Danh sách nhân sự
            </CardTitle>
            <CardDescription>
              Tổng cộng <strong>{totalCount}</strong> nhân viên trong hệ thống
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="text-base py-4 w-20">ID</TableHead>
                  <TableHead className="text-base py-4">Nhân viên</TableHead>
                  <TableHead className="text-base py-4">Mô tả</TableHead>
                  <TableHead className="text-base py-4">Dữ liệu khuôn mặt</TableHead>
                  <TableHead className="text-base py-4">Ngày tạo</TableHead>
                  <TableHead className="text-base py-4 w-24 text-center">Hành động</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {currentEmployees.length > 0 ? (
                  currentEmployees.map((emp) => {
                    const hasFaceVector = emp.face_Vector && emp.face_Vector.length > 10; 
                    return (
                      <TableRow key={emp.employee_Id}>
                        <TableCell className="font-medium text-base py-4">#{emp.employee_Id}</TableCell>
                        
                        <TableCell className="py-4">
                          <div className="flex items-center gap-3">
                            <div className="flex size-10 items-center justify-center rounded-full bg-muted">
                              <IconIdBadge2 className="size-5 text-muted-foreground" />
                            </div>
                            <div className="flex flex-col">
                              <span className="font-semibold text-sm">{emp.full_Name}</span>
                              <span className="text-xs text-muted-foreground">Mã: {emp.employee_Code}</span>
                            </div>
                          </div>
                        </TableCell>

                        <TableCell className="text-base py-4">
                          <Badge variant="outline" className="text-sm">
                            {emp.department || "Chưa cập nhật"}
                          </Badge>
                        </TableCell>

                        <TableCell className="py-4">
                          {hasFaceVector ? (
                            <Badge className="bg-green-600 hover:bg-green-700 text-xs px-2 py-1">Đã đăng ký AI</Badge>
                          ) : (
                            <Badge variant="destructive" className="text-xs px-2 py-1">Chưa có dữ liệu</Badge>
                          )}
                        </TableCell>

                        <TableCell className="text-sm py-4 text-muted-foreground">
                          {emp.created_At ? new Date(emp.created_At).toLocaleDateString("vi-VN") : "---"}
                        </TableCell>
                        <TableCell className="py-4 text-center">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleDelete(emp.employee_Id)}
                          >
                            <IconTrash className="size-4" />
                          </Button>
                        </TableCell>

                      </TableRow>
                    )
                  })
                ) : (
                  <TableRow>
                    <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                      Không tìm thấy nhân viên nào phù hợp.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>

            {totalPages > 1 && (
              <div className="mt-4">
                <Pagination>
                  <PaginationContent>
                    <PaginationItem>
                      <PaginationPrevious
                        href="#"
                        onClick={(e) => {
                          e.preventDefault()
                          if (page > 1) setPage((p) => p - 1)
                        }}
                        className={page <= 1 ? "pointer-events-none opacity-50" : ""}
                      />
                    </PaginationItem>

                    {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => {
                      if (p === 1 || p === totalPages || (p >= page - 1 && p <= page + 1)) {
                        return (
                          <PaginationItem key={p}>
                            <PaginationLink
                              href="#"
                              isActive={p === page}
                              onClick={(e) => {
                                e.preventDefault()
                                setPage(p)
                              }}
                            >
                              {p}
                            </PaginationLink>
                          </PaginationItem>
                        )
                      }
                      if ((p === page - 2 && p > 1) || (p === page + 2 && p < totalPages)) {
                        return (
                          <PaginationItem key={p}>
                            <PaginationEllipsis />
                          </PaginationItem>
                        )
                      }
                      return null
                    })}

                    <PaginationItem>
                      <PaginationNext
                        href="#"
                        onClick={(e) => {
                          e.preventDefault()
                          if (page < totalPages) setPage((p) => p + 1)
                        }}
                        className={page >= totalPages ? "pointer-events-none opacity-50" : ""}
                      />
                    </PaginationItem>
                  </PaginationContent>
                </Pagination>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  )
}

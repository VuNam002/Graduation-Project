"use client"

import * as React from "react"
import {
  IconCheck,
  IconX,
  IconTrash,
  IconPencil
} from "@tabler/icons-react"
import { toast } from "sonner"
import Link from "next/link"

import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"

import { Card, CardContent, CardDescription, CardHeader, CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationPrevious,
  PaginationNext,
  PaginationLink
} from "@/components/ui/pagination";
import { fetchAccounts, fetchUpdateStatusAccount, fetchDeleteAccount} from "@/lib/api"
import { Account } from "@/lib/types"
import { useAuth } from "@/lib/auth"

export default function AccountPage() {
  const [accounts, setAccounts] = React.useState<Account[]>([])
  const [loading, setLoading] = React.useState(true)
  const [updating, setUpdating] = React.useState<string | null>(null)
  const { user } = useAuth()
  const isAdmin = user?.role === "Admin"
  const [currentPage, setCurrentPage] = React.useState(1);
  const accountsPerPage = 10;

  React.useEffect(() => {
    const loadData = async () => {
      try {
        const data = await fetchAccounts()
        setAccounts(data)
      } catch (error) {
        console.error("Failed to fetch accounts:", error)
      } finally {
        setLoading(false)
      }
    }
    loadData()
  }, [])

  const handleStatusChange = async (username: string, currentIsActive: boolean) => {
    if (updating) return
    setUpdating(username)
    const newStatus = currentIsActive ? 0 : 1
    try {
      const result = await fetchUpdateStatusAccount(username, newStatus)
      if (result.success) {
        setAccounts((prev) =>
          prev.map((acc) =>
            acc.username === username ? { ...acc, status: newStatus } : acc
          )
        )
        toast.success(`Đã cập nhật trạng thái tài khoản ${username}`)
      } else {
        toast.error(result.message || "Không thể cập nhật trạng thái")
      }
    } catch (error) {
      toast.error("Đã xảy ra lỗi khi kết nối đến server")
    } finally {
      setUpdating(null)
    }
  }

  const handleDeleteAccount = async (username: string) => {
    if (!confirm(`Bạn có chắc chắn muốn xóa tài khoản ${username} không?`)) return

    try {
      const result = await fetchDeleteAccount(username)
      if (result.success !== false) {
        setAccounts((prev) => prev.filter((acc) => acc.username !== username))
        toast.success(result.message || "Xóa tài khoản thành công")
      } else {
        toast.error(result.message || "Không thể xóa tài khoản")
      }
    } catch (error) {
      console.error("Failed to delete account:", error)
      toast.error("Đã xảy ra lỗi khi xóa tài khoản")
    }
  }

  const indexOfLastAccount = currentPage * accountsPerPage;
  const indexOfFirstAccount = indexOfLastAccount - accountsPerPage;
  const currentAccounts = accounts.slice(indexOfFirstAccount, indexOfLastAccount);
  const totalPages = Math.ceil(accounts.length / accountsPerPage);

  return (
    <SidebarProvider
      style={
        {
          "--sidebar-width": "calc(var(--spacing) * 72)",
          "--header-height": "calc(var(--spacing) * 12)",
        } as React.CSSProperties
      }
    >
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-1 flex-col gap-4 p-4">
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
                <CardTitle>Danh sách tài khoản</CardTitle>
                <CardDescription>
                  Quản lý thông tin tài khoản
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="text-base py-4">Tên đăng nhập</TableHead>
                      <TableHead className="text-base py-4">Họ và tên</TableHead>
                      <TableHead className="text-base py-4">Vai trò</TableHead>
                      <TableHead className="text-base py-4">Trạng thái</TableHead>
                      <TableHead className="text-base py-4">Hành động</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {currentAccounts.length > 0 ? (
                      currentAccounts.map((account) => (
                        <TableRow key={account.username}>
                          <TableCell className="font-medium text-base py-4">{account.username}</TableCell>
                          <TableCell className="text-base py-4">{account.fullName}</TableCell>
                          <TableCell className="py-4">
                            <Badge variant="outline" className="text-sm px-3 py-1">{account.role}</Badge>
                          </TableCell>
                          <TableCell className="py-4">
                            <div 
                              className={`${isAdmin ? "cursor-pointer hover:opacity-80" : ""} inline-flex transition-opacity ${updating === account.username ? "opacity-50 pointer-events-none" : ""}`}
                              onClick={() => isAdmin && handleStatusChange(account.username, account.status === 1)}
                            >
                              {account.status === 1 ? (
                                <Badge className="bg-green-600 hover:bg-green-700 text-sm px-3 py-1">
                                  <IconCheck className="mr-1 size-4" /> Hoạt động
                                </Badge>
                              ) : (
                                <Badge variant="destructive" className="text-sm px-3 py-1">
                                  <IconX className="mr-1 size-4" /> Tạm dừng
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="py-4">
                            {isAdmin && (
                              <>
                                <Link href={`/account/update/${account.username}`}>
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                  >
                                    <IconPencil className="size-5 text-blue-500" />
                                  </Button>
                                </Link>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  onClick={() => handleDeleteAccount(account.username)}
                                >
                                  <IconTrash className="size-5 text-red-500" />
                                </Button>
                              </>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    ) : (
                      <TableRow>
                        <TableCell colSpan={5} className="h-24 text-center">
                          Không có dữ liệu.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
                <Pagination>
                  <PaginationContent>
                    <PaginationItem>
                      <PaginationPrevious
                        href="#"
                        onClick={(e) => {
                          e.preventDefault();
                          setCurrentPage((prev) => Math.max(prev - 1, 1));
                        }}
                      />
                    </PaginationItem>
                    {Array.from({ length: totalPages }, (_, index) => (
                      <PaginationItem key={index}>
                        <PaginationLink
                          href="#"
                          isActive={index + 1 === currentPage}
                          onClick={(e) => {
                            e.preventDefault();
                            setCurrentPage(index + 1);
                          }}
                        >
                          {index + 1}
                        </PaginationLink>
                      </PaginationItem>
                    ))}
                    <PaginationItem>
                      <PaginationNext
                        href="#"
                        onClick={(e) => {
                          e.preventDefault();
                          setCurrentPage((prev) => Math.min(prev + 1, totalPages));
                        }}
                      />
                    </PaginationItem>
                  </PaginationContent>
                </Pagination>
              </CardContent>
            </Card>
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

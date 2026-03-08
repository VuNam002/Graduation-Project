"use client"

import * as React from "react"
import {
  IconCheck,
  IconX,
  IconTrash   
} from "@tabler/icons-react"
import { toast } from "sonner"

import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
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
import { fetchAccounts, fetchUpdateStatusAccount, fetchDeleteAccount} from "@/lib/api"
import { Account } from "@/lib/types"
import { useAuth } from "@/lib/auth"

export default function AccountPage() {
  const [accounts, setAccounts] = React.useState<Account[]>([])
  const [loading, setLoading] = React.useState(true)
  const [updating, setUpdating] = React.useState<string | null>(null)
  const { user } = useAuth()
  const isAdmin = user?.role === "Admin"

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
                    {accounts.length > 0 ? (
                      accounts.map((account) => (
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
                                  <IconX className="mr-1 size-4" /> Vô hiệu hóa
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="py-4">
                            {isAdmin && (
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => handleDeleteAccount(account.username)}
                              >
                                <IconTrash className="size-5 text-red-500" />
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    ) : (
                      <TableRow>
                        <TableCell colSpan={4} className="h-24 text-center">
                          Không có dữ liệu.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
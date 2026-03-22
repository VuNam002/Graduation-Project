"use client"
import * as React from "react"
import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { EmployeesTable } from "@/components/employee-table" 

export default function EmployeePage() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <SiteHeader />
        <main className="flex flex-1 flex-col gap-4 p-4 md:gap-8 md:p-8">
          <div className="flex items-center justify-between space-y-2">
            <div>
              <h2 className="text-2xl font-bold tracking-tight">Quản lý Nhân sự & Dữ liệu Khuôn mặt</h2>
              <p className="text-muted-foreground">
                Quản lý thông tin nhân viên và định danh eKYC cho hệ thống AI nhận diện.
              </p>
            </div>
          </div>
          <div className="flex-1 w-full">
            <EmployeesTable />
          </div>
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}
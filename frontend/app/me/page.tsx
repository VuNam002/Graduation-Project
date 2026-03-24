"use client";

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { fetchMeAccount } from '@/lib/api';
import { MeAccountResponse } from '@/lib/types';
import { AppSidebar } from "@/components/app-sidebar";
import { SiteHeader } from "@/components/site-header";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Mail, UserCircle, Shield, Activity, ArrowLeft } from "lucide-react";

export default function MeProfilePage() {
  const router = useRouter();
  const [account, setAccount] = useState<MeAccountResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const getMeData = async () => {
      try {
        const res: any = await fetchMeAccount();
        if (res.data) {
          setAccount(res.data);
        } else {
          setAccount(res);
        }
      } catch (err: any) {
        setError("Không thể tải thông tin tài khoản. Phiên đăng nhập có thể đã hết hạn.");
        console.error("Error fetching Me profile:", err);
      } finally {
        setLoading(false);
      }
    };

    getMeData();
  }, []);

  const getStatusBadge = (status?: number) => {
    if (status === 1) {
      return <Badge className="bg-green-500 hover:bg-green-600 px-3 py-1 text-sm">Đang hoạt động</Badge>;
    }
    if (status === 0) {
      return <Badge variant="destructive" className="px-3 py-1 text-sm">Đã khóa</Badge>;
    }
    return <Badge variant="outline" className="px-3 py-1 text-sm">Không xác định</Badge>;
  };

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
        <div className="flex flex-1 flex-col gap-6 p-4 md:p-6">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.back()}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại
            </Button>
            <h1 className="text-2xl font-bold tracking-tight">Hồ sơ cá nhân</h1>
          </div>

          <Card className="mt-2 border shadow-sm">
            <CardHeader className="bg-muted/30 border-b pb-6">
              <CardTitle className="text-xl text-primary">Thông tin tài khoản</CardTitle>
              <CardDescription>
                Chi tiết thông tin tài khoản của bạn trên hệ thống
              </CardDescription>
            </CardHeader>

            <CardContent className="p-6 md:p-8">
              {loading ? (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  {[1, 2, 3, 4].map((i) => (
                    <div key={i} className="flex items-center gap-4 p-4 rounded-xl border border-muted">
                      <Skeleton className="h-12 w-12 rounded-full" />
                      <div className="space-y-2 flex-1">
                        <Skeleton className="h-4 w-[120px]" />
                        <Skeleton className="h-5 w-[200px]" />
                      </div>
                    </div>
                  ))}
                </div>
              ) : error ? (
                <div className="text-center text-red-500 py-10 flex flex-col items-center">
                  <p className="font-medium text-lg">{error}</p>
                  <Button variant="outline" className="mt-4" onClick={() => window.location.reload()}>
                    Thử lại
                  </Button>
                </div>
              ) : account ? (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="flex items-start gap-4 p-5 rounded-xl bg-muted/20 border border-muted/50 hover:shadow-sm transition-shadow">
                    <div className="p-3 bg-primary/10 rounded-full text-primary mt-1">
                      <UserCircle className="h-6 w-6" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-muted-foreground mb-1">Họ và tên</p>
                      <p className="text-lg font-semibold text-foreground">{account.fullName || "Chưa cập nhật"}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-4 p-5 rounded-xl bg-muted/20 border border-muted/50 hover:shadow-sm transition-shadow">
                    <div className="p-3 bg-primary/10 rounded-full text-primary mt-1">
                      <Mail className="h-6 w-6" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-muted-foreground mb-1">Tên đăng nhập / Email</p>
                      <p className="text-lg font-semibold text-foreground">{account.username}</p>
                    </div>
                  </div>

                  <div className="flex items-start gap-4 p-5 rounded-xl bg-muted/20 border border-muted/50 hover:shadow-sm transition-shadow">
                    <div className="p-3 bg-primary/10 rounded-full text-primary mt-1">
                      <Shield className="h-6 w-6" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-muted-foreground mb-1">Vai trò</p>
                      <p className="text-lg font-semibold text-foreground">{account.role}</p>
                    </div>
                  </div>

                  <div className="flex items-start gap-4 p-5 rounded-xl bg-muted/20 border border-muted/50 hover:shadow-sm transition-shadow">
                    <div className="p-3 bg-primary/10 rounded-full text-primary mt-1">
                      <Activity className="h-6 w-6" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-muted-foreground mb-2">Trạng thái</p>
                      <div className="mt-1">
                        {getStatusBadge(account.status)}
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="text-center text-muted-foreground py-8">
                  Không có dữ liệu tài khoản
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
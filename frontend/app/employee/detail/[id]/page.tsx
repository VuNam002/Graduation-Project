"use client";
 
 import * as React from "react";
 import { useRouter, useParams } from "next/navigation";
 import { fetchDetailEmployee } from "@/lib/api";
 import { toast } from "sonner";
 import { Employee } from "@/lib/types";
 import { AppSidebar } from "@/components/app-sidebar";
 import { SiteHeader } from "@/components/site-header";
 import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
 import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { 
  ArrowLeft, 
  User, 
  Briefcase, 
  Hash, 
  Calendar, 
  Fingerprint, 
  CheckCircle2,
  XCircle
} from "lucide-react";

export default function EmployeeDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params?.id ? Number(params.id) : null;

  const [employee, setEmployee] = React.useState<Employee | null>(null);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    if (!id) return;

    const getEmployeeData = async () => {
      setLoading(true);
      try {
        const data = await fetchDetailEmployee(id);
        const formattedData: Employee = {
          ...data,
          employee_Id: data.employee_Id,
          employee_Code: data.employee_Code,
          full_Name: data.full_Name,
          department: data.department,
          face_Vector: data.Face_Vector || (data as any).face_Vector || "",
          created_At: (data as any).created_At || null,
          is_Deleted: (data as any).is_Deleted || false,
        };
        setEmployee(formattedData);
      } catch (error) {
        console.error(error);
        toast.error("Không thể tải thông tin nhân viên.");
      } finally {
        setLoading(false);
      }
    };

    getEmployeeData();
  }, [id]);

  const formatDate = (dateString?: string) => {
    if (!dateString) return "Chưa cập nhật";
    return new Date(dateString).toLocaleString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  };

  const hasFaceData = employee?.face_Vector && employee.face_Vector.length > 10;

  return (
    <SidebarProvider>
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-1 flex-col gap-6 p-4 md:p-6 w-full">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.push("/employee")}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại
            </Button>
            <h1 className="text-2xl font-bold tracking-tight">Hồ sơ nhân viên</h1>
          </div>

          {loading ? (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div className="md:col-span-1 space-y-6">
                <Skeleton className="h-[300px] w-full rounded-xl" />
              </div>
              <div className="md:col-span-2 space-y-6">
                <Skeleton className="h-[200px] w-full rounded-xl" />
                <Skeleton className="h-[200px] w-full rounded-xl" />
              </div>
            </div>
          ) : !employee ? (
            <Card className="flex flex-col items-center justify-center h-64 text-muted-foreground border-dashed">
              <User className="h-12 w-12 mb-4 text-muted-foreground/50" />
              <p>Không tìm thấy dữ liệu nhân viên này.</p>
            </Card>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <Card className="md:col-span-1 shadow-sm h-fit">
                <CardContent className="pt-6 flex flex-col items-center text-center">
                  <div className="h-24 w-24 rounded-full bg-primary/10 flex items-center justify-center mb-4 ring-4 ring-background shadow-sm">
                    <User className="h-12 w-12 text-primary" />
                  </div>
                  <h2 className="text-xl font-bold mb-1">{employee.full_Name}</h2>
                  <div className="flex items-center gap-2 text-muted-foreground mb-4">
                    <Hash className="h-4 w-4" />
                    <span>{employee.employee_Code}</span>
                  </div>
                  
                  {employee.is_Deleted ? (
                    <Badge variant="destructive" className="flex items-center gap-1">
                      <XCircle className="h-3 w-3" /> Đã xóa/Nghỉ việc
                    </Badge>
                  ) : (
                    <Badge className="bg-green-500 hover:bg-green-600 flex items-center gap-1 text-white">
                      <CheckCircle2 className="h-3 w-3" /> Đang làm việc
                    </Badge>
                  )}
                </CardContent>
              </Card>
              <div className="md:col-span-2 space-y-6">
                <Card className="shadow-sm">
                  <CardHeader>
                    <CardTitle className="text-lg flex items-center gap-2">
                      <Briefcase className="h-5 w-5 text-blue-500" />
                      Thông tin công việc
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div className="p-3 bg-muted/40 rounded-lg">
                      <p className="text-xs text-muted-foreground mb-1">Phòng ban / Chức vụ</p>
                      <p className="font-medium">{employee.department || "Chưa cập nhật"}</p>
                    </div>
                    <div className="p-3 bg-muted/40 rounded-lg">
                      <p className="text-xs text-muted-foreground flex items-center gap-1 mb-1">
                        <Calendar className="h-3 w-3" /> Ngày tạo hồ sơ
                      </p>
                      <p className="font-medium">{formatDate(employee.created_At)}</p>
                    </div>
                  </CardContent>
                </Card>

                <Card className="shadow-sm border-blue-100 dark:border-blue-900">
                  <CardHeader>
                    <CardTitle className="text-lg flex items-center gap-2">
                      <Fingerprint className="h-5 w-5 text-indigo-500" />
                      Dữ liệu sinh trắc học (AI Face Vector)
                    </CardTitle>
                    <CardDescription>
                      Dữ liệu số hóa khuôn mặt được sử dụng để AI nhận diện nhân sự qua Camera.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    {hasFaceData ? (
                      <div className="space-y-3">
                        <Badge variant="outline" className="bg-indigo-50 text-indigo-700 border-indigo-200 dark:bg-indigo-950/50 dark:text-indigo-300 dark:border-indigo-800">
                          Trạng thái: Đã thu thập thành công
                        </Badge>
                        <div className="relative">
                        </div>
                      </div>
                    ) : (
                      <div className="p-4 border-2 border-dashed border-red-200 bg-red-50 text-red-600 rounded-lg flex flex-col items-center justify-center text-center dark:bg-red-950/20 dark:border-red-900/50">
                        <Fingerprint className="h-8 w-8 mb-2 opacity-50" />
                        <p className="font-medium text-sm">Chưa có dữ liệu khuôn mặt</p>
                        <p className="text-xs mt-1 opacity-80">Vui lòng cập nhật khuôn mặt để hệ thống AI có thể nhận diện.</p>
                      </div>
                    )}
                  </CardContent>
                </Card>
              </div>
            </div>
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
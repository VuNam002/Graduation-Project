"use client";
import * as React from "react"; 
import { useRouter, useParams } from "next/navigation";
import { fetchDetailEmployee, fetUpdateEmployee } from "@/lib/api";
import { toast } from "sonner";
import { AppSidebar } from "@/components/app-sidebar";
import { SiteHeader } from "@/components/site-header";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ArrowLeft, Check, Loader2 } from "lucide-react";
import { UpdateEmployee} from "@/lib/types";

export default function UpdateEmployeePage() {
  const router = useRouter();
  const params = useParams();
  const id = params?.employee_id ? Number(params.employee_id) : null;

  const [loading, setLoading] = React.useState(true);
  const [submitting, setSubmitting] = React.useState(false);
  const [formData, setFormData] = React.useState<UpdateEmployee>({
    employee_Code: "",
    full_Name: "",
    department: "",
  });

  React.useEffect(() => {
    if (!id) return;

    const getEmployeeData = async () => {
      setLoading(true);
      try {
        const data = await fetchDetailEmployee(id);
        if (data) {
          setFormData({
            employee_Code: data.employee_Code || "",
            full_Name: data.full_Name || "",
            department: data.department || "",
          });
        }
      } catch (error) {
        console.error(error);
        toast.error("Không thể tải thông tin nhân viên.");
      } finally {
        setLoading(false);
      }
    };

    getEmployeeData();
  }, [id]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { id, value } = e.target;
    setFormData((prev) => ({ ...prev, [id]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id) return;
    if (!formData.full_Name || !formData.employee_Code || !formData.department) {
      toast.error("Vui lòng điền đầy đủ thông tin");
      return;
    }

    setSubmitting(true);
    try {
      const payload = JSON.stringify(formData);
      const result = await fetUpdateEmployee(id, payload as any);

      if (result && result.success !== false) {
        toast.success(result.message || "Cập nhật nhân viên thành công");
        router.push("/employee");
        router.refresh();
      } else {
        toast.error(result?.message || "Không thể cập nhật nhân viên");
      }
    } catch (error) {
      console.error(error);
      toast.error("Đã xảy ra lỗi khi cập nhật nhân viên.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <SidebarProvider>
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-1 flex-col gap-6 p-4 md:p-6 w-full max-w-3xl mx-auto">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.back()}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại
            </Button>
            <h1 className="text-2xl font-bold tracking-tight">Cập nhật nhân viên</h1>
          </div>

          <Card className="shadow-sm">
            <form onSubmit={handleSubmit}>
              <CardHeader>
                <CardTitle>Thông tin nhân viên</CardTitle>
                <CardDescription>
                  Chỉnh sửa thông tin cơ bản và công việc của nhân sự
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {loading ? (
                  <div className="flex justify-center items-center py-12">
                    <Loader2 className="h-8 w-8 animate-spin text-primary" />
                  </div>
                ) : (
                  <>
                    <div className="space-y-2">
                      <Label htmlFor="full_Name">Họ và tên <span className="text-red-500">*</span></Label>
                      <Input id="full_Name" placeholder="Nhập họ và tên" value={formData.full_Name} onChange={handleChange} required />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="employee_Code">Mã nhân viên <span className="text-red-500">*</span></Label>
                      <Input id="employee_Code" placeholder="Nhập mã nhân viên" value={formData.employee_Code} onChange={handleChange} required />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="department">Mô tả / Phòng ban <span className="text-red-500">*</span></Label>
                      <Input id="department" placeholder="Ví dụ: IT, Nhân sự..." value={formData.department} onChange={handleChange} required />
                    </div>
                  </>
                )}
              </CardContent>
              <CardFooter className="flex justify-end gap-2 border-t p-4">
                <Button variant="outline" type="button" onClick={() => router.back()} disabled={loading || submitting}>
                  Hủy
                </Button>
                <Button type="submit" disabled={loading || submitting}>
                  {submitting ? (
                    <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Đang lưu...</>
                  ) : (
                    <><Check className="mr-2 h-4 w-4" /> Lưu thay đổi</>
                  )}
                </Button>
              </CardFooter>
            </form>
          </Card>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
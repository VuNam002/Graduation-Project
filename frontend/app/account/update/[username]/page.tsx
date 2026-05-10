"use client";

import * as React from "react";
import { useRouter, useParams } from "next/navigation";
import { fetchDetailAccount, fetchUpdateAccount, getUserFromToken } from "@/lib/api";
import { toast } from "sonner";
import { Account, UserRole, FormData, isAdmin } from "@/lib/types";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ArrowLeft, Eye, EyeOff, Check, Loader2 } from "lucide-react";


export default function EditAccountPage() {
  const router = useRouter();
  const params = useParams();
  const usernameParam = params.username as string;

  const [loading, setLoading] = React.useState(false);
  const [isAuthorized, setIsAuthorized] = React.useState(false);
  const [formData, setFormData] = React.useState<FormData>({
    username: "",
    fullName: "",
    passwordHash: "",
    role: UserRole.User,
  });
  const [errors, setErrors] = React.useState<Partial<Record<keyof FormData, string>>>({});
  const [showPassword, setShowPassword] = React.useState(false);

  React.useEffect(() => {
    const user = getUserFromToken();
    if (!isAdmin(user)) {
      toast.error("Bạn không có quyền truy cập trang này");
      router.push("/dashboard");
      return;
    }

    if (!usernameParam) return;

    const getAccountData = async () => {
      setLoading(true);
      try {
        const accountData = await fetchDetailAccount(usernameParam);
        setFormData({
          username: accountData.username ?? usernameParam,
          fullName: accountData.fullName ?? "",
          passwordHash: "", 
          role: (accountData.role as UserRole) ?? UserRole.User,
        });
        setIsAuthorized(true);
      } catch {
        toast.error("Không thể tải thông tin tài khoản.");
        router.push("/account");
      } finally {
        setLoading(false);
      }
    };

    getAccountData();
  }, [usernameParam, router]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target; 
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name as keyof FormData]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const handleRoleChange = (value: string) => {
    setFormData((prev) => ({ ...prev, role: value as UserRole }));
  };

  const validateForm = (): boolean => {
    const newErrors: Partial<Record<keyof FormData, string>> = {};

    if (!formData.fullName.trim()) {
      newErrors.fullName = "Họ và tên là bắt buộc";
    }

    if (formData.passwordHash && formData.passwordHash.length < 6) {
      newErrors.passwordHash = "Mật khẩu mới phải có ít nhất 6 ký tự";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateForm()) return;
    setLoading(true);

    const payload: Account = {
      username: formData.username,
      fullName: formData.fullName,
      role: formData.role,
      status: 1, 
      ...(formData.passwordHash ? { passwordHash: formData.passwordHash } : {}),
    };

    try {
      const result = await fetchUpdateAccount(payload);

      if (result.success) {
        toast.success("Cập nhật tài khoản thành công", {
          icon: <Check className="text-green-500" />,
        });
        router.push("/account");
      } else {
        const errorMsg = result.message || "Không thể cập nhật tài khoản";
        toast.error(errorMsg);
      }
    } catch (err) {
      const errorMsg = err instanceof Error 
        ? err.message
        : "Đã xảy ra lỗi khi kết nối đến server";
      toast.error(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  // Khóa chặt trang: Chỉ render khi đã xác thực quyền Admin thành công
  if (!isAuthorized) {
    return (
      <div className="flex h-screen w-full items-center justify-center">
        <Loader2 className="h-10 w-10 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <SidebarProvider>
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-col gap-6 p-4 md:p-6">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.push("/account")}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại danh sách
            </Button>
            <h1 className="text-2xl font-bold">Chỉnh sửa tài khoản</h1>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Thông tin tài khoản</CardTitle>
              <CardDescription>
                Cập nhật thông tin chi tiết cho tài khoản. Để trống mật khẩu nếu không muốn thay đổi.
              </CardDescription>
            </CardHeader>

            <form onSubmit={handleSubmit} autoComplete="off">
              <CardContent className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Full Name */}
                  <div className="space-y-2">
                    <Label htmlFor="fullName">
                      Họ và tên <span className="text-red-500">*</span>
                    </Label>
                    <Input
                      id="fullName"
                      name="fullName"
                      placeholder="Nhập họ và tên"
                      value={formData.fullName}
                      onChange={handleChange}
                      className={errors.fullName ? "border-red-500" : ""}
                    />
                    {errors.fullName && (
                      <p className="text-red-500 text-sm">{errors.fullName}</p>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <Label htmlFor="passwordHash">Mật khẩu mới</Label>
                    <div className="relative">
                      <Input
                        id="passwordHash"
                        name="passwordHash"
                        type={showPassword ? "text" : "password"}
                        placeholder="Để trống nếu không thay đổi"
                        value={formData.passwordHash}
                        onChange={handleChange}
                        className={errors.passwordHash ? "border-red-500 pr-10" : "pr-10"}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="absolute right-0 top-0 h-full w-10 hover:bg-transparent"
                        onClick={() => setShowPassword((v) => !v)}
                        tabIndex={-1}
                      >
                        {showPassword ? (
                          <EyeOff className="h-4 w-4 text-muted-foreground" />
                        ) : (
                          <Eye className="h-4 w-4 text-muted-foreground" />
                        )}
                      </Button>
                    </div>
                    {errors.passwordHash && (
                      <p className="text-red-500 text-sm">{errors.passwordHash}</p>
                    )}
                  </div>

                  {/* Role */}
                  <div className="space-y-2">
                    <Label htmlFor="role">Vai trò</Label>
                    <Select value={formData.role} onValueChange={handleRoleChange}>
                      <SelectTrigger id="role">
                        <SelectValue placeholder="Chọn vai trò" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={UserRole.Admin}>Admin</SelectItem>
                        <SelectItem value={UserRole.User}>User</SelectItem>
                        <SelectItem value={UserRole.Staff}>Staff</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </CardContent>

              <CardFooter className="flex justify-end gap-2">
                <Button variant="outline" type="button" onClick={() => router.back()}>
                  Hủy
                </Button>
                <Button type="submit" disabled={loading}>
                  {loading ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Đang lưu...
                    </>
                  ) : (
                    "Lưu thay đổi"
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
"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { fetchUpdateMeAccount } from "@/lib/api";
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
import { ArrowLeft, Check, Loader2, Eye, EyeOff } from "lucide-react";
import { fetchMeAccount } from "@/lib/api";

interface FormData {
  fullName: string;
  password: string;
  confirmPassword: string;
}

export default function UpdateMePage() {
  const router = useRouter();
  const [loading, setLoading] = React.useState(false);
  const [fetching, setFetching] = React.useState(true);
  const [showPassword, setShowPassword] = React.useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = React.useState(false);

  const [formData, setFormData] = React.useState<FormData>({
    fullName: "",
    password: "",
    confirmPassword: "",
  });
  const [errors, setErrors] = React.useState<Partial<Record<keyof FormData, string>>>({});

  React.useEffect(() => {
    const getMeData = async () => {
      try {
        const res: any = await fetchMeAccount();
        if (res.data) {
          setFormData((prev) => ({ ...prev, fullName: res.data.fullName || "" }));
        } else if (res.fullName) {
          setFormData((prev) => ({ ...prev, fullName: res.fullName || "" }));
        }
      } catch (err) {
        toast.error("Không thể tải thông tin tài khoản.");
      } finally {
        setFetching(false);
      }
    };

    getMeData();
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (errors[name as keyof FormData]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Partial<Record<keyof FormData, string>> = {};

    if (!formData.fullName.trim()) {
      newErrors.fullName = "Họ và tên là bắt buộc";
    }

    if (formData.password && formData.password.length < 6) {
      newErrors.password = "Mật khẩu phải có ít nhất 6 ký tự";
    }

    if (formData.password && formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = "Mật khẩu xác nhận không khớp";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateForm()) return;
    setLoading(true);

    const payload = {
      fullName: formData.fullName,
      ...(formData.password ? { password: formData.password } : {}),
    };

    try {
      const result = await fetchUpdateMeAccount(payload);

      if (result.success !== false) {
        toast.success("Cập nhật thông tin cá nhân thành công", {
          icon: <Check className="text-green-500" />,
        });
        router.push("/me");
      } else {
        toast.error(result.message || "Không thể cập nhật thông tin");
      }
    } catch {
      toast.error("Đã xảy ra lỗi khi kết nối đến server");
    } finally {
      setLoading(false);
    }
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
            <Button variant="outline" size="sm" onClick={() => router.push("/me")}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại hồ sơ
            </Button>
            <h1 className="text-2xl font-bold">Chỉnh sửa thông tin cá nhân</h1>
          </div>

          {fetching ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>Cập nhật tài khoản</CardTitle>
                <CardDescription>
                  Thay đổi họ tên hoặc mật khẩu của bạn. Để trống mật khẩu nếu không muốn thay đổi.
                </CardDescription>
              </CardHeader>

              <form onSubmit={handleSubmit} autoComplete="off">
                <CardContent className="space-y-6">
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

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label htmlFor="password">Mật khẩu mới</Label>
                      <div className="relative">
                        <Input
                          id="password"
                          name="password"
                          type={showPassword ? "text" : "password"}
                          placeholder="Để trống nếu không thay đổi"
                          value={formData.password}
                          onChange={handleChange}
                          className={errors.password ? "border-red-500 pr-10" : "pr-10"}
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
                      {errors.password && (
                        <p className="text-red-500 text-sm">{errors.password}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="confirmPassword">Xác nhận mật khẩu</Label>
                      <div className="relative">
                        <Input
                          id="confirmPassword"
                          name="confirmPassword"
                          type={showConfirmPassword ? "text" : "password"}
                          placeholder="Nhập lại mật khẩu mới"
                          value={formData.confirmPassword}
                          onChange={handleChange}
                          disabled={!formData.password}
                          className={errors.confirmPassword ? "border-red-500 pr-10" : "pr-10"}
                        />
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="absolute right-0 top-0 h-full w-10 hover:bg-transparent"
                          onClick={() => setShowConfirmPassword((v) => !v)}
                          disabled={!formData.password}
                          tabIndex={-1}
                        >
                          {showConfirmPassword ? (
                            <EyeOff className="h-4 w-4 text-muted-foreground" />
                          ) : (
                            <Eye className="h-4 w-4 text-muted-foreground" />
                          )}
                        </Button>
                      </div>
                      {errors.confirmPassword && (
                        <p className="text-red-500 text-sm">{errors.confirmPassword}</p>
                      )}
                    </div>
                  </div>
                </CardContent>

                <CardFooter className="flex justify-end gap-2 border-t pt-6 mt-2">
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
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}

"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { fetchMeAccount, fetchUpdateMeAccount, getUserFromToken } from "@/lib/api";
import { UserRole } from "@/lib/types";
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
import { ArrowLeft, Eye, EyeOff, Check, Loader2, User } from "lucide-react";

interface FormData {
  fullName: string;
  password: string;
}

export default function ProfilePage() {
  const router = useRouter();

  const [loading, setLoading] = React.useState(false);
  const [initLoading, setInitLoading] = React.useState(true);
  const [isLoggedIn, setIsLoggedIn] = React.useState(false);
  const [profileData, setProfileData] = React.useState<{ username: string; role: UserRole | "" }>({ username: "", role: "" });
  const [formData, setFormData] = React.useState<FormData>({
    fullName: "",
    password: "",
  });
  const [errors, setErrors] = React.useState<Partial<Record<keyof FormData, string>>>({});
  const [showPassword, setShowPassword] = React.useState(false);

  React.useEffect(() => {
    const user = getUserFromToken();
    if (!user) {
      router.push("/login");
      return;
    }
    setIsLoggedIn(true);

    const getMyProfile = async () => {
      try {
        const data = await fetchMeAccount();
        setProfileData({
          username: data.username,
          role: data.role,
        });
        setFormData({
          fullName: data.fullName ?? "",
          password: "", 
        });
      } catch {
        toast.error("Không thể tải thông tin hồ sơ của bạn.");
      } finally {
        setInitLoading(false);
      }
    };

    getMyProfile();
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
      newErrors.password = "Mật khẩu mới phải có ít nhất 6 ký tự";
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

      if (result.success) {
        toast.success(result.message || "Cập nhật hồ sơ thành công", {
          icon: <Check className="text-green-500" />,
        });
        setFormData(prev => ({ ...prev, password: "" }));
      } else {
        toast.error(result.message || "Không thể cập nhật hồ sơ");
      }
    } catch {
      toast.error("Đã xảy ra lỗi khi kết nối đến server");
    } finally {
      setLoading(false);
    }
  };

  if (!isLoggedIn) return null;

  return (
    <SidebarProvider>
      <AppSidebar variant="inset"/>
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-col gap-6 p-4 md:p-8 max-w-5xl mx-auto w-full">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.back()}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại
            </Button>
            <h1 className="text-2xl font-bold flex items-center gap-2">
               Hồ sơ cá nhân
            </h1>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Thông tin của bạn</CardTitle>
              <CardDescription>
                Cập nhật thông tin cá nhân. Để trống trường mật khẩu nếu bạn không muốn thay đổi.
              </CardDescription>
            </CardHeader>

            {initLoading ? (
              <CardContent className="flex justify-center py-10">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
              </CardContent>
            ) : (
              <form onSubmit={handleSubmit} autoComplete="off">
                <CardContent className="space-y-6">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label>Tên đăng nhập</Label>
                      <Input value={profileData.username} disabled className="bg-muted" />
                    </div>
                    <div className="space-y-2">
                      <Label>Vai trò</Label>
                      <Input value={profileData.role} disabled className="bg-muted" />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="fullName">Họ và tên <span className="text-red-500">*</span></Label>
                      <Input id="fullName" name="fullName" placeholder="Nhập họ và tên" value={formData.fullName} onChange={handleChange} className={errors.fullName ? "border-red-500" : ""} />
                      {errors.fullName && <p className="text-red-500 text-sm">{errors.fullName}</p>}
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="password">Mật khẩu mới</Label>
                      <div className="relative">
                        <Input id="password" name="password" type={showPassword ? "text" : "password"} placeholder="Để trống nếu không đổi mật khẩu" value={formData.password} onChange={handleChange} className={errors.password ? "border-red-500 pr-10" : "pr-10"} />
                        <Button type="button" variant="ghost" size="icon" className="absolute right-0 top-0 h-full w-10 hover:bg-transparent" onClick={() => setShowPassword((v) => !v)} tabIndex={-1}>
                          {showPassword ? <EyeOff className="h-4 w-4 text-muted-foreground" /> : <Eye className="h-4 w-4 text-muted-foreground" />}
                        </Button>
                      </div>
                      {errors.password && <p className="text-red-500 text-sm">{errors.password}</p>}
                    </div>
                  </div>
                </CardContent>

                <CardFooter className="flex justify-end gap-2 mt-4">
                  <Button variant="outline" type="button" onClick={() => { setFormData(prev => ({ ...prev, password: "" })); setErrors({}); }}>
                    Làm mới
                  </Button>
                  <Button type="submit" disabled={loading}>
                    {loading ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Đang lưu...</> : "Lưu thay đổi"}
                  </Button>
                </CardFooter>
              </form>
            )}
          </Card>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
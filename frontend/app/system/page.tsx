"use client"

import * as React from "react"
import { fetchUpdateSystemConfig, fetchSystemAll } from "@/lib/api";
import { System } from "@/lib/types";
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
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Settings2, Save, RotateCcw, BrainCircuit, ShieldAlert } from "lucide-react";

export default function SystemSettingsPage() {
  const [config, setConfig] = React.useState<System>({ confidenceThreshold: 0.3, nmsThreshold: 0.5, activeModel: "YOLOv8" });
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    const loadConfig = async () => {
      setLoading(true);
      try {
        const res: any = await fetchSystemAll();
        if (res.data) {
          setConfig(res.data);
        } else {
          setConfig(res); 
        }
      } catch (error) {
        toast.error("Không thể tải cấu hình hệ thống");
        console.error(error);
      } finally {
        setLoading(false);
      }
    };
    loadConfig();
  }, []);

  const handleChange = (key: keyof System, value: number) => {
    let safeValue = isNaN(value) ? 0.01 : value;
    if (safeValue > 1) safeValue = 1;
    if (safeValue < 0.01) safeValue = 0.01;
    
    setConfig((prev) => ({ ...prev, [key]: safeValue }));
  };

  const handleSave = async () => {
    if (config.confidenceThreshold <= 0 || config.confidenceThreshold > 1) {
      toast.error("Confidence Threshold phải nằm trong khoảng (0, 1]");
      return;
    }
    if (config.nmsThreshold <= 0 || config.nmsThreshold > 1) {
      toast.error("NMS Threshold phải nằm trong khoảng (0, 1]");
      return;
    }

    setSaving(true);
    try {
      const res = await fetchUpdateSystemConfig(config);
      if (res.success) {
        toast.success(res.message || "Cập nhật cấu hình AI thành công");
      } else {
        toast.error(res.message || "Cập nhật thất bại");
      }
    } catch (error) {
      toast.error("Lỗi kết nối khi lưu cấu hình");
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    setConfig({ confidenceThreshold: 0.3, nmsThreshold: 0.5, activeModel: "YOLOv8" });
    toast.info("Đã đặt lại thông số về mặc định (Nhấn Lưu để áp dụng)");
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
          <div className="flex items-center gap-3">
            <Settings2 className="h-7 w-7 text-primary" />
            <h1 className="text-2xl font-bold tracking-tight">Cài đặt hệ thống</h1>
          </div>
          {loading ? (
            <Card>
              <CardHeader>
                <Skeleton className="h-6 w-48 mb-2" />
                <Skeleton className="h-4 w-72" />
              </CardHeader>
              <CardContent className="space-y-8">
                <Skeleton className="h-20 w-full" />
                <Skeleton className="h-20 w-full" />
              </CardContent>
            </Card>
          ) : (
            <Card className="border-muted shadow-sm">
              <CardHeader className="bg-muted/20 border-b pb-6 mb-6">
                <CardTitle className="text-xl">Cấu hình mô hình AI YOLO</CardTitle>
                <CardDescription className="text-sm">
                  Điều chỉnh các thông số này sẽ ảnh hưởng trực tiếp đến độ nhạy và cách AI nhận diện vi phạm trên luồng Camera. Camera sẽ tự động áp dụng cấu hình mới sau mỗi 10 giây.
                </CardDescription>
              </CardHeader>
              
              <CardContent className="space-y-10">
                {/* Active Model */}
                <div className="space-y-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1">
                      <Label className="text-base flex items-center gap-2 font-semibold">
                        <BrainCircuit className="h-4 w-4 text-purple-500" /> Mô hình AI (Active Model)
                      </Label>
                      <p className="text-sm text-muted-foreground">Chọn phiên bản mô hình YOLO (YOLOv8 hoặc YOLOv11) để sử dụng cho việc nhận diện trên luồng camera.</p>
                    </div>
                    <Select
                      value={config.activeModel || "YOLOv8"}
                      onValueChange={(value) => setConfig((prev) => ({ ...prev, activeModel: value }))}
                    >
                      <SelectTrigger className="w-32 font-semibold">
                        <SelectValue placeholder="Chọn model" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="YOLOv8">YOLOv8</SelectItem>
                        <SelectItem value="YOLOv11">YOLOv11</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                {/* Confidence Threshold */}
                <div className="space-y-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1">
                      <Label className="text-base flex items-center gap-2 font-semibold">
                        <BrainCircuit className="h-4 w-4 text-blue-500" /> Ngưỡng độ tin cậy (Confidence Threshold)
                      </Label>
                      <p className="text-sm text-muted-foreground">Giá trị càng lớn, AI càng khắt khe khi báo lỗi (giảm báo động giả nhưng có thể bỏ sót). Khoảng từ 0.01 đến 1.00.</p>
                    </div>
                    <Input
                      type="number"
                      min="0.01" max="1" step="0.01"
                      value={config.confidenceThreshold}
                      onChange={(e) => handleChange("confidenceThreshold", parseFloat(e.target.value))}
                      className="w-24 text-center font-semibold"
                    />
                  </div>
                  <input type="range" min="0.01" max="1" step="0.01" value={config.confidenceThreshold} onChange={(e) => handleChange("confidenceThreshold", parseFloat(e.target.value))} className="w-full h-2 bg-secondary rounded-lg appearance-none cursor-pointer accent-primary" />
                </div>

                {/* NMS Threshold */}
                <div className="space-y-4 pt-2">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1">
                      <Label className="text-base flex items-center gap-2 font-semibold">
                        <ShieldAlert className="h-4 w-4 text-orange-500" /> Ngưỡng triệt tiêu hộp nhiễu (NMS Threshold)
                      </Label>
                      <p className="text-sm text-muted-foreground">Giúp gộp các vùng nhận diện trùng lặp. Giá trị càng nhỏ, AI càng hợp nhất nhiều ô vuông lại với nhau.</p>
                    </div>
                    <Input
                      type="number"
                      min="0.01" max="1" step="0.01"
                      value={config.nmsThreshold}
                      onChange={(e) => handleChange("nmsThreshold", parseFloat(e.target.value))}
                      className="w-24 text-center font-semibold"
                    />
                  </div>
                  <input type="range" min="0.01" max="1" step="0.01" value={config.nmsThreshold} onChange={(e) => handleChange("nmsThreshold", parseFloat(e.target.value))} className="w-full h-2 bg-secondary rounded-lg appearance-none cursor-pointer accent-primary" />
                </div>
              </CardContent>

              <CardFooter className="flex justify-between border-t bg-muted/10 py-4 mt-6">
                <Button variant="outline" onClick={handleReset} type="button" disabled={saving}>
                  <RotateCcw className="mr-2 h-4 w-4" /> Đặt lại mặc định
                </Button>
                <Button onClick={handleSave} disabled={saving}>
                  <Save className="mr-2 h-4 w-4" /> 
                  {saving ? "Đang lưu..." : "Lưu thay đổi"}
                </Button>
              </CardFooter>
            </Card>
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
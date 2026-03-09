"use client"

import * as React from "react"
import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Camera, Play, Square, Activity, Tv, Video } from "lucide-react"
import { useCamera } from "../../hooks/use-camera"

export default function CameraPage() {
  const { isStreaming, frameData, fps, wsStatus, startCamera, stopCamera } = useCamera(0);

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
          <div className="flex items-center justify-between">
             <h1 className="text-2xl font-bold flex items-center gap-2">
               <Video className="h-6 w-6" /> Giám sát Camera
             </h1>
          </div>

          {/* Status Cards */}
          <div className="grid gap-4 md:grid-cols-3">
             <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle className="text-sm font-medium">Trạng thái</CardTitle>
                  <Camera className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">
                    {isStreaming ? "Đang phát" : "Đã dừng"}
                  </div>
                  <p className="text-xs text-muted-foreground">Camera ID: 0</p>
                </CardContent>
             </Card>
             <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle className="text-sm font-medium">Kết nối</CardTitle>
                  <Activity className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div className="flex items-center gap-2">
                     <Badge variant={wsStatus === 'connected' ? 'default' : wsStatus === 'error' ? 'destructive' : 'secondary'}>
                        {wsStatus === 'connected' ? 'Đã kết nối' : wsStatus === 'connecting' ? 'Đang kết nối' : wsStatus === 'error' ? 'Lỗi' : 'Ngắt kết nối'}
                     </Badge>
                  </div>
                </CardContent>
             </Card>
             <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle className="text-sm font-medium">Tốc độ khung hình</CardTitle>
                  <Tv className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{fps} FPS</div>
                </CardContent>
             </Card>
          </div>

          {/* Main Video Feed */}
          <Card className="flex-1 flex flex-col overflow-hidden min-h-[500px]">
            <CardHeader>
              <CardTitle>Live View</CardTitle>
              <CardDescription>Hình ảnh trực tiếp từ camera giám sát</CardDescription>
            </CardHeader>
            <CardContent className="flex-1 bg-black/95 flex items-center justify-center p-0 relative overflow-hidden rounded-md mx-6 mb-6 border border-border/50">
               {isStreaming && frameData ? (
                 <img 
                   src={frameData} 
                   alt="Camera Feed" 
                   className="w-full h-full object-contain"
                 />
               ) : (
                 <div className="flex flex-col items-center justify-center text-muted-foreground gap-3 animate-pulse">
                    <Camera className="h-20 w-20 opacity-20" />
                    <p className="text-lg font-medium">Không có tín hiệu</p>
                    <p className="text-sm opacity-70">Vui lòng bật camera để xem hình ảnh</p>
                 </div>
               )}
               
               {/* Overlay Status */}
               {isStreaming && (
                 <div className="absolute top-4 right-4 flex items-center gap-2 bg-black/50 px-3 py-1 rounded-full backdrop-blur-sm">
                    <span className="relative flex h-3 w-3">
                      <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
                      <span className="relative inline-flex rounded-full h-3 w-3 bg-red-500"></span>
                    </span>
                    <span className="text-white text-xs font-bold">LIVE</span>
                 </div>
               )}
            </CardContent>
            <CardFooter className="flex justify-between border-t p-4 bg-muted/20">
               <div className="flex gap-3">
                  <Button 
                    size="lg"
                    className={isStreaming ? "bg-red-600 hover:bg-red-700" : "bg-green-600 hover:bg-green-700"}
                    onClick={isStreaming ? stopCamera : startCamera}
                    disabled={wsStatus === 'connecting'}
                  >
                    {isStreaming ? (
                      <>
                        <Square className="mr-2 h-5 w-5 fill-current" /> Dừng Camera
                      </>
                    ) : (
                      <>
                        <Play className="mr-2 h-5 w-5 fill-current" /> Bật Camera
                      </>
                    )}
                  </Button>
               </div>
               <div className="text-sm text-muted-foreground flex items-center">
                  {wsStatus === 'error' && <span className="text-red-500">Lỗi kết nối đến máy chủ</span>}
               </div>
            </CardFooter>
          </Card>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

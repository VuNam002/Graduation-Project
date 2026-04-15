"use client"

import * as React from "react"
import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from "@/components/ui/card"
import { fetchVideoUrl, getBackendUrl } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { toast } from "sonner"
import { Loader2, UploadCloud, FileVideo, Download } from "lucide-react"

export default function VideoPage() {
  const [file, setFile] = React.useState<File | null>(null)
  const [loading, setLoading] = React.useState<boolean>(false)
  const [resultVideo, setResultVideo] = React.useState<string | null>(null)

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0])
      setResultVideo(null)
    }
  }

  const handleUpload = async () => {
    if (!file) {
      toast.error("Vui lòng chọn một file video để tải lên.")
      return
    }

    setLoading(true)

    const formData = new FormData()
    formData.append("file", file)

    try {
      const res = await fetchVideoUrl(formData)
      toast.success(res.message || "Xử lý video thành công!")
      if (res.videoUrl) {
        setResultVideo(`${getBackendUrl()}${res.videoUrl.startsWith('/') ? '' : '/'}${res.videoUrl}`)
      } 
    } catch (error) {
      console.error("Upload error:", error)
      toast.error("Đã xảy ra lỗi khi tải video lên máy chủ.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <SidebarProvider>
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-1 flex-col gap-6 p-4 md:p-6">
          <div className="flex flex-col gap-2">
            <h1 className="text-2xl font-bold tracking-tight">Xử lý Video AI Offline</h1>
            <p className="text-muted-foreground">
              Tải lên một đoạn video có sẵn để hệ thống YOLO quét, nhận diện và xuất ra video kết quả.
            </p>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card className="h-fit shadow-sm">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <UploadCloud className="h-5 w-5 text-blue-500" />
                  Tải lên Video
                </CardTitle>
                <CardDescription>
                  Hỗ trợ định dạng MP4, WebM, AVI... Dung lượng khuyến nghị &lt; 50MB để xử lý nhanh.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid w-full max-w-sm items-center gap-1.5">
                  <Input
                    id="video-upload"
                    type="file"
                    accept="video/mp4,video/webm,video/x-msvideo,video/*"
                    onChange={handleFileChange}
                    disabled={loading}
                  />
                </div>
                {file && (
                  <div className="text-sm text-muted-foreground flex items-center gap-2">
                    <FileVideo className="h-4 w-4" />
                    <span>Đã chọn: <strong>{file.name}</strong> ({(file.size / 1024 / 1024).toFixed(2)} MB)</span>
                  </div>
                )}
              </CardContent>
              <CardFooter>
                <Button onClick={handleUpload} disabled={!file || loading} className="w-full sm:w-auto">
                  {loading ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Hệ thống AI đang xử lý...
                    </>
                  ) : (
                    "Bắt đầu quét AI"
                  )}
                </Button>
              </CardFooter>
            </Card>

            <Card className="shadow-sm">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <FileVideo className="h-5 w-5 text-green-500" />
                  Video Kết Quả
                </CardTitle>
                <CardDescription>
                  Video đã được vẽ bounding box nhận diện sẽ hiển thị tại đây.
                </CardDescription>
              </CardHeader>
              <CardContent>
                {!resultVideo && !loading && (
                  <div className="flex flex-col items-center justify-center p-8 border-2 border-dashed rounded-lg bg-muted/50 text-muted-foreground">
                    <FileVideo className="h-12 w-12 mb-3 opacity-20" />
                    <p>Chưa có video nào được xử lý.</p>
                  </div>
                )}

                {loading && (
                  <div className="flex flex-col items-center justify-center p-8 border-2 border-dashed rounded-lg bg-muted/50 text-muted-foreground">
                    <Loader2 className="h-12 w-12 mb-3 animate-spin text-primary" />
                    <p>Đang thực hiện nhận diện trên từng frame...</p>
                    <p className="text-xs mt-2 text-center opacity-70">
                      Đang chờ luồng hình ảnh trực tiếp từ máy chủ...
                    </p>
                  </div>
                )}

                {resultVideo && !loading && (
                  <div className="flex flex-col gap-4">
                    <div className="relative w-full aspect-video bg-black rounded-lg overflow-hidden shadow-md border">
                      <video 
                        src={resultVideo} 
                        controls 
                        autoPlay 
                        className="absolute inset-0 w-full h-full object-contain"
                      />
                    </div>
                    <Button variant="outline" asChild className="w-full sm:w-auto self-start">
                      <a href={resultVideo} download target="_blank" rel="noreferrer">
                        <Download className="mr-2 h-4 w-4" />
                        Tải xuống Video Kết Quả
                      </a>
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
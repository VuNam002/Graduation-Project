"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { fetchCreateEmployee } from "@/lib/api"
import { toast } from "sonner"
import { AppSidebar } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { ArrowLeft, Camera, Check, RefreshCw } from "lucide-react"

const ANGLES = [
  { id: "center", label: "Nhìn thẳng" },
  { id: "left", label: "Quay trái" },
  { id: "right", label: "Quay phải" },
  { id: "up", label: "Ngẩng lên" },
  { id: "down", label: "Cúi xuống" },
]

export default function CreateEmployeePage() {
  const router = useRouter()
  const [loading, setLoading] = React.useState(false)

  const [formData, setFormData] = React.useState({
    fullName: "",
    employeeCode: "",
    department: "",
  })

  const videoRef = React.useRef<HTMLVideoElement>(null)
  const canvasRef = React.useRef<HTMLCanvasElement>(null)
  const streamRef = React.useRef<MediaStream | null>(null)

  const [isCapturing, setIsCapturing] = React.useState(false)
  const [currentAngleIndex, setCurrentAngleIndex] = React.useState(0)
  const [photos, setPhotos] = React.useState<Record<string, Blob>>({})
  const [photoUrls, setPhotoUrls] = React.useState<Record<string, string>>({})

  const playSuccessBeep = () => {
    try {
      const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)()
      const oscillator = audioCtx.createOscillator()
      const gainNode = audioCtx.createGain()
      oscillator.connect(gainNode)
      gainNode.connect(audioCtx.destination)
      oscillator.type = "sine" 
      oscillator.frequency.setValueAtTime(800, audioCtx.currentTime) 
      gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime) 
      oscillator.start()
      oscillator.stop(audioCtx.currentTime + 0.15) 
    } catch (e) {
      console.error("Trình duyệt không hỗ trợ Web Audio API", e)
    }
  }

  React.useEffect(() => {
    return () => {
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((t) => t.stop())
      }
    }
  }, [])

  // Tự động gắn luồng stream vào thẻ video ngay khi thẻ video được render (isCapturing = true)
  React.useEffect(() => {
    if (isCapturing && videoRef.current && streamRef.current) {
      videoRef.current.srcObject = streamRef.current
      videoRef.current.play().catch((e) => console.error("Lỗi phát video:", e))
    }
  }, [isCapturing])

  const startCamera = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { width: 640, height: 480 },
      })
      streamRef.current = stream
      setIsCapturing(true)
      setCurrentAngleIndex(0)
      setPhotos({})
      setPhotoUrls({})
    } catch {
      toast.error("Không thể truy cập camera. Vui lòng kiểm tra quyền truy cập trên trình duyệt.")
    }
  }

  const stopCamera = () => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop())
      streamRef.current = null
    }
    setIsCapturing(false)
  }

  const capturePhoto = () => {
    if (videoRef.current && canvasRef.current) {
      const video = videoRef.current
      // Đảm bảo video đã có dữ liệu kích thước trước khi vẽ lên canvas
      if (video.videoWidth === 0 || video.videoHeight === 0) {
        toast.error("Camera chưa sẵn sàng, vui lòng đợi giây lát rồi thử lại.")
        return
      }

      const canvas = canvasRef.current
      canvas.width = video.videoWidth
      canvas.height = video.videoHeight
      const ctx = canvas.getContext("2d")
      if (ctx) {
        // Vẽ ảnh lật ngược lại để file lưu đúng chiều thực (do video ở frontend đang được dùng hiệu ứng mirror)
        ctx.translate(canvas.width, 0)
        ctx.scale(-1, 1)
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height)
        
        canvas.toBlob((blob) => {
          if (blob) {
            const angleId = ANGLES[currentAngleIndex].id
            const url = URL.createObjectURL(blob)

            setPhotos((prev) => ({ ...prev, [angleId]: blob }))
            setPhotoUrls((prev) => ({ ...prev, [angleId]: url }))

            if (currentAngleIndex < ANGLES.length - 1) {
              setCurrentAngleIndex((prev) => prev + 1)
            } else {
              stopCamera()
            }
          }
        }, "image/jpeg", 0.9)
      }
    }
  }

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { id, value } = e.target
    setFormData((prev) => ({ ...prev, [id]: value }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!formData.fullName || !formData.employeeCode || !formData.department) {
      toast.error("Vui lòng điền đầy đủ thông tin nhân viên")
      return
    }
    if (Object.keys(photos).length < 5) {
      toast.error("Vui lòng chụp đủ 5 góc mặt để định danh eKYC")
      return
    }

    setLoading(true)
    const payload = new FormData()
    payload.append("fullName", formData.fullName)
    payload.append("employeeCode", formData.employeeCode)
    payload.append("department", formData.department)

    ANGLES.forEach((angle) => {
      if (photos[angle.id]) {
        payload.append("faceImages", photos[angle.id], `${angle.id}.jpg`)
      }
    })

    try {
      const result = await fetchCreateEmployee(payload)
      if (result && result.success !== false) {
        playSuccessBeep()
        const countMsg = result.totalAnglesEnrolled !== undefined ? ` (Đã trích xuất ${result.totalAnglesEnrolled}/5 ảnh)` : ""
        toast.success((result.message || "Thêm nhân viên thành công") + countMsg)
        router.push("/employee")
      } else {
        toast.error(result?.message || "Không thể thêm nhân viên")
      }
    } catch (error ) {
      console.error(error)
      toast.error("Đã xảy ra lỗi khi thêm nhân viên. Vui lòng thử lại.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <SidebarProvider>
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-col gap-6 p-4 md:p-6">
          <div className="flex items-center gap-4">
            <Button variant="outline" size="sm" onClick={() => router.push("/employee")}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại
            </Button>
            <h1 className="text-2xl font-bold">Thêm nhân viên mới (eKYC)</h1>
          </div>

          <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card className="h-fit">
              <CardHeader>
                <CardTitle>Thông tin cơ bản</CardTitle>
                <CardDescription>Nhập thông tin cá nhân của nhân viên</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="fullName">Họ và tên <span className="text-red-500">*</span></Label>
                  <Input id="fullName" placeholder="Nhập họ và tên" value={formData.fullName} onChange={handleChange} required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="employeeCode">Mã nhân viên <span className="text-red-500">*</span></Label>
                  <Input id="employeeCode" placeholder="Nhập mã nhân viên" value={formData.employeeCode} onChange={handleChange} required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="department">Mô tả <span className="text-red-500">*</span></Label>
                  <Input id="department" placeholder="Ví dụ: IT, Nhân sự..." value={formData.department} onChange={handleChange} required />
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Dữ liệu khuôn mặt (eKYC)</CardTitle>
                <CardDescription>Cần chụp 5 góc độ khuôn mặt để hệ thống AI nhận diện chính xác</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col items-center gap-4">
                {!isCapturing && Object.keys(photos).length < 5 && (
                  <div className="w-full aspect-video bg-muted rounded-md border-2 border-dashed flex flex-col items-center justify-center p-6 text-center text-muted-foreground">
                    <Camera className="w-12 h-12 mb-4 opacity-50" />
                    <p className="mb-4">Hệ thống yêu cầu chụp ảnh từ camera trực tiếp</p>
                    <Button type="button" onClick={startCamera}>
                      Bắt đầu chụp ảnh
                    </Button>
                  </div>
                )}

                {isCapturing && (
                  <div className="w-full flex flex-col items-center gap-4">
                    <div className="relative w-full aspect-video bg-black rounded-md overflow-hidden flex items-center justify-center">
                      <video ref={videoRef} className="absolute inset-0 w-full h-full object-cover transform scale-x-[-1]" playsInline muted />
                      <div className="absolute inset-0 border-[4px] border-primary/60 rounded-[50%] m-6 md:m-12 pointer-events-none" style={{ borderRadius: '50% 50% 50% 50% / 60% 60% 40% 40%'}}></div>
                      
                      <div className="absolute bottom-4 left-0 right-0 text-center z-10">
                        <div className="inline-block bg-black/60 text-white px-4 py-2 rounded-full font-medium shadow-sm">
                          Góc {currentAngleIndex + 1}/5: {ANGLES[currentAngleIndex].label}
                        </div>
                      </div>
                    </div>
                    
                    <Button type="button" size="lg" className="w-full max-w-sm rounded-full" onClick={capturePhoto}>
                      <Camera className="mr-2 h-5 w-5" />
                      Chụp ảnh ({ANGLES[currentAngleIndex].label})
                    </Button>
                  </div>
                )}

                {Object.keys(photos).length === 5 && !isCapturing && (
                  <div className="w-full space-y-4">
                    <div className="p-3 bg-green-500/10 text-green-700 border border-green-200 rounded-md flex items-center gap-2">
                      <Check className="w-5 h-5 flex-shrink-0" />
                      <span className="text-sm font-medium">Đã thu thập đủ 5 ảnh định danh.</span>
                    </div>
                    
                    <div className="grid grid-cols-5 gap-2">
                      {ANGLES.map((angle) => (
                        <div key={angle.id} className="flex flex-col items-center gap-1">
                          <div className="aspect-square w-full rounded bg-muted overflow-hidden relative border">
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            {photoUrls[angle.id] && <img src={photoUrls[angle.id]} alt={angle.label} className="object-cover w-full h-full transform scale-x-[-1]" />}
                          </div>
                          <span className="text-[10px] text-muted-foreground text-center leading-tight font-medium">
                            {angle.label}
                          </span>
                        </div>
                      ))}
                    </div>

                    <Button type="button" variant="outline" className="w-full" onClick={startCamera}>
                      <RefreshCw className="mr-2 h-4 w-4" />
                      Chụp lại từ đầu
                    </Button>
                  </div>
                )}

                <canvas ref={canvasRef} className="hidden" />
              </CardContent>
              <CardFooter className="flex justify-end gap-2 border-t p-4">
                <Button variant="outline" type="button" onClick={() => router.back()} disabled={loading}>
                  Hủy
                </Button>
                <Button type="submit" disabled={loading || Object.keys(photos).length < 5}>
                  {loading ? "Đang xử lý..." : "Lưu nhân viên"}
                </Button>
              </CardFooter>
            </Card>
          </form>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
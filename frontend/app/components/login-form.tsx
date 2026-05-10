"use client"

import { useState } from "react"
import { useRouter } from "next/navigation"
import Image from "next/image"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { fetchlogin } from "@/lib/api"
import { Input } from "@/components/ui/input"
import { useAuth } from "@/lib/auth"
import { toast } from "sonner"
import { isLoginSuccess } from "@/lib/types"

export function LoginForm({
  className,
  ...props
}: React.ComponentProps<"div">) {
  const router = useRouter()
  const { login } = useAuth()
  const [username, setUsername] = useState("")
  const [password, setPassword] = useState("")
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setLoading(true)

    try {
      const response = await fetchlogin(username, password)
      
      if (isLoginSuccess(response) && response.user) {
        login(response.user)
        const redirectPath = "/dashboard";
        
        toast.success("Đăng nhập thành công!")
        router.push(redirectPath)
      } else {
        const errorMsg = response.message || 
                         "Đăng nhập thất bại. Vui lòng kiểm tra tên đăng nhập và mật khẩu."
        toast.error(errorMsg)
      }
    } catch (err) {
      let errorMsg = err instanceof Error 
        ? err.message
        : "Đã xảy ra lỗi không mong muốn."
      
      console.error("Login error:", err);
      toast.error(errorMsg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className={cn("fixed inset-0 w-full h-full flex items-center justify-center p-4 overflow-hidden shadow-none", className)} {...props}>
      {/* Lớp hình nền bao phủ toàn bộ viewport */}
      <div className="absolute inset-0 z-0">
        <Image
          src="/utc-15-tang.jpg"
          alt="Background"
          fill
          priority
          className="object-cover object-center pointer-events-none"
        />
        <div className="absolute inset-0 bg-gradient-to-br from-black/60 via-black/40 to-black/60 backdrop-blur-[1px]" />
      </div>

      <Card className="relative z-10 w-full max-w-md bg-white/95 backdrop-blur-md shadow-[0_20px_50px_rgba(0,0,0,0.3)] border-none ring-1 ring-white/20">
        <CardHeader>
          <CardTitle className="text-3xl font-bold text-center tracking-tight text-slate-900 mb-1">Đăng nhập</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="username">Tên đăng nhập</FieldLabel>
                <Input
                  id="username"
                  type="text"
                  placeholder="username"
                  required
                  value={username}
                  onChange={(e) => setUsername(e.target.value.trim())}
                  disabled={loading}
                  autoComplete="username"
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="password">Mật khẩu</FieldLabel>
                <Input
                  id="password"
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={loading}
                  autoComplete="current-password"
                />
              </Field>
              <Field>
                <Button type="submit" disabled={loading} className="w-full">
                  {loading ? "Đang đăng nhập..." : "Đăng nhập"}
                </Button>
                <FieldDescription className="text-center mt-4">
                  Vũ Hà Nam ❤️ TUD-K63{" "}
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
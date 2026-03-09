"use client"

import { useState } from "react"
import { useRouter } from "next/navigation"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
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
        const errorMsg = response.message || "Đăng nhập thất bại. Vui lòng kiểm tra tên đăng nhập và mật khẩu."
        toast.error(errorMsg)
      }
    } catch (err) {
      const errorMsg = err instanceof Error 
        ? err.message 
        : "Đã xảy ra lỗi không mong muốn."
      console.error("Login error:", err);
      toast.error(errorMsg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className={cn("flex flex-col gap-6", className)} {...props}>
      <Card>
        <CardHeader>
          <CardTitle>Đăng nhập</CardTitle>
          <CardDescription>
            Nhập tên đăng nhập và mật khẩu để tiếp tục.
          </CardDescription>
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
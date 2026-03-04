import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

export function middleware(request: NextRequest) {
  // Lấy token từ cookie
  const token = request.cookies.get('token')?.value
  const { pathname } = request.nextUrl

  // 1. Nếu người dùng ĐÃ đăng nhập mà cố truy cập trang login -> chuyển hướng về dashboard
  if (pathname.startsWith('/login') && token) {
    return NextResponse.redirect(new URL('/dashboard', request.url))
  }

  // 2. Nếu người dùng CHƯA đăng nhập mà cố truy cập trang dashboard (hoặc các trang bảo mật khác) -> chuyển hướng về login
  if (pathname.startsWith('/dashboard') && !token) {
    return NextResponse.redirect(new URL('/login', request.url))
  }

  // Cho phép tiếp tục nếu không vi phạm các điều kiện trên
  return NextResponse.next()
}

// Cấu hình các đường dẫn mà middleware sẽ chạy qua
export const config = {
  matcher: ['/dashboard/:path*', '/login'],
}
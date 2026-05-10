import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const token = request.cookies.get('token')?.value;
  const { pathname } = request.nextUrl;

  if (!token) {
    if (pathname === '/login') return NextResponse.next();
    return NextResponse.redirect(new URL('/login', request.url));
  }

  try {
    const payloadBase64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const decodedPayload = JSON.parse(atob(payloadBase64));

    // Lấy role từ payload (kiểm tra cả 'role', 'Role' và chuẩn Claim Type)
    const rawRole = 
      decodedPayload.role ||
      decodedPayload.Role ||
      decodedPayload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    
    const role = Array.isArray(rawRole) ? rawRole[0] : rawRole;

    if (pathname.startsWith('/account') || pathname.startsWith('/employee')) {
      if (role !== 'Admin' && role !== 'admin') {
        return NextResponse.redirect(new URL('/dashboard', request.url));
      }
    }

    // 2. Các trang Admin và Staff được vào (User bị chặn)
    if (pathname.startsWith('/violation') || pathname.startsWith('/system')) {
      if (role !== 'Admin' && role !== 'Staff') {
        return NextResponse.redirect(new URL('/dashboard', request.url));
      }
    }

    return NextResponse.next();
  } catch (error) {
    // Nếu token lỗi, xóa cookie và về login
    const response = NextResponse.redirect(new URL('/login', request.url));
    response.cookies.delete('token');
    return response;
  }
}

export const config = {
  matcher: [
    '/dashboard/:path*',
    '/account/:path*',
    '/employee/:path*',
    '/violation/:path*',
    '/system/:path*',
    '/camera/:path*',
    '/me/:path*',
  ],
};
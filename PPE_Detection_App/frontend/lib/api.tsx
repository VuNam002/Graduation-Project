import { LoginResponse, AccountDetail } from './types';

const API_URL = 'https://localhost:7215/api';

// Generic API helper function
async function api<T>(url: string, options?: RequestInit): Promise<T> {
  try {
    const response = await fetch(url, {
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
      ...options,
    });

    // Try to get error details from response
    let errorDetails = '';
    try {
      const errorBody = await response.clone().json();
      errorDetails = errorBody.message || errorBody.title || JSON.stringify(errorBody);
    } catch {
      errorDetails = await response.text();
    }

    if (!response.ok) {
      throw new Error(
        `API error (${response.status}): ${errorDetails || response.statusText}`
      );
    }

    return response.json();
  } catch (error) {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      throw new Error('Không thể kết nối đến server. Vui lòng kiểm tra kết nối mạng.');
    }
    throw error;
  }
}

// Helper to safely access localStorage
function getStoredToken(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem("token");
}

function setStoredToken(token: string): void {
  if (typeof window !== 'undefined') {
    localStorage.setItem("token", token);
    // Lưu cookie để Middleware có thể đọc được (hết hạn sau 8 giờ giống backend)
    document.cookie = `token=${token}; path=/; max-age=28800; SameSite=Lax`;
  }
}

export function clearStoredToken(): void {
  if (typeof window !== 'undefined') {
    localStorage.removeItem("token");
    // Xóa cookie
    document.cookie = "token=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
  }
}

// Decode JWT token to get user info (no API call needed)
function decodeJWT(token: string): AccountDetail | null {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    
    const payload = JSON.parse(jsonPayload);
    
    // Extract user info from JWT payload
    // Adjust these field names based on your JWT structure
    return {
      id: payload.sub || payload.id || payload.userId || '',
      username: payload.username || payload.name || payload.unique_name || '',
      email: payload.email || '',
      role: payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'User',
    };
  } catch (error) {
    console.error('Failed to decode JWT:', error);
    return null;
  }
}

export async function fetchlogin(
  username: string,
  password: string
): Promise<LoginResponse & { user?: AccountDetail }> {
  try {
    console.log('Attempting login with username:', username);
    
    const data = await api<{ token: string }>(`${API_URL}/Admin/login`, {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });

    console.log('Login successful, received token');

    const token = data?.token;

    if (!token) {
      return { message: "Received an empty token." };
    }

    setStoredToken(token);
    
    // Decode JWT to get user info
    const user = decodeJWT(token);
    
    return { token, user: user || undefined };
  } catch (error: unknown) {
    console.error("Login API error:", error);
    const errorMessage = error instanceof Error 
      ? error.message 
      : "An unexpected error occurred.";
    return { message: errorMessage };
  }
}

export function getUserFromToken(): AccountDetail | null {
  const token = getStoredToken();
  if (!token) return null;
  return decodeJWT(token);
}
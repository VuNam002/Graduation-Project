import { LoginResponse, AccountDetail, DashboardResponse, RecentViolationsResponse } from './types';

const API_URL = 'https://localhost:7215/api';

// Generic API helper function
async function api<T>(url: string, options: RequestInit = {}): Promise<T> {
  try {
    const headers: HeadersInit = { ...options.headers };

    // Only add Content-Type for requests with a body
    if (options.body) {
      headers['Content-Type'] = 'application/json';
    }

    const response = await fetch(url, {
      ...options,
      headers,
    });

    let errorDetails = '';
    if (!response.ok) {
      try {
        const errorBody = await response.json();
        errorDetails = errorBody.title || errorBody.message || JSON.stringify(errorBody.errors) || JSON.stringify(errorBody);
      } catch {
        errorDetails = await response.text();
      }
      throw new Error(
        `API error (${response.status}): ${errorDetails || response.statusText}`
      );
    }

    // Handle cases where response is empty
    const text = await response.text();
    return text ? (JSON.parse(text) as T) : ({} as T);

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
    document.cookie = `token=${token}; path=/; max-age=28800; SameSite=Lax`;
  }
}

export function clearStoredToken(): void {
  if (typeof window !== 'undefined') {
    localStorage.removeItem("token");
    document.cookie = "token=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
  }
}

// Decode JWT token to get user info
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

function getAuthHeaders(): Record<string, string> {
  const token = getStoredToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

// ===== AUTH =====

export async function fetchlogin(
  username: string,
  password: string
): Promise<LoginResponse & { user?: AccountDetail }> {
  try {
    const data = await api<{ token: string }>(`${API_URL}/Admin/login`, {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    });

    const token = data?.token;

    if (!token) {
      return { message: 'Received an empty token.' };
    }

    setStoredToken(token);

    const user = decodeJWT(token);

    return { token, user: user || undefined };
  } catch (error: unknown) {
    console.error('Login API error:', error);
    const errorMessage =
      error instanceof Error ? error.message : 'An unexpected error occurred.';
    return { message: errorMessage };
  }
}

export function getUserFromToken(): AccountDetail | null {
  const token = getStoredToken();
  if (!token) return null;
  return decodeJWT(token);
}

export interface DashboardParams {
  daysRange?: number; 
}

export async function fetchDashboardOverview(
  params?: DashboardParams
): Promise<DashboardResponse> {
  const query = new URLSearchParams();

  if (params?.daysRange !== undefined) {
    query.append('daysRange', String(params.daysRange));
  }

  const queryString = query.toString();
  const url = `${API_URL}/Dashboard/overview${queryString ? `?${queryString}` : ''}`;

  return api<DashboardResponse>(url, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export async function fetchRealtimeViolations(): Promise<RecentViolationsResponse> {
  return api<RecentViolationsResponse>(`${API_URL}/Dashboard/realtime`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export async function fetchRecentViolations(): Promise<RecentViolationsResponse> {
  return api<RecentViolationsResponse>(`${API_URL}/Dashboard/recent`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}
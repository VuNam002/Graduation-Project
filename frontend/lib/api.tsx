import { LoginResponse, AccountDetail, DashboardResponse, RecentViolationsResponse, DashboardMonthlyResponse, DashboardWidgetsResponse, Account, CameraResponse, PaginatedViolationsResponse, ViolationCategory } from './types';

const API_URL = 'https://localhost:7215/api';
async function api<T>(url: string, options: RequestInit = {}): Promise<T> {
  try {     
    const headers: Record<string, string> = { ...options.headers };
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

    const text = await response.text();
    return text ? (JSON.parse(text) as T) : ({} as T);

  } catch (error) {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      throw new Error('Không thể kết nối đến server. Vui lòng kiểm tra kết nối mạng.');
    }
    throw error;
  }
}

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

export async function fetchDashboardWidgets(): Promise<DashboardWidgetsResponse> {
  return api<DashboardWidgetsResponse>(`${API_URL}/Dashboard/widgets`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export interface DashboardMonthlyParams {
  year?: number;
  month?: number;
}

export async function fetchDashboardMonthly(
  params?: DashboardMonthlyParams
): Promise<DashboardMonthlyResponse> {
  const query = new URLSearchParams();

  if (params?.year !== undefined) {
    query.append('year', String(params.year));
  }
  if (params?.month !== undefined) {
    query.append('month', String(params.month));
  }

  const queryString = query.toString();
  const url = `${API_URL}/Dashboard/monthly${queryString ? `?${queryString}` : ''}`;

  return api<DashboardMonthlyResponse>(url, {
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

export async function fetchAccounts(): Promise<Account[]> {
  return api<Account[]>(`${API_URL}/Admin/accounts`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export async function fetchDeleteAccount(username: string): Promise<{ success: boolean; message?: string }> {
  return api<{ success: boolean; message?: string }>(`${API_URL}/Admin/delete-account/${encodeURIComponent(username)}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });
}

export async function fetchCreateAccount(account: { username: string; fullName: string; role: string; password: string }): Promise<{ success: boolean; message?: string }> {
  return api<{ success: boolean; message?: string }>(`${API_URL}/Admin/create-account`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(account),
  });
}

export async function fetchUpdateStatusAccount(username: string, status: number): Promise<{ success: boolean; message?: string }> {
  return api<{ success: boolean; message?: string }>(`${API_URL}/Admin/status-account`, {
    method: 'PATCH',
    headers: getAuthHeaders(),
    body: JSON.stringify({ username, status }),
  });
}

export async function fetchCameraStatus(cameraId: number | string): Promise<any> {
  return api<any>(`${API_URL}/Stream/status/${cameraId}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export async function fetchStartCamera(cameraId: number | string): Promise<CameraResponse> {
  return api<CameraResponse>(`${API_URL}/Stream/start/${cameraId}`, {
    method: 'POST',
    headers: {
      ...getAuthHeaders(),
      'Content-Type': 'application/json',
    },
  });
}

export async function logout(): Promise<void> {
  await api<void>(`${API_URL}/Auth/logout`, {
    method: 'POST',
    headers: {
      ...getAuthHeaders(),
      'Content-Type': 'application/json',
    },
  });
  localStorage.removeItem('token');
}

export async function fetchStopCamera(cameraId: number | string): Promise<CameraResponse> {
  return api<CameraResponse>(`${API_URL}/Stream/stop/${cameraId}`, {
    method: 'POST',
    headers: {
      ...getAuthHeaders(),
      'Content-Type': 'application/json',
    },
  });
}

export interface ViolationParams {
  page?: number;
  pageSize?: number;
  fromDate?: string;
  toDate?: string;
  categoryId?: string;
  status?: number | string;
}

export async function fetchViolations(
  params: ViolationParams = {}
): Promise<PaginatedViolationsResponse> {
  const query = new URLSearchParams();

  if (params.page) query.append('page', String(params.page));
  if (params.pageSize) query.append('pageSize', String(params.pageSize));
  if (params.fromDate) query.append('fromDate', params.fromDate);
  if (params.toDate) query.append('toDate', params.toDate);
  if (params.categoryId && params.categoryId !== 'all') query.append('categoryId', params.categoryId);
  if (params.status && params.status !== 'all') query.append('status', String(params.status));

  const queryString = query.toString();
  const url = `${API_URL}/Violation${queryString ? `?${queryString}` : ''}`;

  return api<PaginatedViolationsResponse>(url, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}

export async function fetchUpdateViolationStatus(
  id: number,
  status: 0 | 1 | 2
): Promise<{ success: boolean; message?: string }> {
  return api<{ success: boolean; message?: string }>(`${API_URL}/Violation/status`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify({ violationId: id, status: status }),
  });
}

export async function fetchDeleteViolation(
  id: number
): Promise<{ success: boolean; message?: string }> {
  return api<{ success: boolean; message?: string }>(`${API_URL}/Violation/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });
}

export async function fetchCategories(): Promise<ViolationCategory[]> {
  return api<ViolationCategory[]>(`${API_URL}/Category`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });
}
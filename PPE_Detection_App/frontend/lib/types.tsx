export interface LoginResponse {
  token?: string;
  message?: string;
  user?: AccountDetail;
}

export interface ApiError {
  message: string;
  statusCode?: number;
  errors?: Record<string, string[]>;
}

export interface AccountDetail {
  id: string;
  username: string;
  email?: string;
  role?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface AuthState {
  user: AccountDetail | null;
  isAuthenticated: boolean;
  loading: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export enum UserRole {
  Admin = 'Admin',
  User = 'User',
  Guest = 'Guest',
}

export function isLoginSuccess(response: LoginResponse): response is LoginResponse & { token: string; user: AccountDetail } {
  return !!response.token && !!response.user;
}

export function hasRole(user: AccountDetail | null, role: UserRole): boolean {
  return user?.role === role;
}

export function isAdmin(user: AccountDetail | null): boolean {
  return hasRole(user, UserRole.Admin);
}
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

export interface TopCategory {
  id: string | null;
  displayName: string | null;
}

export interface TodaySummary {
  date: string | null;
  totalViolations: string | null;
  newViolations: number;
  viewedViolations: number;
  falseAlerts: number;
  avgConfidence: number;
  topCategory: TopCategory;
}

export interface ChartDataset {
  label: string;
  data: number[];
  borderColor: string;
  backgroundColor: string;
}

export interface PieChartDataset {
  data: number[];
  backgroundColor: string[];
}

export interface BarChartDataset {
  label: string;
  data: number[];
  backgroundColor: string;
}

export interface ViolationsTrend {
  labels: string[];
  datasets: ChartDataset[];
  rawData: unknown[];
}

export interface ViolationsByCategory {
  labels: string[];
  datasets: PieChartDataset[];
  rawData: unknown[];
}

export interface TopViolation {
  id?: string;
  name?: string;
  count?: number;
  [key: string]: unknown;
}

export interface PeakHours {
  labels: string[];
  datasets: BarChartDataset[];
  rawData: unknown[];
}

export interface TrendPeriod {
  startDate: string;
  endDate: string;
  count: number;
}

export interface TrendChange {
  percentage: number;
  isIncreasing: boolean;
  direction: 'stable' | 'increasing' | 'decreasing';
  text: string;
  color: string;
}

export interface Trend {
  currentPeriod: TrendPeriod;
  previousPeriod: TrendPeriod;
  change: TrendChange;
}

export interface DashboardPeriod {
  startDate: string;
  endDate: string;
  days: number;
}

export interface DashboardResponse {
  success: boolean;
  generatedAt: string;
  period: DashboardPeriod;
  todaySummary: TodaySummary;
  violationsTrend: ViolationsTrend;
  violationsByCategory: ViolationsByCategory;
  topViolations: TopViolation[];
  peakHours: PeakHours;
  trend: Trend;
}

export interface RecentSummary {
  totalToday: number;
  newCount: number;
  last30Minutes: number;
}

export interface RecentViolation {
  id?: string;
  [key: string]: unknown;
}

export interface RecentViolationsResponse {
  success: boolean;
  timestamp: string;
  summary: RecentSummary;
  recentViolations: RecentViolation[];
}

export interface MonthlyPeriod {
  year: number;
  month: number;
  monthName: string;
  startDate: string;
  endDate: string;
}

export interface MonthlySummary {
  totalViolations: number;
  avgPerDay: number;
  peakDay: string;
  peakDayCount: number;
}

export interface DailyStat {
  date: string;
  dayOfWeek: string;
  total: number;
  new_count: number;
  viewed: number;
  falseAlert: number;
}

export interface CategoryStat {
  categoryId: string;
  displayName: string;
  count: number;
  percentage: number;
  avgConfidence: number;
}

export interface DashboardMonthlyResponse {
  success: boolean;
  period: MonthlyPeriod;
  summary: MonthlySummary;
  dailyStats: DailyStat[];
  categoryStats: CategoryStat[];
}

export interface WidgetItem {
  value: number;
  label: string;
  change?: number;
  changePercent?: number;
  trend?: string;
  percentage?: number;
  avgPerDay?: number;
  icon: string;
  color: string;
}

export interface DashboardWidgetsResponse {
  success: boolean;
  widgets: {
    today: WidgetItem;
    newViolations: WidgetItem;
    thisWeek: WidgetItem;
    thisMonth: WidgetItem;
  };
}

export interface Account {
  username: string;
  fullName: string;
  role: string;
  status: number;
}

export interface StatusAccount {
  success: boolean;
  message?: string;
}

export interface CreateAccount {
  username: string;
  password: string;
  fullName: string;
  role: string;
}

export interface CameraResponse {
  message: string;
  cameraId: number;
}

export interface ViolationLog {
  id: number
  categoryId: string
  categoryDisplayName?: string
  severityLevel?: number
  colorCode?: string
  imagePath: string
  confidenceScore?: number
  detectedTime: string
  boxX?: number
  boxY?: number
  boxW?: number
  boxH?: number
  status: number       
  isDeleted: boolean
}

export interface ViolationCategory {
  id: string
  displayName: string
  severityLevel: number
  colorCode: string
  count: number       
}

export interface PaginatedViolationsResponse {
  data: ViolationLog[]
  totalCount: number
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
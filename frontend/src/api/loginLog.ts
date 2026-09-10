import { get } from './request'
import type { PagedResult } from './user'

/** 登录日志行（对应后端 LoginLogListItemDto） */
export interface LoginLogListItem {
  id: string
  userId: string
  username: string
  displayName: string
  loginAt: string
  ipAddress: string | null
  userAgent: string | null
}

/** 登录日志查询参数（对应后端 GetLoginLogsRequest；时间均为 UTC ISO 串） */
export interface LoginLogQuery {
  username?: string
  startTime?: string
  endTime?: string
  page: number
  pageSize: number
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC。
 * 库内统一存 UTC，按 UTC 直接比较；这样"选今天"能覆盖刚产生的记录。
 */
export function toUtcRange(
  startDate?: string,
  endDate?: string,
): { startTime?: string; endTime?: string } {
  const startTime = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : undefined
  const endTime = endDate ? new Date(`${endDate}T23:59:59.999`).toISOString() : undefined
  return { startTime, endTime }
}

/** 分页查询登录日志（只读，按登录时间倒序） */
export function getLoginLogs(query: LoginLogQuery): Promise<PagedResult<LoginLogListItem>> {
  return get<PagedResult<LoginLogListItem>>('/login-logs', { params: query })
}

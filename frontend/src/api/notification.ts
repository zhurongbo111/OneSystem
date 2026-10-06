import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 站内信类型（0 低库存 / 1 近效期 / 2 过期 / 5 逾期应收预留，对应后端 NotificationType） */
export type NotificationType = 0 | 1 | 2 | 5

/** 站内信出参（对应后端 NotificationListItemDto） */
export interface NotificationItem {
  id: string
  type: NotificationType
  title: string
  content: string
  /** 跳转路由名（null = 仅提示不跳转） */
  linkRouteName: string | null
  /** 跳转 query（JSON 文本，`router.push({ name, query })` 前的 query） */
  linkQuery: string | null
  /** 业务对象标识（排查用） */
  resourceKey: string | null
  isRead: boolean
  readAt: string | null
  createdAt: string
}

/** 站内信列表查询参数（对应后端 GetNotificationsRequest） */
export interface NotificationListQuery {
  type?: NotificationType
  isRead?: boolean
  /** 标题关键词 */
  keyword?: string
  page: number
  pageSize: number
}

/** 顶栏未读汇总（对应后端 NotificationSummaryDto） */
export interface NotificationSummary {
  unreadCount: number
  recent: NotificationItem[]
}

/** 全部标记已读结果（对应后端 MarkAllNotificationsReadResponse） */
export interface MarkAllNotificationsReadResult {
  affectedCount: number
}

/** 库存预警扫描统计（对应后端 StockAlertScanResultDto） */
export interface StockAlertScanResult {
  signalCount: number
  lowStockCount: number
  expiringBatchCount: number
  expiredBatchCount: number
  messageCount: number
  skippedCount: number
  recipientCount: number
  truncated: boolean
}

/**
 * 消息类型文案与颜色映射（全项目唯一来源，顶栏与列表页共用）：
 * 与后端 `NotificationType` 整型取值一一对应。
 */
export const NOTIFICATION_TYPE_META: Record<NotificationType, { label: string; color: string }> = {
  0: { label: '低库存', color: 'red' },
  1: { label: '近效期', color: 'orange' },
  2: { label: '已过期', color: 'gray' },
  5: { label: '逾期应收', color: 'arcoblue' },
}

/** 类型下拉选项（顺序即映射表顺序） */
export const NOTIFICATION_TYPE_OPTIONS = (Object.keys(NOTIFICATION_TYPE_META) as unknown as NotificationType[]).map(
  (value) => ({ label: NOTIFICATION_TYPE_META[value].label, value }),
)

/** 分页查询本人站内信（类型 / 已读状态 / 标题关键词筛选） */
export function getNotifications(query: NotificationListQuery): Promise<PagedResult<NotificationItem>> {
  return get<PagedResult<NotificationItem>>('/notifications', { params: query })
}

/** 未读汇总（未读数 + 最近若干条，顶栏铃铛用） */
export function getNotificationSummary(): Promise<NotificationSummary> {
  return get<NotificationSummary>('/notifications/summary')
}

/** 单条标记已读（仅本人消息） */
export function markNotificationRead(id: string): Promise<null> {
  return put<null>(`/notifications/${id}/read`)
}

/** 全部标记已读（只影响本人未读消息） */
export function markAllNotificationsRead(): Promise<MarkAllNotificationsReadResult> {
  return put<MarkAllNotificationsReadResult>('/notifications/read-all')
}

/** 手动触发库存预警扫描（与定时宿主共用同一实现） */
export function scanStockAlerts(): Promise<StockAlertScanResult> {
  return post<StockAlertScanResult>('/notifications/scan')
}

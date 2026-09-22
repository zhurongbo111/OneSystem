import { get } from './request'
import type { PagedResult } from './user'

/** 资源类型枚举值（对应后端 AuditResource） */
export type AuditResource =
  | 0
  | 1
  | 2
  | 3
  | 4
  | 5
  | 6
  | 7
  | 8
  | 9
  | 10
  | 11
  | 12
  | 13
  | 14
  | 15
  | 16
  | 17
  | 18

/** 动作枚举值（对应后端 AuditAction） */
export type AuditAction = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9

/** 操作日志列表行（对应后端 AuditLogListItemDto；列表不返回字段级差异） */
export interface AuditLogListItem {
  id: string
  username: string | null
  displayName: string | null
  resource: AuditResource
  action: AuditAction
  resourceNo: string | null
  summary: string
  createdAt: string
}

/** 字段级差异项（对应后端 AuditChangeDto；before / after 为空表示该侧无值） */
export interface AuditChangeItem {
  field: string
  label: string
  before: string | null
  after: string | null
}

/** 操作日志详情（对应后端 AuditLogDetailDto） */
export interface AuditLogDetail extends AuditLogListItem {
  userId: string | null
  resourceId: string | null
  changes: AuditChangeItem[]
}

/** 操作日志查询参数（对应后端 GetAuditLogsRequest；时间均为 UTC ISO 串） */
export interface AuditLogQuery {
  keyword?: string
  resource?: AuditResource
  action?: AuditAction
  userId?: string
  start?: string
  end?: string
  page: number
  pageSize: number
}

/** 资源类型中文文案（与后端 AuditResource 一一对应，全项目唯一来源） */
export const AUDIT_RESOURCE_LABELS: Record<number, string> = {
  0: '商品',
  1: '商品分类',
  2: '往来单位',
  3: '仓库',
  4: '客户价格',
  5: '用户',
  6: '角色',
  7: '采购入库单',
  8: '销售出库单',
  9: '采购退货单',
  10: '销售退货单',
  11: '收付款单',
  12: '库存盘点单',
  13: '调拨单',
  14: '发票',
  15: '成本重算',
  16: '单据审批',
  17: '采购订单',
  18: '销售订单',
}

/** 资源类型标签颜色（specs/006-list-showcase §0：枚举字段用 a-tag 着色展示） */
export const AUDIT_RESOURCE_COLORS: Record<number, string> = {
  0: 'arcoblue',
  1: 'cyan',
  2: 'purple',
  3: 'green',
  4: 'lime',
  5: 'arcoblue',
  6: 'purple',
  7: 'orange',
  8: 'green',
  9: 'orangered',
  10: 'gold',
  11: 'magenta',
  12: 'cyan',
  13: 'blue',
  14: 'pinkpurple',
  15: 'lime',
  16: 'purple',
  17: 'orange',
  18: 'green',
}

/** 动作中文文案（与后端 AuditAction 一一对应） */
export const AUDIT_ACTION_LABELS: Record<number, string> = {
  0: '新增',
  1: '编辑',
  2: '删除',
  3: '启停',
  4: '作废',
  5: '关闭',
  6: '结算',
  7: '审批',
  8: '盘点调整',
  9: '成本重算',
}

/** 动作标签颜色 */
export const AUDIT_ACTION_COLORS: Record<number, string> = {
  0: 'green',
  1: 'arcoblue',
  2: 'red',
  3: 'orange',
  4: 'red',
  5: 'gray',
  6: 'gold',
  7: 'purple',
  8: 'cyan',
  9: 'purple',
}

/** 资源类型下拉选项（按枚举序号） */
export const AUDIT_RESOURCE_OPTIONS: { label: string; value: AuditResource }[] = Object.keys(
  AUDIT_RESOURCE_LABELS,
)
  .map(Number)
  .sort((a, b) => a - b)
  .map((value) => ({ label: AUDIT_RESOURCE_LABELS[value], value: value as AuditResource }))

/** 动作下拉选项（按枚举序号） */
export const AUDIT_ACTION_OPTIONS: { label: string; value: AuditAction }[] = Object.keys(
  AUDIT_ACTION_LABELS,
)
  .map(Number)
  .sort((a, b) => a - b)
  .map((value) => ({ label: AUDIT_ACTION_LABELS[value], value: value as AuditAction }))

/** 资源类型文案（未知值兜底为 `-`，避免后端新增枚举时前端空白） */
export function auditResourceLabel(resource: number): string {
  return AUDIT_RESOURCE_LABELS[resource] ?? '-'
}

/** 动作文案（未知值兜底为 `-`） */
export function auditActionLabel(action: number): string {
  return AUDIT_ACTION_LABELS[action] ?? '-'
}

/** 操作人展示文本：`显示名（登录名）`，系统动作（无操作人）显示 `-` */
export function operatorText(row: { username: string | null; displayName: string | null }): string {
  if (!row.username && !row.displayName) return '-'
  if (!row.username) return row.displayName ?? '-'
  if (!row.displayName) return row.username
  return `${row.displayName}（${row.username}）`
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间（同既有约定）：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC。
 */
export function toDateRange(
  startDate?: string,
  endDate?: string,
): { start?: string; end?: string } {
  const start = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : undefined
  const end = endDate ? new Date(`${endDate}T23:59:59.999`).toISOString() : undefined
  return { start, end }
}

/** 分页查询操作日志（只读，按操作时间倒序） */
export function getAuditLogs(query: AuditLogQuery): Promise<PagedResult<AuditLogListItem>> {
  return get<PagedResult<AuditLogListItem>>('/audit-logs', { params: query })
}

/** 查询操作日志详情（含字段级差异数组） */
export function getAuditLogById(id: string): Promise<AuditLogDetail> {
  return get<AuditLogDetail>(`/audit-logs/${id}`)
}

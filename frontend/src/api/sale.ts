import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 结算状态（0 未收 / 1 已收） */
export type SettlementStatus = 0 | 1

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 销售单列表行（对应后端 SalesOrderListItemDto） */
export interface SalesOrderListItem {
  id: string
  orderNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  totalAmount: number
  settlementStatus: SettlementStatus
  status: OrderStatus
  createdAt: string
}

/** 销售单明细行（快照字段原样返回，对应后端 SalesOrderItemDto） */
export interface SalesOrderItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 销售单详情（对应后端 SalesOrderDetailDto，明细按插入顺序） */
export interface SalesOrderDetail extends Omit<SalesOrderListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: SalesOrderItem[]
  status: OrderStatus
}

/** 销售单列表查询参数（对应后端 GetSalesOrdersRequest） */
export interface SalesOrderQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  start?: string
  end?: string
  settlement?: SettlementStatus
}

/**
 * 开单明细行本地类型（含快照展示字段）：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额（design §4.2）。
 */
export interface SalesFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增销售单入参（对应后端 CreateSalesOrderRequest；不传小计 / 总额） */
export interface CreateSalesOrderPayload {
  partnerId: string
  orderDate: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC（同采购单约定）。
 */
export function toDateRange(
  startDate?: string,
  endDate?: string,
): { start?: string; end?: string } {
  const start = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : undefined
  const end = endDate ? new Date(`${endDate}T23:59:59.999`).toISOString() : undefined
  return { start, end }
}

/**
 * 把所选本地日期（YYYY-MM-DD）转换为后端所需的「所选日期的 UTC 午夜」ISO 串（design §4.2 / §3.5）。
 * 直接以 UTC 解释该日历日 00:00:00，避免后端把裸日期按服务器本地时区解析
 * （Npgsql 拒绝把非 UTC 偏移的 DateTimeOffset 写入 timestamptz 列）。
 */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 分页查询销售单（含作废单据，作废行前端置灰） */
export function getSalesOrders(query: SalesOrderQuery): Promise<PagedResult<SalesOrderListItem>> {
  return get<PagedResult<SalesOrderListItem>>('/sales-orders', { params: query })
}

/** 查询销售单详情（含明细行，快照字段原样返回） */
export function getSalesOrder(id: string): Promise<SalesOrderDetail> {
  return get<SalesOrderDetail>(`/sales-orders/${id}`)
}

/** 新增销售单（一步式：保存即生效，库存立即减少；任一行库存不足整单拒绝） */
export function createSalesOrder(payload: CreateSalesOrderPayload): Promise<SalesOrderDetail> {
  return post<SalesOrderDetail>('/sales-orders', payload)
}

/** 作废销售单（回冲库存；仅改状态不删数据） */
export function voidSalesOrder(id: string): Promise<SalesOrderDetail> {
  return put<SalesOrderDetail>(`/sales-orders/${id}/void`)
}

/** 更新结算状态（仅 未收 ↔ 已收；库存不变） */
export function updateSalesOrderSettlement(id: string, settlementStatus: SettlementStatus): Promise<SalesOrderDetail> {
  return put<SalesOrderDetail>(`/sales-orders/${id}/settlement`, { settlementStatus })
}

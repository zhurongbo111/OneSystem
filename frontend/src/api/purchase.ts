import type { SettlementState } from '@/utils/settlement'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 采购单列表行（对应后端 PurchaseOrderListItemDto） */
export interface PurchaseOrderListItem {
  id: string
  orderNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  totalAmount: number
  settledAmount: number
  unsettledAmount: number
  settlementState: SettlementState
  status: OrderStatus
  createdAt: string
}

/** 采购单明细行（快照字段原样返回，对应后端 PurchaseOrderItemDto） */
export interface PurchaseOrderItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 采购单详情（对应后端 PurchaseOrderDetailDto，明细按插入顺序） */
export interface PurchaseOrderDetail extends Omit<PurchaseOrderListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: PurchaseOrderItem[]
  status: OrderStatus
}

/** 采购单列表查询参数（对应后端 GetPurchaseOrdersRequest） */
export interface PurchaseOrderQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  start?: string
  end?: string
  settlementState?: SettlementState
}

/**
 * 开单明细行本地类型（含快照展示字段）：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额（design §4.2）。
 */
export interface PurchaseFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增采购单入参（对应后端 CreatePurchaseOrderRequest；不传小计 / 总额） */
export interface CreatePurchaseOrderPayload {
  partnerId: string
  orderDate: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC（同登录日志约定）。
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

/** 分页查询采购单（含作废单据，作废行前端置灰） */
export function getPurchaseOrders(query: PurchaseOrderQuery): Promise<PagedResult<PurchaseOrderListItem>> {
  return get<PagedResult<PurchaseOrderListItem>>('/purchase-orders', { params: query })
}

/** 查询采购单详情（含明细行，快照字段原样返回） */
export function getPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return get<PurchaseOrderDetail>(`/purchase-orders/${id}`)
}

/** 新增采购单（一步式：保存即生效，库存立即增加；返回详情含后端生成的单号与重算金额） */
export function createPurchaseOrder(payload: CreatePurchaseOrderPayload): Promise<PurchaseOrderDetail> {
  return post<PurchaseOrderDetail>('/purchase-orders', payload)
}

/** 作废采购单（回冲库存；仅改状态不删数据） */
export function voidPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return put<PurchaseOrderDetail>(`/purchase-orders/${id}/void`)
}

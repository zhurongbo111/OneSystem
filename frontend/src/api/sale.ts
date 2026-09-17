import type { SettlementState } from '@/utils/settlement'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 销售出库单列表行（对应后端 SalesShipmentListItemDto） */
export interface SalesShipmentListItem {
  id: string
  shipmentNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  /** 关联销售订单 id（可空：不关联订单的直通单据） */
  orderId: string | null
  /** 关联销售订单号快照（可空） */
  orderNo: string | null
  totalAmount: number
  settledAmount: number
  unsettledAmount: number
  settlementState: SettlementState
  status: OrderStatus
  createdAt: string
}

/** 销售出库单明细行（快照字段原样返回，对应后端 SalesShipmentItemDto） */
export interface SalesShipmentItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
  /** 关联销售订单明细行 id（可空：不关联订单的直通单据） */
  orderItemId: string | null
}

/** 销售出库单详情（对应后端 SalesShipmentDetailDto，明细按插入顺序） */
export interface SalesShipmentDetail extends Omit<SalesShipmentListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: SalesShipmentItem[]
  status: OrderStatus
}

/** 销售出库单列表查询参数（对应后端 GetSalesShipmentsRequest） */
export interface SalesShipmentQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  /** 关联销售订单 id（订单详情的「关联出库单」列表用） */
  orderId?: string
  start?: string
  end?: string
  settlementState?: SettlementState
}

/**
 * 开单明细行本地类型（含快照展示字段）：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额（design §4.2）。
 * 关联订单时额外携带订单明细行 id 与未发数量（作为本次数量上限，design §4.3）。
 */
export interface SalesFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
  /** 关联订单明细行 id（关联模式下提交时必填） */
  orderItemId?: string
  /** 未发数量（关联模式下的数量上限；未关联订单时为空表示不限） */
  remainingQuantity?: number
  /** 订购数量（关联订单时展示订单行信息） */
  orderedQuantity?: number
  /** 累计已发数量（关联订单时展示订单行信息） */
  fulfilledQuantity?: number
}

/** 新增销售出库单入参（对应后端 CreateSalesShipmentRequest；不传小计 / 总额） */
export interface CreateSalesShipmentPayload {
  partnerId: string
  orderDate: string
  /** 关联销售订单 id（可选） */
  orderId?: string
  items: { productId: string; quantity: number; unitPrice: number; orderItemId?: string }[]
  remark?: string
}

/** 可关联销售订单候选（对应后端 SalesOrderPickDto） */
export interface SalesOrderPick {
  id: string
  orderNo: string
  orderDate: string
  expectedDate: string | null
  totalAmount: number
}

/** 关联订单明细行（对应后端 SalesOrderLineDto） */
export interface SalesOrderLine {
  orderItemId: string
  productId: string
  productName: string
  unit: string
  quantity: number
  fulfilledQuantity: number
  remainingQuantity: number
  unitPrice: number
}

/** 关联订单明细（对应后端 SalesOrderLinesDto） */
export interface SalesOrderLines {
  orderId: string
  orderNo: string
  partnerId: string
  partnerName: string
  expectedDate: string | null
  items: SalesOrderLine[]
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

/** 分页查询销售出库单（含作废单据，作废行前端置灰） */
export function getSalesShipments(query: SalesShipmentQuery): Promise<PagedResult<SalesShipmentListItem>> {
  return get<PagedResult<SalesShipmentListItem>>('/sales-shipments', { params: query })
}

/** 查询销售出库单详情（含明细行，快照字段原样返回） */
export function getSalesShipment(id: string): Promise<SalesShipmentDetail> {
  return get<SalesShipmentDetail>(`/sales-shipments/${id}`)
}

/** 新增销售出库单（一步式：保存即生效，库存立即减少；可关联销售订单） */
export function createSalesShipment(payload: CreateSalesShipmentPayload): Promise<SalesShipmentDetail> {
  return post<SalesShipmentDetail>('/sales-shipments', payload)
}

/** 作废销售出库单（回增库存，关联订单时回退累计已发；仅改状态不删数据） */
export function voidSalesShipment(id: string): Promise<SalesShipmentDetail> {
  return put<SalesShipmentDetail>(`/sales-shipments/${id}/void`)
}

/** 可关联销售订单候选（按客户，状态为待发货 / 部分发货；出库开单页下拉） */
export function getSalesOrderPicks(partnerId: string): Promise<SalesOrderPick[]> {
  return get<SalesOrderPick[]>('/sales-shipments/pick-orders', { params: { partnerId } })
}

/** 关联订单明细（含未发数量；出库开单页选择订单后带出） */
export function getSalesOrderLines(orderId: string): Promise<SalesOrderLines> {
  return get<SalesOrderLines>('/sales-shipments/order-lines', { params: { orderId } })
}

import type { SettlementState } from '@/utils/settlement'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 采购入库单列表行（对应后端 PurchaseReceiptListItemDto） */
export interface PurchaseReceiptListItem {
  id: string
  receiptNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  /** 关联采购订单 id（可空：不关联订单的直通单据） */
  orderId: string | null
  /** 关联采购订单号快照（可空） */
  orderNo: string | null
  totalAmount: number
  settledAmount: number
  unsettledAmount: number
  settlementState: SettlementState
  /** 数量合计（= Σ 明细数量；订单详情「关联入库单」跟单展示） */
  totalQuantity: number
  status: OrderStatus
  createdAt: string
}

/** 采购入库单明细行（快照字段原样返回，对应后端 PurchaseReceiptItemDto） */
export interface PurchaseReceiptItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
  /** 关联采购订单明细行 id（可空：不关联订单的直通单据） */
  orderItemId: string | null
}

/** 采购入库单详情（对应后端 PurchaseReceiptDetailDto，明细按插入顺序） */
export interface PurchaseReceiptDetail extends Omit<PurchaseReceiptListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: PurchaseReceiptItem[]
  status: OrderStatus
}

/** 采购入库单列表查询参数（对应后端 GetPurchaseReceiptsRequest） */
export interface PurchaseReceiptQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  /** 关联采购订单 id（订单详情的「关联入库单」列表用） */
  orderId?: string
  start?: string
  end?: string
  settlementState?: SettlementState
}

/**
 * 开单明细行本地类型（含快照展示字段）：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额（design §4.2）。
 * 关联订单时额外携带订单明细行 id 与未收数量（作为本次数量上限，design §4.3）。
 */
export interface PurchaseFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
  /** 关联订单明细行 id（关联模式下提交时必填） */
  orderItemId?: string
  /** 未收数量（关联模式下的数量上限；未关联订单时为空表示不限） */
  remainingQuantity?: number
  /** 订购数量（关联订单时展示订单行信息） */
  orderedQuantity?: number
  /** 累计已收数量（关联订单时展示订单行信息） */
  fulfilledQuantity?: number
}

/** 新增采购入库单入参（对应后端 CreatePurchaseReceiptRequest；不传小计 / 总额） */
export interface CreatePurchaseReceiptPayload {
  partnerId: string
  orderDate: string
  /** 关联采购订单 id（可选） */
  orderId?: string
  items: { productId: string; quantity: number; unitPrice: number; orderItemId?: string }[]
  remark?: string
}

/** 可关联采购订单候选（对应后端 PurchaseOrderPickDto） */
export interface PurchaseOrderPick {
  id: string
  orderNo: string
  orderDate: string
  expectedDate: string | null
  totalAmount: number
}

/** 关联订单明细行（对应后端 PurchaseOrderLineDto） */
export interface PurchaseOrderLine {
  orderItemId: string
  productId: string
  productName: string
  unit: string
  quantity: number
  fulfilledQuantity: number
  remainingQuantity: number
  unitPrice: number
}

/** 关联订单明细（对应后端 PurchaseOrderLinesDto） */
export interface PurchaseOrderLines {
  orderId: string
  orderNo: string
  partnerId: string
  partnerName: string
  expectedDate: string | null
  items: PurchaseOrderLine[]
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

/** 分页查询采购入库单（含作废单据，作废行前端置灰） */
export function getPurchaseReceipts(query: PurchaseReceiptQuery): Promise<PagedResult<PurchaseReceiptListItem>> {
  return get<PagedResult<PurchaseReceiptListItem>>('/purchase-receipts', { params: query })
}

/** 查询采购入库单详情（含明细行，快照字段原样返回） */
export function getPurchaseReceipt(id: string): Promise<PurchaseReceiptDetail> {
  return get<PurchaseReceiptDetail>(`/purchase-receipts/${id}`)
}

/** 新增采购入库单（一步式：保存即生效，库存立即增加；可关联采购订单） */
export function createPurchaseReceipt(payload: CreatePurchaseReceiptPayload): Promise<PurchaseReceiptDetail> {
  return post<PurchaseReceiptDetail>('/purchase-receipts', payload)
}

/** 作废采购入库单（回冲库存，关联订单时回退累计已收；仅改状态不删数据） */
export function voidPurchaseReceipt(id: string): Promise<PurchaseReceiptDetail> {
  return put<PurchaseReceiptDetail>(`/purchase-receipts/${id}/void`)
}

/** 可关联采购订单候选（按供应商，状态为待收货 / 部分收货；入库开单页下拉） */
export function getPurchaseOrderPicks(partnerId: string): Promise<PurchaseOrderPick[]> {
  return get<PurchaseOrderPick[]>('/purchase-receipts/pick-orders', { params: { partnerId } })
}

/** 关联订单明细（含未收数量；入库开单页选择订单后带出） */
export function getPurchaseOrderLines(orderId: string): Promise<PurchaseOrderLines> {
  return get<PurchaseOrderLines>('/purchase-receipts/order-lines', { params: { orderId } })
}

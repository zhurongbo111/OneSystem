import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 结算状态（0 未结算 / 1 已结算，复用单据域语义） */
export type SettlementStatus = 0 | 1

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 采购退货单列表行（对应后端 PurchaseReturnListItemDto） */
export interface PurchaseReturnListItem {
  id: string
  returnNo: string
  partnerId: string
  partnerName: string
  returnDate: string
  totalAmount: number
  settlementStatus: SettlementStatus
  status: OrderStatus
  createdAt: string
}

/** 采购退货单明细行（快照字段原样返回，对应后端 PurchaseReturnItemDto） */
export interface PurchaseReturnItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 采购退货单详情（对应后端 PurchaseReturnDetailDto，明细按插入顺序） */
export interface PurchaseReturnDetail extends Omit<PurchaseReturnListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: PurchaseReturnItem[]
  status: OrderStatus
}

/** 采购退货单列表查询参数（对应后端 GetPurchaseReturnsRequest） */
export interface PurchaseReturnQuery {
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
 * `stockQuantity` 为该商品当前库存，用于「退货数量 > 库存」行内预警（前端预警，最终以后端 40103 为准）。
 */
export interface PurchaseReturnFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  stockQuantity: number
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增采购退货单入参（对应后端 CreatePurchaseReturnRequest；不传小计 / 总额） */
export interface CreatePurchaseReturnPayload {
  partnerId: string
  returnDate: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC（同采购 / 销售约定）。
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
 * 把所选本地日期（YYYY-MM-DD）转换为后端所需的「所选日期的 UTC 午夜」ISO 串（design §4.2）。
 * 直接以 UTC 解释该日历日 00:00:00，避免后端把裸日期按服务器本地时区解析
 * （Npgsql 拒绝把非 UTC 偏移的 DateTimeOffset 写入 timestamptz 列）。
 */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 分页查询采购退货单（含作废单据，作废行前端置灰） */
export function getPurchaseReturns(query: PurchaseReturnQuery): Promise<PagedResult<PurchaseReturnListItem>> {
  return get<PagedResult<PurchaseReturnListItem>>('/purchase-returns', { params: query })
}

/** 查询采购退货单详情（含明细行，快照字段原样返回） */
export function getPurchaseReturn(id: string): Promise<PurchaseReturnDetail> {
  return get<PurchaseReturnDetail>(`/purchase-returns/${id}`)
}

/** 新增采购退货单（一步式：保存即生效，库存立即减少并写流水；返回详情含后端生成的单号与重算金额） */
export function createPurchaseReturn(payload: CreatePurchaseReturnPayload): Promise<PurchaseReturnDetail> {
  return post<PurchaseReturnDetail>('/purchase-returns', payload)
}

/** 作废采购退货单（回冲库存；仅改状态不删数据） */
export function voidPurchaseReturn(id: string): Promise<PurchaseReturnDetail> {
  return put<PurchaseReturnDetail>(`/purchase-returns/${id}/void`)
}

/** 更新结算状态（仅 未结算 ↔ 已结算；库存不变） */
export function updatePurchaseReturnSettlement(id: string, settlementStatus: SettlementStatus): Promise<PurchaseReturnDetail> {
  return put<PurchaseReturnDetail>(`/purchase-returns/${id}/settlement`, { settlementStatus })
}

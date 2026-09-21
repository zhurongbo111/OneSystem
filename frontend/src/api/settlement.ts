import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 收付款类型（0 收款 / 1 付款） */
export type SettlementType = 0 | 1

/** 收付款方式（0 现金 / 1 银行转账 / 2 其他） */
export type SettlementMethod = 0 | 1 | 2

/** 被核销单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货） */
export type SettlementOrderType = 0 | 1 | 2 | 3

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 往来单位类型（1 供应商 / 2 客户 / 3 两者） */
export type PartnerType = 1 | 2 | 3

/** 收付款单列表行（对应后端 SettlementListItemDto） */
export interface SettlementListItem {
  id: string
  settlementNo: string
  type: SettlementType
  partnerId: string
  partnerName: string
  settlementDate: string
  totalAmount: number
  method: SettlementMethod
  status: OrderStatus
  createdAt: string
  /** 本次核销金额（仅按被核销单据反查时返回，普通列表为 null） */
  orderAmount?: number | null
}

/** 核销明细行（快照字段原样返回，对应后端 SettlementItemDto） */
export interface SettlementItem {
  id: string
  orderType: SettlementOrderType
  orderId: string
  orderNo: string
  orderDate: string
  orderTotalAmount: number
  amount: number
}

/** 收付款单详情（对应后端 SettlementDetailDto） */
export interface SettlementDetail extends SettlementListItem {
  remark: string | null
  createdBy: string | null
  items: SettlementItem[]
}

/** 收付款单列表查询参数（对应后端 GetSettlementsRequest） */
export interface SettlementQuery {
  page: number
  pageSize: number
  keyword?: string
  type?: SettlementType
  partnerId?: string
  method?: SettlementMethod
  start?: string
  end?: string
  /** 被核销单据类型 / id：成对传入，用于按单据反查（单据详情「收付款明细」） */
  orderType?: SettlementOrderType
  orderId?: string
}

/** 核销明细行入参（对应后端 CreateSettlementItem） */
export interface CreateSettlementItemPayload {
  orderType: SettlementOrderType
  orderId: string
  amount: number
}

/** 新增收付款单入参（对应后端 CreateSettlementRequest；总额由后端按 Σ 核销金额重算） */
export interface CreateSettlementPayload {
  type: SettlementType
  partnerId: string
  settlementDate: string
  method: SettlementMethod
  items: CreateSettlementItemPayload[]
  remark?: string
}

/** 可核销单据候选（对应后端 SettlementCandidateDto） */
export interface SettlementCandidate {
  orderType: SettlementOrderType
  orderId: string
  orderNo: string
  orderDate: string
  totalAmount: number
  settledAmount: number
  unsettledAmount: number
}

/** 可核销单据候选查询参数（对应后端 GetUnsettledOrdersRequest） */
export interface UnsettledOrdersQuery {
  partnerId: string
  type: SettlementType
  page: number
  pageSize: number
}

/** 往来对账台账行（对应后端 ReconciliationListItemDto） */
export interface ReconciliationListItem {
  partnerId: string
  partnerName: string
  partnerType: PartnerType
  receivableAmount: number
  payableAmount: number
  unsettledOrderCount: number
}

/** 往来对账台账查询参数（对应后端 GetReconciliationRequest） */
export interface ReconciliationQuery {
  page: number
  pageSize: number
  keyword?: string
  type?: PartnerType
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC（同单据域约定）。
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
 * 把所选本地日期（YYYY-MM-DD）转换为后端所需的「所选日期的 UTC 午夜」ISO 串。
 * 直接以 UTC 解释该日历日 00:00:00，避免后端把裸日期按服务器本地时区解析。
 */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 分页查询收付款单（含作废单据，作废行前端置灰） */
export function getSettlements(query: SettlementQuery): Promise<PagedResult<SettlementListItem>> {
  return get<PagedResult<SettlementListItem>>('/settlements', { params: query })
}

/** 查询收付款单详情（含核销明细，快照字段原样返回） */
export function getSettlement(id: string): Promise<SettlementDetail> {
  return get<SettlementDetail>(`/settlements/${id}`)
}

/** 新增收付款单（核销即生效：累加各被核销单据已结算金额） */
export function createSettlement(payload: CreateSettlementPayload): Promise<SettlementDetail> {
  return post<SettlementDetail>('/settlements', payload)
}

/** 作废收付款单（逐行回退被核销单据已结算金额；仅改状态不删数据） */
export function voidSettlement(id: string): Promise<SettlementDetail> {
  return put<SettlementDetail>(`/settlements/${id}/void`)
}

/** 查询可核销单据候选（按往来单位 + 方向返回未结单据） */
export function getUnsettledOrders(query: UnsettledOrdersQuery): Promise<PagedResult<SettlementCandidate>> {
  return get<PagedResult<SettlementCandidate>>('/settlements/unsettled-orders', { params: query })
}

/** 查询往来对账台账（按往来单位聚合应收 / 应付与未结单据数） */
export function getReconciliation(query: ReconciliationQuery): Promise<PagedResult<ReconciliationListItem>> {
  return get<PagedResult<ReconciliationListItem>>('/reconciliation', { params: query })
}

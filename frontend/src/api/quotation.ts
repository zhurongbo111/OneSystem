import type { QuotationStatus } from '@/utils/quotation'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 报价单列表行（对应后端 QuotationListItemDto） */
export interface QuotationListItem {
  id: string
  quotationNo: string
  partnerId: string
  partnerName: string
  quotationDate: string
  /** 有效期至（`YYYY-MM-DD`，可空） */
  validUntil: string | null
  totalAmount: number
  /** 明细行数 */
  itemCount: number
  status: QuotationStatus
  /** 转出的销售订单号快照（已转订单时有值） */
  convertedOrderNo: string | null
  createdAt: string
}

/** 报价单明细行（对应后端 QuotationItemDto） */
export interface QuotationItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 报价单详情（对应后端 QuotationDetailDto） */
export interface QuotationDetail {
  id: string
  quotationNo: string
  partnerId: string
  partnerName: string
  quotationDate: string
  validUntil: string | null
  totalAmount: number
  status: QuotationStatus
  convertedOrderId: string | null
  convertedOrderNo: string | null
  remark: string | null
  createdBy: string | null
  createdAt: string
  items: QuotationItem[]
}

/** 报价单列表查询参数（对应后端 GetQuotationsRequest） */
export interface QuotationQuery {
  page: number
  pageSize: number
  /** 关键词：报价单号 / 客户名称 */
  keyword?: string
  status?: QuotationStatus
  start?: string
  end?: string
}

/**
 * 报价单表单明细行本地类型：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额。
 */
export interface QuotationFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增 / 编辑报价单入参（对应后端 Create / UpdateQuotationRequest） */
export interface SaveQuotationPayload {
  partnerId: string
  quotationDate: string
  /** 有效期至（`YYYY-MM-DD`，可空；全量覆盖语义：不传即清空） */
  validUntil?: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/** 转销售订单结果（对应后端 ConvertQuotationResultDto） */
export interface ConvertQuotationResult {
  orderId: string
  orderNo: string
}

/** 分页查询报价单（含已转订单 / 已作废单，作废行前端置灰） */
export function getQuotations(query: QuotationQuery): Promise<PagedResult<QuotationListItem>> {
  return get<PagedResult<QuotationListItem>>('/quotations', { params: query })
}

/** 查询报价单详情（含明细与转单信息） */
export function getQuotation(id: string): Promise<QuotationDetail> {
  return get<QuotationDetail>(`/quotations/${id}`)
}

/** 新增报价单（意向单据：不动库存、不写流水、不产生应收） */
export function createQuotation(payload: SaveQuotationPayload): Promise<QuotationDetail> {
  return post<QuotationDetail>('/quotations', payload)
}

/** 编辑报价单（仅「草稿」状态允许，否则 40166；明细整体替换） */
export function updateQuotation(id: string, payload: SaveQuotationPayload): Promise<QuotationDetail> {
  return put<QuotationDetail>(`/quotations/${id}`, payload)
}

/** 作废报价单（仅「草稿」状态允许，否则 40166） */
export function voidQuotation(id: string): Promise<QuotationDetail> {
  return put<QuotationDetail>(`/quotations/${id}/void`)
}

/** 报价单转销售订单（仅「草稿」状态允许，否则 40167；只能转一次） */
export function convertQuotation(id: string): Promise<ConvertQuotationResult> {
  return post<ConvertQuotationResult>(`/quotations/${id}/convert`)
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

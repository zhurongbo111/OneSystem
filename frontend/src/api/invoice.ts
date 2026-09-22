import type { PartnerType } from './partner'
import type { PagedResult } from './product'
import { get, post, put } from './request'
import type { OrderStatus, SettlementOrderType } from './settlement'

/** 发票类型（0 进项 / 1 销项） */
export type InvoiceType = 0 | 1

/** 发票列表行（对应后端 InvoiceListItemDto） */
export interface InvoiceListItem {
  id: string
  invoiceNo: string
  type: InvoiceType
  partnerName: string
  invoiceDate: string
  amountExcludingTax: number
  /** 税率（0–1 小数口径，0.13 表示 13%） */
  taxRate: number
  taxAmount: number
  totalAmount: number
  status: OrderStatus
  createdAt: string
  /** 关联单据号拼接（无明细时为空串） */
  orderNoSummary: string
}

/** 关联单据明细行（快照字段原样返回，对应后端 InvoiceItemDto） */
export interface InvoiceItem {
  id: string
  orderType: SettlementOrderType
  orderId: string
  orderNo: string
  orderDate: string
  orderTotalAmount: number
  amount: number
}

/** 发票详情（对应后端 InvoiceDetailDto） */
export interface InvoiceDetail {
  id: string
  invoiceNo: string
  type: InvoiceType
  partnerId: string
  partnerName: string
  invoiceDate: string
  amountExcludingTax: number
  /** 税率（0–1 小数口径） */
  taxRate: number
  taxAmount: number
  totalAmount: number
  status: OrderStatus
  remark: string | null
  createdBy: string | null
  createdAt: string
  items: InvoiceItem[]
}

/** 发票列表查询参数（对应后端 GetInvoicesRequest） */
export interface InvoiceQuery {
  page: number
  pageSize: number
  keyword?: string
  type?: InvoiceType
  partnerId?: string
  start?: string
  end?: string
}

/** 关联单据明细行入参（对应后端 CreateInvoiceItem） */
export interface CreateInvoiceItemPayload {
  orderType: SettlementOrderType
  orderId: string
  amount: number
}

/** 登记发票入参（对应后端 CreateInvoiceRequest；税额与价税合计由后端按税率重算） */
export interface CreateInvoicePayload {
  invoiceNo: string
  type: InvoiceType
  partnerId: string
  invoiceDate: string
  amountExcludingTax: number
  /** 税率（0–1 小数口径） */
  taxRate: number
  items: CreateInvoiceItemPayload[]
  remark?: string
}

/** 可开票单据候选（对应后端 InvoicableOrderDto） */
export interface InvoicableOrder {
  orderType: SettlementOrderType
  orderId: string
  orderNo: string
  orderDate: string
  totalAmount: number
  invoicedAmount: number
  uninvoicedAmount: number
}

/** 可开票单据候选查询参数（对应后端 GetInvoicableOrdersRequest） */
export interface InvoicableOrdersQuery {
  partnerId: string
  type: InvoiceType
  page: number
  pageSize: number
}

/** 发票类型标签元数据（列表 / 详情共用；文案与颜色唯一来源 specs/032-erp-invoice/design.md §0.4） */
export const INVOICE_TYPE_META: Record<InvoiceType, { label: string; color: string }> = {
  0: { label: '进项', color: 'arcoblue' },
  1: { label: '销项', color: 'green' },
}

/** 发票类型下拉（进项 / 销项） */
export const INVOICE_TYPE_OPTIONS: { label: string; value: InvoiceType }[] = [
  { label: '进项', value: 0 },
  { label: '销项', value: 1 },
]

/** 标准税率下拉（值为 0–1 小数口径，0.13 表示 13%） */
export const TAX_RATE_OPTIONS: { label: string; value: number }[] = [
  { label: '0%', value: 0 },
  { label: '1%', value: 0.01 },
  { label: '3%', value: 0.03 },
  { label: '6%', value: 0.06 },
  { label: '9%', value: 0.09 },
  { label: '13%', value: 0.13 },
]

/** 自定义税率选项值（选中后切换为数字输入，范围 0–1 四位小数） */
export const CUSTOM_TAX_RATE = -1

/** 税率展示文案（0.13 → 13%；去尾零，13.50% → 13.5%） */
export function formatTaxRate(rate: number): string {
  return `${Number((rate * 100).toFixed(4))}%`
}

/** 各发票方向可选的往来档案类型（进项需供应商 / 两者，销项需客户 / 两者；design.md §3.4） */
export function allowedPartnerTypes(type: InvoiceType): PartnerType[] {
  return type === 0 ? [1, 3] : [2, 3]
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

/** 分页查询发票（含作废发票，作废行前端置灰） */
export function getInvoices(query: InvoiceQuery): Promise<PagedResult<InvoiceListItem>> {
  return get<PagedResult<InvoiceListItem>>('/invoices', { params: query })
}

/** 查询发票详情（含关联单据明细，快照字段原样返回） */
export function getInvoice(id: string): Promise<InvoiceDetail> {
  return get<InvoiceDetail>(`/invoices/${id}`)
}

/** 登记发票（每行开票金额不得超过该单据未开票金额） */
export function createInvoice(payload: CreateInvoicePayload): Promise<InvoiceDetail> {
  return post<InvoiceDetail>('/invoices', payload)
}

/** 作废发票（占用金额按聚合自动释放；仅改状态不删数据） */
export function voidInvoice(id: string): Promise<InvoiceDetail> {
  return put<InvoiceDetail>(`/invoices/${id}/void`)
}

/** 查询可开票单据候选（按往来单位 + 发票类型返回未开票单据） */
export function getInvoicableOrders(query: InvoicableOrdersQuery): Promise<PagedResult<InvoicableOrder>> {
  return get<PagedResult<InvoicableOrder>>('/invoices/invoicable-orders', { params: query })
}

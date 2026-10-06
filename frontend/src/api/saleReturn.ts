import type { SettlementState } from '@/utils/settlement'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 单据状态（0 已作废 / 1 正常） */
export type OrderStatus = 0 | 1

/** 销售退货单列表行（对应后端 SalesReturnListItemDto） */
export interface SalesReturnListItem {
  id: string
  returnNo: string
  partnerId: string
  partnerName: string
  returnDate: string
  /** 入库仓 id（038） */
  warehouseId: string
  /** 入库仓名称快照（038） */
  warehouseName: string
  totalAmount: number
  settledAmount: number
  unsettledAmount: number
  settlementState: SettlementState
  status: OrderStatus
  createdAt: string
}

/** 销售退货单明细行（快照字段原样返回，对应后端 SalesReturnItemDto） */
export interface SalesReturnItem {
  id: string
  productId: string
  productName: string
  unit: string
  /** 批次 id（040：按批次商品有值，非批次 null） */
  batchId: string | null
  /** 批次号快照（040；非批次 null） */
  batchNo: string | null
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 销售退货单详情（对应后端 SalesReturnDetailDto，明细按插入顺序） */
export interface SalesReturnDetail extends Omit<SalesReturnListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: SalesReturnItem[]
  status: OrderStatus
}

/** 销售退货单列表查询参数（对应后端 GetSalesReturnsRequest） */
export interface SalesReturnQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  /** 入库仓 id，可空（038；不传 = 全部仓） */
  warehouseId?: string
  start?: string
  end?: string
  settlementState?: SettlementState
}

/**
 * 开单明细行本地类型（含快照展示字段）：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额。
 * 销售退货回增库存，**不做库存上限预警**（design.md §4.4）。
 */
export interface SalesReturnFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
  /**
   * 批次选择值（040，v-model 绑定 BatchPickSelect；销售退货支持就地新建）：
   * 选中已有批次 → `batchId` / `batchNo` 有值；就地新建 → `newBatchNo` 等三字段有值（与 batchId 互斥）。
   */
  batch: {
    batchId?: string
    batchNo?: string
    newBatchNo?: string
    newProductionDate?: string
    newExpiryDate?: string
  }
}

/** 新增销售退货单入参（对应后端 CreateSalesReturnRequest；不传小计 / 总额） */
export interface CreateSalesReturnPayload {
  partnerId: string
  returnDate: string
  /** 入库仓 id（038；不传 = 默认仓，前端一律显式传仓） */
  warehouseId?: string
  items: {
    productId: string
    quantity: number
    unitPrice: number
    /** 批次 id（040：按批次商品必填；就地新建时省略） */
    batchId?: string
    /** 就地新建批次号（与 batchId 互斥） */
    newBatchNo?: string
    /** 就地新建批次生产日期（YYYY-MM-DD） */
    newProductionDate?: string
    /** 就地新建批次到期日（YYYY-MM-DD） */
    newExpiryDate?: string
  }[]
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
 * 把所选本地日期（YYYY-MM-DD）转换为后端所需的「所选日期的 UTC 午夜」ISO 串（design.md §4.2）。
 * 直接以 UTC 解释该日历日 00:00:00，避免后端把裸日期按服务器本地时区解析
 * （Npgsql 拒绝把非 UTC 偏移的 DateTimeOffset 写入 timestamptz 列）。
 */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 分页查询销售退货单（含作废单据，作废行前端置灰） */
export function getSalesReturns(query: SalesReturnQuery): Promise<PagedResult<SalesReturnListItem>> {
  return get<PagedResult<SalesReturnListItem>>('/sales-returns', { params: query })
}

/** 查询销售退货单详情（含明细行，快照字段原样返回） */
export function getSalesReturn(id: string): Promise<SalesReturnDetail> {
  return get<SalesReturnDetail>(`/sales-returns/${id}`)
}

/** 新增销售退货单（一步式：保存即生效，库存立即回增并写流水；返回详情含后端生成的单号与重算金额） */
export function createSalesReturn(payload: CreateSalesReturnPayload): Promise<SalesReturnDetail> {
  return post<SalesReturnDetail>('/sales-returns', payload)
}

/** 作废销售退货单（回冲库存；仅改状态不删数据） */
export function voidSalesReturn(id: string): Promise<SalesReturnDetail> {
  return put<SalesReturnDetail>(`/sales-returns/${id}/void`)
}

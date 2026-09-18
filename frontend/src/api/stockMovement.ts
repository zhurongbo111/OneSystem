import { get } from './request'
import type { PagedResult } from './user'

/** 变动类型（对应后端 StockMovementType 小整数；取值 5–10 由后续规格追加） */
export type StockMovementType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10

/** 库存流水行（对应后端 StockMovementListItemDto） */
export interface StockMovementListItem {
  id: string
  productId: string
  productCode: string
  productName: string
  unit: string
  movementType: StockMovementType
  quantity: number
  /** 本次变动成本单价（erp-cost；numeric(18,4)） */
  unitCost: number
  /** 本次变动成本金额（erp-cost；与 quantity 同号） */
  totalCost: number
  sourceNo: string | null
  remark: string | null
  createdAt: string
  createdByName: string | null
}

/** 库存流水查询参数（对应后端 GetStockMovementsRequest；时间均为 UTC ISO 串） */
export interface StockMovementQuery {
  keyword?: string
  productId?: string
  type?: StockMovementType
  start?: string
  end?: string
  page: number
  pageSize: number
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC。
 * 库内统一存 UTC，按 UTC 直接比较；这样"选今天"能覆盖刚产生的记录。
 */
export function toUtcRange(
  startDate?: string,
  endDate?: string,
): { start?: string; end?: string } {
  const start = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : undefined
  const end = endDate ? new Date(`${endDate}T23:59:59.999`).toISOString() : undefined
  return { start, end }
}

/** 分页查询库存流水（只读，按变动时间倒序） */
export function getStockMovements(query: StockMovementQuery): Promise<PagedResult<StockMovementListItem>> {
  return get<PagedResult<StockMovementListItem>>('/stock-movements', { params: query })
}

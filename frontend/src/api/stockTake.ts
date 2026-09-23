import type { PagedResult } from './product'
import { get, post } from './request'

/** 盘点单类型（0 期初建账 / 1 库存盘点） */
export type StockTakeType = 0 | 1

/** 盘点单列表行（对应后端 StockTakeListItemDto） */
export interface StockTakeListItem {
  id: string
  takeNo: string
  type: StockTakeType
  /** 盘点仓 id（038） */
  warehouseId: string
  /** 盘点仓名称快照（038） */
  warehouseName: string
  takeDate: string
  itemCount: number
  diffItemCount: number
  remark: string | null
  createdAt: string
}

/** 盘点单明细行（快照字段原样返回，差异为后端重算值；对应后端 StockTakeItemDto） */
export interface StockTakeItem {
  id: string
  productId: string
  productCode: string
  productName: string
  unit: string
  bookQuantity: number
  actualQuantity: number
  difference: number
  /** 期初成本单价（erp-cost；仅期初建账明细有值，盘点明细为 0） */
  unitCost: number
}

/** 盘点单详情（对应后端 StockTakeDetailDto，明细按插入顺序） */
export interface StockTakeDetail {
  id: string
  takeNo: string
  /** 盘点仓 id（038） */
  warehouseId: string
  /** 盘点仓名称快照（038） */
  warehouseName: string
  type: StockTakeType
  takeDate: string
  itemCount: number
  diffItemCount: number
  remark: string | null
  createdBy: string | null
  createdAt: string
  items: StockTakeItem[]
}

/** 盘点商品选择（对应后端 StockTakeProductPickDto） */
export interface StockTakeProductPick {
  id: string
  code: string
  name: string
  unit: string
  stockQuantity: number
  hasMovements: boolean
}

/** 盘点单列表查询参数（对应后端 GetStockTakesRequest；时间均为 UTC ISO 串） */
export interface StockTakeQuery {
  page: number
  pageSize: number
  keyword?: string
  type?: StockTakeType
  /** 盘点仓 id，可空（038；不传 = 全部仓） */
  warehouseId?: string
  start?: string
  end?: string
}

/** 新建盘点单入参（对应后端 CreateStockTakeRequest；账面 / 差异由后端重算，不传） */
export interface CreateStockTakePayload {
  type: StockTakeType
  takeDate: string
  /** 盘点仓 id（038；必填：账面 / 差异与库存设定都作用于该仓） */
  warehouseId?: string
  /** unitCost 仅期初建账模式传（erp-cost：成本基线必填）；库存盘点模式不传 */
  items: { productId: string; actualQuantity: number; unitCost?: number }[]
  remark?: string
}

/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间（同登录日志约定）：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC。
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

/** 分页查询盘点单（单号关键词 / 类型 / 盘点日期范围筛选） */
export function getStockTakes(query: StockTakeQuery): Promise<PagedResult<StockTakeListItem>> {
  return get<PagedResult<StockTakeListItem>>('/stock-takes', { params: query })
}

/** 查询盘点单详情（含明细行，快照字段原样返回） */
export function getStockTakeById(id: string): Promise<StockTakeDetail> {
  return get<StockTakeDetail>(`/stock-takes/${id}`)
}

/** 新增盘点 / 期初建账单（保存即生效，库存按实盘设定 + 差异行写流水；返回详情含后端生成的单号与重算差异） */
export function createStockTake(payload: CreateStockTakePayload): Promise<StockTakeDetail> {
  return post<StockTakeDetail>('/stock-takes', payload)
}

/**
 * 盘点商品选择（启用商品 + 所选仓当前库存 + 该仓是否已发生库存变动）
 * @param warehouseId 盘点仓 id（038；不传 = 默认仓）
 */
export function getStockTakePickProducts(warehouseId?: string): Promise<StockTakeProductPick[]> {
  return get<StockTakeProductPick[]>('/stock-takes/pick-products', {
    params: warehouseId ? { warehouseId } : undefined,
  })
}

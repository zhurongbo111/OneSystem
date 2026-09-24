import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 调拨单状态（0 已作废 / 1 正常，复用后端 OrderStatus 整型口径） */
export type TransferStatus = 0 | 1

/** 调拨单列表行（对应后端 TransferListItemDto；快照仓名原样返回，免联查） */
export interface TransferListItem {
  id: string
  transferNo: string
  fromWarehouseId: string
  /** 转出仓名称快照（保存即固化） */
  fromWarehouseName: string
  toWarehouseId: string
  /** 转入仓名称快照（保存即固化） */
  toWarehouseName: string
  transferDate: string
  /** 商品行数（列表展示） */
  itemCount: number
  /** 数量合计（列表展示） */
  totalQuantity: number
  status: TransferStatus
  createdAt: string
}

/** 调拨单明细行（对应后端 TransferItemDto；040 前无 batchId，调拨无价格 → 无金额列） */
export interface TransferItem {
  id: string
  productId: string
  productCode: string
  productName: string
  unit: string
  quantity: number
}

/** 调拨单详情（对应后端 TransferDetailDto，明细按插入顺序） */
export interface TransferDetail extends Omit<TransferListItem, 'status'> {
  remark: string | null
  createdBy: string | null
  items: TransferItem[]
  status: TransferStatus
}

/** 调拨单列表查询参数（对应后端 GetTransfersRequest；转出 / 转入仓可空 = 全部仓） */
export interface TransferQuery {
  page: number
  pageSize: number
  keyword?: string
  fromWarehouseId?: string
  toWarehouseId?: string
  start?: string
  end?: string
}

/** 新增调拨单入参（对应后端 CreateTransferRequest；明细仅含 productId / quantity，调拨无价格） */
export interface CreateTransferPayload {
  fromWarehouseId: string
  toWarehouseId: string
  transferDate: string
  items: { productId: string; quantity: number }[]
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

/** 分页查询调拨单（含作废单据，作废行前端置灰） */
export function getTransfers(query: TransferQuery): Promise<PagedResult<TransferListItem>> {
  return get<PagedResult<TransferListItem>>('/transfers', { params: query })
}

/** 查询调拨单详情（含明细行，快照字段原样返回） */
export function getTransfer(id: string): Promise<TransferDetail> {
  return get<TransferDetail>(`/transfers/${id}`)
}

/** 新增调拨单（一步式：保存即生效，转出仓 −、转入仓 + 同一事务；返回详情含后端生成的单号与统计） */
export function createTransfer(payload: CreateTransferPayload): Promise<TransferDetail> {
  return post<TransferDetail>('/transfers', payload)
}

/** 作废调拨单（双向回冲库存与流水；仅改状态不删数据） */
export function voidTransfer(id: string): Promise<TransferDetail> {
  return put<TransferDetail>(`/transfers/${id}/void`)
}

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 本地日期 YYYY-MM-DD → UTC 午夜 ISO（后端 timestamptz 口径，同其他开单 api） */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 批次状态：1 启用 / 0 停用（复用后端 PartnerStatus 整型口径） */
export type BatchStatus = 0 | 1

/** 批次出参（详情 / 新增 / 编辑 / 启停共用，对应后端 BatchDetailDto） */
export interface Batch {
  id: string
  productId: string
  productCode: string
  productName: string
  /** 批次号（创建后不可改） */
  batchNo: string
  productionDate: string | null
  /** 到期日（null = 永不过期） */
  expiryDate: string | null
  status: BatchStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 批次列表行（对应后端 BatchListItemDto，联查商品 + 跨仓库存合计） */
export interface BatchListItem {
  id: string
  productId: string
  productCode: string
  productName: string
  batchNo: string
  productionDate: string | null
  expiryDate: string | null
  status: BatchStatus
  /** 跨仓库存合计（Σ 该批次所有仓批次行） */
  totalStock: number
  createdAt: string
  remark: string | null
}

/** 批次下拉项（开单页选择控件，对应后端 BatchPickDto） */
export interface BatchPickItem {
  batchId: string
  batchNo: string
  productionDate: string | null
  expiryDate: string | null
  status: BatchStatus
  /** 该仓可用库存 */
  availableQuantity: number
  /** 是否已过期 */
  isExpired: boolean
  /** 是否近效期 */
  isNearExpiry: boolean
}

/** 批次列表查询参数（对应后端 GetBatchesRequest） */
export interface BatchListQuery {
  keyword?: string
  productId?: string
  status?: BatchStatus
  /** 仅看近效期或过期 */
  onlyExpiring?: boolean
  page: number
  pageSize: number
}

/** 新增批次入参（对应后端 CreateBatchRequest） */
export interface CreateBatchPayload {
  productId: string
  batchNo: string
  productionDate?: string
  expiryDate?: string
  remark?: string
}

/** 编辑批次入参（对应后端 UpdateBatchRequest，批次号不可改） */
export interface UpdateBatchPayload {
  productionDate?: string
  expiryDate?: string
  remark?: string
}

/** 批次下拉查询参数（对应后端 GetBatchPickListRequest） */
export interface BatchPickQuery {
  productId: string
  warehouseId: string
}

/**
 * 开单页批次选择值（v-model 绑定 BatchPickSelect，随明细行持有，规格 §3.4）：
 * 选中已有批次 → `batchId` / `batchNo` 有值；就地新建 → `newBatchNo` 等三字段有值（与 batchId 互斥）。
 * 非按批次商品恒为 `{}`；提交时展平为 `batchId` / `newBatchNo` / `newProductionDate` / `newExpiryDate`。
 */
export interface BatchPickValue {
  batchId?: string
  batchNo?: string
  newBatchNo?: string
  newProductionDate?: string
  newExpiryDate?: string
}

/** 分页查询批次（批次号关键词 / 商品 / 状态 / 仅看近效期或过期筛选） */
export function getBatches(query: BatchListQuery): Promise<PagedResult<BatchListItem>> {
  return get<PagedResult<BatchListItem>>('/batches', { params: query })
}

/** 新增批次（同商品内批次号唯一） */
export function createBatch(payload: CreateBatchPayload): Promise<Batch> {
  return post<Batch>('/batches', payload)
}

/** 查询批次详情 */
export function getBatchById(id: string): Promise<Batch> {
  return get<Batch>(`/batches/${id}`)
}

/** 编辑批次（批次号不可改） */
export function updateBatch(id: string, payload: UpdateBatchPayload): Promise<Batch> {
  return put<Batch>(`/batches/${id}`, payload)
}

/** 启用 / 停用批次 */
export function updateBatchStatus(id: string, status: BatchStatus): Promise<Batch> {
  return put<Batch>(`/batches/${id}/status`, { status })
}

/** 批次下拉查询（开单页；按商品 + 仓返回启用批次与该仓可用库存，到期日升序） */
export function getBatchPickList(query: BatchPickQuery): Promise<BatchPickItem[]> {
  return get<BatchPickItem[]>('/batches/pick', { params: query })
}

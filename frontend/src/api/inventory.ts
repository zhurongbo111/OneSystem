import type { PagedResult } from './product'
import { get, put } from './request'

/** 库存查询列表项（对应后端 InventoryItemDto，camelCase 一一对应；038 起一行 = 商品 × 仓） */
export interface InventoryItem {
  productId: string
  code: string
  name: string
  categoryName: string
  unit: string
  /** 仓库 ID（038） */
  warehouseId: string
  /** 仓库名称（038） */
  warehouseName: string
  stockQuantity: number
  /** 仓级安全库存阈值（038 起为低库存判定的唯一来源） */
  safetyStock: number
  /** 批次 ID（040：展开批次视图有值，汇总视图为 null） */
  batchId: string | null
  /** 批次号（040；汇总视图为 null） */
  batchNo: string | null
  /** 到期日（040；null = 永不过期或汇总视图） */
  expiryDate: string | null
  /** 是否已过期（040） */
  isExpired: boolean
  /** 是否近效期（040） */
  isNearExpiry: boolean
  isBelowSafetyStock: boolean
  /** 最近库存变动时间（无库存行时为 null） */
  updatedAt: string | null
}

/** 库存列表查询参数（对应后端 GetInventoryRequest；warehouseId 不传 = 全部仓） */
export interface InventoryListQuery {
  keyword?: string
  categoryId?: string
  warehouseId?: string
  /** 批次 id（040：按批次精确筛选） */
  batchId?: string
  /** 批次号模糊筛选（040，大小写不敏感；与 batchId 同时提供时取交集） */
  batchNo?: string
  /** 是否按批次展开行（040，默认 false = 按「商品 × 仓」汇总） */
  expandBatch?: boolean
  page: number
  pageSize: number
}

/** 分页查询库存（只读，仅启用商品，按编码升序） */
export function getInventory(query: InventoryListQuery): Promise<PagedResult<InventoryItem>> {
  return get<PagedResult<InventoryItem>>('/inventory', { params: query })
}

/** 维护仓级安全库存的入参（对应后端 UpdateInventorySafetyStockRequest） */
export interface UpdateInventorySafetyStockPayload {
  productId: string
  warehouseId: string
  safetyStock: number
}

/** 维护仓级安全库存的出参（对应后端 UpdateInventorySafetyStockResponse） */
export interface InventorySafetyStockResult {
  productId: string
  warehouseId: string
  safetyStock: number
  stockQuantity: number
  isBelowSafetyStock: boolean
}

/** 维护仓级安全库存（库存查询页行内单字段 Modal，权限 inventory.update；该仓无库存行时后端返回 40400） */
export function updateInventorySafetyStock(
  payload: UpdateInventorySafetyStockPayload,
): Promise<InventorySafetyStockResult> {
  return put<InventorySafetyStockResult>('/inventory/safety-stock', payload)
}

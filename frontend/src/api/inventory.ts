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
  isBelowSafetyStock: boolean
  /** 最近库存变动时间（无库存行时为 null） */
  updatedAt: string | null
}

/** 库存列表查询参数（对应后端 GetInventoryRequest；warehouseId 不传 = 全部仓） */
export interface InventoryListQuery {
  keyword?: string
  categoryId?: string
  warehouseId?: string
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

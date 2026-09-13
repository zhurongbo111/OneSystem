import type { PagedResult } from './product'
import { get } from './request'

/** 库存查询列表项（对应后端 InventoryItemDto，camelCase 一一对应） */
export interface InventoryItem {
  productId: string
  code: string
  name: string
  categoryName: string
  unit: string
  stockQuantity: number
  safetyStock: number
  isBelowSafetyStock: boolean
  /** 最近库存变动时间（无库存行时为 null） */
  updatedAt: string | null
}

/** 库存列表查询参数（对应后端 GetInventoryRequest） */
export interface InventoryListQuery {
  keyword?: string
  categoryId?: string
  page: number
  pageSize: number
}

/** 分页查询库存（只读，仅启用商品，按编码升序） */
export function getInventory(query: InventoryListQuery): Promise<PagedResult<InventoryItem>> {
  return get<PagedResult<InventoryItem>>('/inventory', { params: query })
}

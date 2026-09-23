import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 仓库状态：1 启用 / 0 停用（复用后端 PartnerStatus 整型口径） */
export type WarehouseStatus = 0 | 1

/** 仓库出参（列表 / 详情 / 新增 / 编辑 / 启停 / 设为默认共用，对应后端 WarehouseDto） */
export interface Warehouse {
  id: string
  /** 仓库编码（创建后不可修改） */
  code: string
  name: string
  address: string | null
  contact: string | null
  phone: string | null
  /** 是否默认仓（全系统唯一，不可停用） */
  isDefault: boolean
  status: WarehouseStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 开单页仓库下拉项（对应后端 WarehousePickDto，仅启用仓） */
export interface WarehousePickItem {
  id: string
  code: string
  name: string
  isDefault: boolean
}

/** 仓库列表查询参数（对应后端 GetWarehousesRequest，默认仓置顶） */
export interface WarehouseListQuery {
  keyword?: string
  status?: WarehouseStatus
  page: number
  pageSize: number
}

/** 新增仓库入参（对应后端 CreateWarehouseRequest，默认启用、非默认仓） */
export interface CreateWarehousePayload {
  code: string
  name: string
  address?: string
  contact?: string
  phone?: string
  remark?: string
}

/** 编辑仓库入参（对应后端 UpdateWarehouseRequest，编码不可改） */
export interface UpdateWarehousePayload {
  name: string
  address?: string
  contact?: string
  phone?: string
  remark?: string
}

/** 分页查询仓库（关键词 / 状态筛选，默认仓置顶，编码升序） */
export function getWarehouses(query: WarehouseListQuery): Promise<PagedResult<Warehouse>> {
  return get<PagedResult<Warehouse>>('/warehouses', { params: query })
}

/** 查询仓库详情 */
export function getWarehouse(id: string): Promise<Warehouse> {
  return get<Warehouse>(`/warehouses/${id}`)
}

/** 新增仓库（编码 / 名称唯一） */
export function createWarehouse(payload: CreateWarehousePayload): Promise<Warehouse> {
  return post<Warehouse>('/warehouses', payload)
}

/** 编辑仓库（编码不可改） */
export function updateWarehouse(id: string, payload: UpdateWarehousePayload): Promise<Warehouse> {
  return put<Warehouse>(`/warehouses/${id}`, payload)
}

/** 启用 / 停用仓库（默认仓不可停用，后端返回 40124） */
export function updateWarehouseStatus(id: string, status: WarehouseStatus): Promise<Warehouse> {
  return put<Warehouse>(`/warehouses/${id}/status`, { status })
}

/** 设为默认仓（同一事务内清空其他仓默认标记） */
export function setDefaultWarehouse(id: string): Promise<Warehouse> {
  return put<Warehouse>(`/warehouses/${id}/default`, {})
}

/** 开单页仓库下拉（仅启用仓，默认仓供预选） */
export function getWarehousePickList(): Promise<WarehousePickItem[]> {
  return get<WarehousePickItem[]>('/warehouses/pick')
}

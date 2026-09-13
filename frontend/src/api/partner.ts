import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 往来单位类型（1 供应商 / 2 客户 / 3 两者） */
export type PartnerType = 1 | 2 | 3

/** 往来单位状态（0 停用 / 1 启用） */
export type PartnerStatus = 0 | 1

export interface Partner {
  id: string
  name: string
  type: PartnerType
  contact?: string | null
  phone?: string | null
  address?: string | null
  remark?: string | null
  status: PartnerStatus
  createdAt: string
  updatedAt: string
}

export interface GetPartnersParams {
  page?: number
  pageSize?: number
  keyword?: string
  type?: PartnerType
  status?: PartnerStatus
}

export interface CreatePartnerPayload {
  name: string
  type: PartnerType
  contact?: string
  phone?: string
  address?: string
  remark?: string
}

export interface UpdatePartnerPayload {
  type: PartnerType
  contact?: string
  phone?: string
  address?: string
  remark?: string
}

/** 分页查询往来单位（erp-purchase / erp-sale 开单下拉数据源也复用本函数） */
export function getPartners(params: GetPartnersParams): Promise<PagedResult<Partner>> {
  return get<PagedResult<Partner>>('/partners', { params })
}

/** 查询往来单位详情 */
export function getPartner(id: string): Promise<Partner> {
  return get<Partner>(`/partners/${id}`)
}

/** 新增往来单位 */
export function createPartner(payload: CreatePartnerPayload): Promise<Partner> {
  return post<Partner>('/partners', payload)
}

/** 编辑往来单位（名称不可改） */
export function updatePartner(id: string, payload: UpdatePartnerPayload): Promise<Partner> {
  return put<Partner>(`/partners/${id}`, payload)
}

/** 停用 / 启用往来单位 */
export function updatePartnerStatus(id: string, status: PartnerStatus): Promise<Partner> {
  return put<Partner>(`/partners/${id}/status`, { status })
}

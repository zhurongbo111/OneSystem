import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/** 税率状态（0 停用 / 1 启用） */
export type TaxRateStatus = 0 | 1

/** 税率列表行 / 详情（对应后端 TaxRateListItemDto / TaxRateDetailDto） */
export interface TaxRate {
  id: string
  code: string
  name: string
  /** 税率百分比数值（13 表示 13%） */
  rate: number
  status: TaxRateStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

export interface GetTaxRatesParams {
  page?: number
  pageSize?: number
  keyword?: string
  status?: TaxRateStatus
}

export interface CreateTaxRatePayload {
  code: string
  name: string
  rate: number
  status: TaxRateStatus
  remark?: string
}

export interface UpdateTaxRatePayload {
  code: string
  name: string
  rate: number
  status: TaxRateStatus
  remark?: string
}

/** 分页查询税率 */
export function getTaxRates(params: GetTaxRatesParams): Promise<PagedResult<TaxRate>> {
  return get<PagedResult<TaxRate>>('/tax-rates', { params })
}

/** 查询税率详情 */
export function getTaxRate(id: string): Promise<TaxRate> {
  return get<TaxRate>(`/tax-rates/${id}`)
}

/** 新增税率 */
export function createTaxRate(payload: CreateTaxRatePayload): Promise<TaxRate> {
  return post<TaxRate>('/tax-rates', payload)
}

/** 编辑税率（编码 / 名称 / 税率 / 状态 / 备注全量覆盖） */
export function updateTaxRate(id: string, payload: UpdateTaxRatePayload): Promise<TaxRate> {
  return put<TaxRate>(`/tax-rates/${id}`, payload)
}

/** 删除税率 */
export function deleteTaxRate(id: string): Promise<null> {
  return del<null>(`/tax-rates/${id}`)
}

/** 停用 / 启用税率 */
export function updateTaxRateStatus(id: string, status: TaxRateStatus): Promise<TaxRate> {
  return put<TaxRate>(`/tax-rates/${id}/status`, { status })
}
import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/**
 * 生效价来源（0 协议价 / 1 默认价 = 商品销售价）
 *
 * 取值由后端 `PriceSource` 枚举序列化而来，口径见 `specs/036-erp-partner-price/design.md` §0.1。
 */
export type PriceSource = 0 | 1

/** 客户价格列表行（对应后端 PartnerPriceListItemDto） */
export interface PartnerPriceListItem {
  id: string
  partnerId: string
  partnerName: string
  productId: string
  productCode: string
  productName: string
  unit: string
  /** 协议单价 */
  price: number
  /** 商品当前销售价（用于「高于默认价」提示与价差展示） */
  salePrice: number
  remark: string | null
  createdAt: string
}

/** 客户价格详情（对应后端 PartnerPriceDetailDto；客户与商品不可改，故读写一致） */
export interface PartnerPriceDetail {
  id: string
  partnerId: string
  partnerName: string
  productId: string
  productCode: string
  productName: string
  unit: string
  price: number
  salePrice: number
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 客户价格列表查询参数（对应后端 GetPartnerPricesRequest） */
export interface PartnerPriceQuery {
  page: number
  pageSize: number
  /** 关键词：匹配客户名 / 商品编码 / 商品名称 */
  keyword?: string
  partnerId?: string
  productId?: string
}

/** 新增客户协议价入参（对应后端 CreatePartnerPriceRequest） */
export interface CreatePartnerPricePayload {
  partnerId: string
  productId: string
  price: number
  remark?: string
}

/** 编辑客户协议价入参（对应后端 UpdatePartnerPriceRequest；客户与商品不可改） */
export interface UpdatePartnerPricePayload {
  price: number
  remark?: string
}

/** 批量取价入参（对应后端 GetEffectivePricesRequest） */
export interface EffectivePriceQuery {
  partnerId: string
  productIds: string[]
}

/** 生效价（对应后端 EffectivePriceDto） */
export interface EffectivePrice {
  productId: string
  /** 生效单价（协议价优先，未配置时为商品销售价） */
  unitPrice: number
  source: PriceSource
}

/** 单价来源标签元数据（开单页来源标注唯一来源，specs/036-erp-partner-price/design.md §4.4） */
export const PRICE_SOURCE_META: Record<PriceSource, { label: string; color: string }> = {
  0: { label: '协议价', color: 'arcoblue' },
  1: { label: '默认价', color: 'gray' },
}

/** 分页查询客户价格（联查客户名与商品编码 / 名称 / 单位 / 当前销售价） */
export function getPartnerPrices(query: PartnerPriceQuery): Promise<PagedResult<PartnerPriceListItem>> {
  return get<PagedResult<PartnerPriceListItem>>('/partner-prices', { params: query })
}

/** 查询客户价格详情 */
export function getPartnerPrice(id: string): Promise<PartnerPriceDetail> {
  return get<PartnerPriceDetail>(`/partner-prices/${id}`)
}

/** 新增客户协议价（同一「客户 × 商品」只允许一条） */
export function createPartnerPrice(payload: CreatePartnerPricePayload): Promise<PartnerPriceDetail> {
  return post<PartnerPriceDetail>('/partner-prices', payload)
}

/** 编辑客户协议价（仅改单价与备注） */
export function updatePartnerPrice(id: string, payload: UpdatePartnerPricePayload): Promise<PartnerPriceDetail> {
  return put<PartnerPriceDetail>(`/partner-prices/${id}`, payload)
}

/** 删除客户协议价（删除后该商品回退默认价） */
export function deletePartnerPrice(id: string): Promise<null> {
  return del<null>(`/partner-prices/${id}`)
}

/**
 * 批量取价（协议价优先，未配置时取商品销售价）。
 *
 * `productIds` 用重复键拼接（`productIds=a&productIds=b`），避免 Axios 默认的 `productIds[]=` 形式
 * 无法被后端模型绑定接收。
 */
export function getEffectivePrices(query: EffectivePriceQuery): Promise<EffectivePrice[]> {
  const params = new URLSearchParams({ partnerId: query.partnerId })
  query.productIds.forEach((productId) => params.append('productIds', productId))
  return get<EffectivePrice[]>('/partner-prices/effective', { params })
}

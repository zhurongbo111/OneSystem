import type { OrderFlowStatus } from '@/utils/orderFlow'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 销售订单列表行（对应后端 SalesOrderListItemDto） */
export interface SalesOrderListItem {
  id: string
  orderNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  expectedDate: string | null
  totalAmount: number
  /** 未发数量合计（Σ 明细未执行量；= 0 表示已发齐） */
  unfulfilledQuantity: number
  flowStatus: OrderFlowStatus
  createdAt: string
}

/** 销售订单明细行（含累计已发与未发数量，对应后端 SalesOrderItemDto） */
export interface SalesOrderItem {
  id: string
  productId: string
  productName: string
  unit: string
  quantity: number
  fulfilledQuantity: number
  remainingQuantity: number
  unitPrice: number
  subtotal: number
}

/** 销售订单详情（对应后端 SalesOrderDetailDto） */
export interface SalesOrderDetail {
  id: string
  orderNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  expectedDate: string | null
  totalAmount: number
  flowStatus: OrderFlowStatus
  remark: string | null
  createdBy: string | null
  createdAt: string
  items: SalesOrderItem[]
}

/** 销售订单列表查询参数（对应后端 GetSalesOrdersRequest） */
export interface SalesOrderQuery {
  page: number
  pageSize: number
  keyword?: string
  partnerId?: string
  flowStatus?: OrderFlowStatus
  start?: string
  end?: string
}

/**
 * 订单表单明细行本地类型：
 * `subtotal` 为前端实时计算（数量 × 单价）仅用于展示，提交 payload 不含小计 / 总额。
 */
export interface SalesOrderFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增 / 编辑销售订单入参（对应后端 Create / UpdateSalesOrderRequest） */
export interface SaveSalesOrderPayload {
  partnerId: string
  orderDate: string
  expectedDate?: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/** 分页查询销售订单（含作废 / 已关闭订单，前端置灰） */
export function getSalesOrders(query: SalesOrderQuery): Promise<PagedResult<SalesOrderListItem>> {
  return get<PagedResult<SalesOrderListItem>>('/sales-orders', { params: query })
}

/** 查询销售订单详情（含明细的订购 / 已发 / 未发数量） */
export function getSalesOrder(id: string): Promise<SalesOrderDetail> {
  return get<SalesOrderDetail>(`/sales-orders/${id}`)
}

/** 新增销售订单（计划单据：不动库存、不写流水） */
export function createSalesOrder(payload: SaveSalesOrderPayload): Promise<SalesOrderDetail> {
  return post<SalesOrderDetail>('/sales-orders', payload)
}

/** 编辑销售订单（仅「待发货」状态允许；明细整体替换） */
export function updateSalesOrder(id: string, payload: SaveSalesOrderPayload): Promise<SalesOrderDetail> {
  return put<SalesOrderDetail>(`/sales-orders/${id}`, payload)
}

/** 作废销售订单（仅「待发货」状态允许） */
export function voidSalesOrder(id: string): Promise<SalesOrderDetail> {
  return put<SalesOrderDetail>(`/sales-orders/${id}/void`)
}

/** 关闭销售订单（待发货 / 部分发货允许；剩余不再发货） */
export function closeSalesOrder(id: string): Promise<SalesOrderDetail> {
  return put<SalesOrderDetail>(`/sales-orders/${id}/close`)
}

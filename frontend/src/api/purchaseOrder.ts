import type { OrderFlowStatus } from '@/utils/orderFlow'

import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 采购订单列表行（对应后端 PurchaseOrderListItemDto） */
export interface PurchaseOrderListItem {
  id: string
  orderNo: string
  partnerId: string
  partnerName: string
  orderDate: string
  expectedDate: string | null
  totalAmount: number
  /** 未收数量合计（Σ 明细未执行量；= 0 表示已收齐） */
  unfulfilledQuantity: number
  flowStatus: OrderFlowStatus
  createdAt: string
}

/** 采购订单明细行（含累计已收与未收数量，对应后端 PurchaseOrderItemDto） */
export interface PurchaseOrderItem {
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

/** 采购订单详情（对应后端 PurchaseOrderDetailDto） */
export interface PurchaseOrderDetail {
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
  items: PurchaseOrderItem[]
}

/** 采购订单列表查询参数（对应后端 GetPurchaseOrdersRequest） */
export interface PurchaseOrderQuery {
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
export interface PurchaseOrderFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  quantity: number
  unitPrice: number
  subtotal: number
}

/** 新增 / 编辑采购订单入参（对应后端 Create / UpdatePurchaseOrderRequest） */
export interface SavePurchaseOrderPayload {
  partnerId: string
  orderDate: string
  expectedDate?: string
  items: { productId: string; quantity: number; unitPrice: number }[]
  remark?: string
}

/** 分页查询采购订单（含作废 / 已关闭订单，前端置灰） */
export function getPurchaseOrders(query: PurchaseOrderQuery): Promise<PagedResult<PurchaseOrderListItem>> {
  return get<PagedResult<PurchaseOrderListItem>>('/purchase-orders', { params: query })
}

/** 查询采购订单详情（含明细的订购 / 已收 / 未收数量） */
export function getPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return get<PurchaseOrderDetail>(`/purchase-orders/${id}`)
}

/** 新增采购订单（计划单据：不动库存、不写流水） */
export function createPurchaseOrder(payload: SavePurchaseOrderPayload): Promise<PurchaseOrderDetail> {
  return post<PurchaseOrderDetail>('/purchase-orders', payload)
}

/** 编辑采购订单（仅「待收货」状态允许；明细整体替换） */
export function updatePurchaseOrder(id: string, payload: SavePurchaseOrderPayload): Promise<PurchaseOrderDetail> {
  return put<PurchaseOrderDetail>(`/purchase-orders/${id}`, payload)
}

/** 作废采购订单（仅「待收货」状态允许） */
export function voidPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return put<PurchaseOrderDetail>(`/purchase-orders/${id}/void`)
}

/** 关闭采购订单（待收货 / 部分收货允许；剩余不再收货） */
export function closePurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return put<PurchaseOrderDetail>(`/purchase-orders/${id}/close`)
}

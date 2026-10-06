import type { PagedResult } from './product'
import { get, put } from './request'

/** 审批状态（0 无需审批 / 1 待审批 / 2 已通过 / 3 已驳回 / 4 已撤回，对应后端 ApprovalStatus） */
export type ApprovalStatus = 0 | 1 | 2 | 3 | 4

/**
 * 审批可用的单据类型（0 采购入库单 / 1 销售出库单 / 2 采购退货单 / 3 销售退货单，
 * 对应后端 SettlementOrderType 的审批子集）。
 */
export type ApprovalOrderType = 0 | 1 | 2 | 3

/** 审批列表行（对应后端 ApprovalListItemDto） */
export interface ApprovalListItem {
  /** 审批记录 id */
  id: string
  orderType: ApprovalOrderType
  /** 被审批单据 id */
  orderId: string
  /** 单据号快照 */
  orderNo: string
  /** 往来单位名称快照 */
  partnerName: string
  /** 单据金额快照 */
  amount: number
  /** 审批状态（1 待审批 / 2 已通过 / 3 已驳回 / 4 已撤回） */
  status: ApprovalStatus
  /** 提交人 id */
  submittedBy: string
  /** 提交人显示名 */
  submittedByName: string
  /** 提交时间 */
  submittedAt: string
  /** 审批人 id（未决定时为空） */
  decidedBy: string | null
  /** 审批人显示名（未决定时为空） */
  decidedByName: string | null
  /** 审批时间（未决定时为空） */
  decidedAt: string | null
  /** 审批意见（驳回必填、通过可空） */
  decisionRemark: string | null
}

/** 被审批单据明细行（对应后端 ApprovalItemDto，只读展示） */
export interface ApprovalItem {
  /** 商品名称快照 */
  productName: string
  /** 计量单位快照 */
  unit: string
  /** 批次号快照（040；非批次商品为空） */
  batchNo: string | null
  quantity: number
  unitPrice: number
  /** 小计（后端重算值） */
  subtotal: number
}

/** 审批详情（对应后端 ApprovalDetailDto：审批记录快照 + 被审批单据摘要与明细） */
export interface ApprovalDetail extends ApprovalListItem {
  /** 被审批单据的业务日期 */
  orderDate: string
  /** 被审批单据的仓库名称快照 */
  warehouseName: string
  /** 被审批单据明细（只读，按插入顺序） */
  items: ApprovalItem[]
}

/** 审批列表查询参数（对应后端 GetApprovalsRequest；status 不传 = 全部） */
export interface ApprovalQuery {
  page: number
  pageSize: number
  /** 审批状态（「待我审批」= 1；不传 = 全部） */
  status?: ApprovalStatus
  orderType?: ApprovalOrderType
  /** 提交人 id */
  submittedBy?: string
  /** 起始提交时间（含） */
  start?: string
  /** 结束提交时间（含） */
  end?: string
}

/** 审批规则（对应后端 ApprovalRuleDto；未配置的类型返回默认「未启用」） */
export interface ApprovalRule {
  orderType: ApprovalOrderType
  /** 审批阈值（未配置时为 0） */
  thresholdAmount: number
  /** 是否启用（未启用则该类单据保存即生效） */
  enabled: boolean
}

/** 通过入参（对应后端 ApproveOrderRequest；审批意见可空、≤ 200 字符） */
export interface ApprovePayload {
  remark?: string
}

/** 驳回入参（对应后端 RejectApprovalRequest；审批意见必填、≤ 200 字符） */
export interface RejectPayload {
  remark: string
}

/**
 * 审批状态文案与 a-tag 颜色映射（全项目唯一来源，审批页与四类单据页共用）：
 * 与后端 `ApprovalStatus` 整型取值一一对应。
 */
export const APPROVAL_STATUS_META: Record<ApprovalStatus, { label: string; color: string }> = {
  0: { label: '无需审批', color: 'gray' },
  1: { label: '待审批', color: 'orange' },
  2: { label: '已通过', color: 'green' },
  3: { label: '已驳回', color: 'red' },
  4: { label: '已撤回', color: 'gray' },
}

/** 审批状态下拉选项（顺序即映射表顺序） */
export const APPROVAL_STATUS_OPTIONS = (Object.keys(APPROVAL_STATUS_META) as unknown as ApprovalStatus[]).map(
  (value) => ({ label: APPROVAL_STATUS_META[value].label, value }),
)

/** 单据类型文案与 a-tag 颜色映射（全项目唯一来源） */
export const APPROVAL_ORDER_TYPE_META: Record<ApprovalOrderType, { label: string; color: string }> = {
  0: { label: '采购入库单', color: 'arcoblue' },
  1: { label: '销售出库单', color: 'green' },
  2: { label: '采购退货单', color: 'orange' },
  3: { label: '销售退货单', color: 'purple' },
}

/** 单据类型下拉选项（顺序即映射表顺序） */
export const APPROVAL_ORDER_TYPE_OPTIONS = (Object.keys(APPROVAL_ORDER_TYPE_META) as unknown as ApprovalOrderType[]).map(
  (value) => ({ label: APPROVAL_ORDER_TYPE_META[value].label, value }),
)

/** 分页查询审批记录（「待我审批」= status 传 1；支持单据类型 / 提交人 / 时间筛选） */
export function getApprovals(query: ApprovalQuery): Promise<PagedResult<ApprovalListItem>> {
  return get<PagedResult<ApprovalListItem>>('/approvals', { params: query })
}

/** 查询审批详情（审批记录快照 + 被审批单据摘要与明细） */
export function getApproval(id: string): Promise<ApprovalDetail> {
  return get<ApprovalDetail>(`/approvals/${id}`)
}

/** 审批通过（通过时单据才生效；生效失败如库存不足则整体回滚、保持待审批） */
export function approveOrder(id: string, payload: ApprovePayload): Promise<ApprovalDetail> {
  return put<ApprovalDetail>(`/approvals/${id}/approve`, payload)
}

/** 审批驳回（意见必填；驳回即单据作废，不做任何库存回冲） */
export function rejectApproval(id: string, payload: RejectPayload): Promise<ApprovalDetail> {
  return put<ApprovalDetail>(`/approvals/${id}/reject`, payload)
}

/** 撤回待审批单据（仅提交人本人；撤回即单据作废，不做任何库存回冲） */
export function withdrawApproval(id: string): Promise<ApprovalDetail> {
  return put<ApprovalDetail>(`/approvals/${id}/withdraw`)
}

/** 查询审批规则（四类单据各一行，未配置返回「未启用」） */
export function getApprovalRules(): Promise<ApprovalRule[]> {
  return get<ApprovalRule[]>('/approval-rules')
}

/** 保存审批规则（逐类型 upsert；阈值以上需审批，未启用则该类单据保存即生效） */
export function updateApprovalRules(rules: ApprovalRule[]): Promise<ApprovalRule[]> {
  return put<ApprovalRule[]>('/approval-rules', { rules })
}

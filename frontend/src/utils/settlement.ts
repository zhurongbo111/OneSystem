/**
 * 单据结算状态展示口径（唯一来源：`specs/023-erp-settlement/design.md` §0）。
 * 单据列表 / 详情与收付款页共用，禁止在各页面重复硬编码文案与颜色。
 */
import type { SettlementOrderType } from '@/api/settlement'

/** 结算状态（0 未结算 / 1 部分结算 / 2 已结算；后端按已结金额推导，不落列） */
export type SettlementState = 0 | 1 | 2

/** 结算状态标签元数据：文案（部分结算）/ 颜色 */
const SETTLEMENT_STATE_META: Record<SettlementState, { label: string; color: string }> = {
  0: { label: '未结算', color: 'gray' },
  1: { label: '部分结算', color: 'orange' },
  2: { label: '已结算', color: 'green' },
}

/** 结算状态下拉选项（列表筛选：未结算 / 部分结算 / 已结算） */
export const SETTLEMENT_STATE_OPTIONS: { label: string; value: SettlementState }[] = [
  { label: '未结算', value: 0 },
  { label: '部分结算', value: 1 },
  { label: '已结算', value: 2 },
]

/**
 * 结算状态标签文案：部分结算时追加未结金额（如「部分结算（未结 600.00）」）。
 * @param state 结算状态
 * @param unsettledAmount 未结金额（仅部分结算时展示）
 */
export function settlementStateLabel(state: SettlementState, unsettledAmount = 0): string {
  const meta = SETTLEMENT_STATE_META[state] ?? SETTLEMENT_STATE_META[0]
  return state === 1 ? `${meta.label}（未结 ${unsettledAmount.toFixed(2)}）` : meta.label
}

/** 结算状态标签颜色（a-tag color） */
export function settlementStateColor(state: SettlementState): string {
  return (SETTLEMENT_STATE_META[state] ?? SETTLEMENT_STATE_META[0]).color
}

/**
 * 是否可发起收付款（四类单据列表 / 详情「收付款 / 去收付款」按钮的显隐判据，唯一来源）：
 * 单据正常（未作废）且**仍有未结金额**——已结算时没有可核销金额，
 * 点入新建页必然得到空候选列表（`GetUnsettledOrders` 只返回未结单据），故不显示入口。
 */
export function canStartSettlement(order: { status: number; settlementState: SettlementState }): boolean {
  return order.status === 1 && order.settlementState !== 2
}

/** 已核销单据的作废提示（四类单据列表 / 详情共用；与后端 `40120` 处置顺序一致：先作废对应收付款单回退金额） */
export const VOID_SETTLED_HINT = '已被收付款单核销，请先作废对应收付款单'

/**
 * 是否可作废单据（四类单据列表 / 详情「作废」按钮的唯一判据）：
 * 单据正常（未作废）且**未被收付款单核销**——已核销（`settledAmount > 0`）时后端拒绝（`40120`），
 * 前端提前禁用并给出处置提示，避免「点了确认才被拒」。
 */
export function canVoidOrder(order: { status: number; settledAmount: number }): boolean {
  return order.status === 1 && order.settledAmount <= 0
}

/** 被核销单据类型元数据：文案 + 详情路由名（唯一来源，收付款详情 / 往来对账的「单号」超链接共用） */
const SETTLEMENT_ORDER_TYPE_META: Record<SettlementOrderType, { label: string; routeName: string }> = {
  0: { label: '采购入库单', routeName: 'purchaseDetail' },
  1: { label: '销售出库单', routeName: 'salesDetail' },
  2: { label: '采购退货单', routeName: 'purchaseReturnDetail' },
  3: { label: '销售退货单', routeName: 'saleReturnDetail' },
}

/** 被核销单据类型文案 */
export function settlementOrderTypeLabel(orderType: SettlementOrderType): string {
  return SETTLEMENT_ORDER_TYPE_META[orderType]?.label ?? ''
}

/** 被核销单据类型 → 详情路由名（单号超链接跳转用；未知取值返回 null） */
export function settlementOrderTypeRouteName(orderType: SettlementOrderType): string | null {
  return SETTLEMENT_ORDER_TYPE_META[orderType]?.routeName ?? null
}

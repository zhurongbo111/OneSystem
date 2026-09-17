/**
 * 订单流转状态展示约定（specs/024-erp-order-flow design.md §0，全项目唯一来源）。
 * 采购侧与销售侧文案不同，颜色共用；列表 / 详情 / 下单页与 e2e 断言均以此为准。
 */
export type OrderFlowStatus = 0 | 1 | 2 | 3 | 4

/** 订单所属侧别（决定「待收货 / 待发货」等文案） */
export type OrderFlowSide = 'purchase' | 'sales'

const LABELS: Record<OrderFlowSide, Record<OrderFlowStatus, string>> = {
  purchase: {
    0: '已作废',
    1: '待收货',
    2: '部分收货',
    3: '已完成',
    4: '已关闭',
  },
  sales: {
    0: '已作废',
    1: '待发货',
    2: '部分发货',
    3: '已完成',
    4: '已关闭',
  },
}

const COLORS: Record<OrderFlowStatus, string> = {
  0: 'red',
  1: 'blue',
  2: 'orange',
  3: 'green',
  4: 'gray',
}

/** 订单状态文案（按侧别取词） */
export function orderFlowStatusLabel(status: OrderFlowStatus, side: OrderFlowSide): string {
  return LABELS[side][status]
}

/** 订单状态标签色（design.md §0） */
export function orderFlowStatusColor(status: OrderFlowStatus): string {
  return COLORS[status]
}

/** 订单状态下拉选项（按侧别取词，顺序与枚举一致） */
export function orderFlowStatusOptions(side: OrderFlowSide): { label: string; value: OrderFlowStatus }[] {
  return ([0, 1, 2, 3, 4] as OrderFlowStatus[]).map((status) => ({
    label: LABELS[side][status],
    value: status,
  }))
}

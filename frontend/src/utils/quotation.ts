/**
 * 报价单状态展示约定（specs/037-erp-quotation design.md §0.1，全项目唯一来源）。
 * 状态取值与文案、是否「已过期」的判据均以此为准；列表 / 详情 / 转单提示与 e2e 断言共用。
 */
export type QuotationStatus = 0 | 1 | 2

const LABELS: Record<QuotationStatus, string> = {
  0: '草稿',
  1: '已转订单',
  2: '已作废',
}

const COLORS: Record<QuotationStatus, string> = {
  0: 'blue',
  1: 'green',
  2: 'gray',
}

/** 报价单状态文案 */
export function quotationStatusLabel(status: QuotationStatus): string {
  return LABELS[status]
}

/** 报价单状态标签色（design.md §0.1） */
export function quotationStatusColor(status: QuotationStatus): string {
  return COLORS[status]
}

/** 报价单状态下拉选项（顺序与枚举一致） */
export function quotationStatusOptions(): { label: string; value: QuotationStatus }[] {
  return ([0, 1, 2] as QuotationStatus[]).map((status) => ({
    label: LABELS[status],
    value: status,
  }))
}

/**
 * 是否显示「已过期」：仅草稿且有效期早于今天（展示概念，不改变状态，design.md §0.1）。
 *
 * @param status 报价单状态
 * @param validUntil 有效期至（`YYYY-MM-DD`，可空）
 */
export function isQuotationExpired(status: QuotationStatus, validUntil: string | null): boolean {
  if (status !== 0 || !validUntil) return false
  const today = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  const todayText = `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`
  return validUntil < todayText
}

import { post } from './request'

/** 成本重算结果（对应后端 CostRecalculateResultDto） */
export interface CostRecalculateResult {
  /** 本次重算（写回）的流水条数 */
  movementCount: number
  /** 单价无法推算、按 0 计入的流水条数（> 0 需人工补期初成本或执行盘点调整） */
  missingCostCount: number
  /** 涉及商品数 */
  productCount: number
}

/** 成本重算入参（对应后端 RecalculateCostsRequest；三个条件均可空，不传表示全量重算） */
export interface RecalculateCostsPayload {
  productId?: string
  /** 期间起（含，UTC ISO 串） */
  start?: string
  /** 期间止（不含，UTC ISO 串） */
  end?: string
}

/**
 * 按流水时间顺序重算成本（运维 / 初始化动作）：
 * 幂等且不改变任何库存数量；并发执行时后端返回 40118，由请求层统一提示。
 */
export function recalculateCosts(payload: RecalculateCostsPayload = {}): Promise<CostRecalculateResult> {
  return post<CostRecalculateResult>('/costs/recalculate', undefined, { params: payload })
}

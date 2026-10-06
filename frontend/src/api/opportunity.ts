import type { ActivityItem, SaveActivityPayload } from './lead'
import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 商机阶段（0 初步接洽 / 1 需求确认 / 2 方案报价 / 3 谈判 / 4 赢单 / 5 输单，specs/043-erp-crm-presale design.md §0.1） */
export type OpportunityStage = 0 | 1 | 2 | 3 | 4 | 5

/** 商机列表行（对应后端 OpportunityListItemDto） */
export interface OpportunityListItem {
  id: string
  opportunityNo: string
  name: string
  partnerId: string | null
  /** 客户名称快照（可空：线索阶段可能还没有正式客户档案） */
  partnerName: string | null
  amount: number
  stage: OpportunityStage
  /** 预计成交日期（`YYYY-MM-DD`，可空） */
  expectedCloseDate: string | null
  ownerId: string | null
  ownerName: string | null
  createdAt: string
}

/** 商机详情（对应后端 OpportunityDetailDto） */
export interface OpportunityDetail {
  id: string
  opportunityNo: string
  name: string
  leadId: string | null
  partnerId: string | null
  partnerName: string | null
  amount: number
  stage: OpportunityStage
  expectedCloseDate: string | null
  ownerId: string | null
  ownerName: string | null
  remark: string | null
  createdBy: string | null
  createdAt: string
  updatedAt: string
}

/** 商机列表查询参数（对应后端 GetOpportunitiesRequest） */
export interface OpportunityQuery {
  page: number
  pageSize: number
  /** 关键词：商机号 / 名称 */
  keyword?: string
  stage?: OpportunityStage
  partnerId?: string
  ownerId?: string
}

/**
 * 新增 / 编辑商机入参（对应后端 Create / UpdateOpportunityRequest，全量覆盖语义）。
 * 来源线索不可改，故编辑载荷不含 `leadId`。
 */
export interface SaveOpportunityPayload {
  name: string
  partnerId?: string
  amount: number
  stage: OpportunityStage
  /** 预计成交日期（`YYYY-MM-DD`） */
  expectedCloseDate?: string
  ownerId?: string
  remark?: string
}

/** 商机阶段文案与标签色（design.md §0.1，前端唯一来源） */
export const OPPORTUNITY_STAGE_META: Record<OpportunityStage, { label: string; color: string }> = {
  0: { label: '初步接洽', color: 'blue' },
  1: { label: '需求确认', color: 'blue' },
  2: { label: '方案报价', color: 'blue' },
  3: { label: '谈判', color: 'blue' },
  4: { label: '赢单', color: 'green' },
  5: { label: '输单', color: 'red' },
}

/** 商机阶段下拉选项（顺序与枚举一致） */
export const OPPORTUNITY_STAGE_OPTIONS: { label: string; value: OpportunityStage }[] = (
  [0, 1, 2, 3, 4, 5] as OpportunityStage[]
).map((value) => ({ label: OPPORTUNITY_STAGE_META[value].label, value }))

/** 非终态阶段（可继续推进：初步接洽 → 谈判） */
export const OPPORTUNITY_STAGE_OPEN: OpportunityStage[] = [0, 1, 2, 3]

/** 商机阶段文案 */
export function opportunityStageLabel(stage: OpportunityStage): string {
  return OPPORTUNITY_STAGE_META[stage].label
}

/** 商机阶段标签色 */
export function opportunityStageColor(stage: OpportunityStage): string {
  return OPPORTUNITY_STAGE_META[stage].color
}

/** 阶段是否为终态（赢单 / 输单，终态后不可再改阶段，40169） */
export function isOpportunityStageTerminal(stage: OpportunityStage): boolean {
  return stage === 4 || stage === 5
}

/** 分页查询商机（关键词 / 阶段 / 客户 / 负责人筛选） */
export function getOpportunities(query: OpportunityQuery): Promise<PagedResult<OpportunityListItem>> {
  return get<PagedResult<OpportunityListItem>>('/opportunities', { params: query })
}

/** 查询商机详情（含负责人姓名） */
export function getOpportunity(id: string): Promise<OpportunityDetail> {
  return get<OpportunityDetail>(`/opportunities/${id}`)
}

/** 新增商机（售前意向：不动库存、不写流水、不产生应收） */
export function createOpportunity(payload: SaveOpportunityPayload): Promise<OpportunityDetail> {
  return post<OpportunityDetail>('/opportunities', payload)
}

/** 编辑商机（全量覆盖语义；终态阶段不可改阶段，否则 40169） */
export function updateOpportunity(id: string, payload: SaveOpportunityPayload): Promise<OpportunityDetail> {
  return put<OpportunityDetail>(`/opportunities/${id}`, payload)
}

/** 商机阶段推进（终态赢单 / 输单后不可再改，否则 40169） */
export function updateOpportunityStage(id: string, stage: OpportunityStage): Promise<OpportunityDetail> {
  return put<OpportunityDetail>(`/opportunities/${id}/stage`, { stage })
}

/** 查询商机跟进活动（跟进时间倒序） */
export function getOpportunityActivities(id: string): Promise<ActivityItem[]> {
  return get<ActivityItem[]>(`/opportunities/${id}/activities`)
}

/** 新增商机跟进活动（只增不改不删） */
export function createOpportunityActivity(id: string, payload: SaveActivityPayload): Promise<ActivityItem> {
  return post<ActivityItem>(`/opportunities/${id}/activities`, payload)
}

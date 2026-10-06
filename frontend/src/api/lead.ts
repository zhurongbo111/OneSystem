import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 线索状态（0 新线索 / 1 跟进中 / 2 已转化 / 3 已废弃，specs/043-erp-crm-presale design.md §0.1） */
export type LeadStatus = 0 | 1 | 2 | 3

/** 线索来源（0 网站 / 1 电话 / 2 推荐 / 3 展会 / 4 其他） */
export type LeadSource = 0 | 1 | 2 | 3 | 4

/** 跟进活动归属业务类型（0 线索 / 1 商机） */
export type ActivityBizType = 0 | 1

/** 跟进活动方式（0 电话 / 1 拜访 / 2 邮件 / 3 其他） */
export type ActivityType = 0 | 1 | 2 | 3

/** 线索列表行（对应后端 LeadListItemDto） */
export interface LeadListItem {
  id: string
  leadNo: string
  name: string
  contact: string | null
  phone: string | null
  source: LeadSource
  status: LeadStatus
  ownerId: string | null
  /** 负责人姓名（未指派为空） */
  ownerName: string | null
  /** 转出的商机号快照（已转化时有值） */
  opportunityNo: string | null
  createdAt: string
}

/** 线索详情（对应后端 LeadDetailDto） */
export interface LeadDetail {
  id: string
  leadNo: string
  name: string
  contact: string | null
  phone: string | null
  source: LeadSource
  status: LeadStatus
  ownerId: string | null
  ownerName: string | null
  opportunityId: string | null
  opportunityNo: string | null
  remark: string | null
  createdBy: string | null
  createdAt: string
  updatedAt: string
}

/** 线索列表查询参数（对应后端 GetLeadsRequest） */
export interface LeadQuery {
  page: number
  pageSize: number
  /** 关键词：线索号 / 名称 / 联系人 / 电话 */
  keyword?: string
  source?: LeadSource
  status?: LeadStatus
  ownerId?: string
}

/** 新增 / 编辑线索入参（对应后端 Create / UpdateLeadRequest，全量覆盖语义） */
export interface SaveLeadPayload {
  name: string
  contact?: string
  phone?: string
  source: LeadSource
  status: LeadStatus
  ownerId?: string
  remark?: string
}

/** 线索转商机结果（对应后端 ConvertLeadResultDto） */
export interface ConvertLeadResult {
  opportunityId: string
  opportunityNo: string
}

/**
 * 跟进活动（对应后端 ActivityItemDto；线索与商机共用同一资源，故类型与文案集中在本文件，
 * `api/opportunity.ts` 复用，见 specs/043-erp-crm-presale design.md §4.1）。
 */
export interface ActivityItem {
  id: string
  bizType: ActivityBizType
  bizId: string
  type: ActivityType
  content: string
  activityTime: string
  /** 记录人显示名 */
  recorderName: string | null
  createdAt: string
}

/** 新增跟进活动入参（线索 / 商机结构一致，仅路径不同） */
export interface SaveActivityPayload {
  type: ActivityType
  content: string
  activityTime: string
}

/** 线索状态文案与标签色（design.md §0.1，前端唯一来源） */
export const LEAD_STATUS_META: Record<LeadStatus, { label: string; color: string }> = {
  0: { label: '新线索', color: 'blue' },
  1: { label: '跟进中', color: 'orange' },
  2: { label: '已转化', color: 'green' },
  3: { label: '已废弃', color: 'gray' },
}

/** 线索来源文案（design.md §0.1） */
export const LEAD_SOURCE_LABELS: Record<LeadSource, string> = {
  0: '网站',
  1: '电话',
  2: '推荐',
  3: '展会',
  4: '其他',
}

/** 跟进活动方式文案（design.md §0.1） */
export const ACTIVITY_TYPE_LABELS: Record<ActivityType, string> = {
  0: '电话',
  1: '拜访',
  2: '邮件',
  3: '其他',
}

/** 线索来源下拉选项 */
export const LEAD_SOURCE_OPTIONS: { label: string; value: LeadSource }[] = ([0, 1, 2, 3, 4] as LeadSource[]).map(
  (value) => ({ label: LEAD_SOURCE_LABELS[value], value }),
)

/** 线索状态下拉选项（新建只允许前两项，见 CreateLeadRequestValidator） */
export const LEAD_STATUS_OPTIONS: { label: string; value: LeadStatus }[] = ([0, 1, 2, 3] as LeadStatus[]).map(
  (value) => ({ label: LEAD_STATUS_META[value].label, value }),
)

/** 跟进活动方式下拉选项 */
export const ACTIVITY_TYPE_OPTIONS: { label: string; value: ActivityType }[] = (
  [0, 1, 2, 3] as ActivityType[]
).map((value) => ({ label: ACTIVITY_TYPE_LABELS[value], value }))

/** 线索状态文案 */
export function leadStatusLabel(status: LeadStatus): string {
  return LEAD_STATUS_META[status].label
}

/** 线索状态标签色 */
export function leadStatusColor(status: LeadStatus): string {
  return LEAD_STATUS_META[status].color
}

/** 分页查询线索（关键词 / 来源 / 状态 / 负责人筛选） */
export function getLeads(query: LeadQuery): Promise<PagedResult<LeadListItem>> {
  return get<PagedResult<LeadListItem>>('/leads', { params: query })
}

/** 查询线索详情（含负责人姓名与转出商机信息） */
export function getLead(id: string): Promise<LeadDetail> {
  return get<LeadDetail>(`/leads/${id}`)
}

/** 新增线索（售前意向：不动库存、不写流水、不产生应收） */
export function createLead(payload: SaveLeadPayload): Promise<LeadDetail> {
  return post<LeadDetail>('/leads', payload)
}

/** 编辑线索（全量覆盖语义；终态不可改状态，否则 40168） */
export function updateLead(id: string, payload: SaveLeadPayload): Promise<LeadDetail> {
  return put<LeadDetail>(`/leads/${id}`, payload)
}

/** 线索状态流转（终态不可再改；「已转化」只能由转商机产生，否则 40168） */
export function updateLeadStatus(id: string, status: LeadStatus): Promise<LeadDetail> {
  return put<LeadDetail>(`/leads/${id}/status`, { status })
}

/** 线索转商机（一次性整转；终态线索不可转，否则 40168） */
export function convertLead(id: string): Promise<ConvertLeadResult> {
  return post<ConvertLeadResult>(`/leads/${id}/convert`)
}

/** 查询线索跟进活动（跟进时间倒序） */
export function getLeadActivities(id: string): Promise<ActivityItem[]> {
  return get<ActivityItem[]>(`/leads/${id}/activities`)
}

/** 新增线索跟进活动（只增不改不删） */
export function createLeadActivity(id: string, payload: SaveActivityPayload): Promise<ActivityItem> {
  return post<ActivityItem>(`/leads/${id}/activities`, payload)
}

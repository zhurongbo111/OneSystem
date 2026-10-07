import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 工单状态（0 待处理 / 1 处理中 / 2 已解决 / 3 已关闭，specs/045-erp-crm-service design.md §0.1） */
export type TicketStatus = 0 | 1 | 2 | 3

/** 工单优先级（0 低 / 1 中 / 2 高） */
export type TicketPriority = 0 | 1 | 2

/** 工单列表行（对应后端 ServiceTicketListItemDto） */
export interface ServiceTicketListItem {
  id: string
  ticketNo: string
  partnerId: string
  /** 客户名称快照 */
  partnerName: string
  title: string
  priority: TicketPriority
  status: TicketStatus
  ownerId: string | null
  /** 负责人姓名（未指派为空） */
  ownerName: string | null
  /** 解决时间（已解决 / 已关闭后有值） */
  resolvedAt: string | null
  createdAt: string
}

/** 工单详情（对应后端 ServiceTicketDetailDto） */
export interface ServiceTicketDetail {
  id: string
  ticketNo: string
  partnerId: string
  partnerName: string
  contact: string | null
  phone: string | null
  title: string
  description: string | null
  priority: TicketPriority
  status: TicketStatus
  ownerId: string | null
  ownerName: string | null
  resolvedAt: string | null
  remark: string | null
  createdBy: string | null
  createdAt: string
  updatedAt: string
}

/** 工单列表查询参数（对应后端 GetServiceTicketsRequest） */
export interface ServiceTicketQuery {
  page: number
  pageSize: number
  /** 关键词：工单号 / 客户名 / 标题 */
  keyword?: string
  status?: TicketStatus
  priority?: TicketPriority
  ownerId?: string
}

/** 登记 / 编辑工单入参（对应后端 Create / UpdateServiceTicketRequest，全量覆盖语义；状态与单号不可改） */
export interface SaveServiceTicketPayload {
  partnerId: string
  contact?: string
  phone?: string
  title: string
  description?: string
  priority: TicketPriority
  ownerId?: string
  remark?: string
}

/** 工单状态文案与标签色（design.md §0.1，前端唯一来源） */
export const SERVICE_TICKET_STATUS_META: Record<TicketStatus, { label: string; color: string }> = {
  0: { label: '待处理', color: 'orange' },
  1: { label: '处理中', color: 'blue' },
  2: { label: '已解决', color: 'green' },
  3: { label: '已关闭', color: 'gray' },
}

/** 工单优先级文案与标签色（design.md §0.1） */
export const SERVICE_TICKET_PRIORITY_META: Record<TicketPriority, { label: string; color: string }> = {
  0: { label: '低', color: 'gray' },
  1: { label: '中', color: 'blue' },
  2: { label: '高', color: 'red' },
}

/** 允许的状态流转白名单（design.md §0.1：其余组合后端返回 40172，前端只展示可用动作） */
export const SERVICE_TICKET_TRANSITIONS: Record<TicketStatus, TicketStatus[]> = {
  0: [1, 2, 3],
  1: [2, 3],
  2: [3, 1],
  3: [],
}

/** 状态流转动作文案（按「当前 → 目标」给出按钮名，design.md §0.1） */
export const SERVICE_TICKET_TRANSITION_LABELS: Record<string, string> = {
  '0-1': '受理',
  '0-2': '解决',
  '0-3': '关闭',
  '1-2': '解决',
  '1-3': '关闭',
  '2-1': '重开',
  '2-3': '关闭',
}

/** 工单状态下拉选项 */
export const SERVICE_TICKET_STATUS_OPTIONS: { label: string; value: TicketStatus }[] = (
  [0, 1, 2, 3] as TicketStatus[]
).map((value) => ({ label: SERVICE_TICKET_STATUS_META[value].label, value }))

/** 工单优先级下拉选项 */
export const SERVICE_TICKET_PRIORITY_OPTIONS: { label: string; value: TicketPriority }[] = (
  [0, 1, 2] as TicketPriority[]
).map((value) => ({ label: SERVICE_TICKET_PRIORITY_META[value].label, value }))

/** 工单状态文案 */
export function ticketStatusLabel(status: TicketStatus): string {
  return SERVICE_TICKET_STATUS_META[status].label
}

/** 工单状态标签色 */
export function ticketStatusColor(status: TicketStatus): string {
  return SERVICE_TICKET_STATUS_META[status].color
}

/** 工单优先级文案 */
export function ticketPriorityLabel(priority: TicketPriority): string {
  return SERVICE_TICKET_PRIORITY_META[priority].label
}

/** 工单优先级标签色 */
export function ticketPriorityColor(priority: TicketPriority): string {
  return SERVICE_TICKET_PRIORITY_META[priority].color
}

/** 取某状态下允许的流转目标（已关闭返回空数组 = 终态） */
export function allowedTicketTransitions(status: TicketStatus): TicketStatus[] {
  return SERVICE_TICKET_TRANSITIONS[status]
}

/** 状态流转动作文案（无对应组合时回退为「变更状态」） */
export function ticketTransitionLabel(from: TicketStatus, to: TicketStatus): string {
  return SERVICE_TICKET_TRANSITION_LABELS[`${from}-${to}`] ?? '变更状态'
}

/** 分页查询服务工单（关键词 / 状态 / 优先级 / 负责人筛选） */
export function getServiceTickets(query: ServiceTicketQuery): Promise<PagedResult<ServiceTicketListItem>> {
  return get<PagedResult<ServiceTicketListItem>>('/service-tickets', { params: query })
}

/** 查询服务工单详情（含负责人姓名） */
export function getServiceTicket(id: string): Promise<ServiceTicketDetail> {
  return get<ServiceTicketDetail>(`/service-tickets/${id}`)
}

/** 登记服务工单（售后留痕：不动库存、不写流水、不产生应收） */
export function createServiceTicket(payload: SaveServiceTicketPayload): Promise<ServiceTicketDetail> {
  return post<ServiceTicketDetail>('/service-tickets', payload)
}

/** 编辑服务工单（全量覆盖语义；已关闭不可编辑，否则 40172） */
export function updateServiceTicket(id: string, payload: SaveServiceTicketPayload): Promise<ServiceTicketDetail> {
  return put<ServiceTicketDetail>(`/service-tickets/${id}`, payload)
}

/** 工单状态流转（白名单外 / 已关闭终态 → 40172） */
export function updateServiceTicketStatus(id: string, status: TicketStatus): Promise<ServiceTicketDetail> {
  return put<ServiceTicketDetail>(`/service-tickets/${id}/status`, { status })
}

/** 指派工单负责人（已关闭不可指派 → 40172；负责人不存在 → 40400） */
export function assignServiceTicket(id: string, ownerId: string): Promise<ServiceTicketDetail> {
  return put<ServiceTicketDetail>(`/service-tickets/${id}/assign`, { ownerId })
}

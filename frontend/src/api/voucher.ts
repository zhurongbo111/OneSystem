import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 会计期间状态（0 未结账 / 1 已结账） */
export type PeriodStatus = 0 | 1

/** 凭证状态（0 已作废 / 1 已过账） */
export type VoucherStatus = 0 | 1

/** 凭证来源类型（对应后端 VoucherSourceType） */
export type VoucherSourceType = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7

/** 会计期间（对应后端 PeriodDto） */
export interface Period {
  id: string
  year: number
  month: number
  status: PeriodStatus
  closedAt: string | null
  closedBy: string | null
}

/** 凭证列表行（对应后端 VoucherListItemDto） */
export interface VoucherListItem {
  id: string
  voucherNo: string
  voucherDate: string
  summary: string
  sourceType: VoucherSourceType
  sourceId: string | null
  sourceNo: string | null
  totalDebit: number
  totalCredit: number
  status: VoucherStatus
  createdAt: string
}

/** 凭证分录（对应后端 VoucherEntryDto） */
export interface VoucherEntry {
  id: string
  lineNo: number
  accountId: string
  accountCode: string
  accountName: string
  summary: string | null
  debit: number
  credit: number
}

/** 凭证详情（对应后端 VoucherDetailDto） */
export interface VoucherDetail {
  id: string
  voucherNo: string
  voucherDate: string
  summary: string
  sourceType: VoucherSourceType
  sourceId: string | null
  sourceNo: string | null
  totalDebit: number
  totalCredit: number
  status: VoucherStatus
  createdAt: string
  updatedAt: string
  items: VoucherEntry[]
}

/** 凭证分页查询参数（对应后端 GetVouchersRequest） */
export interface VoucherQuery {
  page: number
  pageSize: number
  year?: number
  month?: number
  sourceType?: VoucherSourceType
  keyword?: string
}

/** 手工凭证分录入参（对应后端 CreateVoucherItem） */
export interface CreateVoucherItemPayload {
  accountId: string
  summary?: string
  debit: number
  credit: number
}

/** 录入手工凭证入参（对应后端 CreateVoucherRequest；平衡校验在后端） */
export interface CreateVoucherPayload {
  voucherDate: string
  summary: string
  items: CreateVoucherItemPayload[]
}

/** 科目映射（对应后端 AccountMappingDto；未配置时 accountId 为空串） */
export interface AccountMapping {
  key: string
  label: string
  accountId: string
  accountCode: string
  accountName: string
}

/** 科目映射维护项（对应后端 UpdateAccountMappingItem） */
export interface UpdateAccountMappingPayload {
  key: string
  accountId: string
}

/** 期间状态标签元数据 */
export const PERIOD_STATUS_META: Record<PeriodStatus, { label: string; color: string }> = {
  0: { label: '未结账', color: 'green' },
  1: { label: '已结账', color: 'gray' },
}

/** 凭证状态标签元数据 */
export const VOUCHER_STATUS_META: Record<VoucherStatus, { label: string; color: string }> = {
  1: { label: '已过账', color: 'green' },
  0: { label: '已作废', color: 'gray' },
}

/** 凭证来源类型标签元数据（列表 / 详情共用） */
export const VOUCHER_SOURCE_TYPE_META: Record<VoucherSourceType, { label: string; color: string }> = {
  0: { label: '手工凭证', color: 'arcoblue' },
  1: { label: '采购入库单', color: 'cyan' },
  2: { label: '销售出库单', color: 'green' },
  3: { label: '采购退货单', color: 'orange' },
  4: { label: '销售退货单', color: 'gold' },
  5: { label: '收款单', color: 'lime' },
  6: { label: '付款单', color: 'purple' },
  7: { label: '成本结转', color: 'gray' },
}

/** 凭证来源类型下拉（筛选用；含「全部」由页面自行补空白项） */
export const VOUCHER_SOURCE_TYPE_OPTIONS: { label: string; value: VoucherSourceType }[] = [
  { label: '手工凭证', value: 0 },
  { label: '采购入库单', value: 1 },
  { label: '销售出库单', value: 2 },
  { label: '采购退货单', value: 3 },
  { label: '销售退货单', value: 4 },
  { label: '收款单', value: 5 },
  { label: '付款单', value: 6 },
  { label: '成本结转', value: 7 },
]

/** 把本地日期（YYYY-MM-DD）转换为后端所需的「该日历日 UTC 午夜」ISO 串 */
export function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

/** 查询会计期间列表（可按年过滤） */
export function getPeriods(year?: number): Promise<Period[]> {
  return get<Period[]>('/accounting-periods', { params: { year } })
}

/** 会计期间结账（已结账幂等返回） */
export function closePeriod(id: string): Promise<Period> {
  return put<Period>(`/accounting-periods/${id}/close`)
}

/** 会计期间反结账（未结账幂等返回） */
export function reversePeriod(id: string): Promise<Period> {
  return put<Period>(`/accounting-periods/${id}/reverse`)
}

/** 分页查询凭证（含作废凭证，作废行前端置灰） */
export function getVouchers(query: VoucherQuery): Promise<PagedResult<VoucherListItem>> {
  return get<PagedResult<VoucherListItem>>('/vouchers', { params: query })
}

/** 查询凭证详情（含分录） */
export function getVoucher(id: string): Promise<VoucherDetail> {
  return get<VoucherDetail>(`/vouchers/${id}`)
}

/** 录入手工凭证（借贷必须平衡） */
export function createVoucher(payload: CreateVoucherPayload): Promise<VoucherDetail> {
  return post<VoucherDetail>('/vouchers', payload)
}

/** 作废凭证（仅改状态不删数据，余额随之回退） */
export function voidVoucher(id: string): Promise<VoucherDetail> {
  return put<VoucherDetail>(`/vouchers/${id}/void`)
}

/** 查询科目映射（返回全部映射键，未配置的键科目字段为空串） */
export function getAccountMappings(): Promise<AccountMapping[]> {
  return get<AccountMapping[]>('/account-mappings')
}

/** 维护科目映射（全量覆盖，须提交全部映射键） */
export function updateAccountMappings(items: UpdateAccountMappingPayload[]): Promise<AccountMapping[]> {
  return put<AccountMapping[]>('/account-mappings', { items })
}

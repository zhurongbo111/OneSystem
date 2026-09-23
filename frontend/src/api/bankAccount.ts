import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/** 资金账户类型（1 现金 / 2 银行） */
export type BankAccountType = 1 | 2

/** 资金账户状态（0 停用 / 1 启用） */
export type BankAccountStatus = 0 | 1

/** 资金账户详情（对应后端 BankAccountDetailDto） */
export interface BankAccount {
  id: string
  code: string
  name: string
  type: BankAccountType
  bankName: string | null
  accountNo: string | null
  initialBalance: number
  status: BankAccountStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 资金账户列表行（对应后端 BankAccountListItemDto；balance 为派生列） */
export interface BankAccountListItem extends BankAccount {
  /** 当前余额 = 初始余额 + Σ 收款 − Σ 付款（由收付款单聚合，不落列） */
  balance: number
}

/** 资金账户余额总览行（对应后端 BankAccountBalanceItemDto） */
export interface BankAccountBalanceItem {
  id: string
  code: string
  name: string
  type: BankAccountType
  status: BankAccountStatus
  balance: number
}

/** 资金日记账流水行（对应后端 CashJournalEntryDto） */
export interface CashJournalEntry {
  date: string
  settlementNo: string
  summary: string
  /** 收款金额（付款行为 0） */
  debit: number
  /** 付款金额（收款行为 0） */
  credit: number
  /** 本笔之后的账户结余 */
  balance: number
}

/** 资金日记账（对应后端 CashJournalDto） */
export interface CashJournal {
  bankAccountId: string
  openingBalance: number
  closingBalance: number
  entries: CashJournalEntry[]
}

export interface GetBankAccountsParams {
  page?: number
  pageSize?: number
  keyword?: string
  type?: BankAccountType
  status?: BankAccountStatus
}

export interface CreateBankAccountPayload {
  code: string
  name: string
  type: BankAccountType
  bankName?: string
  accountNo?: string
  initialBalance: number
  status: BankAccountStatus
  remark?: string
}

export interface UpdateBankAccountPayload {
  code: string
  name: string
  type: BankAccountType
  bankName?: string
  accountNo?: string
  initialBalance: number
  status: BankAccountStatus
  remark?: string
}

export interface GetCashJournalParams {
  bankAccountId: string
  /** 起始业务日期（含），UTC ISO 串 */
  start: string
  /** 结束业务日期（含），UTC ISO 串 */
  end: string
}

/** 分页查询资金账户（含派生余额列） */
export function getBankAccounts(params: GetBankAccountsParams): Promise<PagedResult<BankAccountListItem>> {
  return get<PagedResult<BankAccountListItem>>('/bank-accounts', { params })
}

/** 查询资金账户详情 */
export function getBankAccount(id: string): Promise<BankAccount> {
  return get<BankAccount>(`/bank-accounts/${id}`)
}

/** 新增资金账户 */
export function createBankAccount(payload: CreateBankAccountPayload): Promise<BankAccount> {
  return post<BankAccount>('/bank-accounts', payload)
}

/** 编辑资金账户（全量覆盖） */
export function updateBankAccount(id: string, payload: UpdateBankAccountPayload): Promise<BankAccount> {
  return put<BankAccount>(`/bank-accounts/${id}`, payload)
}

/** 删除资金账户（被收付款单引用时返回 40161） */
export function deleteBankAccount(id: string): Promise<null> {
  return del<null>(`/bank-accounts/${id}`)
}

/** 启用 / 停用资金账户 */
export function updateBankAccountStatus(id: string, status: BankAccountStatus): Promise<BankAccount> {
  return put<BankAccount>(`/bank-accounts/${id}/status`, { status })
}

/** 查询各资金账户当前余额（余额总览） */
export function getBankAccountSummary(): Promise<BankAccountBalanceItem[]> {
  return get<BankAccountBalanceItem[]>('/bank-accounts/summary')
}

/** 查询资金日记账（期初 / 逐笔收付与结余 / 期末） */
export function getCashJournal(params: GetCashJournalParams): Promise<CashJournal> {
  return get<CashJournal>('/cash-journals', { params })
}

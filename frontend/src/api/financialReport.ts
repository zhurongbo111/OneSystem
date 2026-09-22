import { get } from './request'

/** 科目余额表行（对应后端 AccountBalanceItemDto，按一级科目列示） */
export interface AccountBalanceItem {
  accountId: string
  code: string
  name: string
  /** 科目类别（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益） */
  category: number
  /** 余额方向（1 借 / 2 贷） */
  direction: number
  openingBalance: number
  periodDebit: number
  periodCredit: number
  closingBalance: number
}

/** 资产负债表行（对应后端 BalanceSheetItemDto） */
export interface BalanceSheetItem {
  accountId: string
  code: string
  name: string
  amount: number
}

/** 资产负债表（对应后端 BalanceSheetDto） */
export interface BalanceSheet {
  year: number
  month: number
  assets: BalanceSheetItem[]
  liabilities: BalanceSheetItem[]
  equities: BalanceSheetItem[]
  profitLossItems: BalanceSheetItem[]
  totalAssets: number
  totalLiabilities: number
  totalEquities: number
  currentProfit: number
  totalLiabilitiesAndEquity: number
}

/** 利润表行（对应后端 IncomeStatementItemDto） */
export interface IncomeStatementItem {
  accountId: string
  code: string
  name: string
  amount: number
}

/** 利润表（对应后端 IncomeStatementDto） */
export interface IncomeStatement {
  year: number
  month: number
  revenueItems: IncomeStatementItem[]
  costItems: IncomeStatementItem[]
  totalRevenue: number
  totalCost: number
  netProfit: number
}

/** 查询科目余额表（按期间） */
export function getAccountBalance(year: number, month: number): Promise<AccountBalanceItem[]> {
  return get<AccountBalanceItem[]>('/reports/account-balance', { params: { year, month } })
}

/** 查询资产负债表（按期间） */
export function getBalanceSheet(year: number, month: number): Promise<BalanceSheet> {
  return get<BalanceSheet>('/reports/balance-sheet', { params: { year, month } })
}

/** 查询利润表（按期间） */
export function getIncomeStatement(year: number, month: number): Promise<IncomeStatement> {
  return get<IncomeStatement>('/reports/income-statement', { params: { year, month } })
}

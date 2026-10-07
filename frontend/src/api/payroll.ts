import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/** 工资单状态（0 草稿 / 1 已发放） */
export type PayrollStatus = 0 | 1

/** 工资单列表行（对应后端 PayrollListItemDto） */
export interface Payroll {
  id: string
  employeeId: string
  employeeName: string
  year: number
  month: number
  baseSalary: number
  allowance: number
  deduction: number
  /** 实发（后端计算：基本工资 + 津贴 − 扣款） */
  netPay: number
  status: PayrollStatus
  statusText: string
  remark: string | null
  createdAt: string
}

/** 工资单详情（对应后端 PayrollDetailDto） */
export interface PayrollDetail extends Payroll {
  updatedAt: string
}

export interface GetPayrollsParams {
  page?: number
  pageSize?: number
  year?: number
  month?: number
  employeeId?: string
  status?: PayrollStatus
}

/** 新增载荷（实发由后端重算） */
export interface CreatePayrollPayload {
  employeeId: string
  year: number
  month: number
  baseSalary: number
  allowance: number
  deduction: number
  remark?: string
}

/** 编辑载荷：员工与期间**不可改**（不在请求体内） */
export interface UpdatePayrollPayload {
  baseSalary: number
  allowance: number
  deduction: number
  remark?: string
}

/** 批量生成结果（对应后端 GeneratePayrollsResponse） */
export interface GeneratePayrollsResult {
  created: number
  skipped: number
}

/** 分页查询工资单 */
export function getPayrolls(params: GetPayrollsParams): Promise<PagedResult<Payroll>> {
  return get<PagedResult<Payroll>>('/payrolls', { params })
}

/** 新增工资单（一个员工一个月一条，重复返回 40170） */
export function createPayroll(payload: CreatePayrollPayload): Promise<PayrollDetail> {
  return post<PayrollDetail>('/payrolls', payload)
}

/** 编辑工资单（仅草稿；已发放返回 40171） */
export function updatePayroll(id: string, payload: UpdatePayrollPayload): Promise<PayrollDetail> {
  return put<PayrollDetail>(`/payrolls/${id}`, payload)
}

/** 发放 / 反发放工资单 */
export function updatePayrollStatus(id: string, status: PayrollStatus): Promise<PayrollDetail> {
  return put<PayrollDetail>(`/payrolls/${id}/status`, { status })
}

/** 删除工资单（仅草稿；已发放返回 40171） */
export function deletePayroll(id: string): Promise<null> {
  return del<null>(`/payrolls/${id}`)
}

/** 批量生成指定期间的工资单草稿（为在职员工生成，已存在则跳过） */
export function generatePayrolls(year: number, month: number): Promise<GeneratePayrollsResult> {
  return post<GeneratePayrollsResult>('/payrolls/generate', undefined, { params: { year, month } })
}

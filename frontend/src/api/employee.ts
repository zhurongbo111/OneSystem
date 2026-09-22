import type { PagedResult } from './product'
import { get, post, put } from './request'

/** 在职状态（0 离职 / 1 在职） */
export type EmployeeStatus = 0 | 1

/** 性别（0 未填 / 1 男 / 2 女） */
export type Gender = 0 | 1 | 2

/** 员工列表行（对应后端 EmployeeListItemDto） */
export interface Employee {
  id: string
  employeeNo: string
  name: string
  gender: Gender | null
  phone: string | null
  departmentId: string | null
  departmentName: string | null
  positionId: string | null
  positionName: string | null
  hireDate: string
  resignDate: string | null
  status: EmployeeStatus
  statusText: string
  userId: string | null
  userDisplayName: string | null
  createdAt: string
}

/** 员工详情（对应后端 EmployeeDetailDto） */
export interface EmployeeDetail extends Employee {
  email: string | null
  remark: string | null
  updatedAt: string
}

/** 员工可选账号（对应后端 EmployeePickUserDto） */
export interface EmployeePickUser {
  id: string
  username: string
  displayName: string
}

export interface GetEmployeesParams {
  page?: number
  pageSize?: number
  keyword?: string
  departmentId?: string
  positionId?: string
  status?: EmployeeStatus
}

export interface CreateEmployeePayload {
  employeeNo: string
  name: string
  gender?: Gender
  phone?: string
  email?: string
  departmentId?: string
  positionId?: string
  hireDate: string
  resignDate?: string
  status: EmployeeStatus
  userId?: string
  remark?: string
}

/** 编辑载荷：不含工号（创建后不可改，AGENTS.md §4.5） */
export interface UpdateEmployeePayload {
  name: string
  gender?: Gender
  phone?: string
  email?: string
  departmentId?: string
  positionId?: string
  hireDate: string
  resignDate?: string
  status: EmployeeStatus
  userId?: string
  remark?: string
}

/** 分页查询员工 */
export function getEmployees(params: GetEmployeesParams): Promise<PagedResult<Employee>> {
  return get<PagedResult<Employee>>('/employees', { params })
}

/** 查询员工详情 */
export function getEmployee(id: string): Promise<EmployeeDetail> {
  return get<EmployeeDetail>(`/employees/${id}`)
}

/** 新增员工 */
export function createEmployee(payload: CreateEmployeePayload): Promise<EmployeeDetail> {
  return post<EmployeeDetail>('/employees', payload)
}

/** 编辑员工（工号不可改） */
export function updateEmployee(id: string, payload: UpdateEmployeePayload): Promise<EmployeeDetail> {
  return put<EmployeeDetail>(`/employees/${id}`, payload)
}

/** 在职 / 离职切换 */
export function updateEmployeeStatus(id: string, status: EmployeeStatus): Promise<EmployeeDetail> {
  return put<EmployeeDetail>(`/employees/${id}/status`, { status })
}

/** 可选账号来源：启用且未被绑定的账号 ∪ 当前员工已绑定的账号 */
export function getAvailableUsers(employeeId?: string): Promise<EmployeePickUser[]> {
  return get<EmployeePickUser[]>('/employees/available-users', {
    params: employeeId ? { employeeId } : undefined,
  })
}

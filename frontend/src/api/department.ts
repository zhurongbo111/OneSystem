import { del, get, post, put } from './request'

/** 部门状态（0 停用 / 1 启用） */
export type DepartmentStatus = 0 | 1

/** 部门树节点（对应后端 DepartmentTreeNodeDto；children 已按同级排序） */
export interface DepartmentTreeNode {
  id: string
  code: string
  name: string
  parentId: string | null
  sortOrder: number
  status: DepartmentStatus
  remark: string | null
  /** 该部门在职员工数 */
  employeeCount: number
  children: DepartmentTreeNode[]
}

/** 部门详情（对应后端 DepartmentDetailDto） */
export interface DepartmentDetail {
  id: string
  code: string
  name: string
  parentId: string | null
  sortOrder: number
  status: DepartmentStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateDepartmentPayload {
  code: string
  name: string
  parentId?: string
  sortOrder: number
  status: DepartmentStatus
  remark?: string
}

export interface UpdateDepartmentPayload {
  code: string
  name: string
  parentId?: string
  sortOrder: number
  status: DepartmentStatus
  remark?: string
}

/** 查询部门树（全量，含各节点在职人数） */
export function getDepartments(): Promise<DepartmentTreeNode[]> {
  return get<DepartmentTreeNode[]>('/departments')
}

/** 查询部门详情 */
export function getDepartment(id: string): Promise<DepartmentDetail> {
  return get<DepartmentDetail>(`/departments/${id}`)
}

/** 新增部门 */
export function createDepartment(payload: CreateDepartmentPayload): Promise<DepartmentDetail> {
  return post<DepartmentDetail>('/departments', payload)
}

/** 编辑部门（编码 / 名称 / 上级 / 排序 / 状态 / 备注全量覆盖；上级不得为自身或后代） */
export function updateDepartment(id: string, payload: UpdateDepartmentPayload): Promise<DepartmentDetail> {
  return put<DepartmentDetail>(`/departments/${id}`, payload)
}

/** 删除部门（存在子部门或员工引用时后端拒绝） */
export function deleteDepartment(id: string): Promise<null> {
  return del<null>(`/departments/${id}`)
}

/** 停用 / 启用部门 */
export function updateDepartmentStatus(id: string, status: DepartmentStatus): Promise<DepartmentDetail> {
  return put<DepartmentDetail>(`/departments/${id}/status`, { status })
}

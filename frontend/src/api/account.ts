import { del, get, post, put } from './request'

/** 科目类别（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益） */
export type AccountCategory = 1 | 2 | 3 | 4 | 5

/** 余额方向（1 借 / 2 贷） */
export type AccountDirection = 1 | 2

/** 科目状态（0 停用 / 1 启用） */
export type AccountStatus = 0 | 1

/** 科目树节点（对应后端 AccountTreeNodeDto；children 已按同级排序，空集合表示末级科目） */
export interface AccountTreeNode {
  id: string
  code: string
  name: string
  category: AccountCategory
  direction: AccountDirection
  parentId: string | null
  sortOrder: number
  /** 预置科目（不可删除） */
  isPreset: boolean
  status: AccountStatus
  remark: string | null
  children: AccountTreeNode[]
}

/** 科目详情（对应后端 AccountDetailDto） */
export interface AccountDetail {
  id: string
  code: string
  name: string
  category: AccountCategory
  direction: AccountDirection
  parentId: string | null
  sortOrder: number
  /** 预置科目（不可删除） */
  isPreset: boolean
  status: AccountStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateAccountPayload {
  code: string
  name: string
  category: AccountCategory
  direction: AccountDirection
  parentId?: string
  sortOrder: number
  status: AccountStatus
  remark?: string
}

export interface UpdateAccountPayload {
  code: string
  name: string
  category: AccountCategory
  direction: AccountDirection
  parentId?: string
  sortOrder: number
  status: AccountStatus
  remark?: string
}

/** 查询会计科目树（全量，末级科目 = children 为空） */
export function getAccounts(): Promise<AccountTreeNode[]> {
  return get<AccountTreeNode[]>('/accounts')
}

/** 查询会计科目详情 */
export function getAccount(id: string): Promise<AccountDetail> {
  return get<AccountDetail>(`/accounts/${id}`)
}

/** 新增会计科目 */
export function createAccount(payload: CreateAccountPayload): Promise<AccountDetail> {
  return post<AccountDetail>('/accounts', payload)
}

/** 编辑会计科目（编码 / 名称 / 类别 / 方向 / 上级 / 排序 / 状态 / 备注全量覆盖；上级不得为自身或下级） */
export function updateAccount(id: string, payload: UpdateAccountPayload): Promise<AccountDetail> {
  return put<AccountDetail>(`/accounts/${id}`, payload)
}

/** 删除会计科目（预置科目、有子科目、已被凭证引用时后端拒绝） */
export function deleteAccount(id: string): Promise<null> {
  return del<null>(`/accounts/${id}`)
}

/** 停用 / 启用会计科目 */
export function updateAccountStatus(id: string, status: AccountStatus): Promise<AccountDetail> {
  return put<AccountDetail>(`/accounts/${id}/status`, { status })
}
import { get, post, put } from './request'

/** 用户状态：1 启用 / 0 禁用 */
export type UserStatus = 0 | 1

/** 分页结果（对应后端 PagedResult<T>，AGENTS.md §4.3） */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/** 用户所属角色（对应后端 UserRoleDto） */
export interface UserRole {
  id: string
  name: string
}

/** 用户列表行（对应后端 UserListItemDto） */
export interface UserListItem {
  id: string
  username: string
  displayName: string
  email: string | null
  phone: string | null
  status: UserStatus
  /** 所属角色（列表 / 详情共用，至少一个） */
  roles: UserRole[]
  lastLoginAt: string | null
  createdAt: string
}

/** 用户详情（对应后端 UserDetailDto） */
export interface UserDetail extends UserListItem {
  updatedAt: string
}

/** 用户列表查询参数（对应后端 GetUsersRequest） */
export interface UserListQuery {
  keyword?: string
  status?: UserStatus
  page: number
  pageSize: number
}

/** 新增用户入参（对应后端 CreateUserRequest） */
export interface CreateUserPayload {
  username: string
  displayName: string
  email?: string
  phone?: string
  password: string
  /** 角色 id 集合（全量替换，至少一个） */
  roleIds: string[]
}

/** 编辑用户入参（对应后端 UpdateUserRequest，用户名不可改） */
export interface UpdateUserPayload {
  displayName: string
  email?: string
  phone?: string
  /** 角色 id 集合（全量替换，至少一个） */
  roleIds: string[]
}

/** 分页查询用户列表 */
export function getUsers(query: UserListQuery): Promise<PagedResult<UserListItem>> {
  return get<PagedResult<UserListItem>>('/users', { params: query })
}

/** 查询用户详情 */
export function getUser(id: string): Promise<UserDetail> {
  return get<UserDetail>(`/users/${id}`)
}

/** 新增用户 */
export function createUser(payload: CreateUserPayload): Promise<UserDetail> {
  return post<UserDetail>('/users', payload)
}

/** 编辑用户（仅展示类字段） */
export function updateUser(id: string, payload: UpdateUserPayload): Promise<UserDetail> {
  return put<UserDetail>(`/users/${id}`, payload)
}

/** 启用 / 禁用用户 */
export function updateUserStatus(id: string, status: UserStatus): Promise<UserDetail> {
  return put<UserDetail>(`/users/${id}/status`, { status })
}

/** 重置用户密码（管理员无需原密码） */
export function resetUserPassword(id: string, newPassword: string): Promise<null> {
  return put<null>(`/users/${id}/password`, { newPassword })
}

import { del, get, post, put } from './request'
import type { PagedResult } from './user'

/** 角色列表行（对应后端 RoleListItemDto） */
export interface RoleListItem {
  id: string
  name: string
  remark: string | null
  /** 是否内置角色（内置 / 自定义） */
  isBuiltin: boolean
  permissionCount: number
  userCount: number
  createdAt: string
}

/** 角色详情（对应后端 RoleDetailDto） */
export interface RoleDetail {
  id: string
  name: string
  remark: string | null
  isBuiltin: boolean
  /** 已勾选权限点 key 集合 */
  permissionKeys: string[]
  userCount: number
  createdAt: string
  updatedAt: string
}

/** 角色列表查询参数（对应后端 GetRolesRequest） */
export interface RoleListQuery {
  keyword?: string
  page: number
  pageSize: number
}

/** 新增角色入参（对应后端 CreateRoleRequest） */
export interface CreateRolePayload {
  name: string
  remark?: string
  permissionKeys: string[]
}

/** 编辑角色入参（对应后端 UpdateRoleRequest，权限点全量替换） */
export interface UpdateRolePayload {
  name: string
  remark?: string
  permissionKeys: string[]
}

/** 权限点（对应后端 PermissionItemDto） */
export interface PermissionItem {
  key: string
  name: string
}

/** 权限点分组（对应后端 PermissionGroupDto，权限树数据源） */
export interface PermissionGroup {
  groupName: string
  items: PermissionItem[]
}

/** 分页查询角色列表 */
export function getRoles(query: RoleListQuery): Promise<PagedResult<RoleListItem>> {
  return get<PagedResult<RoleListItem>>('/roles', { params: query })
}

/** 查询角色详情 */
export function getRole(id: string): Promise<RoleDetail> {
  return get<RoleDetail>(`/roles/${id}`)
}

/** 新增角色 */
export function createRole(payload: CreateRolePayload): Promise<RoleDetail> {
  return post<RoleDetail>('/roles', payload)
}

/** 编辑角色 */
export function updateRole(id: string, payload: UpdateRolePayload): Promise<RoleDetail> {
  return put<RoleDetail>(`/roles/${id}`, payload)
}

/** 删除角色 */
export function deleteRole(id: string): Promise<null> {
  return del<null>(`/roles/${id}`)
}

/** 查询权限点分组清单（中文名称由后端返回，前端不硬编码） */
export function getPermissions(): Promise<PermissionGroup[]> {
  return get<PermissionGroup[]>('/permissions')
}

import { get, post } from './request'

/** 登录请求（对应后端 LoginRequest） */
export interface LoginRequest {
  username: string
  password: string
}

/** 用户信息（对应后端 UserDto） */
export interface UserDto {
  id: string
  username: string
  displayName: string
}

/** 登录结果（对应后端 LoginResponse） */
export interface LoginResult {
  token: string
  user: UserDto
  /** 当前用户权限点 key 集合（超级管理员由后端返回全量，前端不做特例） */
  permissions: string[]
}

/**
 * 登录，签发 JWT（baseURL 已含 /api 前缀，见 VITE_API_BASE_URL）
 */
export function login(request: LoginRequest): Promise<LoginResult> {
  return post<LoginResult>('/auth/login', request)
}

/**
 * 获取当前登录用户
 */
export function getCurrentUser(): Promise<UserDto> {
  return get<UserDto>('/users/me')
}

/**
 * 刷新当前用户权限点集合（角色变更后无需重新登录即可生效）
 */
export function getMyPermissions(): Promise<string[]> {
  return get<string[]>('/users/me/permissions')
}

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

/** 登录结果（对应后端 LoginResult） */
export interface LoginResult {
  token: string
  user: UserDto
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

import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { Message } from '@arco-design/web-vue'

/**
 * 统一响应结构（与后端 { code, message, data } 对应）
 */
export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}

/** 未登录或 token 无效 */
export const CODE_UNAUTHORIZED = 40100
/** 成功 */
export const CODE_SUCCESS = 0

/** localStorage 凭证 key */
const TOKEN_KEY = 'app:token'
/** 防止多个请求同时触发 40100 重复跳转 */
let redirecting = false

export const tokenStorage = {
  get: (): string | null => localStorage.getItem(TOKEN_KEY),
  set: (token: string): void => localStorage.setItem(TOKEN_KEY, token),
  clear: (): void => localStorage.removeItem(TOKEN_KEY),
}

/** 40100 处理：清除凭证并跳转登录页（防重复） */
function handleUnauthorized(): void {
  tokenStorage.clear()
  if (redirecting) return
  redirecting = true
  const { pathname } = window.location
  if (pathname !== '/login') {
    window.location.href = '/login'
  }
  // 跳转后复位（hash 路由下 pathname 不变，依赖 location.href 触发整页刷新）
  window.setTimeout(() => {
    redirecting = false
  }, 1000)
}

/** 创建 Axios 实例并挂载统一拦截器 */
function createHttpClient(): AxiosInstance {
  const instance = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL,
    timeout: 15000,
  })

  // 请求拦截器：附加 Bearer token
  instance.interceptors.request.use((config) => {
    const token = tokenStorage.get()
    if (token) {
      config.headers = config.headers ?? {}
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  })

  // 响应拦截器：解包 { code, message, data }
  instance.interceptors.response.use(
    (response) => {
      const body = response.data as ApiResponse<unknown>
      // 部分接口可能不返回统一结构（非 JSON），原样返回
      if (body && typeof body === 'object' && 'code' in body) {
        if (body.code === CODE_SUCCESS) {
          // 用 data 替换响应体，业务代码只处理数据本身
          response.data = body.data
          return response
        }
        if (body.code === CODE_UNAUTHORIZED) {
          handleUnauthorized()
          return Promise.reject(new Error(body.message))
        }
        Message.error(body.message)
        return Promise.reject(new Error(body.message))
      }
      return response
    },
    (error: AxiosError<ApiResponse<unknown>>) => {
      const body = error.response?.data
      if (body && typeof body === 'object' && 'code' in body) {
        if (body.code === CODE_UNAUTHORIZED) {
          handleUnauthorized()
          return Promise.reject(new Error(body.message))
        }
        Message.error(body.message)
        return Promise.reject(new Error(body.message))
      }
      Message.error('网络异常，请稍后重试')
      return Promise.reject(error)
    },
  )

  return instance
}

export const http = createHttpClient()

/** 请求 GET 并返回解包后的 data（拦截器已把响应体替换为 data） */
export function get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
  return http.get(url, config).then((r) => r.data as T)
}

/** 请求 POST 并返回解包后的 data（拦截器已把响应体替换为 data） */
export function post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
  return http.post(url, data, config).then((r) => r.data as T)
}

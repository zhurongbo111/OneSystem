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

/**
 * 40100 处置回调（由应用层注入）。
 *
 * 请求层不直接依赖 router / store：`stores/auth` 已依赖本模块，反向依赖会成环；
 * 且"跳哪里、怎么跳"属路由层职责，本层只负责清凭证。
 */
let unauthorizedHandler: (() => void) | null = null

/** 注册 40100 处置回调（应用启动时调用一次） */
export function setUnauthorizedHandler(handler: () => void): void {
  unauthorizedHandler = handler
}

/** 40100 处理：清除本地凭证，并交由处置回调跳转登录页（防重复触发） */
function handleUnauthorized(): void {
  tokenStorage.clear()
  if (redirecting) return
  redirecting = true
  unauthorizedHandler?.()
  // 处置回调为 SPA 内跳转，稍后复位标志位，避免并发 40100 重复跳转
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

/** 请求 PUT 并返回解包后的 data（拦截器已把响应体替换为 data） */
export function put<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
  return http.put(url, data, config).then((r) => r.data as T)
}

/** 请求 DELETE 并返回解包后的 data（拦截器已把响应体替换为 data） */
export function del<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
  return http.delete(url, config).then((r) => r.data as T)
}

/**
 * 文件下载（erp-export）：GET 二进制流并触发浏览器落盘。
 *
 * 导出接口是统一响应的**契约例外**（`specs/027-erp-export/design.md` §0.1）：
 * 成功返回 xlsx 二进制流，参数非法 / 服务端异常仍返回统一响应 JSON，
 * 故此处按 `Content-Type` 分流——含 `json` 走统一响应提示，否则按 Blob 下载。
 * 全项目仅此一处实现该分流。
 */
export async function downloadBlob(url: string, params?: object): Promise<void> {
  const response = await http.get<Blob>(url, { params, responseType: 'blob' })
  const contentType = String(response.headers['content-type'] ?? '')
  const blob = response.data

  // 失败回退：后端仍返回统一响应 JSON（HTTP 200），按统一响应解包并提示
  if (contentType.includes('json')) {
    const body = JSON.parse(await blob.text()) as ApiResponse<unknown>
    if (body.code === CODE_UNAUTHORIZED) {
      handleUnauthorized()
      throw new Error(body.message)
    }
    Message.error(body.message)
    throw new Error(body.message)
  }

  const fileName = parseDownloadFileName(response.headers['content-disposition'])
  saveBlob(blob, fileName ?? `导出_${timestampSuffix()}.xlsx`)
}

/** 从 Content-Disposition 解析文件名（优先 RFC 5987 的 filename*，回退 filename）；解析不到返回 null */
function parseDownloadFileName(contentDisposition: unknown): string | null {
  const value = typeof contentDisposition === 'string' ? contentDisposition : ''
  if (!value) return null

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(value)
  if (utf8Match?.[1]) {
    try {
      return decodeURIComponent(utf8Match[1])
    } catch {
      return utf8Match[1]
    }
  }

  const plainMatch = /filename="?([^";]+)"?/i.exec(value)
  return plainMatch?.[1] ?? null
}

/** 浏览器落盘：Blob + 临时 `<a download>`（下载后立即释放 objectURL） */
function saveBlob(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = objectUrl
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(objectUrl)
}

/** 本地时间戳 `yyyyMMddHHmm`（无 Content-Disposition 时的兜底文件名用） */
function timestampSuffix(): string {
  const now = new Date()
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}`
}

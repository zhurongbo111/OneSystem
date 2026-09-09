/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API 基础地址（dev 为 /api，走 Vite proxy） */
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

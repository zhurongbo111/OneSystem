/** 补零到两位 */
function pad(n: number): string {
  return String(n).padStart(2, '0')
}

/**
 * 将 `Date` 格式化为本地 `YYYY-MM-DD`（日期区间选择器默认值用）。
 */
export function toDateInput(value: Date): string {
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`
}

/**
 * 将 UTC ISO 时间格式化为本地 `YYYY-MM-DD HH:mm:ss`；空值或非法值返回 `-`。
 */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '-'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '-'
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

/**
 * 格式化为相对时间（站内信顶栏与列表用）：
 * 1 分钟内「刚刚」、1 小时内「N 分钟前」、24 小时内「N 小时前」、7 天内「N 天前」，更早显示本地 `YYYY-MM-DD`。
 */
export function formatRelativeTime(iso: string | null | undefined): string {
  if (!iso) return '-'
  const target = new Date(iso)
  if (Number.isNaN(target.getTime())) return '-'
  const diffMinutes = Math.floor((Date.now() - target.getTime()) / 60000)
  if (diffMinutes < 1) return '刚刚'
  if (diffMinutes < 60) return `${diffMinutes} 分钟前`
  const diffHours = Math.floor(diffMinutes / 60)
  if (diffHours < 24) return `${diffHours} 小时前`
  const diffDays = Math.floor(diffHours / 24)
  if (diffDays < 7) return `${diffDays} 天前`
  return `${target.getFullYear()}-${pad(target.getMonth() + 1)}-${pad(target.getDate())}`
}

/**
 * 当前本地时间 `YYYY-MM-DDTHH:mm:ss`（日期时间选择器带时分秒的默认值，如跟进时间）。
 */
export function nowInput(now: Date = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`
}

/**
 * 将选择器给出的本地 `YYYY-MM-DDTHH:mm:ss` 转为 UTC ISO 串（后端 `DateTimeOffset` 入参）。
 */
export function toUtcIso(localDateTime: string): string {
  return new Date(localDateTime).toISOString()
}

/**
 * 格式化为本地 `YYYY-MM-DD`（仅日期，如批次到期日）；空值或非法值返回 `-`。
 * 裸日期 `YYYY-MM-DD` 原样返回（它已是日历日，避免 `new Date` 时区漂移）。
 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '-'
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '-'
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

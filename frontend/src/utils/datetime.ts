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

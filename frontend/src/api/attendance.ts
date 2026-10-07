import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/** 考勤类型（0 请假 / 1 加班） */
export type AttendanceType = 0 | 1

/** 考勤记录列表行（对应后端 AttendanceListItemDto） */
export interface Attendance {
  id: string
  employeeId: string
  employeeName: string
  type: AttendanceType
  typeText: string
  startDate: string
  endDate: string
  /** 天数（含首尾，后端派生） */
  days: number
  remark: string | null
  createdAt: string
}

/** 考勤记录详情（对应后端 AttendanceDetailDto） */
export interface AttendanceDetail extends Attendance {
  updatedAt: string
}

export interface GetAttendancesParams {
  page?: number
  pageSize?: number
  employeeId?: string
  type?: AttendanceType
  startDate?: string
  endDate?: string
}

/** 新增 / 编辑载荷（全量覆盖；编辑可改员工） */
export interface AttendancePayload {
  employeeId: string
  type: AttendanceType
  startDate: string
  endDate: string
  remark?: string
}

/** 分页查询考勤记录（日期范围按区间重叠判定） */
export function getAttendances(params: GetAttendancesParams): Promise<PagedResult<Attendance>> {
  return get<PagedResult<Attendance>>('/attendances', { params })
}

/** 新增考勤登记（员工须在职；同员工同类型区间不可重叠） */
export function createAttendance(payload: AttendancePayload): Promise<AttendanceDetail> {
  return post<AttendanceDetail>('/attendances', payload)
}

/** 编辑考勤登记 */
export function updateAttendance(id: string, payload: AttendancePayload): Promise<AttendanceDetail> {
  return put<AttendanceDetail>(`/attendances/${id}`, payload)
}

/** 删除考勤登记 */
export function deleteAttendance(id: string): Promise<null> {
  return del<null>(`/attendances/${id}`)
}

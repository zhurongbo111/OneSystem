import type { PagedResult } from './product'
import { del, get, post, put } from './request'

/** 岗位状态（0 停用 / 1 启用） */
export type PositionStatus = 0 | 1

/** 岗位列表行 / 详情（对应后端 PositionListItemDto / PositionDetailDto） */
export interface Position {
  id: string
  code: string
  name: string
  status: PositionStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 岗位选择项（员工表单下拉用，仅启用岗位） */
export interface PositionPick {
  id: string
  code: string
  name: string
}

export interface GetPositionsParams {
  page?: number
  pageSize?: number
  keyword?: string
  status?: PositionStatus
}

export interface CreatePositionPayload {
  code: string
  name: string
  status: PositionStatus
  remark?: string
}

export interface UpdatePositionPayload {
  code: string
  name: string
  status: PositionStatus
  remark?: string
}

/** 分页查询岗位 */
export function getPositions(params: GetPositionsParams): Promise<PagedResult<Position>> {
  return get<PagedResult<Position>>('/positions', { params })
}

/** 查询岗位详情 */
export function getPosition(id: string): Promise<Position> {
  return get<Position>(`/positions/${id}`)
}

/** 新增岗位 */
export function createPosition(payload: CreatePositionPayload): Promise<Position> {
  return post<Position>('/positions', payload)
}

/** 编辑岗位 */
export function updatePosition(id: string, payload: UpdatePositionPayload): Promise<Position> {
  return put<Position>(`/positions/${id}`, payload)
}

/** 删除岗位（被员工引用时后端拒绝） */
export function deletePosition(id: string): Promise<null> {
  return del<null>(`/positions/${id}`)
}

/** 停用 / 启用岗位 */
export function updatePositionStatus(id: string, status: PositionStatus): Promise<Position> {
  return put<Position>(`/positions/${id}/status`, { status })
}

/** 岗位选择（仅启用岗位，全量；员工表单下拉数据源） */
export function getPositionPicks(): Promise<PositionPick[]> {
  return get<PositionPick[]>('/positions/picks')
}

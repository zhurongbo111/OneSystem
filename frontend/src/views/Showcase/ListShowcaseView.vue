<script setup lang="ts">
import { computed, ref } from 'vue'

import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconDotsVertical,
  IconDownload,
  IconEdit,
  IconEye,
  IconLock,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
  IconTrash,
} from '@tabler/icons-vue'

// —— types ——
/** 列表行数据模型（静态 Mock，参照用，非真实业务） */
interface UserRow {
  id: string
  name: string
  email: string
  role: 'admin' | 'editor' | 'viewer'
  status: 'active' | 'disabled'
  createdAt: string // ISO 日期
  seq?: number // 过滤后序号（运行时注入）
}

// —— constants ——
/** 静态种子数据：跨角色/状态/时间，便于验证搜索、筛选、排序、分页 */
const SEED: UserRow[] = [
  { id: 'u01', name: '张伟', email: 'zhang.wei@example.com', role: 'admin', status: 'active', createdAt: '2025-01-08' },
  { id: 'u02', name: '李娜', email: 'li.na@example.com', role: 'editor', status: 'active', createdAt: '2025-01-15' },
  { id: 'u03', name: '王强', email: 'wang.qiang@example.com', role: 'viewer', status: 'disabled', createdAt: '2025-02-03' },
  { id: 'u04', name: '刘敏', email: 'liu.min@example.com', role: 'editor', status: 'active', createdAt: '2025-02-19' },
  { id: 'u05', name: '陈杰', email: 'chen.jie@example.com', role: 'viewer', status: 'active', createdAt: '2025-03-02' },
  { id: 'u06', name: '杨洋', email: 'yang.yang@example.com', role: 'admin', status: 'disabled', createdAt: '2025-03-21' },
  { id: 'u07', name: '赵磊', email: 'zhao.lei@example.com', role: 'editor', status: 'active', createdAt: '2025-04-11' },
  { id: 'u08', name: '黄丽', email: 'huang.li@example.com', role: 'viewer', status: 'active', createdAt: '2025-04-25' },
  { id: 'u09', name: '周涛', email: 'zhou.tao@example.com', role: 'editor', status: 'disabled', createdAt: '2025-05-06' },
  { id: 'u10', name: '吴芳', email: 'wu.fang@example.com', role: 'viewer', status: 'active', createdAt: '2025-05-18' },
  { id: 'u11', name: '徐明', email: 'xu.ming@example.com', role: 'admin', status: 'active', createdAt: '2025-06-04' },
  { id: 'u12', name: '孙丽', email: 'sun.li@example.com', role: 'editor', status: 'active', createdAt: '2025-06-22' },
  { id: 'u13', name: '胡军', email: 'hu.jun@example.com', role: 'viewer', status: 'disabled', createdAt: '2025-07-09' },
  { id: 'u14', name: '朱霞', email: 'zhu.xia@example.com', role: 'editor', status: 'active', createdAt: '2025-07-28' },
  { id: 'u15', name: '高翔', email: 'gao.xiang@example.com', role: 'viewer', status: 'active', createdAt: '2025-08-05' },
  { id: 'u16', name: '林静', email: 'lin.jing@example.com', role: 'admin', status: 'active', createdAt: '2025-08-19' },
  { id: 'u17', name: '何平', email: 'he.ping@example.com', role: 'editor', status: 'disabled', createdAt: '2025-09-02' },
  { id: 'u18', name: '郭敏', email: 'guo.min@example.com', role: 'viewer', status: 'active', createdAt: '2025-09-16' },
  { id: 'u19', name: '罗刚', email: 'luo.gang@example.com', role: 'editor', status: 'active', createdAt: '2025-10-01' },
  { id: 'u20', name: '梁静', email: 'liang.jing@example.com', role: 'viewer', status: 'disabled', createdAt: '2025-10-20' },
  { id: 'u21', name: '宋佳', email: 'song.jia@example.com', role: 'admin', status: 'active', createdAt: '2025-11-07' },
  { id: 'u22', name: '唐磊', email: 'tang.lei@example.com', role: 'editor', status: 'active', createdAt: '2025-11-24' },
  { id: 'u23', name: '韩雪', email: 'han.xue@example.com', role: 'viewer', status: 'active', createdAt: '2025-12-03' },
  { id: 'u24', name: '冯军', email: 'feng.jun@example.com', role: 'editor', status: 'disabled', createdAt: '2026-01-10' },
  { id: 'u25', name: '董洁', email: 'dong.jie@example.com', role: 'viewer', status: 'active', createdAt: '2026-02-01' },
]

const statusOptions = [
  { label: '启用', value: 'active' },
  { label: '禁用', value: 'disabled' },
]
const roleOptions = [
  { label: '管理员', value: 'admin' },
  { label: '编辑', value: 'editor' },
  { label: '访客', value: 'viewer' },
]

const columnOptions = [
  { label: '名称', value: 'name' },
  { label: '邮箱', value: 'email' },
  { label: '角色', value: 'role' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

const rowSelection = { type: 'checkbox' as const, showCheckedAll: true }

const pagination = {
  defaultPageSize: 10,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}

const roleColor: Record<UserRow['role'], string> = {
  admin: 'arcoblue',
  editor: 'orangered',
  viewer: 'green',
}
const roleLabel: Record<UserRow['role'], string> = {
  admin: '管理员',
  editor: '编辑',
  viewer: '访客',
}

// —— helpers ——
function cloneData(list: UserRow[]): UserRow[] {
  return list.map((r) => ({ ...r }))
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

/** 按列 dataIndex 取展示值（创建时间格式化，角色/状态转中文） */
function getExportValue(row: UserRow, key: string): string {
  switch (key) {
    case 'createdAt':
      return formatDate(row.createdAt)
    case 'role':
      return roleLabel[row.role]
    case 'status':
      return row.status === 'active' ? '启用' : '禁用'
    case 'name':
      return row.name
    case 'email':
      return row.email
    default:
      return ''
  }
}

// —— reactive state ——
/** 可变数据源（删除/刷新操作它），初始为种子数据拷贝 */
const data = ref<UserRow[]>(cloneData(SEED))

/** 搜索词：输入态 / 已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const appliedKeyword = ref('')
/** 状态筛选：输入态 / 已应用态 */
const statusInput = ref<string | undefined>(undefined)
const appliedStatus = ref<string | undefined>(undefined)
/** 角色筛选：输入态 / 已应用态 */
const roleInput = ref<string | undefined>(undefined)
const appliedRole = ref<string | undefined>(undefined)

/** 当前勾选行 */
const selectedKeys = ref<string[]>([])

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>(['name', 'email', 'role', 'status', 'createdAt'])

// —— computed ——
/** 过滤（搜索 + 状态）后的数据，注入序号；排序由 Arco 表格内置完成 */
const tableData = computed<UserRow[]>(() => {
  const kw = appliedKeyword.value.trim().toLowerCase()
  return data.value
    .filter((r) => {
      const okKw = !kw || r.name.toLowerCase().includes(kw) || r.email.toLowerCase().includes(kw)
      const okSt = !appliedStatus.value || r.status === appliedStatus.value
      const okRole = !appliedRole.value || r.role === appliedRole.value
      return okKw && okSt && okRole
    })
    .map((r, i) => ({ ...r, seq: i + 1 }))
})

/** 依据列显示设置动态拼列（序号与操作列固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', dataIndex: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('name')) {
    cols.push({
      title: '名称',
      dataIndex: 'name',
      width: 120,
      sortable: { sorter: true, sortDirections: ['ascend', 'descend'] },
    })
  }
  if (visibleColumns.value.includes('email')) {
    // 固定宽 + 省略：避免成为唯一弹性列吸收全部剩余空间
    cols.push({ title: '邮箱', dataIndex: 'email', width: 150, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('role')) {
    cols.push({ title: '角色', dataIndex: 'role', width: 110, slotName: 'role' })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', dataIndex: 'status', width: 100, slotName: 'status' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({
      title: '创建时间',
      dataIndex: 'createdAt',
      width: 140,
      sortable: { sorter: true, sortDirections: ['ascend', 'descend'] },
      slotName: 'createdAt',
    })
  }
  // 列宽须 ≥ 实测内容 238px（3×66 文本按钮 + 28 纯图标「更多」+ 3×4 间距），取 240，
  // 否则 td 内容溢出、表头与内容错位（specs/action-column §2）
  cols.push({ title: '操作', slotName: 'action', width: 240, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/action-column §2 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

/** 表格重挂载 key：搜索/筛选变化时回第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedStatus.value ?? ''}|${appliedRole.value ?? ''}`,
)

// —— methods ——
/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedStatus.value = statusInput.value
  appliedRole.value = roleInput.value
  selectedKeys.value = []
}

/** 重置：清空条件，还原全量 */
function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  roleInput.value = undefined
  appliedKeyword.value = ''
  appliedStatus.value = undefined
  appliedRole.value = undefined
  selectedKeys.value = []
}

/** 单行删除（Popconfirm 确认后） */
function removeRow(id: string): void {
  data.value = data.value.filter((r) => r.id !== id)
  selectedKeys.value = selectedKeys.value.filter((k) => k !== id)
  Message.success('已删除 1 条')
}

/** 批量删除（Popconfirm 确认后） */
function onBatchDelete(): void {
  const keys = new Set(selectedKeys.value)
  if (keys.size === 0) return
  data.value = data.value.filter((r) => !keys.has(r.id))
  const n = keys.size
  selectedKeys.value = []
  Message.success(`已删除 ${n} 条`)
}

/** 新增（演示占位） */
function onCreate(): void {
  Message.info('演示页面，暂未实现新增')
}

/** 操作列：详情（演示占位，中性） */
function onActionDetail(row: UserRow): void {
  Message.info(`演示：查看详情 ${row.name}`)
}

/** 操作列：编辑（演示占位，主操作） */
function onActionEdit(row: UserRow): void {
  Message.info(`演示：编辑 ${row.name}`)
}

/** 操作列：重置密码（演示占位，收纳于「更多」） */
function onActionResetPassword(row: UserRow): void {
  Message.info(`演示：重置密码 ${row.name}`)
}

/** 刷新：恢复初始数据并清空条件/勾选 */
function onRefresh(): void {
  data.value = cloneData(SEED)
  keywordInput.value = ''
  statusInput.value = undefined
  roleInput.value = undefined
  appliedKeyword.value = ''
  appliedStatus.value = undefined
  appliedRole.value = undefined
  selectedKeys.value = []
  Message.success('已刷新数据')
}

/** 导出当前筛选结果为 CSV（含 BOM，Excel 中文兼容） */
function onExport(): void {
  const rows = tableData.value
  if (rows.length === 0) {
    Message.warning('暂无可导出数据')
    return
  }
  const cols = columns.value.filter((c) => c.dataIndex && c.dataIndex !== 'seq')
  const header = cols.map((c) => `"${String(c.title)}"`)
  const lines = rows.map((r) =>
    cols
      .map((c) => {
        const v = getExportValue(r, c.dataIndex as string)
        return `"${v.replace(/"/g, '""')}"`
      })
      .join(','),
  )
  const csv = '\uFEFF' + [header.join(','), ...lines].join('\r\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = '用户列表.csv'
  a.click()
  URL.revokeObjectURL(url)
  Message.success(`已导出 ${rows.length} 条`)
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        用户列表
      </h1>
    </div>

    <!-- 表格卡片：工具条（筛选 + 操作）+ 表格，整体贴在一起 -->
    <a-card
      :bordered="false"
      class="table-card"
    >
      <!-- 工具条：筛选行（底部分隔线）+ 操作行（贴表格） -->
      <div class="toolbar">
        <!-- 筛选行：栅格多列，窄屏自动换行为多行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="8">
            <a-input
              v-model="keywordInput"
              class="filter-bar__search"
              placeholder="搜索名称或邮箱"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              class="filter-bar__status"
              :options="statusOptions"
              placeholder="状态"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="roleInput"
              class="filter-bar__role"
              :options="roleOptions"
              placeholder="角色"
              allow-clear
            />
          </a-col>
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                @click="onSearch"
              >
                <template #icon>
                  <IconSearch />
                </template>
                搜索
              </a-button>
              <a-button @click="onReset">
                <template #icon>
                  <IconRestore />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：紧贴表格；左组主操作（新增）靠左，右组数据/视图操作靠右，同一行 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              size="small"
              @click="onExport"
            >
              <template #icon>
                <IconDownload />
              </template>
              导出
            </a-button>
            <a-popconfirm
              type="warning"
              content="确认删除选中项？"
              @ok="onBatchDelete"
            >
              <a-button
                status="danger"
                size="small"
                :disabled="selectedKeys.length === 0"
              >
                <template #icon>
                  <IconTrash />
                </template>
                批量删除
              </a-button>
            </a-popconfirm>
            <a-divider
              direction="vertical"
              class="toolbar-actions__divider"
            />
            <a-dropdown trigger="click">
              <a-button size="small">
                <template #icon>
                  <IconSettings />
                </template>
                列设置
              </a-button>
              <template #content>
                <div class="col-settings">
                  <a-checkbox-group v-model="visibleColumns">
                    <a-space direction="vertical">
                      <a-checkbox
                        v-for="opt in columnOptions"
                        :key="opt.value"
                        :value="opt.value"
                      >
                        {{ opt.label }}
                      </a-checkbox>
                    </a-space>
                  </a-checkbox-group>
                </div>
              </template>
            </a-dropdown>
            <a-button
              size="small"
              @click="onRefresh"
            >
              <template #icon>
                <IconRefresh />
              </template>
              刷新
            </a-button>
          </div>
        </div>
      </div>

      <!-- 表格 -->
      <a-table
        :key="tableKey"
        v-model:selected-keys="selectedKeys"
        row-key="id"
        :columns="columns"
        :data="tableData"
        :pagination="pagination"
        :scroll="{ x: tableScrollX }"
        :row-selection="rowSelection"
      >
        <template #role="{ record }">
          <a-tag :color="roleColor[(record as UserRow).role]">
            {{ roleLabel[(record as UserRow).role] }}
          </a-tag>
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as UserRow).status === 'active' ? 'green' : 'gray'">
            {{ (record as UserRow).status === 'active' ? '启用' : '禁用' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDate((record as UserRow).createdAt) }}
        </template>
        <!-- 操作列（specs/action-column §2~§5）：4 个操作 > 3，平铺 详情/编辑/删除，「重置密码」收纳进「更多」；顺序 主操作→中性→危险 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onActionEdit(record as UserRow)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-button
              type="text"
              size="small"
              @click="onActionDetail(record as UserRow)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
            <a-popconfirm
              type="warning"
              content="确认删除该用户？"
              @ok="removeRow(record.id)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
            <a-dropdown trigger="click">
              <a-button
                type="text"
                size="small"
                aria-label="更多操作"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  value="reset-password"
                  @click="onActionResetPassword(record as UserRow)"
                >
                  <template #icon>
                    <IconLock />
                  </template>
                  重置密码
                </a-doption>
              </template>
            </a-dropdown>
          </a-space>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<style scoped>
.list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

/* 操作列密度（specs/action-column §5）：收窄 Arco 文本/纯图标按钮默认 0 15px 的水平 padding，避免相邻操作视觉间距过大 */
.row-actions :deep(.arco-btn-text),
.row-actions :deep(.arco-btn-only-icon) {
  padding: 0 8px;
}

/* 操作列兜底（specs/action-column §2.1）：按钮组不折行，防止列宽不足时换行导致行高异常 */
:deep(.action-cell) {
  white-space: nowrap;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.page-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-1);
}

.toolbar-filter {
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--color-border);
}

.toolbar-filter .arco-col {
  display: flex;
}

.toolbar-filter .arco-col > .arco-input,
.toolbar-filter .arco-col > .arco-select {
  flex: 1;
}

.toolbar-filter__actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.toolbar-actions__left,
.toolbar-actions__right {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.toolbar-actions__divider {
  margin: 0;
}

.table-card {
  border-radius: var(--border-radius-medium);
}

.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}
</style>

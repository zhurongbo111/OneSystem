<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getDepartments } from '@/api/department'
import type { DepartmentTreeNode } from '@/api/department'
import { getEmployees, updateEmployeeStatus } from '@/api/employee'
import type { Employee, EmployeeStatus } from '@/api/employee'
import { exportEmployees } from '@/api/export'
import { getPositionPicks } from '@/api/position'
import type { PositionPick } from '@/api/position'
import { useAuthStore } from '@/stores/auth'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconDownload,
  IconEdit,
  IconPlayerPlay,
  IconPlus,
  IconPower,
  IconRefresh,
  IconRestore,
  IconSearch,
} from '@tabler/icons-vue'

import EmployeeFormDrawer from './EmployeeFormDrawer.vue'

const auth = useAuthStore()

// —— types ——
/** 部门下拉节点（Arco TreeSelect：key / title / children） */
interface DepartmentTreeOption {
  key: string
  title: string
  children: DepartmentTreeOption[]
}

// —— constants ——
const statusOptions = [
  { label: '在职', value: 1 },
  { label: '离职', value: 0 },
]

/** 性别文案（null / 0 未填显示占位） */
const genderLabels: Record<number, string> = { 1: '男', 2: '女' }

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
/** 正在切换在职状态的员工 id */
const togglingId = ref<string | undefined>(undefined)
const items = ref<Employee[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 筛选数据源：部门树（含停用部门，仅作筛选展示用）与岗位下拉 */
const departmentTree = ref<DepartmentTreeNode[]>([])
const positionPicks = ref<PositionPick[]>([])

/** 关键词 / 部门 / 岗位 / 状态：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const departmentInput = ref<string | undefined>(undefined)
const positionInput = ref<string | undefined>(undefined)
const statusInput = ref<EmployeeStatus | undefined>(undefined)
const appliedKeyword = ref('')
const appliedDepartmentId = ref<string | undefined>(undefined)
const appliedPositionId = ref<string | undefined>(undefined)
const appliedStatus = ref<EmployeeStatus | undefined>(undefined)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedDepartmentId.value ?? ''}|${appliedPositionId.value ?? ''}|${appliedStatus.value ?? ''}`,
)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

/** 部门下拉的树节点（Arco TreeSelect：key / title / children） */
const departmentOptions = computed(() => buildTreeOptions(departmentTree.value))

/** 岗位下拉选项 */
const positionOptions = computed(() => positionPicks.value.map((p) => ({ label: `${p.name}（${p.code}）`, value: p.id })))

const departmentFieldNames = { key: 'key', title: 'title', children: 'children' }

/** 列定义（操作列 2 个操作平铺，宽 180，specs/011-action-column §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '工号', dataIndex: 'employeeNo', width: 130 },
  { title: '姓名', dataIndex: 'name', width: 110, ellipsis: true, tooltip: true },
  { title: '性别', slotName: 'gender', width: 70, align: 'center' },
  { title: '手机号', dataIndex: 'phone', width: 140 },
  { title: '部门', dataIndex: 'departmentName', width: 150, ellipsis: true, tooltip: true },
  { title: '岗位', dataIndex: 'positionName', width: 150, ellipsis: true, tooltip: true },
  { title: '入职日期', slotName: 'hireDate', width: 120 },
  { title: '状态', slotName: 'status', width: 90, align: 'center' },
  { title: '关联账号', dataIndex: 'userDisplayName', width: 140, ellipsis: true, tooltip: true },
  { title: '操作', slotName: 'action', width: 180, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchFilterOptions()
})

// —— methods ——
/** 构造 TreeSelect 节点（递归） */
function buildTreeOptions(nodes: DepartmentTreeNode[]): DepartmentTreeOption[] {
  return nodes.map((node) => ({
    key: node.id,
    title: node.status === 1 ? node.name : `${node.name}（停用）`,
    children: buildTreeOptions(node.children),
  }))
}

/** 拉取筛选数据源（部门树 + 岗位下拉） */
async function fetchFilterOptions(): Promise<void> {
  try {
    const [departments, positions] = await Promise.all([getDepartments(), getPositionPicks()])
    departmentTree.value = departments
    positionPicks.value = positions
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getEmployees({
      keyword: appliedKeyword.value.trim() || undefined,
      departmentId: appliedDepartmentId.value,
      positionId: appliedPositionId.value,
      status: appliedStatus.value,
      page: page.value,
      pageSize: pageSize.value,
    })
    if (seq !== fetchSeq) return
    items.value = result.items
    total.value = result.total
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedDepartmentId.value = departmentInput.value
  appliedPositionId.value = positionInput.value
  appliedStatus.value = statusInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  departmentInput.value = undefined
  positionInput.value = undefined
  statusInput.value = undefined
  appliedKeyword.value = ''
  appliedDepartmentId.value = undefined
  appliedPositionId.value = undefined
  appliedStatus.value = undefined
  page.value = 1
  void fetchList()
}

/** 刷新当前页 */
function onRefresh(): void {
  void fetchList()
}

function onPageChange(current: number): void {
  page.value = current
  void fetchList()
}

function onPageSizeChange(size: number): void {
  pageSize.value = size
  page.value = 1
  void fetchList()
}

/** 新增（抽屉） */
function onCreate(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉） */
function onEdit(row: Employee): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 抽屉保存后刷新列表与筛选数据源（新增部门 / 岗位后选项可能变化） */
function onSaved(): void {
  void fetchList()
  void fetchFilterOptions()
}

/** 导出当前已应用筛选的全量员工列表；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    await exportEmployees({
      keyword: appliedKeyword.value.trim() || undefined,
      departmentId: appliedDepartmentId.value,
      positionId: appliedPositionId.value,
      status: appliedStatus.value,
    })
    if (total.value === 0) {
      Message.info('已导出空数据模板')
    }
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    exporting.value = false
  }
}

/** 在职 / 离职切换（离职不删除记录） */
async function onToggleStatus(row: Employee): Promise<void> {
  if (togglingId.value) return
  const next: EmployeeStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateEmployeeStatus(row.id, next)
    Message.success(next === 1 ? '已复职' : '已办理离职')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 性别展示文案（未填显示占位） */
function genderText(gender: number | null): string {
  return gender ? genderLabels[gender] ?? '-' : '-'
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        员工档案
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <!-- 筛选行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="6">
            <a-input
              v-model="keywordInput"
              placeholder="搜索工号或姓名"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="5">
            <a-tree-select
              v-model="departmentInput"
              :data="departmentOptions"
              :field-names="departmentFieldNames"
              placeholder="全部部门"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="positionInput"
              :options="positionOptions"
              placeholder="全部岗位"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              :options="statusOptions"
              placeholder="在职状态"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                :loading="loading"
                @click="onSearch"
              >
                <template #icon>
                  <IconSearch />
                </template>
                搜索
              </a-button>
              <a-button
                :loading="loading"
                @click="onReset"
              >
                <template #icon>
                  <IconRestore />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：左组主操作靠左，右组数据操作（导出 / 刷新）靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('employees.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增员工
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              v-if="auth.hasPermission('employees.export')"
              size="small"
              :loading="exporting"
              :disabled="exporting"
              @click="onExport"
            >
              <template #icon>
                <IconDownload />
              </template>
              导出
            </a-button>
            <a-divider
              direction="vertical"
              class="toolbar-actions__divider"
            />
            <a-button
              size="small"
              :loading="loading"
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

      <a-table
        :key="tableKey"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="items"
        :pagination="pagination"
        :scroll="{ x: tableScrollX }"
        @page-change="onPageChange"
        @page-size-change="onPageSizeChange"
      >
        <template #seq="{ rowIndex }">
          {{ (page - 1) * pageSize + rowIndex + 1 }}
        </template>
        <template #gender="{ record }">
          {{ genderText((record as Employee).gender) }}
        </template>
        <template #hireDate="{ record }">
          {{ (record as Employee).hireDate }}
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as Employee).status === 1 ? 'green' : 'gray'">
            {{ (record as Employee).statusText }}
          </a-tag>
        </template>
        <!-- 操作列（specs/011-action-column）：2 个操作平铺 编辑 / 在职离职切换 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('employees.update')"
              type="text"
              size="small"
              @click="onEdit(record as Employee)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('employees.status')"
              type="warning"
              :content="`确认${(record as Employee).status === 1 ? '办理离职' : '复职'}？`"
              @ok="onToggleStatus(record as Employee)"
            >
              <a-button
                type="text"
                :status="(record as Employee).status === 1 ? 'warning' : 'normal'"
                size="small"
                :loading="togglingId === (record as Employee).id"
              >
                <template #icon>
                  <IconPower v-if="(record as Employee).status === 1" />
                  <IconPlayerPlay v-else />
                </template>
                {{ (record as Employee).status === 1 ? '离职' : '复职' }}
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <EmployeeFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      :department-tree="departmentTree"
      :position-options="positionPicks"
      @saved="onSaved"
    />
  </div>
</template>

<style scoped>
.list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

/* 操作列密度（specs/011-action-column §0）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

/* 操作列兜底：按钮组不折行 */
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
</style>

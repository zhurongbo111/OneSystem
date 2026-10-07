<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { deleteAttendance, getAttendances } from '@/api/attendance'
import type { Attendance, AttendanceType } from '@/api/attendance'
import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import { useAuthStore } from '@/stores/auth'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconEdit,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconTrash,
} from '@tabler/icons-vue'

import AttendanceFormDrawer from './AttendanceFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
/** 考勤类型文案与标签色（specs/044-erp-hcm-payroll/design.md §0.1，唯一来源） */
const typeMeta: Record<number, { label: string; color: string }> = {
  0: { label: '请假', color: 'orange' },
  1: { label: '加班', color: 'blue' },
}

const typeOptions = [
  { label: '请假', value: 0 },
  { label: '加班', value: 1 },
]

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在删除的考勤记录 id */
const deletingId = ref<string | undefined>(undefined)
const items = ref<Attendance[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 员工下拉（在职员工） */
const employeeOptions = ref<Employee[]>([])

/** 筛选输入态与应用态分离（点搜索才生效） */
const employeeInput = ref<string | undefined>(undefined)
const typeInput = ref<AttendanceType | undefined>(undefined)
const rangeInput = ref<string[]>([])
const appliedEmployeeId = ref<string | undefined>(undefined)
const appliedType = ref<AttendanceType | undefined>(undefined)
const appliedStartDate = ref<string | undefined>(undefined)
const appliedEndDate = ref<string | undefined>(undefined)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerRecord = ref<Attendance | undefined>(undefined)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedEmployeeId.value ?? ''}|${appliedType.value ?? ''}|${appliedStartDate.value ?? ''}|${appliedEndDate.value ?? ''}`,
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

/** 员工下拉选项（姓名 + 工号） */
const employeeSelectOptions = computed(() =>
  employeeOptions.value.map((e) => ({ label: `${e.name}（${e.employeeNo}）`, value: e.id })),
)

/** 列定义（操作列 2 个操作平铺，宽 180，specs/011-action-column §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '员工', dataIndex: 'employeeName', width: 130, ellipsis: true, tooltip: true },
  { title: '类型', slotName: 'type', width: 90, align: 'center' },
  { title: '起始日', dataIndex: 'startDate', width: 120 },
  { title: '结束日', dataIndex: 'endDate', width: 120 },
  { title: '天数', dataIndex: 'days', width: 80, align: 'center' },
  { title: '事由', dataIndex: 'remark', width: 220, ellipsis: true, tooltip: true },
  { title: '操作', slotName: 'action', width: 180, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchEmployees()
})

// —— methods ——
/** 拉取员工下拉（仅在职员工，离职员工不可新登记考勤） */
async function fetchEmployees(): Promise<void> {
  try {
    const result = await getEmployees({ status: 1, page: 1, pageSize: 100 })
    employeeOptions.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getAttendances({
      employeeId: appliedEmployeeId.value,
      type: appliedType.value,
      startDate: appliedStartDate.value,
      endDate: appliedEndDate.value,
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
  appliedEmployeeId.value = employeeInput.value
  appliedType.value = typeInput.value
  appliedStartDate.value = rangeInput.value[0] || undefined
  appliedEndDate.value = rangeInput.value[1] || undefined
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  employeeInput.value = undefined
  typeInput.value = undefined
  rangeInput.value = []
  appliedEmployeeId.value = undefined
  appliedType.value = undefined
  appliedStartDate.value = undefined
  appliedEndDate.value = undefined
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
  drawerRecord.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉，直接以列表行回填：列表已含全部字段） */
function onEdit(row: Attendance): void {
  drawerMode.value = 'edit'
  drawerRecord.value = row
  drawerVisible.value = true
}

/** 抽屉保存后刷新列表 */
function onSaved(): void {
  void fetchList()
}

/** 删除（二次确认后执行） */
async function onDelete(row: Attendance): Promise<void> {
  if (deletingId.value) return
  deletingId.value = row.id
  try {
    await deleteAttendance(row.id)
    Message.success('考勤记录已删除')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    deletingId.value = undefined
  }
}

/** 类型文案（未知值回退占位） */
function typeText(type: number): string {
  return typeMeta[type]?.label ?? '-'
}

/** 类型标签色 */
function typeColor(type: number): string {
  return typeMeta[type]?.color ?? 'gray'
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        考勤登记
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
            <a-select
              v-model="employeeInput"
              :options="employeeSelectOptions"
              placeholder="全部员工（在职）"
              allow-clear
              allow-search
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="typeInput"
              :options="typeOptions"
              placeholder="全部类型"
              allow-clear
            />
          </a-col>
          <a-col :span="7">
            <a-range-picker
              v-model="rangeInput"
              value-format="YYYY-MM-DD"
              :placeholder="['起始日', '结束日']"
            />
          </a-col>
          <a-col :span="5">
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

        <!-- 操作行：左组主操作靠左，右组数据操作靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('attendance.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增登记
            </a-button>
          </div>
          <div class="toolbar-actions__right">
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
        <template #type="{ record }">
          <a-tag :color="typeColor((record as Attendance).type)">
            {{ typeText((record as Attendance).type) }}
          </a-tag>
        </template>
        <!-- 操作列（specs/011-action-column）：2 个操作平铺 编辑 / 删除 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('attendance.update')"
              type="text"
              size="small"
              @click="onEdit(record as Attendance)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('attendance.delete')"
              type="warning"
              content="确认删除该考勤记录？"
              @ok="onDelete(record as Attendance)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
                :loading="deletingId === (record as Attendance).id"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <AttendanceFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :record="drawerRecord"
      :employee-options="employeeOptions"
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
::deep(.action-cell) {
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
.toolbar-filter .arco-col > .arco-select,
.toolbar-filter .arco-col > .arco-picker {
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

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import {
  AUDIT_ACTION_COLORS,
  AUDIT_ACTION_OPTIONS,
  AUDIT_RESOURCE_COLORS,
  AUDIT_RESOURCE_OPTIONS,
  auditActionLabel,
  auditResourceLabel,
  getAuditLogs,
  operatorText,
  toDateRange,
} from '@/api/auditLog'
import type { AuditAction, AuditLogListItem, AuditResource } from '@/api/auditLog'
import { getUsers } from '@/api/user'
import type { UserListItem } from '@/api/user'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconEye,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

import AuditLogDetailDrawer from './AuditLogDetailDrawer.vue'

// —— reactive state ——
const auth = useAuthStore()

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

const loading = ref(false)
const items = ref<AuditLogListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 筛选输入态与已应用态分离（点搜索 / 回车才生效） */
const keywordInput = ref('')
const resourceInput = ref<AuditResource | undefined>(undefined)
const actionInput = ref<AuditAction | undefined>(undefined)
const operatorInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedResource = ref<AuditResource | undefined>(undefined)
const appliedAction = ref<AuditAction | undefined>(undefined)
const appliedOperator = ref<string | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

/** 操作人下拉数据源（复用用户列表；无 users.view 权限时不可选） */
const operators = ref<UserListItem[]>([])

/** 详情抽屉 */
const drawerVisible = ref(false)
const activeLogId = ref<string | null>(null)

/** 可选列（序号与操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '操作时间', value: 'createdAt' },
  { label: '操作人', value: 'operator' },
  { label: '资源类型', value: 'resource' },
  { label: '动作', value: 'action' },
  { label: '业务标识', value: 'resourceNo' },
  { label: '摘要', value: 'summary' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'createdAt',
  'operator',
  'resource',
  'action',
  'resourceNo',
  'summary',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedResource.value ?? ''}|${appliedAction.value ?? ''}|${appliedOperator.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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

/** 操作人下拉选项：`显示名（登录名）` */
const operatorOptions = computed(() =>
  operators.value.map((user) => ({ label: operatorText(user), value: user.id })),
)

/** 是否可选操作人（依赖用户列表权限） */
const canPickOperator = computed(() => auth.hasPermission('users.view'))

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '操作时间', slotName: 'createdAt', width: 180 })
  }
  if (visibleColumns.value.includes('operator')) {
    cols.push({ title: '操作人', slotName: 'operator', width: 170, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('resource')) {
    cols.push({ title: '资源类型', slotName: 'resource', width: 120, align: 'center' })
  }
  if (visibleColumns.value.includes('action')) {
    cols.push({ title: '动作', slotName: 'action', width: 110, align: 'center' })
  }
  if (visibleColumns.value.includes('resourceNo')) {
    cols.push({ title: '业务标识', slotName: 'resourceNo', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('summary')) {
    cols.push({ title: '摘要', slotName: 'summary', width: 320, ellipsis: true, tooltip: true })
  }
  // 操作列：1 个操作（详情），宽度按 specs/011-action-column §0 单操作取值 90
  cols.push({ title: '操作', slotName: 'action-cell', width: 90, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, column) => sum + (column.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchOperators()
})

// —— methods ——
/** 拉取当前条件下的日志（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = appliedRange.value
      ? toDateRange(appliedRange.value[0], appliedRange.value[1])
      : {}
    const result = await getAuditLogs({
      keyword: appliedKeyword.value.trim() || undefined,
      resource: appliedResource.value,
      action: appliedAction.value,
      userId: appliedOperator.value,
      start,
      end,
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

/** 加载操作人下拉（无 users.view 权限时跳过，避免无谓的 40300 提示） */
async function fetchOperators(): Promise<void> {
  if (!canPickOperator.value) return
  try {
    const result = await getUsers({ page: 1, pageSize: 100 })
    operators.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedResource.value = resourceInput.value
  appliedAction.value = actionInput.value
  appliedOperator.value = operatorInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  resourceInput.value = undefined
  actionInput.value = undefined
  operatorInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedResource.value = undefined
  appliedAction.value = undefined
  appliedOperator.value = undefined
  appliedRange.value = null
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

/** 打开详情抽屉（同步动作不置 loading，数据由抽屉自己拉取） */
function onDetail(row: AuditLogListItem): void {
  activeLogId.value = row.id
  drawerVisible.value = true
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        操作日志
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
          <a-col :span="5">
            <a-input
              v-model="keywordInput"
              class="filter-bar__search"
              placeholder="搜索业务标识 / 操作人"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="resourceInput"
              class="filter-bar__resource"
              :options="AUDIT_RESOURCE_OPTIONS"
              placeholder="资源类型"
              allow-clear
            />
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="actionInput"
              class="filter-bar__action"
              :options="AUDIT_ACTION_OPTIONS"
              placeholder="动作"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="operatorInput"
              class="filter-bar__operator"
              :options="operatorOptions"
              placeholder="全部操作人"
              allow-clear
              allow-search
              :disabled="!canPickOperator"
            />
          </a-col>
          <a-col :span="6">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              class="filter-bar__range"
              allow-clear
            />
          </a-col>
          <a-col :span="3">
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

        <!-- 操作行：日志只读，仅视图操作（列设置 / 刷新） -->
        <div class="toolbar-actions">
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
        <template #createdAt="{ record }">
          {{ formatDateTime((record as AuditLogListItem).createdAt) }}
        </template>
        <template #operator="{ record }">
          {{ operatorText(record as AuditLogListItem) }}
        </template>
        <template #resource="{ record }">
          <a-tag :color="AUDIT_RESOURCE_COLORS[(record as AuditLogListItem).resource] ?? 'gray'">
            {{ auditResourceLabel((record as AuditLogListItem).resource) }}
          </a-tag>
        </template>
        <template #action="{ record }">
          <a-tag :color="AUDIT_ACTION_COLORS[(record as AuditLogListItem).action] ?? 'gray'">
            {{ auditActionLabel((record as AuditLogListItem).action) }}
          </a-tag>
        </template>
        <template #resourceNo="{ record }">
          {{ (record as AuditLogListItem).resourceNo || '-' }}
        </template>
        <template #summary="{ record }">
          {{ (record as AuditLogListItem).summary }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：日志只读，仅「详情」一个操作 -->
        <template #action-cell="{ record }">
          <a-button
            type="text"
            size="small"
            @click="onDetail(record as AuditLogListItem)"
          >
            <template #icon>
              <IconEye />
            </template>
            详情
          </a-button>
        </template>
      </a-table>
    </a-card>

    <AuditLogDetailDrawer
      v-model:visible="drawerVisible"
      :log-id="activeLogId"
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
.toolbar-filter .arco-col > .arco-range-picker {
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
  justify-content: flex-end;
  gap: 8px;
  margin-bottom: 8px;
}

.col-settings {
  padding: 8px 12px;
}

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

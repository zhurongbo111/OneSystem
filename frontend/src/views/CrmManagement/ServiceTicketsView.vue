<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { Component } from 'vue'
import { useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  SERVICE_TICKET_PRIORITY_OPTIONS,
  SERVICE_TICKET_STATUS_OPTIONS,
  allowedTicketTransitions,
  getServiceTickets,
  ticketPriorityColor,
  ticketPriorityLabel,
  ticketStatusColor,
  ticketStatusLabel,
  ticketTransitionLabel,
  updateServiceTicketStatus,
} from '@/api/serviceTicket'
import type { ServiceTicketListItem, TicketPriority, TicketStatus } from '@/api/serviceTicket'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconArchive,
  IconCircleCheck,
  IconDotsVertical,
  IconEdit,
  IconEye,
  IconPlayerPlay,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconRotate,
  IconSearch,
} from '@tabler/icons-vue'

const auth = useAuthStore()

// —— types ——
/** 行内动作（按状态派生；前 3 个平铺，其余收纳进「更多」，specs/011-action-column §0） */
interface TicketRowAction {
  /** 动作标识（detail / edit / `status-<目标状态>`） */
  key: string
  label: string
  icon: Component
  /** 目标状态（状态流转动作才有） */
  target?: TicketStatus
  /** 按钮颜色语义（完成 / 危险） */
  status?: 'success' | 'danger'
  /** 是否需二次确认（关闭为终态） */
  confirm?: boolean
}

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 已关闭（终态）：不可编辑 / 不可改状态 */
const CLOSED_STATUS: TicketStatus = 3

/** 状态流转动作的展示顺序（受理 / 重开 → 解决 → 关闭） */
const TRANSITION_ORDER: TicketStatus[] = [1, 2, 3]

/** 状态流转按钮图标（按动作语义取，Tabler 首选，前端规则 §4.7） */
const TRANSITION_ICONS: Record<string, Component> = {
  受理: IconPlayerPlay,
  解决: IconCircleCheck,
  重开: IconRotate,
  关闭: IconArchive,
}

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 正在改状态的工单 id（前端规则 §4.6：xxingId） */
const statusingId = ref<string | undefined>(undefined)
const items = ref<ServiceTicketListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 状态 / 优先级 / 负责人：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const statusInput = ref<TicketStatus | undefined>(undefined)
const priorityInput = ref<TicketPriority | undefined>(undefined)
const ownerInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedStatus = ref<TicketStatus | undefined>(undefined)
const appliedPriority = ref<TicketPriority | undefined>(undefined)
const appliedOwnerId = ref<string | undefined>(undefined)

/** 负责人下拉数据源（仅在职员工，一次取前 100 条） */
const employees = ref<Employee[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedStatus.value ?? ''}|${appliedPriority.value ?? ''}|${appliedOwnerId.value ?? ''}`,
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

const statusOptions = SERVICE_TICKET_STATUS_OPTIONS
const priorityOptions = SERVICE_TICKET_PRIORITY_OPTIONS

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 表格列（每列必设宽度，specs/006-list-showcase §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '工单号', dataIndex: 'ticketNo', width: 160 },
  { title: '客户', dataIndex: 'partnerName', width: 180, ellipsis: true, tooltip: true },
  { title: '标题', dataIndex: 'title', width: 200, ellipsis: true, tooltip: true },
  { title: '优先级', slotName: 'priority', width: 90, align: 'center' },
  { title: '状态', slotName: 'status', width: 100, align: 'center' },
  { title: '负责人', slotName: 'owner', width: 110 },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  { title: '操作', slotName: 'action', width: 240, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §0 列宽策略） */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— lifecycle ——
onMounted(async () => {
  await Promise.all([fetchList(), loadEmployees()])
})

// —— methods ——
/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getServiceTickets({
      keyword: appliedKeyword.value.trim() || undefined,
      status: appliedStatus.value,
      priority: appliedPriority.value,
      ownerId: appliedOwnerId.value,
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

/** 负责人下拉（仅在职员工） */
async function loadEmployees(): Promise<void> {
  try {
    const result = await getEmployees({ page: 1, pageSize: 100, status: 1 })
    employees.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedStatus.value = statusInput.value
  appliedPriority.value = priorityInput.value
  appliedOwnerId.value = ownerInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  priorityInput.value = undefined
  ownerInput.value = undefined
  appliedKeyword.value = ''
  appliedStatus.value = undefined
  appliedPriority.value = undefined
  appliedOwnerId.value = undefined
  page.value = 1
  void fetchList()
}

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

function onCreate(): void {
  void router.push({ name: 'serviceTicketCreate' })
}

/** 编辑经表单页承载（编辑态由查询参数 id 表达，见 ServiceTicketFormPage） */
function onEdit(row: ServiceTicketListItem): void {
  void router.push({ name: 'serviceTicketCreate', query: { id: row.id } })
}

function onDetail(row: ServiceTicketListItem): void {
  void router.push({ name: 'serviceTicketDetail', params: { id: row.id } })
}

/** 按状态派生可用动作：查看 + 编辑（已关闭锁定）+ 白名单内的状态流转（design.md §0.1） */
function rowActions(row: ServiceTicketListItem): TicketRowAction[] {
  const actions: TicketRowAction[] = [
    { key: 'detail', label: '查看', icon: IconEye },
  ]

  if (row.status !== CLOSED_STATUS && auth.hasPermission('serviceTickets.update')) {
    actions.push({ key: 'edit', label: '编辑', icon: IconEdit })
  }

  if (row.status !== CLOSED_STATUS && auth.hasPermission('serviceTickets.status')) {
    const transitions = allowedTicketTransitions(row.status)
    for (const target of TRANSITION_ORDER) {
      if (!transitions.includes(target)) continue
      const label = ticketTransitionLabel(row.status, target)
      actions.push({
        key: `status-${target}`,
        label,
        icon: TRANSITION_ICONS[label] ?? IconPlayerPlay,
        target,
        status: target === 2 ? 'success' : target === 3 ? 'danger' : undefined,
        confirm: target === 3,
      })
    }
  }

  return actions
}

/** 平铺动作（前 3 个） */
function flatActions(row: ServiceTicketListItem): TicketRowAction[] {
  return rowActions(row).slice(0, 3)
}

/** 收纳进「更多」的动作 */
function moreActions(row: ServiceTicketListItem): TicketRowAction[] {
  return rowActions(row).slice(3)
}

function onAction(row: ServiceTicketListItem, action: TicketRowAction): void {
  if (action.key === 'detail') {
    onDetail(row)
    return
  }

  if (action.key === 'edit') {
    onEdit(row)
    return
  }

  if (action.confirm) {
    confirmStatusChange(row, action)
    return
  }

  void changeStatus(row, action)
}

/** 关闭工单为终态（不可再改状态）：二次确认后执行 */
function confirmStatusChange(row: ServiceTicketListItem, action: TicketRowAction): void {
  Modal.warning({
    title: `${action.label}工单`,
    content: `确认${action.label}工单 ${row.ticketNo}？关闭后为终态，不可再编辑或改状态`,
    hideCancel: false,
    okText: `确认${action.label}`,
    onOk: () => changeStatus(row, action),
  })
}

/** 状态流转（白名单由后端兜底，非法组合返回 40172） */
async function changeStatus(row: ServiceTicketListItem, action: TicketRowAction): Promise<void> {
  if (action.target === undefined || statusingId.value) return
  statusingId.value = row.id
  try {
    await updateServiceTicketStatus(row.id, action.target)
    Message.success(`工单已${action.label}`)
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理（40172 非法流转 / 已关闭终态）
  } finally {
    statusingId.value = undefined
  }
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        服务工单
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="6">
            <a-input
              v-model="keywordInput"
              class="filter-bar__search"
              placeholder="搜索工单号 / 客户 / 标题"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              :options="statusOptions"
              placeholder="状态"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="priorityInput"
              :options="priorityOptions"
              placeholder="优先级"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="ownerInput"
              :options="ownerOptions"
              placeholder="负责人"
              allow-clear
              allow-search
            />
          </a-col>
          <a-col :span="6">
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

        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('serviceTickets.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建工单
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
        <template #priority="{ record }">
          <a-tag :color="ticketPriorityColor((record as ServiceTicketListItem).priority)">
            {{ ticketPriorityLabel((record as ServiceTicketListItem).priority) }}
          </a-tag>
        </template>
        <template #status="{ record }">
          <a-tag :color="ticketStatusColor((record as ServiceTicketListItem).status)">
            {{ ticketStatusLabel((record as ServiceTicketListItem).status) }}
          </a-tag>
        </template>
        <template #owner="{ record }">
          {{ (record as ServiceTicketListItem).ownerName ?? '—' }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as ServiceTicketListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-for="action in flatActions(record as ServiceTicketListItem)"
              :key="action.key"
              type="text"
              size="small"
              :status="action.status"
              :loading="statusingId === (record as ServiceTicketListItem).id && action.target !== undefined"
              @click="onAction(record as ServiceTicketListItem, action)"
            >
              <template #icon>
                <component :is="action.icon" />
              </template>
              {{ action.label }}
            </a-button>

            <a-dropdown
              v-if="moreActions(record as ServiceTicketListItem).length > 0"
              trigger="click"
            >
              <a-button
                type="text"
                size="small"
                aria-label="更多操作"
                :loading="statusingId === (record as ServiceTicketListItem).id"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  v-for="action in moreActions(record as ServiceTicketListItem)"
                  :key="action.key"
                  :disabled="!!statusingId"
                  @click="onAction(record as ServiceTicketListItem, action)"
                >
                  <template #icon>
                    <component :is="action.icon" />
                  </template>
                  {{ action.label }}
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

.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  LEAD_SOURCE_LABELS,
  LEAD_SOURCE_OPTIONS,
  LEAD_STATUS_OPTIONS,
  convertLead,
  getLeads,
  leadStatusColor,
  leadStatusLabel,
  updateLeadStatus,
} from '@/api/lead'
import type { LeadListItem, LeadSource, LeadStatus } from '@/api/lead'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconArrowForwardUp,
  IconBan,
  IconDotsVertical,
  IconEdit,
  IconEye,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
} from '@tabler/icons-vue'

import LeadFormDrawer from './LeadFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 终态（已转化 / 已废弃）：不可编辑 / 转商机 / 废弃（design.md §0.1） */
const TERMINAL_STATUSES: LeadStatus[] = [2, 3]

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 正在转商机的线索 id（前端规则 §4.6：convertingId） */
const convertingId = ref<string | undefined>(undefined)
/** 正在改状态的线索 id（前端规则 §4.6：updatingId） */
const updatingId = ref<string | undefined>(undefined)
const items = ref<LeadListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 抽屉（新增 / 编辑共用） */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

/** 关键词 / 来源 / 状态 / 负责人：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const sourceInput = ref<LeadSource | undefined>(undefined)
const statusInput = ref<LeadStatus | undefined>(undefined)
const ownerInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedSource = ref<LeadSource | undefined>(undefined)
const appliedStatus = ref<LeadStatus | undefined>(undefined)
const appliedOwnerId = ref<string | undefined>(undefined)

/** 负责人下拉数据源（启用员工，一次取前 100 条） */
const employees = ref<Employee[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedSource.value ?? ''}|${appliedStatus.value ?? ''}|${appliedOwnerId.value ?? ''}`,
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

const sourceOptions = LEAD_SOURCE_OPTIONS
const statusOptions = LEAD_STATUS_OPTIONS

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 表格列（每列必设宽度，specs/006-list-showcase §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '线索号', dataIndex: 'leadNo', width: 160 },
  { title: '名称', dataIndex: 'name', width: 180, ellipsis: true, tooltip: true },
  { title: '联系人', dataIndex: 'contact', width: 110, ellipsis: true, tooltip: true },
  { title: '电话', dataIndex: 'phone', width: 130 },
  { title: '来源', slotName: 'source', width: 90 },
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
    const result = await getLeads({
      keyword: appliedKeyword.value.trim() || undefined,
      source: appliedSource.value,
      status: appliedStatus.value,
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
  appliedSource.value = sourceInput.value
  appliedStatus.value = statusInput.value
  appliedOwnerId.value = ownerInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  sourceInput.value = undefined
  statusInput.value = undefined
  ownerInput.value = undefined
  appliedKeyword.value = ''
  appliedSource.value = undefined
  appliedStatus.value = undefined
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
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

function onEdit(row: LeadListItem): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

function onDetail(row: LeadListItem): void {
  void router.push({ name: 'leadDetail', params: { id: row.id } })
}

/** 终态线索锁定：不可编辑 / 转商机 / 废弃 */
function isTerminal(row: LeadListItem): boolean {
  return TERMINAL_STATUSES.includes(row.status)
}

/** 转商机：一次性整转，成功后线索置「已转化」并生成商机 */
async function onConvert(row: LeadListItem): Promise<void> {
  if (convertingId.value) return
  convertingId.value = row.id
  try {
    const result = await convertLead(row.id)
    Message.success(`已转商机 ${result.opportunityNo}`)
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理（40168 终态线索）
  } finally {
    convertingId.value = undefined
  }
}

/** 转单不可逆（线索锁定），列表内先二次确认再执行 */
function confirmConvert(row: LeadListItem): void {
  Modal.warning({
    title: '转商机',
    content: `确认将线索 ${row.leadNo} 转为商机？转商机后线索置「已转化」，不可再编辑 / 转商机 / 废弃`,
    hideCancel: false,
    okText: '确认转商机',
    onOk: () => onConvert(row),
  })
}

/** 废弃线索：仅改状态不删数据（无库存 / 资金影响） */
async function onAbandon(row: LeadListItem): Promise<void> {
  if (updatingId.value) return
  updatingId.value = row.id
  try {
    await updateLeadStatus(row.id, 3)
    Message.success('线索已废弃')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理（40168 终态线索）
  } finally {
    updatingId.value = undefined
  }
}

/** 「更多」内的废弃无法用 a-popconfirm 包裹菜单项，改用函数式确认框（等价二次确认，specs/011-action-column §0） */
function confirmAbandon(row: LeadListItem): void {
  Modal.warning({
    title: '废弃线索',
    content: `确认废弃线索 ${row.leadNo}？废弃后不可恢复`,
    hideCancel: false,
    okText: '确认废弃',
    onOk: () => onAbandon(row),
  })
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        线索
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
              placeholder="搜索线索号 / 名称 / 联系人"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="sourceInput"
              :options="sourceOptions"
              placeholder="来源"
              allow-clear
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
              v-if="auth.hasPermission('leads.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建线索
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
        <template #source="{ record }">
          {{ LEAD_SOURCE_LABELS[(record as LeadListItem).source] }}
        </template>
        <template #status="{ record }">
          <a-tag :color="leadStatusColor((record as LeadListItem).status)">
            {{ leadStatusLabel((record as LeadListItem).status) }}
          </a-tag>
        </template>
        <template #owner="{ record }">
          {{ (record as LeadListItem).ownerName ?? '—' }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as LeadListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as LeadListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              查看
            </a-button>

            <a-button
              v-if="!isTerminal(record as LeadListItem) && auth.hasPermission('leads.update')"
              type="text"
              size="small"
              @click="onEdit(record as LeadListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>

            <a-button
              v-if="!isTerminal(record as LeadListItem) && auth.hasPermission('leads.convert')"
              type="text"
              size="small"
              status="success"
              :loading="convertingId === (record as LeadListItem).id"
              @click="confirmConvert(record as LeadListItem)"
            >
              <template #icon>
                <IconArrowForwardUp />
              </template>
              转商机
            </a-button>

            <a-dropdown
              v-if="!isTerminal(record as LeadListItem) && auth.hasPermission('leads.status')"
              trigger="click"
            >
              <a-button
                type="text"
                size="small"
                aria-label="更多操作"
                :loading="updatingId === (record as LeadListItem).id"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  :disabled="!!updatingId"
                  @click="confirmAbandon(record as LeadListItem)"
                >
                  <template #icon>
                    <IconBan />
                  </template>
                  废弃
                </a-doption>
              </template>
            </a-dropdown>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <LeadFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      @saved="fetchList"
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

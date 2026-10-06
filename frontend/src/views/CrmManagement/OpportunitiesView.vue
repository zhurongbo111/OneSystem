<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  OPPORTUNITY_STAGE_OPTIONS,
  getOpportunities,
  opportunityStageColor,
  opportunityStageLabel,
} from '@/api/opportunity'
import type { OpportunityListItem, OpportunityStage } from '@/api/opportunity'
import { useAuthStore } from '@/stores/auth'
import { formatDate, formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconEdit, IconEye, IconPlus, IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

const auth = useAuthStore()

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
const items = ref<OpportunityListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 阶段 / 负责人：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const stageInput = ref<OpportunityStage | undefined>(undefined)
const ownerInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedStage = ref<OpportunityStage | undefined>(undefined)
const appliedOwnerId = ref<string | undefined>(undefined)

/** 负责人下拉数据源（启用员工，一次取前 100 条） */
const employees = ref<Employee[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedStage.value ?? ''}|${appliedOwnerId.value ?? ''}`,
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

const stageOptions = OPPORTUNITY_STAGE_OPTIONS

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 表格列（每列必设宽度，specs/006-list-showcase §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商机号', dataIndex: 'opportunityNo', width: 160 },
  { title: '名称', dataIndex: 'name', width: 180, ellipsis: true, tooltip: true },
  { title: '客户', slotName: 'partner', width: 160, ellipsis: true, tooltip: true },
  { title: '预计金额', slotName: 'amount', width: 130, align: 'right' },
  { title: '阶段', slotName: 'stage', width: 110, align: 'center' },
  { title: '预计成交日期', slotName: 'expectedCloseDate', width: 130 },
  { title: '负责人', slotName: 'owner', width: 110 },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  { title: '操作', slotName: 'action', width: 180, bodyCellClass: 'action-cell' },
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
    const result = await getOpportunities({
      keyword: appliedKeyword.value.trim() || undefined,
      stage: appliedStage.value,
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
  appliedStage.value = stageInput.value
  appliedOwnerId.value = ownerInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  stageInput.value = undefined
  ownerInput.value = undefined
  appliedKeyword.value = ''
  appliedStage.value = undefined
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
  void router.push({ name: 'opportunityCreate' })
}

function onEdit(row: OpportunityListItem): void {
  void router.push({ name: 'opportunityEdit', params: { id: row.id } })
}

function onDetail(row: OpportunityListItem): void {
  void router.push({ name: 'opportunityDetail', params: { id: row.id } })
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        商机
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
              placeholder="搜索商机号 / 名称"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="stageInput"
              :options="stageOptions"
              placeholder="阶段"
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
              v-if="auth.hasPermission('opportunities.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建商机
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
        <template #partner="{ record }">
          {{ (record as OpportunityListItem).partnerName ?? '—' }}
        </template>
        <template #amount="{ record }">
          <span class="amount">
            ¥ {{ (record as OpportunityListItem).amount.toFixed(2) }}
          </span>
        </template>
        <template #stage="{ record }">
          <a-tag :color="opportunityStageColor((record as OpportunityListItem).stage)">
            {{ opportunityStageLabel((record as OpportunityListItem).stage) }}
          </a-tag>
        </template>
        <template #expectedCloseDate="{ record }">
          {{ (record as OpportunityListItem).expectedCloseDate ? formatDate((record as OpportunityListItem).expectedCloseDate) : '—' }}
        </template>
        <template #owner="{ record }">
          {{ (record as OpportunityListItem).ownerName ?? '—' }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as OpportunityListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as OpportunityListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              查看
            </a-button>

            <a-button
              v-if="auth.hasPermission('opportunities.update')"
              type="text"
              size="small"
              @click="onEdit(record as OpportunityListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
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

.amount {
  font-variant-numeric: tabular-nums;
}

.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import {
  APPROVAL_ORDER_TYPE_META,
  APPROVAL_ORDER_TYPE_OPTIONS,
  APPROVAL_STATUS_META,
  getApprovals,
} from '@/api/approval'
import type { ApprovalListItem, ApprovalOrderType, ApprovalStatus } from '@/api/approval'
import { getUsers } from '@/api/user'
import type { UserListItem } from '@/api/user'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconAdjustments,
  IconCheck,
  IconEye,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

import ApprovalDecideDrawer from './ApprovalDecideDrawer.vue'
import ApprovalRulesDrawer from './ApprovalRulesDrawer.vue'

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 可选列（序号与操作列固定显示，不参与列设置：specs/011-action-column §5） */
const columnOptions = [
  { label: '单据类型', value: 'orderType' },
  { label: '单据号', value: 'orderNo' },
  { label: '往来单位', value: 'partnerName' },
  { label: '金额', value: 'amount' },
  { label: '提交人', value: 'submittedBy' },
  { label: '提交时间', value: 'submittedAt' },
  { label: '状态', value: 'status' },
]

// —— helpers ——
/**
 * 把页面选择的本地日期范围转换为后端所需的 UTC ISO 闭区间：
 * 起始取当天本地 00:00:00、结束取当天本地 23:59:59.999，再转 UTC（同单据域约定）。
 */
function toApprovalDateRange(
  startDate?: string,
  endDate?: string,
): { start?: string; end?: string } {
  const start = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : undefined
  const end = endDate ? new Date(`${endDate}T23:59:59.999`).toISOString() : undefined
  return { start, end }
}

// —— stores/composables ——
const auth = useAuthStore()

// —— reactive state ——
const loading = ref(false)
const items = ref<ApprovalListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 当前 tab：「待我审批」（默认，status = 1）/「全部」（不限状态） */
const activeTab = ref('pending')

/** 单据类型 / 提交人 / 提交时间：输入态与已应用态分离（点搜索才生效） */
const orderTypeInput = ref<ApprovalOrderType | undefined>(undefined)
const submittedByInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedOrderType = ref<ApprovalOrderType | undefined>(undefined)
const appliedSubmittedBy = ref<string | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

/** 提交人下拉数据源（依赖 users.view 权限，无权限时禁用并跳过请求） */
const users = ref<UserListItem[]>([])

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'orderType',
  'orderNo',
  'partnerName',
  'amount',
  'submittedBy',
  'submittedAt',
  'status',
])

/** 审批抽屉：可见性 + 当前审批记录 id */
const decideVisible = ref(false)
const decideId = ref('')

/** 审批规则抽屉可见性 */
const rulesVisible = ref(false)

// —— computed ——
/** 当前 tab 对应的审批状态筛选（待我审批 = 待审批；全部 = 不限） */
const statusFilter = computed<ApprovalStatus | undefined>(() => (activeTab.value === 'pending' ? 1 : undefined))

/** 是否可选提交人（依赖用户列表权限；与操作日志页同一取舍） */
const canPickSubmitter = computed(() => auth.hasPermission('users.view'))

/** 提交人下拉选项 */
const submitterOptions = computed(() => users.value.map((u) => ({ label: u.displayName, value: u.id })))

/** 表格重挂载 key：已应用条件或 tab 变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${activeTab.value}|${appliedOrderType.value ?? ''}|${appliedSubmittedBy.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('orderType')) {
    cols.push({ title: '单据类型', slotName: 'orderType', width: 120, align: 'center' })
  }
  if (visibleColumns.value.includes('orderNo')) {
    cols.push({ title: '单据号', slotName: 'orderNo', width: 160 })
  }
  if (visibleColumns.value.includes('partnerName')) {
    cols.push({ title: '往来单位', dataIndex: 'partnerName', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('amount')) {
    cols.push({ title: '金额', slotName: 'amount', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('submittedBy')) {
    cols.push({ title: '提交人', slotName: 'submittedBy', width: 120 })
  }
  if (visibleColumns.value.includes('submittedAt')) {
    cols.push({ title: '提交时间', slotName: 'submittedAt', width: 172 })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', slotName: 'status', width: 100, align: 'center' })
  }
  // 操作列（specs/011-action-column §0）：2 个操作（审批 / 详情），全部平铺
  cols.push({ title: '操作', slotName: 'action', width: 180, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §2 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchSubmitters()
})

// —— methods ——
/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = appliedRange.value
      ? toApprovalDateRange(appliedRange.value[0], appliedRange.value[1])
      : {}
    const result = await getApprovals({
      status: statusFilter.value,
      orderType: appliedOrderType.value,
      submittedBy: appliedSubmittedBy.value,
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

/** 加载提交人下拉（无 users.view 权限时跳过，避免无谓的 40300 提示） */
async function fetchSubmitters(): Promise<void> {
  if (!canPickSubmitter.value) return
  try {
    const result = await getUsers({ page: 1, pageSize: 100 })
    users.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 切换 tab：重置筛选与页码（「待我审批」与「全部」条件不互通） */
function onTabChange(): void {
  orderTypeInput.value = undefined
  submittedByInput.value = undefined
  dateRangeInput.value = undefined
  appliedOrderType.value = undefined
  appliedSubmittedBy.value = undefined
  appliedRange.value = null
  page.value = 1
  void fetchList()
}

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedOrderType.value = orderTypeInput.value
  appliedSubmittedBy.value = submittedByInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  orderTypeInput.value = undefined
  submittedByInput.value = undefined
  dateRangeInput.value = undefined
  appliedOrderType.value = undefined
  appliedSubmittedBy.value = undefined
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

/** 打开审批抽屉（待审批单据点「审批」，其余点「详情」） */
function onDecide(row: ApprovalListItem): void {
  decideId.value = row.id
  decideVisible.value = true
}

/** 打开审批规则抽屉（同步动作，不置 loading） */
function onOpenRules(): void {
  rulesVisible.value = true
}

/** 审批完成（成功）：关闭抽屉并刷新列表 */
function onDecided(): void {
  decideVisible.value = false
  void fetchList()
}

/** 审批失败（如库存不足）：保留抽屉并刷新列表（后端错误已统一提示） */
function onDecideFailed(): void {
  void fetchList()
}

/** 规则保存成功：关闭抽屉并刷新列表（规则变化会影响后续单据，不影响已有审批记录） */
function onRulesSaved(): void {
  rulesVisible.value = false
  Message.success('审批规则已保存')
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（主操作已并入表格上方工具条左组） -->
    <div class="page-header">
      <h1 class="page-title">
        单据审批
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <a-tabs
        v-model:active-key="activeTab"
        class="approval-tabs"
        @change="onTabChange"
      >
        <a-tab-pane
          key="pending"
          title="待我审批"
        />
        <a-tab-pane
          key="all"
          title="全部"
        />
      </a-tabs>

      <div class="toolbar">
        <!-- 筛选行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="5">
            <a-select
              v-model="orderTypeInput"
              :options="APPROVAL_ORDER_TYPE_OPTIONS"
              placeholder="全部单据类型"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="submittedByInput"
              :options="submitterOptions"
              placeholder="全部提交人"
              allow-clear
              allow-search
              :loading="canPickSubmitter && users.length === 0"
              :disabled="!canPickSubmitter"
            />
          </a-col>
          <a-col :span="6">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              :allow-clear="true"
              style="width: 100%"
            />
          </a-col>
          <a-col :span="8">
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

        <!-- 操作行：左组主操作（审批规则）靠左，右组视图操作（列设置 / 刷新）靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('approvals.rules')"
              size="small"
              @click="onOpenRules"
            >
              <template #icon>
                <IconAdjustments />
              </template>
              审批规则
            </a-button>
          </div>
          <div class="toolbar-actions__right">
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
        <template #orderType="{ record }">
          <a-tag :color="APPROVAL_ORDER_TYPE_META[(record as ApprovalListItem).orderType].color">
            {{ APPROVAL_ORDER_TYPE_META[(record as ApprovalListItem).orderType].label }}
          </a-tag>
        </template>
        <template #orderNo="{ record }">
          {{ (record as ApprovalListItem).orderNo }}
        </template>
        <template #amount="{ record }">
          <span class="amount">¥ {{ (record as ApprovalListItem).amount.toFixed(2) }}</span>
        </template>
        <template #submittedBy="{ record }">
          {{ (record as ApprovalListItem).submittedByName }}
        </template>
        <template #submittedAt="{ record }">
          {{ formatDateTime((record as ApprovalListItem).submittedAt) }}
        </template>
        <template #status="{ record }">
          <a-tag :color="APPROVAL_STATUS_META[(record as ApprovalListItem).status].color">
            {{ APPROVAL_STATUS_META[(record as ApprovalListItem).status].label }}
          </a-tag>
        </template>
        <!-- 操作列（specs/011-action-column §0）：待审批显示「审批」（主操作），「详情」恒显 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="(record as ApprovalListItem).status === 1"
              type="text"
              size="small"
              @click="onDecide(record as ApprovalListItem)"
            >
              <template #icon>
                <IconCheck />
              </template>
              审批
            </a-button>
            <a-button
              type="text"
              size="small"
              @click="onDecide(record as ApprovalListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <ApprovalDecideDrawer
      v-model:visible="decideVisible"
      :approval-id="decideId"
      @decided="onDecided"
      @failed="onDecideFailed"
    />
    <ApprovalRulesDrawer
      v-model:visible="rulesVisible"
      @saved="onRulesSaved"
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

.approval-tabs {
  margin-bottom: 4px;
}

.approval-tabs :deep(.arco-tabs-nav)::before {
  display: none;
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

/* 列设置下拉面板 */
.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}

/* 操作列密度：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

/* 操作列兜底：按钮组不折行 */
:deep(.action-cell) {
  white-space: nowrap;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  getReconciliation,
  getUnsettledOrders,
  type PartnerType,
  type ReconciliationListItem,
  type SettlementCandidate,
} from '@/api/settlement'
import { formatDateTime } from '@/utils/datetime'
import { settlementOrderTypeLabel, settlementOrderTypeRouteName } from '@/utils/settlement'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconListDetails,
  IconRefresh,
  IconRestore,
  IconSearch,
} from '@tabler/icons-vue'

// —— constants ——
/** 往来类型下拉（1 供应商 / 2 客户 / 3 两者） */
const partnerTypeOptions: { label: string; value: PartnerType }[] = [
  { label: '供应商', value: 1 },
  { label: '客户', value: 2 },
  { label: '两者', value: 3 },
]

/** 往来类型文案 */
const PARTNER_TYPE_LABELS: Record<PartnerType, string> = { 1: '供应商', 2: '客户', 3: '两者' }

/** 未结单据抽屉表格列（只读；单号为超链接 → 对应单据详情） */
const unsettledColumns: TableColumnData[] = [
  { title: '单据类型', slotName: 'orderType', width: 120 },
  { title: '单号', slotName: 'orderNo', width: 180 },
  { title: '单据日期', slotName: 'orderDate', width: 110 },
  { title: '单据总额', slotName: 'totalAmount', width: 120, align: 'right' },
  { title: '已结金额', slotName: 'settledAmount', width: 120, align: 'right' },
  { title: '未结金额', slotName: 'unsettledAmount', width: 120, align: 'right' },
  // 到期日（036 §4.4）：由后端按「单据日期 + 账期」推导，前端不做日期加减
  { title: '到期日', slotName: 'dueDate', width: 110, align: 'center' },
]

// —— stores/composables ——
const router = useRouter()

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<ReconciliationListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 类型 / 仅看逾期：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const typeInput = ref<PartnerType | undefined>(undefined)
const overdueOnlyInput = ref(false)
const appliedKeyword = ref('')
const appliedType = ref<PartnerType | undefined>(undefined)
const appliedOverdueOnly = ref(false)

/** 未结单据抽屉 */
const drawerVisible = ref(false)
const candidatesLoading = ref(false)
const drawerPartnerName = ref('')
/** 应收未结单据（收款方向：销售出库单 + 采购退货单） */
const receivableOrders = ref<SettlementCandidate[]>([])
/** 应付未结单据（付款方向：采购入库单 + 销售退货单） */
const payableOrders = ref<SettlementCandidate[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedType.value ?? ''}|${appliedOverdueOnly.value}`,
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

/** 表格列 */
const columns = computed<TableColumnData[]>(() => [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '往来单位', dataIndex: 'partnerName', width: 220, ellipsis: true, tooltip: true },
  { title: '类型', slotName: 'partnerType', width: 100, align: 'center' },
  { title: '应收余额', slotName: 'receivableAmount', width: 140, align: 'right' },
  { title: '应付余额', slotName: 'payableAmount', width: 140, align: 'right' },
  { title: '未结单据数', slotName: 'unsettledOrderCount', width: 120, align: 'right' },
  // 账期与逾期（036 §4.4）：到期日与逾期天数由后端推导，前端不做日期加减
  { title: '账期（天）', slotName: 'paymentTermDays', width: 110, align: 'right' },
  { title: '最早到期日', slotName: 'earliestDueDate', width: 130, align: 'center' },
  { title: '最大逾期天数', slotName: 'maxOverdueDays', width: 130, align: 'right' },
  { title: '操作', slotName: 'action', width: 130, bodyCellClass: 'action-cell' },
])

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
})

// —— methods ——
/** 拉取当前条件下的往来台账（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getReconciliation({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
      overdueOnly: appliedOverdueOnly.value ? true : undefined,
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
  appliedType.value = typeInput.value
  appliedOverdueOnly.value = overdueOnlyInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  typeInput.value = undefined
  overdueOnlyInput.value = false
  appliedKeyword.value = ''
  appliedType.value = undefined
  appliedOverdueOnly.value = false
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

/** 打开未结单据抽屉：分别按收款 / 付款方向查询该往来的未结单据 */
async function onOpenUnsettled(row: ReconciliationListItem): Promise<void> {
  drawerPartnerName.value = row.partnerName
  drawerVisible.value = true
  candidatesLoading.value = true
  receivableOrders.value = []
  payableOrders.value = []
  try {
    const [receipt, payment] = await Promise.all([
      getUnsettledOrders({ partnerId: row.partnerId, type: 0, page: 1, pageSize: 100 }),
      getUnsettledOrders({ partnerId: row.partnerId, type: 1, page: 1, pageSize: 100 }),
    ])
    receivableOrders.value = receipt.items
    payableOrders.value = payment.items
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    candidatesLoading.value = false
  }
}

/** 未结单据详情路径（单号超链接 href；未知单据类型返回空串） */
function orderHref(candidate: SettlementCandidate): string {
  const routeName = settlementOrderTypeRouteName(candidate.orderType)
  return routeName ? router.resolve({ name: routeName, params: { id: candidate.orderId } }).href : ''
}

/** 打开未结单据详情（同步路由跳转不置 loading） */
function onOrderDetail(candidate: SettlementCandidate): void {
  const routeName = settlementOrderTypeRouteName(candidate.orderType)
  if (routeName) void router.push({ name: routeName, params: { id: candidate.orderId } })
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        往来对账
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
              class="filter-bar__search"
              placeholder="搜索往来名称"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="typeInput"
              :options="partnerTypeOptions"
              placeholder="全部类型"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-checkbox
              v-model="overdueOnlyInput"
              class="filter-bar__overdue"
            >
              仅看逾期
            </a-checkbox>
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

        <!-- 操作行：仅右组视图操作（刷新） -->
        <div class="toolbar-actions">
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
        row-key="partnerId"
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
        <template #partnerType="{ record }">
          {{ PARTNER_TYPE_LABELS[(record as ReconciliationListItem).partnerType] }}
        </template>
        <template #receivableAmount="{ record }">
          <span class="amount">¥ {{ (record as ReconciliationListItem).receivableAmount.toFixed(2) }}</span>
        </template>
        <template #payableAmount="{ record }">
          <span class="amount">¥ {{ (record as ReconciliationListItem).payableAmount.toFixed(2) }}</span>
        </template>
        <template #unsettledOrderCount="{ record }">
          {{ (record as ReconciliationListItem).unsettledOrderCount }}
        </template>
        <template #paymentTermDays="{ record }">
          {{ (record as ReconciliationListItem).paymentTermDays }}
        </template>
        <template #earliestDueDate="{ record }">
          {{ (record as ReconciliationListItem).earliestDueDate ?? '-' }}
        </template>
        <!-- 逾期天数 > 0 标红 -->
        <template #maxOverdueDays="{ record }">
          <span :class="(record as ReconciliationListItem).maxOverdueDays > 0 ? 'overdue' : 'amount'">
            {{ (record as ReconciliationListItem).maxOverdueDays }}
          </span>
        </template>
        <template #action="{ record }">
          <a-button
            type="text"
            size="small"
            @click="onOpenUnsettled(record as ReconciliationListItem)"
          >
            <template #icon>
              <IconListDetails />
            </template>
            未结单据
          </a-button>
        </template>
      </a-table>
    </a-card>

    <!-- 未结单据抽屉：应收 / 应付方向分别只读展示（不下钻编辑） -->
    <a-drawer
      v-model:visible="drawerVisible"
      :title="`未结单据 — ${drawerPartnerName}`"
      :width="760"
      :footer="false"
    >
      <div
        v-loading="candidatesLoading"
        class="drawer-body"
      >
        <a-divider orientation="left">
          应收未结单据（收款方向）
        </a-divider>
        <a-table
          row-key="orderNo"
          size="small"
          :columns="unsettledColumns"
          :data="receivableOrders"
          :pagination="false"
        >
          <template #orderType="{ record }">
            {{ settlementOrderTypeLabel((record as SettlementCandidate).orderType) }}
          </template>
          <template #orderNo="{ record }">
            <a-link
              :href="orderHref(record as SettlementCandidate)"
              @click.prevent="onOrderDetail(record as SettlementCandidate)"
            >
              {{ (record as SettlementCandidate).orderNo }}
            </a-link>
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as SettlementCandidate).orderDate).slice(0, 10) }}
          </template>
          <template #totalAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).totalAmount.toFixed(2) }}
          </template>
          <template #settledAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).settledAmount.toFixed(2) }}
          </template>
          <template #unsettledAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).unsettledAmount.toFixed(2) }}
          </template>
          <template #dueDate="{ record }">
            {{ (record as SettlementCandidate).dueDate }}
          </template>
        </a-table>
        <a-empty
          v-if="!candidatesLoading && receivableOrders.length === 0"
          description="无应收未结单据"
        />

        <a-divider orientation="left">
          应付未结单据（付款方向）
        </a-divider>
        <a-table
          row-key="orderNo"
          size="small"
          :columns="unsettledColumns"
          :data="payableOrders"
          :pagination="false"
        >
          <template #orderType="{ record }">
            {{ settlementOrderTypeLabel((record as SettlementCandidate).orderType) }}
          </template>
          <template #orderNo="{ record }">
            <a-link
              :href="orderHref(record as SettlementCandidate)"
              @click.prevent="onOrderDetail(record as SettlementCandidate)"
            >
              {{ (record as SettlementCandidate).orderNo }}
            </a-link>
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as SettlementCandidate).orderDate).slice(0, 10) }}
          </template>
          <template #totalAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).totalAmount.toFixed(2) }}
          </template>
          <template #settledAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).settledAmount.toFixed(2) }}
          </template>
          <template #unsettledAmount="{ record }">
            ¥ {{ (record as SettlementCandidate).unsettledAmount.toFixed(2) }}
          </template>
          <template #dueDate="{ record }">
            {{ (record as SettlementCandidate).dueDate }}
          </template>
        </a-table>
        <a-empty
          v-if="!candidatesLoading && payableOrders.length === 0"
          description="无应付未结单据"
        />
      </div>
    </a-drawer>
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
  justify-content: flex-end;
  gap: 8px;
  margin-bottom: 8px;
}

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

/* 逾期标红（036 §4.4） */
.overdue {
  color: rgb(var(--red-6));
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.drawer-body {
  min-height: 200px;
}

/* 操作列兜底：按钮组不折行 */
:deep(.action-cell) {
  white-space: nowrap;
}
</style>

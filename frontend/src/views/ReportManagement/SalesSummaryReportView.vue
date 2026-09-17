<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { getSalesSummary, toReportRangeUtc } from '@/api/report'
import type { SalesSummaryItem, SalesSummaryTotal, SummaryGroupBy } from '@/api/report'
import { toDateInput } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

// —— constants ——
function defaultRange(): string[] {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth(), 1)
  return [toDateInput(first), toDateInput(now)]
}

const groupByOptions = [
  { label: '往来单位', value: 'partner' },
  { label: '商品', value: 'product' },
]

let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<SalesSummaryItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const summary = ref<SalesSummaryTotal>({
  orderCount: 0,
  outboundQuantity: 0,
  outboundAmount: 0,
  returnQuantity: 0,
  returnAmount: 0,
  netQuantity: 0,
  netAmount: 0,
})

const rangeInput = ref<string[]>(defaultRange())
const partnerIdInput = ref<string | undefined>(undefined)
const groupByInput = ref<SummaryGroupBy>('partner')

const appliedRange = ref<string[]>(defaultRange())
const appliedPartnerId = ref<string | undefined>(undefined)
const appliedGroupBy = ref<SummaryGroupBy>('partner')

const partnerOptions = ref<Partner[]>([])

// —— computed ——
const tableKey = computed(
  () => `${appliedRange.value.join('~')}|${appliedPartnerId.value ?? ''}|${appliedGroupBy.value}`,
)

const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

const columns = computed<TableColumnData[]>(() => {
  const nameTitle = appliedGroupBy.value === 'product' ? '商品' : '客户'
  const cols: TableColumnData[] = [
    { title: '序号', slotName: 'seq', width: 64, align: 'center' },
    { title: nameTitle, dataIndex: 'name', width: 180, ellipsis: true, tooltip: true },
  ]
  if (appliedGroupBy.value === 'product') {
    cols.push({ title: '单位', dataIndex: 'unit', width: 80, align: 'center' })
  }
  cols.push(
    { title: '出库单数', dataIndex: 'orderCount', width: 100, align: 'right' },
    { title: '出库数量', dataIndex: 'outboundQuantity', width: 110, align: 'right' },
    { title: '出库金额', slotName: 'outboundAmount', width: 130, align: 'right' },
    { title: '退货数量', dataIndex: 'returnQuantity', width: 110, align: 'right' },
    { title: '退货金额', slotName: 'returnAmount', width: 130, align: 'right' },
    { title: '净数量', slotName: 'netQuantity', width: 110, align: 'right' },
    { title: '净金额', slotName: 'netAmount', width: 140, align: 'right' },
  )
  return cols
})

const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchPartners()
  void fetchList()
})

// —— methods ——
async function fetchPartners(): Promise<void> {
  try {
    const result = await getPartners({ type: 2, status: 1, page: 1, pageSize: 100 })
    partnerOptions.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = toReportRangeUtc(appliedRange.value[0], appliedRange.value[1])
    const result = await getSalesSummary({
      start,
      end,
      partnerId: appliedPartnerId.value,
      groupBy: appliedGroupBy.value,
      page: page.value,
      pageSize: pageSize.value,
    })
    if (seq !== fetchSeq) return
    items.value = result.items
    total.value = result.total
    summary.value = result.summary
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

function onSearch(): void {
  if (rangeInput.value.length !== 2) {
    Message.warning('请选择查询期间')
    return
  }
  appliedRange.value = [...rangeInput.value]
  appliedPartnerId.value = partnerIdInput.value
  appliedGroupBy.value = groupByInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  rangeInput.value = defaultRange()
  partnerIdInput.value = undefined
  groupByInput.value = 'partner'
  appliedRange.value = defaultRange()
  appliedPartnerId.value = undefined
  appliedGroupBy.value = 'partner'
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

function formatMoney(value: number): string {
  return value.toFixed(2)
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        销售汇总
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
          <a-col :span="8">
            <a-range-picker
              v-model="rangeInput"
              class="filter-bar__range"
              value-format="YYYY-MM-DD"
              :allow-clear="false"
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="partnerIdInput"
              class="filter-bar__partner"
              :options="partnerOptions.map((p) => ({ label: p.name, value: p.id }))"
              placeholder="全部客户"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <a-radio-group
              v-model="groupByInput"
              type="button"
              :options="groupByOptions"
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

      <a-row
        class="summary-bar"
        :gutter="16"
      >
        <a-col :span="4">
          <a-statistic
            title="出库单数"
            :value="summary.orderCount"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="出库数量"
            :value="summary.outboundQuantity"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="出库金额"
            :value="summary.outboundAmount"
            :precision="2"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="退货金额"
            :value="summary.returnAmount"
            :precision="2"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="净额"
            :value="summary.netAmount"
            :precision="2"
            :value-style="{ fontWeight: 600 }"
          />
        </a-col>
        <a-col :span="4">
          <a-tag
            color="arcoblue"
            size="small"
          >
            全量口径
          </a-tag>
        </a-col>
      </a-row>

      <a-table
        :key="tableKey"
        row-key="key"
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
        <template #outboundAmount="{ record }">
          {{ formatMoney((record as SalesSummaryItem).outboundAmount) }}
        </template>
        <template #returnAmount="{ record }">
          {{ formatMoney((record as SalesSummaryItem).returnAmount) }}
        </template>
        <template #netQuantity="{ record }">
          <span class="net-value">{{ (record as SalesSummaryItem).netQuantity }}</span>
        </template>
        <template #netAmount="{ record }">
          <span class="net-value">{{ formatMoney((record as SalesSummaryItem).netAmount) }}</span>
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
  align-items: center;
}

.toolbar-filter .arco-col > .arco-picker,
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

.summary-bar {
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--color-fill-2);
  border-radius: var(--border-radius-small);
}

.summary-bar .arco-col {
  display: flex;
  align-items: center;
}

.table-card {
  border-radius: var(--border-radius-medium);
}

.net-value {
  font-weight: 600;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { recalculateCosts } from '@/api/cost'
import { getCategories, getProductPickList } from '@/api/product'
import type { Category, ProductPickItem } from '@/api/product'
import { getCostProfitReport, toReportRangeUtc } from '@/api/report'
import type { CostProfitGroupBy, CostProfitItem, CostProfitSummary } from '@/api/report'
import { toDateInput } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconCalculator, IconDownload, IconRefresh, IconRestore, IconSearch, IconSettings } from '@tabler/icons-vue'

// —— constants ——
/** 列显示设置（不持久化） */
const columnOptions = [
  { label: '分组名称', value: 'name' },
  { label: '销售数量', value: 'salesQuantity' },
  { label: '销售收入', value: 'salesAmount' },
  { label: '销售成本', value: 'costAmount' },
  { label: '毛利', value: 'grossProfit' },
  { label: '毛利率', value: 'grossProfitRate' },
  { label: '成本完整性', value: 'hasMissingCost' },
]

/** 默认期间：本月 1 日 ~ 今天（本地 YYYY-MM-DD） */
function defaultRange(): string[] {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth(), 1)
  return [toDateInput(first), toDateInput(now)]
}

const groupByOptions = [
  { label: '单据', value: 'order' },
  { label: '商品', value: 'product' },
  { label: '往来单位', value: 'partner' },
]

/** 列表请求序号：只采纳最后一次发起的请求结果 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 重算成本（运维动作，与查询 loading 分开） */
const recalculating = ref(false)
const items = ref<CostProfitItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const summary = ref<CostProfitSummary>({
  salesQuantity: 0,
  salesAmount: 0,
  costAmount: 0,
  grossProfit: 0,
  grossProfitRate: null,
  hasMissingCost: false,
})

/** 期间 / 商品 / 分类 / 分组维度：输入态与已应用态分离 */
const rangeInput = ref<string[]>(defaultRange())
const productIdInput = ref<string | undefined>(undefined)
const categoryIdInput = ref<string | undefined>(undefined)
const groupByInput = ref<CostProfitGroupBy>('order')

const appliedRange = ref<string[]>(defaultRange())
const appliedProductId = ref<string | undefined>(undefined)
const appliedCategoryId = ref<string | undefined>(undefined)
const appliedGroupBy = ref<CostProfitGroupBy>('order')

const productOptions = ref<ProductPickItem[]>([])
const categoryOptions = ref<Category[]>([])
const visibleColumns = ref<string[]>([
  'name',
  'salesQuantity',
  'salesAmount',
  'costAmount',
  'grossProfit',
  'grossProfitRate',
  'hasMissingCost',
])

// —— computed ——
const tableKey = computed(
  () => `${appliedRange.value.join('~')}|${appliedProductId.value ?? ''}|${appliedCategoryId.value ?? ''}|${appliedGroupBy.value}`,
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
  const nameTitle = appliedGroupBy.value === 'order'
    ? '单据号'
    : (appliedGroupBy.value === 'product' ? '商品' : '往来单位')
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('name')) {
    cols.push({ title: nameTitle, dataIndex: 'name', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('salesQuantity')) {
    cols.push({ title: '销售数量', dataIndex: 'salesQuantity', width: 100, align: 'right' })
  }
  if (visibleColumns.value.includes('salesAmount')) {
    cols.push({ title: '销售收入', slotName: 'salesAmount', width: 130, align: 'right' })
  }
  if (visibleColumns.value.includes('costAmount')) {
    cols.push({ title: '销售成本', slotName: 'costAmount', width: 130, align: 'right' })
  }
  if (visibleColumns.value.includes('grossProfit')) {
    cols.push({ title: '毛利', slotName: 'grossProfit', width: 130, align: 'right' })
  }
  if (visibleColumns.value.includes('grossProfitRate')) {
    cols.push({ title: '毛利率', slotName: 'grossProfitRate', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('hasMissingCost')) {
    cols.push({ title: '成本完整性', slotName: 'hasMissingCost', width: 120, align: 'center' })
  }
  return cols
})

const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchPicks()
  void fetchList()
})

// —— methods ——
async function fetchPicks(): Promise<void> {
  try {
    const [products, categories] = await Promise.all([getProductPickList(), getCategories()])
    productOptions.value = products
    categoryOptions.value = categories
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = toReportRangeUtc(appliedRange.value[0], appliedRange.value[1])
    const result = await getCostProfitReport({
      start,
      end,
      productId: appliedProductId.value,
      categoryId: appliedCategoryId.value,
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
  appliedProductId.value = productIdInput.value
  appliedCategoryId.value = categoryIdInput.value
  appliedGroupBy.value = groupByInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  rangeInput.value = defaultRange()
  productIdInput.value = undefined
  categoryIdInput.value = undefined
  groupByInput.value = 'order'
  appliedRange.value = defaultRange()
  appliedProductId.value = undefined
  appliedCategoryId.value = undefined
  appliedGroupBy.value = 'order'
  page.value = 1
  void fetchList()
}

function onRefresh(): void {
  void fetchList()
}

/** 重算成本（popconfirm 确认；防重入按按钮 loading 状态） */
async function onRecalculate(): Promise<void> {
  recalculating.value = true
  try {
    const result = await recalculateCosts()
    if (result.missingCostCount > 0) {
      Message.warning(`重算完成：流水 ${result.movementCount} 条，缺价 ${result.missingCostCount} 条；请补充期初成本或执行盘点调整`)
    } else {
      Message.success(`重算完成：流水 ${result.movementCount} 条，缺价 0 条`)
    }
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    recalculating.value = false
  }
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

/** 金额统一 2 位展示（存储为 4 位） */
function formatMoney(value: number): string {
  return value.toFixed(2)
}

/** 毛利率为 null（收入为 0）时显示 `-` */
function formatRate(value: number | null): string {
  return value === null ? '-' : `${(value * 100).toFixed(2)}%`
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        成本与毛利
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
          <a-col :span="7">
            <a-range-picker
              v-model="rangeInput"
              class="filter-bar__range"
              value-format="YYYY-MM-DD"
              :allow-clear="false"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="productIdInput"
              class="filter-bar__product"
              :options="productOptions.map((p) => ({ label: `${p.code} ${p.name}`, value: p.id }))"
              placeholder="全部商品"
              allow-search
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="categoryIdInput"
              class="filter-bar__category"
              :options="categoryOptions.map((c) => ({ label: c.name, value: c.id }))"
              placeholder="全部分类"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-radio-group
              v-model="groupByInput"
              type="button"
              :options="groupByOptions"
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

        <div class="toolbar-actions">
          <a-popconfirm
            content="重算将按流水顺序重新计算全部成本，期间不要开单，确认继续？"
            @ok="onRecalculate"
          >
            <a-button
              size="small"
              :loading="recalculating"
            >
              <template #icon>
                <IconCalculator />
              </template>
              重算成本
            </a-button>
          </a-popconfirm>
          <a-button
            size="small"
            disabled
            title="导出功能开发中"
          >
            <template #icon>
              <IconDownload />
            </template>
            导出
          </a-button>
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

      <a-row
        class="summary-bar"
        :gutter="16"
      >
        <a-col :span="4">
          <a-statistic
            title="销售数量"
            :value="summary.salesQuantity"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="销售收入"
            :value="summary.salesAmount"
            :precision="2"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="销售成本"
            :value="summary.costAmount"
            :precision="2"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="毛利"
            :value="summary.grossProfit"
            :precision="2"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="毛利率"
            :value="summary.grossProfitRate === null ? 0 : summary.grossProfitRate * 100"
            :precision="2"
            suffix="%"
          />
        </a-col>
        <a-col :span="4">
          <a-tag
            :color="summary.hasMissingCost ? 'orange' : 'arcoblue'"
            size="small"
          >
            {{ summary.hasMissingCost ? '成本不完整' : '全量口径' }}
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
        <template #salesAmount="{ record }">
          {{ formatMoney((record as CostProfitItem).salesAmount) }}
        </template>
        <template #costAmount="{ record }">
          {{ formatMoney((record as CostProfitItem).costAmount) }}
        </template>
        <template #grossProfit="{ record }">
          <span :class="{ 'profit-negative': (record as CostProfitItem).grossProfit < 0 }">
            {{ formatMoney((record as CostProfitItem).grossProfit) }}
          </span>
        </template>
        <template #grossProfitRate="{ record }">
          {{ formatRate((record as CostProfitItem).grossProfitRate) }}
        </template>
        <template #hasMissingCost="{ record }">
          <a-tag
            v-if="(record as CostProfitItem).hasMissingCost"
            color="orange"
            size="small"
          >
            成本不完整
          </a-tag>
          <span v-else>-</span>
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

.profit-negative {
  color: var(--color-danger-6);
  font-weight: 600;
}

.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}
</style>

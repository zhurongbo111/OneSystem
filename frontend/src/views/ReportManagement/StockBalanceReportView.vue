<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getCategories } from '@/api/product'
import type { Category } from '@/api/product'
import { exportStockBalance, getStockBalance } from '@/api/report'
import type { StockBalanceItem, StockBalanceSummary } from '@/api/report'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconDownload, IconListDetails, IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

const router = useRouter()

/** 列表请求序号：只采纳最后一次发起的请求结果 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
const items = ref<StockBalanceItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const summary = ref<StockBalanceSummary>({
  productCount: 0,
  totalQuantity: 0,
  zeroStockCount: 0,
  belowSafetyCount: 0,
  totalCostAmount: 0,
})

/** 分类 / 关键词：输入态与已应用态分离 */
const keywordInput = ref('')
const categoryIdInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedCategoryId = ref<string | undefined>(undefined)

const categoryOptions = ref<Category[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedCategoryId.value ?? ''}`)

const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '分类', dataIndex: 'categoryName', width: 160, ellipsis: true, tooltip: true },
  { title: '商品数', dataIndex: 'productCount', width: 100, align: 'right' },
  { title: '库存合计', slotName: 'totalQuantity', width: 120, align: 'right' },
  { title: '零库存商品数', slotName: 'zeroStockCount', width: 120, align: 'right' },
  { title: '低库存商品数', slotName: 'belowSafetyCount', width: 130, align: 'right' },
  { title: '库存占比', slotName: 'quantityRatio', width: 180 },
  // 成本列（erp-cost）：库存金额 / 均价 / 成本异常标记
  { title: '库存金额', slotName: 'totalCostAmount', width: 130, align: 'right' },
  { title: '均价', slotName: 'averageCost', width: 110, align: 'right' },
  { title: '成本异常', slotName: 'hasCostAnomaly', width: 110, align: 'center' },
  { title: '操作', slotName: 'actions', width: 110, fixed: 'right' },
]

const tableScrollX = computed(() => columns.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchCategories()
  void fetchList()
})

// —— methods ——
async function fetchCategories(): Promise<void> {
  try {
    categoryOptions.value = await getCategories()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getStockBalance({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
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
  appliedKeyword.value = keywordInput.value
  appliedCategoryId.value = categoryIdInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  categoryIdInput.value = undefined
  appliedKeyword.value = ''
  appliedCategoryId.value = undefined
  page.value = 1
  void fetchList()
}

function onRefresh(): void {
  void fetchList()
}

/** 导出当前已应用筛选的全量库存余额表（含合计行）；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    await exportStockBalance({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
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

function onPageChange(current: number): void {
  page.value = current
  void fetchList()
}

function onPageSizeChange(size: number): void {
  pageSize.value = size
  page.value = 1
  void fetchList()
}

/** 下钻库存查询页（明细仍以 014 为准），带分类预置筛选 */
function onShowDetail(record: StockBalanceItem): void {
  void router.push({ name: 'inventory', query: { categoryId: record.categoryId } })
}

/** 占比文案（0–1 → `xx.xx%`，与导出侧 `FormatRatio` 同口径；进度条文本单独格式化，避免浮点尾数） */
function formatRatio(ratio: number): string {
  return `${(ratio * 100).toFixed(2)}%`
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        库存余额表
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
            <a-input
              v-model="keywordInput"
              class="filter-bar__search"
              placeholder="搜索商品编码或名称"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
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
          <a-col :span="12">
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

        <!-- 操作行：导出 + 刷新 -->
        <div class="toolbar-actions">
          <a-button
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
        <a-col :span="5">
          <a-statistic
            title="商品总数"
            :value="summary.productCount"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="库存总量"
            :value="summary.totalQuantity"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="零库存商品数"
            :value="summary.zeroStockCount"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="低库存商品数"
            :value="summary.belowSafetyCount"
            :value-style="{ color: 'var(--color-danger-6)' }"
          />
        </a-col>
        <a-col :span="4">
          <a-statistic
            title="库存金额"
            :value="summary.totalCostAmount"
            :precision="2"
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
        row-key="categoryId"
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
        <template #totalQuantity="{ record }">
          {{ (record as StockBalanceItem).totalQuantity }}
        </template>
        <template #zeroStockCount="{ record }">
          <span :class="{ 'count-warn': (record as StockBalanceItem).zeroStockCount > 0 }">
            {{ (record as StockBalanceItem).zeroStockCount }}
          </span>
        </template>
        <template #belowSafetyCount="{ record }">
          <span :class="{ 'count-warn': (record as StockBalanceItem).belowSafetyCount > 0 }">
            {{ (record as StockBalanceItem).belowSafetyCount }}
          </span>
        </template>
        <template #quantityRatio="{ record }">
          <!-- percent 直接传 0–1 占比：Arco 组件内部再 ×100（前端规则 §4.9） -->
          <a-progress
            :percent="(record as StockBalanceItem).quantityRatio"
            size="small"
          >
            <template #text>
              {{ formatRatio((record as StockBalanceItem).quantityRatio) }}
            </template>
          </a-progress>
        </template>
        <template #totalCostAmount="{ record }">
          {{ (record as StockBalanceItem).totalCostAmount.toFixed(2) }}
        </template>
        <template #averageCost="{ record }">
          {{ (record as StockBalanceItem).averageCost.toFixed(2) }}
        </template>
        <template #hasCostAnomaly="{ record }">
          <a-tag
            v-if="(record as StockBalanceItem).hasCostAnomaly"
            color="red"
            size="small"
          >
            成本异常
          </a-tag>
          <span v-else>-</span>
        </template>
        <template #actions="{ record }">
          <a-button
            type="text"
            size="small"
            @click="onShowDetail(record as StockBalanceItem)"
          >
            <template #icon>
              <IconListDetails />
            </template>
            查看明细
          </a-button>
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

.count-warn {
  color: var(--color-danger-6);
  font-weight: 600;
}
</style>

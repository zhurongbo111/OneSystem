<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { getStockMovements, toUtcRange } from '@/api/stockMovement'
import type { StockMovementListItem, StockMovementType } from '@/api/stockMovement'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

const route = useRoute()

// —— constants ——
/**
 * 变动类型渲染约定（唯一事实源：specs/019-erp-stock-movement/design.md §0 表）
 * 020 / 021 / 022 追加类型时在此与 §0 表同步续行。
 */
const MOVEMENT_TYPE_META: Record<StockMovementType, { label: string; color: string }> = {
  1: { label: '采购入库', color: 'green' },
  2: { label: '采购作废', color: 'red' },
  3: { label: '销售出库', color: 'blue' },
  4: { label: '销售作废', color: 'orange' },
  5: { label: '期初建账', color: 'purple' },
  6: { label: '盘点调整', color: 'gold' },
  7: { label: '采购退货', color: 'orangered' },
  8: { label: '采购退货作废', color: 'magenta' },
}

/** 类型下拉选项（取 §0 文案） */
const typeOptions = (Object.keys(MOVEMENT_TYPE_META) as unknown as StockMovementType[]).map((v) => ({
  label: MOVEMENT_TYPE_META[v].label,
  value: v,
}))

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<StockMovementListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 筛选：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const productIdInput = ref<string | undefined>(undefined)
const typeInput = ref<StockMovementType | undefined>(undefined)
const dateRange = ref<string[]>([])
const appliedKeyword = ref('')
const appliedProductId = ref<string | undefined>(undefined)
const appliedType = ref<StockMovementType | undefined>(undefined)
const appliedRange = ref<string[]>([])

/** 商品下拉数据源（复用 product.ts getProductPickList，全量，量小） */
const productOptions = ref<ProductPickItem[]>([])

const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '变动时间', slotName: 'createdAt', width: 180 },
  { title: '商品编码', dataIndex: 'productCode', width: 160, ellipsis: true, tooltip: true },
  { title: '商品名称', dataIndex: 'productName', width: 180, ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80, align: 'center' },
  { title: '变动类型', slotName: 'movementType', width: 110, align: 'center' },
  { title: '变动量', slotName: 'quantity', width: 100, align: 'right' },
  { title: '来源单号', slotName: 'sourceNo', width: 180, ellipsis: true, tooltip: true },
  { title: '操作人', slotName: 'createdByName', width: 120 },
  { title: '备注', slotName: 'remark', width: 180, ellipsis: true, tooltip: true },
]

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedProductId.value ?? ''}|${appliedType.value ?? ''}|${appliedRange.value.join('~')}`,
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

// —— lifecycle ——
onMounted(() => {
  // 库存页「流水」下钻：以路由 query 的 productId 初始化筛选并直接查询一次
  const presetProductId = typeof route.query.productId === 'string' ? route.query.productId : undefined
  appliedProductId.value = presetProductId
  productIdInput.value = presetProductId
  // 盘点详情页「查看库存流水」：以路由 query 的 keyword（盘点单号）预置关键词筛选
  const presetKeyword = typeof route.query.keyword === 'string' ? route.query.keyword : ''
  appliedKeyword.value = presetKeyword
  keywordInput.value = presetKeyword

  void fetchProducts()
  void fetchList()
})

// —— methods ——
/** 拉取商品下拉数据（失败静默，不影响主列表） */
async function fetchProducts(): Promise<void> {
  try {
    productOptions.value = await getProductPickList()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的流水列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = toUtcRange(appliedRange.value[0], appliedRange.value[1])
    const result = await getStockMovements({
      keyword: appliedKeyword.value.trim() || undefined,
      productId: appliedProductId.value,
      type: appliedType.value,
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

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedProductId.value = productIdInput.value
  appliedType.value = typeInput.value
  appliedRange.value = [...(dateRange.value ?? [])]
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  productIdInput.value = undefined
  typeInput.value = undefined
  dateRange.value = []
  appliedKeyword.value = ''
  appliedProductId.value = undefined
  appliedType.value = undefined
  appliedRange.value = []
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
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        库存流水
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
              placeholder="搜索来源单号"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="6">
            <a-select
              v-model="productIdInput"
              class="filter-bar__product"
              :options="productOptions.map((p) => ({ label: `${p.code} ${p.name}`, value: p.id }))"
              placeholder="全部商品"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="typeInput"
              class="filter-bar__type"
              :options="typeOptions"
              placeholder="全部类型"
              allow-clear
            />
          </a-col>
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-range-picker
                v-model="dateRange"
                value-format="YYYY-MM-DD"
                class="filter-bar__range"
                allow-clear
              />
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

      <a-table
        :key="tableKey"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="items"
        :pagination="pagination"
        :scroll="{ x: 1354 }"
        @page-change="onPageChange"
        @page-size-change="onPageSizeChange"
      >
        <template #seq="{ rowIndex }">
          {{ (page - 1) * pageSize + rowIndex + 1 }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as StockMovementListItem).createdAt) }}
        </template>
        <!-- 变动类型：a-tag 按 §0 颜色 -->
        <template #movementType="{ record }">
          <a-tag :color="MOVEMENT_TYPE_META[(record as StockMovementListItem).movementType].color">
            {{ MOVEMENT_TYPE_META[(record as StockMovementListItem).movementType].label }}
          </a-tag>
        </template>
        <!-- 变动量：带符号整数，入库 / 回增绿字、出库 / 回冲红字 -->
        <template #quantity="{ record }">
          <span :class="(record as StockMovementListItem).quantity > 0 ? 'qty-plus' : 'qty-minus'">
            {{ (record as StockMovementListItem).quantity > 0 ? '+' : '' }}{{ (record as StockMovementListItem).quantity }}
          </span>
        </template>
        <!-- 空值渲染：来源单号 / 操作人为空显示 - -->
        <template #sourceNo="{ record }">
          {{ (record as StockMovementListItem).sourceNo || '-' }}
        </template>
        <template #createdByName="{ record }">
          {{ (record as StockMovementListItem).createdByName || '-' }}
        </template>
        <template #remark="{ record }">
          {{ (record as StockMovementListItem).remark || '-' }}
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

.toolbar-filter__actions .arco-range-picker {
  flex: 1;
}

.toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  margin-bottom: 8px;
}

.table-card {
  border-radius: var(--border-radius-medium);
}

/* 变动量符号颜色：入库 / 回增绿、出库 / 回冲红（design §0） */
.qty-plus {
  color: var(--color-success-6);
  font-weight: 600;
}

.qty-minus {
  color: var(--color-danger-6);
  font-weight: 600;
}
</style>

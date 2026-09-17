<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getCategories, getProductPickList } from '@/api/product'
import type { Category, ProductPickItem } from '@/api/product'
import { getInventoryFlow, toReportRangeUtc } from '@/api/report'
import type { InventoryFlowItem, InventoryFlowSummary } from '@/api/report'
import { toDateInput } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconDownload, IconRefresh, IconRestore, IconSearch, IconSettings } from '@tabler/icons-vue'

// —— constants ——
/** 列显示设置（不持久化） */
const columnOptions = [
  { label: '商品编码', value: 'code' },
  { label: '商品名称', value: 'name' },
  { label: '分类', value: 'categoryName' },
  { label: '单位', value: 'unit' },
  { label: '期初数量', value: 'openingQuantity' },
  { label: '期间入', value: 'inboundQuantity' },
  { label: '期间出', value: 'outboundQuantity' },
  { label: '期末数量', value: 'closingQuantity' },
]

/** 默认期间：本月 1 日 ~ 今天（本地 YYYY-MM-DD） */
function defaultRange(): string[] {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth(), 1)
  return [toDateInput(first), toDateInput(now)]
}

/** 列表请求序号：只采纳最后一次发起的请求结果 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<InventoryFlowItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const summary = ref<InventoryFlowSummary>({ openingQuantity: 0, inboundQuantity: 0, outboundQuantity: 0, closingQuantity: 0 })

/** 期间 / 商品 / 分类 / 只看有变动：输入态与已应用态分离（点搜索才生效） */
const rangeInput = ref<string[]>(defaultRange())
const productIdInput = ref<string | undefined>(undefined)
const categoryIdInput = ref<string | undefined>(undefined)
const onlyChangedInput = ref(false)

const appliedRange = ref<string[]>(defaultRange())
const appliedProductId = ref<string | undefined>(undefined)
const appliedCategoryId = ref<string | undefined>(undefined)
const appliedOnlyChanged = ref(false)

const productOptions = ref<ProductPickItem[]>([])
const categoryOptions = ref<Category[]>([])
const visibleColumns = ref<string[]>([
  'code',
  'name',
  'categoryName',
  'unit',
  'openingQuantity',
  'inboundQuantity',
  'outboundQuantity',
  'closingQuantity',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedRange.value.join('~')}|${appliedProductId.value ?? ''}|${appliedCategoryId.value ?? ''}|${appliedOnlyChanged.value}`,
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

/** 依据列显示设置动态拼列（序号固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('code')) {
    cols.push({ title: '商品编码', dataIndex: 'code', width: 160, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('name')) {
    cols.push({ title: '商品名称', dataIndex: 'name', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('categoryName')) {
    cols.push({ title: '分类', dataIndex: 'categoryName', width: 120 })
  }
  if (visibleColumns.value.includes('unit')) {
    cols.push({ title: '单位', dataIndex: 'unit', width: 80, align: 'center' })
  }
  if (visibleColumns.value.includes('openingQuantity')) {
    cols.push({ title: '期初数量', dataIndex: 'openingQuantity', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('inboundQuantity')) {
    cols.push({ title: '期间入', slotName: 'inboundQuantity', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('outboundQuantity')) {
    cols.push({ title: '期间出', slotName: 'outboundQuantity', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('closingQuantity')) {
    cols.push({ title: '期末数量', slotName: 'closingQuantity', width: 120, align: 'right' })
  }
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchPicks()
  void fetchList()
})

// —— methods ——
/** 拉取商品 / 分类下拉数据（失败静默，不影响主列表） */
async function fetchPicks(): Promise<void> {
  try {
    const [products, categories] = await Promise.all([getProductPickList(), getCategories()])
    productOptions.value = products
    categoryOptions.value = categories
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的进销存报表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = toReportRangeUtc(appliedRange.value[0], appliedRange.value[1])
    const result = await getInventoryFlow({
      start,
      end,
      productId: appliedProductId.value,
      categoryId: appliedCategoryId.value,
      onlyChanged: appliedOnlyChanged.value,
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

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  if (rangeInput.value.length !== 2) {
    Message.warning('请选择查询期间')
    return
  }
  appliedRange.value = [...rangeInput.value]
  appliedProductId.value = productIdInput.value
  appliedCategoryId.value = categoryIdInput.value
  appliedOnlyChanged.value = onlyChangedInput.value
  page.value = 1
  void fetchList()
}

/** 重置：恢复默认期间并清空筛选 */
function onReset(): void {
  rangeInput.value = defaultRange()
  productIdInput.value = undefined
  categoryIdInput.value = undefined
  onlyChangedInput.value = false
  appliedRange.value = defaultRange()
  appliedProductId.value = undefined
  appliedCategoryId.value = undefined
  appliedOnlyChanged.value = false
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
    <!-- 页面头：仅标题 -->
    <div class="page-header">
      <h1 class="page-title">
        进销存报表
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
              v-model="productIdInput"
              class="filter-bar__product"
              :options="productOptions.map((p) => ({ label: `${p.code} ${p.name}`, value: p.id }))"
              placeholder="全部商品"
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
          <a-col :span="3">
            <a-checkbox v-model="onlyChangedInput">
              只看有变动
            </a-checkbox>
          </a-col>
          <a-col :span="4">
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

        <!-- 操作行：导出（027 交付前禁用）+ 列设置 + 刷新 -->
        <div class="toolbar-actions">
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

      <!-- 合计区：全量筛选结果口径（与当前页无关） -->
      <a-row
        class="summary-bar"
        :gutter="16"
      >
        <a-col :span="5">
          <a-statistic
            title="期初合计"
            :value="summary.openingQuantity"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="期间入合计"
            :value="summary.inboundQuantity"
            :value-style="{ color: 'var(--color-success-6)' }"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="期间出合计"
            :value="summary.outboundQuantity"
            :value-style="{ color: 'var(--color-danger-6)' }"
          />
        </a-col>
        <a-col :span="5">
          <a-statistic
            title="期末合计"
            :value="summary.closingQuantity"
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
        row-key="productId"
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
        <template #inboundQuantity="{ record }">
          <span class="qty-inbound">{{ (record as InventoryFlowItem).inboundQuantity }}</span>
        </template>
        <template #outboundQuantity="{ record }">
          <span class="qty-outbound">{{ (record as InventoryFlowItem).outboundQuantity }}</span>
        </template>
        <template #closingQuantity="{ record }">
          <span class="qty-closing">{{ (record as InventoryFlowItem).closingQuantity }}</span>
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

.qty-inbound {
  color: var(--color-success-6);
}

.qty-outbound {
  color: var(--color-danger-6);
}

.qty-closing {
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

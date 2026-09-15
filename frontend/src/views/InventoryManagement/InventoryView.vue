<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getInventory } from '@/api/inventory'
import type { InventoryItem } from '@/api/inventory'
import { getCategories } from '@/api/product'
import type { Category } from '@/api/product'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconRefresh, IconSearch, IconSettings, IconUndo } from '@arco-design/web-vue/es/icon'

// —— constants ——
/** 列显示设置（不持久化；纯只读页无操作列） */
const columnOptions = [
  { label: '编码', value: 'code' },
  { label: '名称', value: 'name' },
  { label: '分类', value: 'categoryName' },
  { label: '单位', value: 'unit' },
  { label: '当前库存', value: 'stockQuantity' },
  { label: '安全阈值', value: 'safetyStock' },
  { label: '最近变动时间', value: 'updatedAt' },
]

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<InventoryItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 分类：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const categoryIdInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedCategoryId = ref<string | undefined>(undefined)
/** 分类下拉数据源（复用 product.ts getCategories，全量，量小） */
const categoryOptions = ref<Category[]>([])

const visibleColumns = ref<string[]>([
  'code',
  'name',
  'categoryName',
  'unit',
  'stockQuantity',
  'safetyStock',
  'updatedAt',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedCategoryId.value ?? ''}`)

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
    cols.push({ title: '编码', dataIndex: 'code', width: 160, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('name')) {
    cols.push({ title: '名称', dataIndex: 'name', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('categoryName')) {
    cols.push({ title: '分类', dataIndex: 'categoryName', width: 120 })
  }
  if (visibleColumns.value.includes('unit')) {
    cols.push({ title: '单位', dataIndex: 'unit', width: 80, align: 'center' })
  }
  if (visibleColumns.value.includes('stockQuantity')) {
    cols.push({ title: '当前库存', slotName: 'stockQuantity', width: 140, align: 'center' })
  }
  if (visibleColumns.value.includes('safetyStock')) {
    cols.push({ title: '安全阈值', dataIndex: 'safetyStock', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('updatedAt')) {
    cols.push({ title: '最近变动时间', slotName: 'updatedAt', width: 172 })
  }
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchCategories()
  void fetchList()
})

// —— methods ——
/** 拉取分类下拉数据（失败静默，不影响主列表） */
async function fetchCategories(): Promise<void> {
  try {
    categoryOptions.value = await getCategories()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的库存列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getInventory({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
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
  appliedCategoryId.value = categoryIdInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  categoryIdInput.value = undefined
  appliedKeyword.value = ''
  appliedCategoryId.value = undefined
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
    <!-- 页面头：仅标题（纯只读，无新增入口） -->
    <div class="page-header">
      <h1 class="page-title">
        库存查询
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
                  <IconUndo />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：列设置 + 刷新（只读页无新增 / 编辑） -->
        <div class="toolbar-actions">
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
        <!-- 当前库存：低于安全库存（或为 0 且阈值 > 0）标红 + 警示标签 -->
        <template #stockQuantity="{ record }">
          <span
            :class="{ 'stock-below': (record as InventoryItem).isBelowSafetyStock }"
            class="stock-quantity"
          >
            {{ (record as InventoryItem).stockQuantity }}
          </span>
          <a-tag
            v-if="(record as InventoryItem).isBelowSafetyStock"
            color="orangered"
            size="small"
          >
            低于安全库存
          </a-tag>
        </template>
        <template #updatedAt="{ record }">
          {{ formatDateTime((record as InventoryItem).updatedAt) }}
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

.table-card {
  border-radius: var(--border-radius-medium);
}

.stock-quantity {
  margin-right: 4px;
}

/* 低库存 / 缺货数字标红（与警示标签呼应，缺货可见） */
.stock-below {
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

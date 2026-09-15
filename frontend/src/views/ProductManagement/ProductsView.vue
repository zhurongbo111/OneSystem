<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getCategories, getProducts, updateProductStatus } from '@/api/product'
import type { Category, Product, ProductStatus } from '@/api/product'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconEdit,
  IconEye,
  IconPlayCircle,
  IconPlus,
  IconPoweroff,
  IconRefresh,
  IconSearch,
  IconSettings,
  IconTags,
} from '@arco-design/web-vue/es/icon'

import ProductFormDrawer from './ProductFormDrawer.vue'

// —— constants ——
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

const columnOptions = [
  { label: '编码', value: 'code' },
  { label: '名称', value: 'name' },
  { label: '分类', value: 'categoryName' },
  { label: '单位', value: 'unit' },
  { label: '采购价', value: 'purchasePrice' },
  { label: '销售价', value: 'salePrice' },
  { label: '库存', value: 'stockQuantity' },
  { label: '安全库存', value: 'safetyStock' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在启停的商品 id：行内按钮 loading 与写操作互斥用 */
const togglingId = ref<string | undefined>(undefined)
const items = ref<Product[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 分类下拉数据源（全量） */
const categories = ref<Category[]>([])
const categoriesLoading = ref(false)

/** 关键词 / 分类 / 状态：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const categoryIdInput = ref<string | undefined>(undefined)
const statusInput = ref<ProductStatus | undefined>(undefined)
const appliedKeyword = ref('')
const appliedCategoryId = ref<string | undefined>(undefined)
const appliedStatus = ref<ProductStatus | undefined>(undefined)

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'code',
  'name',
  'categoryName',
  'unit',
  'purchasePrice',
  'salePrice',
  'stockQuantity',
  'safetyStock',
  'status',
  'createdAt',
])

/** 新增 / 编辑 / 详情抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit' | 'view'>('create')
const drawerEditId = ref<string | undefined>(undefined)

// —— stores/composables ——
const router = useRouter()

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedCategoryId.value ?? ''}|${appliedStatus.value ?? ''}`)

const categoryOptions = computed(() =>
  categories.value.map((c) => ({ label: c.name, value: c.id })),
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

/** 依据列显示设置动态拼列（序号与操作列固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('code')) {
    cols.push({ title: '编码', dataIndex: 'code', width: 140 })
  }
  if (visibleColumns.value.includes('name')) {
    cols.push({ title: '名称', dataIndex: 'name', width: 160, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('categoryName')) {
    cols.push({ title: '分类', dataIndex: 'categoryName', width: 110 })
  }
  if (visibleColumns.value.includes('unit')) {
    cols.push({ title: '单位', dataIndex: 'unit', width: 72, align: 'center' })
  }
  if (visibleColumns.value.includes('purchasePrice')) {
    cols.push({ title: '采购价', slotName: 'purchasePrice', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('salePrice')) {
    cols.push({ title: '销售价', slotName: 'salePrice', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('stockQuantity')) {
    cols.push({ title: '库存', slotName: 'stockQuantity', width: 130, align: 'right' })
  }
  if (visibleColumns.value.includes('safetyStock')) {
    cols.push({ title: '安全库存', dataIndex: 'safetyStock', width: 90, align: 'right' })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', slotName: 'status', width: 80, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 操作列：3 个操作 ≤ 3 平铺（编辑 / 停用或启用 / 详情），宽度按实测取 210（specs/action-column §2）
  cols.push({ title: '操作', slotName: 'action', width: 210, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void loadCategories()
  void fetchList()
})

// —— methods ——
/** 加载分类下拉（筛选用） */
async function loadCategories(): Promise<void> {
  categoriesLoading.value = true
  try {
    categories.value = await getCategories()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    categoriesLoading.value = false
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getProducts({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
      status: appliedStatus.value,
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
  appliedStatus.value = statusInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  categoryIdInput.value = undefined
  statusInput.value = undefined
  appliedKeyword.value = ''
  appliedCategoryId.value = undefined
  appliedStatus.value = undefined
  page.value = 1
  void fetchList()
}

/** 刷新当前页 */
function onRefresh(): void {
  void fetchList()
}

/** 跳转分类管理页（独立页面，specs/erp-category） */
function onGoCategories(): void {
  void router.push({ name: 'categories' })
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

/** 新增（抽屉） */
function onCreate(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉） */
function onEdit(row: Product): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 详情（抽屉查看态） */
function onDetail(row: Product): void {
  drawerMode.value = 'view'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 启用 / 停用 */
async function onToggleStatus(row: Product): Promise<void> {
  if (togglingId.value) return
  const next: ProductStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateProductStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已停用')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 金额展示统一两位小数 */
function formatAmount(v: number): string {
  return v.toFixed(2)
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        商品管理
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
              :options="categoryOptions"
              :loading="categoriesLoading"
              placeholder="全部分类"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              class="filter-bar__status"
              :options="statusOptions"
              placeholder="状态"
              allow-clear
            />
          </a-col>
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                :loading="loading"
                @click="onSearch"
              >
                搜索
              </a-button>
              <a-button
                :loading="loading"
                @click="onReset"
              >
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：左组主操作（新增/分类管理）靠左，右组视图操作（列设置/刷新）靠右，同一行 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增
            </a-button>
            <a-button
              size="small"
              @click="onGoCategories"
            >
              <template #icon>
                <IconTags />
              </template>
              分类管理
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
        <template #purchasePrice="{ record }">
          {{ formatAmount((record as Product).purchasePrice) }}
        </template>
        <template #salePrice="{ record }">
          {{ formatAmount((record as Product).salePrice) }}
        </template>
        <template #stockQuantity="{ record }">
          <span
            class="stock-cell"
            :class="{ 'stock-cell--low': (record as Product).isBelowSafetyStock }"
          >
            {{ (record as Product).stockQuantity }}
          </span>
          <a-tag
            v-if="(record as Product).isBelowSafetyStock"
            color="orangered"
            size="small"
          >
            低库存
          </a-tag>
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as Product).status === 1 ? 'green' : 'red'">
            {{ (record as Product).status === 1 ? '启用' : '停用' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as Product).createdAt) }}
        </template>
        <!-- 操作列（specs/action-column）：3 个操作 ≤ 3，平铺 编辑 / 停用或启用 / 详情 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onEdit(record as Product)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              type="warning"
              :content="`确认${(record as Product).status === 1 ? '停用' : '启用'}该商品？`"
              @ok="onToggleStatus(record as Product)"
            >
              <a-button
                type="text"
                :status="(record as Product).status === 1 ? 'warning' : 'normal'"
                size="small"
                :loading="togglingId === (record as Product).id"
              >
                <template #icon>
                  <IconPoweroff v-if="(record as Product).status === 1" />
                  <IconPlayCircle v-else />
                </template>
                {{ (record as Product).status === 1 ? '停用' : '启用' }}
              </a-button>
            </a-popconfirm>
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as Product)"
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

    <ProductFormDrawer
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

/* 操作列密度（specs/action-column §5）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

/* 操作列兜底：按钮组不折行 */
:deep(.action-cell) {
  white-space: nowrap;
}

.stock-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.stock-cell--low {
  color: var(--color-danger-6);
  font-weight: 600;
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

.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}
</style>

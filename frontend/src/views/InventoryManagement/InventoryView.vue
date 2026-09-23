<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { exportInventory } from '@/api/export'
import { getInventory, updateInventorySafetyStock } from '@/api/inventory'
import type { InventoryItem } from '@/api/inventory'
import { getCategories } from '@/api/product'
import type { Category } from '@/api/product'
import { getWarehousePickList } from '@/api/warehouse'
import type { WarehousePickItem } from '@/api/warehouse'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconAdjustments,
  IconDownload,
  IconListDetails,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

// —— constants ——
/** 列显示设置（不持久化；操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '编码', value: 'code' },
  { label: '名称', value: 'name' },
  { label: '分类', value: 'categoryName' },
  { label: '单位', value: 'unit' },
  { label: '仓库', value: 'warehouseName' },
  { label: '当前库存', value: 'stockQuantity' },
  { label: '安全阈值', value: 'safetyStock' },
  { label: '最近变动时间', value: 'updatedAt' },
]

/** 安全库存区间（对应后端 WarehouseFieldConstraints.SafetyStockMin/MaxValue，0 表示不提醒） */
const SAFETY_STOCK_MIN = 0
const SAFETY_STOCK_MAX = 999999

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
const items = ref<InventoryItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 分类 / 仓库：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const categoryIdInput = ref<string | undefined>(undefined)
const warehouseIdInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedCategoryId = ref<string | undefined>(undefined)
const appliedWarehouseId = ref<string | undefined>(undefined)
/** 分类下拉数据源（复用 product.ts getCategories，全量，量小） */
const categoryOptions = ref<Category[]>([])
/** 仓库下拉数据源（仅启用仓，038） */
const warehouseOptions = ref<WarehousePickItem[]>([])

/** 仓级安全库存单字段 Modal（038）：目标行 / 输入值 / 提交中 */
const safetyStockVisible = ref(false)
const safetyStockTarget = ref<InventoryItem | null>(null)
const safetyStockInput = ref(0)
const saveSafetyStockSubmitting = ref(false)

const visibleColumns = ref<string[]>([
  'code',
  'name',
  'categoryName',
  'unit',
  'warehouseName',
  'stockQuantity',
  'safetyStock',
  'updatedAt',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedCategoryId.value ?? ''}|${appliedWarehouseId.value ?? ''}`,
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
  if (visibleColumns.value.includes('warehouseName')) {
    cols.push({ title: '仓库', dataIndex: 'warehouseName', width: 140, ellipsis: true, tooltip: true })
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
  // 操作列固定显示：流水下钻（只读）+ 安全库存（038 唯一写入口，权限 inventory.update）
  cols.push({ title: '操作', slotName: 'actions', width: 180, fixed: 'right' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  // 报表「库存余额表」下钻预置分类筛选（specs/025-erp-report design.md §4.4）
  const categoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : undefined
  if (categoryId) {
    categoryIdInput.value = categoryId
    appliedCategoryId.value = categoryId
  }
  void fetchCategories()
  void fetchWarehouses()
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

/** 拉取仓库下拉数据（仅启用仓，038；失败静默，不影响主列表） */
async function fetchWarehouses(): Promise<void> {
  try {
    warehouseOptions.value = await getWarehousePickList()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 行唯一键：038 起一行 = 商品 × 仓，仅 productId 在「全部仓」下会重复 */
function rowKey(record: InventoryItem): string {
  return `${record.productId}:${record.warehouseId}`
}

/** 拉取当前条件下的库存列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getInventory({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
      warehouseId: appliedWarehouseId.value,
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
  appliedWarehouseId.value = warehouseIdInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  categoryIdInput.value = undefined
  warehouseIdInput.value = undefined
  appliedKeyword.value = ''
  appliedCategoryId.value = undefined
  appliedWarehouseId.value = undefined
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

/** 导出当前已应用筛选的全量库存列表；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    await exportInventory({
      keyword: appliedKeyword.value.trim() || undefined,
      categoryId: appliedCategoryId.value,
      warehouseId: appliedWarehouseId.value,
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

/** 下钻流水页：同步路由跳转（瞬时动作不置 loading），带 productId 预置筛选 */
function onShowMovements(record: InventoryItem): void {
  void router.push({ name: 'stockMovements', query: { productId: record.productId } })
}

/** 打开仓级安全库存 Modal（瞬时动作不置 loading），预填该仓当前阈值 */
function onEditSafetyStock(record: InventoryItem): void {
  safetyStockTarget.value = record
  safetyStockInput.value = record.safetyStock
  safetyStockVisible.value = true
}

/** 关闭安全库存 Modal */
function onSafetyStockCancel(): void {
  safetyStockVisible.value = false
  safetyStockTarget.value = null
}

/** 保存仓级安全库存：成功后关闭并刷新列表（低库存标记随之更新） */
async function onSaveSafetyStock(): Promise<void> {
  const target = safetyStockTarget.value
  if (!target || saveSafetyStockSubmitting.value) return
  saveSafetyStockSubmitting.value = true
  try {
    await updateInventorySafetyStock({
      productId: target.productId,
      warehouseId: target.warehouseId,
      safetyStock: safetyStockInput.value,
    })
    Message.success('安全库存已更新')
    onSafetyStockCancel()
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理（该仓无库存行返回 40400）
  } finally {
    saveSafetyStockSubmitting.value = false
  }
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
          <a-col :span="6">
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
          <a-col :span="4">
            <a-select
              v-model="warehouseIdInput"
              class="filter-bar__warehouse"
              :options="warehouseOptions.map((w) => ({ label: w.name, value: w.id }))"
              placeholder="全部仓库"
              allow-clear
            />
          </a-col>
          <a-col :span="10">
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

        <!-- 操作行：导出 + 列设置 + 刷新（只读页无新增 / 编辑） -->
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
          <a-divider
            direction="vertical"
            class="toolbar-actions__divider"
          />
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
        :row-key="rowKey"
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
        <!-- 操作列：流水下钻（只读）+ 安全库存（单字段 Modal，权限 inventory.update） -->
        <template #actions="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onShowMovements(record as InventoryItem)"
            >
              <template #icon>
                <IconListDetails />
              </template>
              流水
            </a-button>
            <a-button
              v-if="auth.hasPermission('inventory.update')"
              type="text"
              size="small"
              @click="onEditSafetyStock(record as InventoryItem)"
            >
              <template #icon>
                <IconAdjustments />
              </template>
              安全库存
            </a-button>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <!-- 仓级安全库存（038）：单字段 Modal，阈值 0 表示不提醒 -->
    <a-modal
      :visible="safetyStockVisible"
      title="安全库存"
      :ok-loading="saveSafetyStockSubmitting"
      :mask-closable="false"
      @ok="onSaveSafetyStock"
      @cancel="onSafetyStockCancel"
    >
      <a-form
        :model="{ safetyStock: safetyStockInput }"
        layout="vertical"
      >
        <a-form-item label="商品 / 仓库">
          <span class="modal-target">
            {{ safetyStockTarget?.name }}（{{ safetyStockTarget?.code }}）/ {{ safetyStockTarget?.warehouseName }}
          </span>
        </a-form-item>
        <a-form-item label="安全库存阈值">
          <a-input-number
            v-model="safetyStockInput"
            class="modal-input"
            :min="SAFETY_STOCK_MIN"
            :max="SAFETY_STOCK_MAX"
            :precision="0"
          >
            <template #suffix>
              0 表示不提醒
            </template>
          </a-input-number>
        </a-form-item>
      </a-form>
    </a-modal>
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

.toolbar-actions__divider {
  margin: 0;
}

.table-card {
  border-radius: var(--border-radius-medium);
}

.stock-quantity {
  margin-right: 4px;
}

/* 操作列密度（specs/011-action-column §0）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

.modal-target {
  color: var(--color-text-2);
}

.modal-input {
  width: 100%;
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

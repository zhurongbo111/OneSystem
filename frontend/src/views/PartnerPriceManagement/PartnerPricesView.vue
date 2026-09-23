<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { exportPartnerPrices } from '@/api/export'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { deletePartnerPrice, getPartnerPrices } from '@/api/partnerPrice'
import type { PartnerPriceListItem } from '@/api/partnerPrice'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconDownload,
  IconEdit,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
  IconTrash,
} from '@tabler/icons-vue'

import PartnerPriceFormDrawer from './PartnerPriceFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 可配置协议价的往来类型（客户 / 两者；后端同口径校验，供应商报 40000） */
const PARTNER_TYPES_FOR_PRICE = [2, 3]

/** 可选列（序号与操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '客户', value: 'partnerName' },
  { label: '商品编码', value: 'productCode' },
  { label: '商品名称', value: 'productName' },
  { label: '单位', value: 'unit' },
  { label: '协议价', value: 'price' },
  { label: '商品销售价', value: 'salePrice' },
  { label: '价差', value: 'diff' },
  { label: '备注', value: 'remark' },
  { label: '创建时间', value: 'createdAt' },
]

// —— reactive state ——
const loading = ref(false)
/** 导出中（036 §4.5：与查询分开，独立 loading 防重入） */
const exporting = ref(false)
/** 行内删除 loading（每张协议价一个入口，按 id 互斥） */
const deletingId = ref<string | undefined>(undefined)
const items = ref<PartnerPriceListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 客户 / 商品：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const partnerInput = ref<string | undefined>(undefined)
const productInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedPartner = ref<string | undefined>(undefined)
const appliedProduct = ref<string | undefined>(undefined)

/** 客户下拉（仅「客户 / 两者」且启用）与商品下拉（启用商品）数据源 */
const customers = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 抽屉状态：新增 / 编辑共用同一表单（specs/007-form-detail-showcase §0） */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const editId = ref<string | undefined>(undefined)

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'partnerName',
  'productCode',
  'productName',
  'unit',
  'price',
  'salePrice',
  'diff',
  'remark',
  'createdAt',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedPartner.value ?? ''}|${appliedProduct.value ?? ''}`,
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

/** 客户下拉选项 */
const partnerOptions = computed(() => customers.value.map((p) => ({ label: p.name, value: p.id })))

/** 商品下拉选项（编码 + 名称，便于按编码检索） */
const productOptions = computed(() =>
  products.value.map((p) => ({ label: `${p.code} ${p.name}`, value: p.id })),
)

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('partnerName')) {
    cols.push({ title: '客户', dataIndex: 'partnerName', width: 160, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('productCode')) {
    cols.push({ title: '商品编码', dataIndex: 'productCode', width: 140 })
  }
  if (visibleColumns.value.includes('productName')) {
    cols.push({ title: '商品名称', dataIndex: 'productName', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('unit')) {
    cols.push({ title: '单位', dataIndex: 'unit', width: 80, align: 'center' })
  }
  if (visibleColumns.value.includes('price')) {
    cols.push({ title: '协议价', slotName: 'price', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('salePrice')) {
    cols.push({ title: '商品销售价', slotName: 'salePrice', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('diff')) {
    cols.push({ title: '价差', slotName: 'diff', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('remark')) {
    cols.push({ title: '备注', slotName: 'remark', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 操作列：2 个操作 ≤ 3 平铺（编辑 / 删除），宽度按 specs/011-action-column §0 两操作取值 150
  cols.push({ title: '操作', slotName: 'action', width: 150, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动的最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

/** 价差（协议价 − 销售价）；> 0 表示协议价高于默认价，前端标橙提示 */
function priceDiff(row: PartnerPriceListItem): number {
  return Number((row.price - row.salePrice).toFixed(2))
}

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void loadOptions()
})

// —— methods ——
/** 加载客户与商品下拉（客户限制为「客户 / 两者」且启用） */
async function loadOptions(): Promise<void> {
  try {
    const [partners, picks] = await Promise.all([
      getPartners({ status: 1, page: 1, pageSize: 100 }),
      getProductPickList(),
    ])
    customers.value = partners.items.filter((p) => PARTNER_TYPES_FOR_PRICE.includes(p.type))
    products.value = picks
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getPartnerPrices({
      keyword: appliedKeyword.value.trim() || undefined,
      partnerId: appliedPartner.value,
      productId: appliedProduct.value,
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
  appliedPartner.value = partnerInput.value
  appliedProduct.value = productInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  partnerInput.value = undefined
  productInput.value = undefined
  appliedKeyword.value = ''
  appliedPartner.value = undefined
  appliedProduct.value = undefined
  page.value = 1
  void fetchList()
}

/** 刷新当前页 */
function onRefresh(): void {
  void fetchList()
}

/** 导出：当前筛选全量（后端忽略分页），失败时请求层统一提示 JSON 错误 */
async function onExport(): Promise<void> {
  if (exporting.value) return
  exporting.value = true
  try {
    await exportPartnerPrices({
      keyword: appliedKeyword.value.trim() || undefined,
      partnerId: appliedPartner.value,
      productId: appliedProduct.value,
    })
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

/** 打开新增抽屉（客户与商品可选） */
function onCreate(): void {
  drawerMode.value = 'create'
  editId.value = undefined
  drawerVisible.value = true
}

/** 打开编辑抽屉（客户与商品不可改，仅改单价与备注） */
function onEdit(row: PartnerPriceListItem): void {
  drawerMode.value = 'edit'
  editId.value = row.id
  drawerVisible.value = true
}

/** 抽屉保存成功：关闭抽屉外的列表负责刷新 */
function onSaved(): void {
  void fetchList()
}

/** 删除：该商品的单价回退为商品销售价 */
async function onDelete(row: PartnerPriceListItem): Promise<void> {
  if (deletingId.value) return
  deletingId.value = row.id
  try {
    await deletePartnerPrice(row.id)
    Message.success('协议价已删除，该商品恢复默认价')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    deletingId.value = undefined
  }
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        客户价格
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <!-- 筛选行：客户 / 商品 / 关键词 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="5">
            <a-select
              v-model="partnerInput"
              :options="partnerOptions"
              placeholder="全部客户"
              allow-clear
              allow-search
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="productInput"
              :options="productOptions"
              placeholder="全部商品"
              allow-clear
              allow-search
            />
          </a-col>
          <a-col :span="8">
            <a-input
              v-model="keywordInput"
              placeholder="搜索客户名 / 商品编码 / 商品名称"
              allow-clear
              @press-enter="onSearch"
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

        <!-- 操作行：左组主操作（新增协议价）靠左，右组视图操作靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('partnerPrices.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增协议价
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-divider
              direction="vertical"
              class="toolbar-actions__divider"
            />
            <a-button
              v-if="auth.hasPermission('partnerPrices.export')"
              size="small"
              :loading="exporting"
              @click="onExport"
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
        <template #price="{ record }">
          <!-- 协议价高于商品销售价时标橙并提示（design.md §4.4） -->
          <a-tooltip
            v-if="priceDiff(record as PartnerPriceListItem) > 0"
            content="高于默认价（商品销售价）"
          >
            <span class="amount price-higher">
              ¥ {{ (record as PartnerPriceListItem).price.toFixed(2) }}
            </span>
          </a-tooltip>
          <span
            v-else
            class="amount"
          >
            ¥ {{ (record as PartnerPriceListItem).price.toFixed(2) }}
          </span>
        </template>
        <template #salePrice="{ record }">
          <span class="amount muted">
            ¥ {{ (record as PartnerPriceListItem).salePrice.toFixed(2) }}
          </span>
        </template>
        <template #diff="{ record }">
          <span
            class="amount"
            :class="priceDiff(record as PartnerPriceListItem) > 0 ? 'price-higher' : 'muted'"
          >
            {{ priceDiff(record as PartnerPriceListItem) > 0 ? '+' : '' }}{{
              priceDiff(record as PartnerPriceListItem).toFixed(2)
            }}
          </span>
        </template>
        <template #remark="{ record }">
          {{ (record as PartnerPriceListItem).remark || '-' }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as PartnerPriceListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：2 个操作平铺 编辑 / 删除 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('partnerPrices.update')"
              type="text"
              size="small"
              @click="onEdit(record as PartnerPriceListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('partnerPrices.delete')"
              type="warning"
              content="确认删除该协议价？删除后该商品恢复默认价"
              @ok="onDelete(record as PartnerPriceListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="danger"
                :loading="deletingId === (record as PartnerPriceListItem).id"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <PartnerPriceFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="editId"
      @saved="onSaved"
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

.toolbar-actions__divider {
  margin: 0;
}

.table-card {
  border-radius: var(--border-radius-medium);
}

.amount {
  font-variant-numeric: tabular-nums;
}

/* 协议价高于默认价：橙色强调（套利 / 录入错误提示） */
.price-higher {
  color: var(--color-warning-6);
}

.muted {
  color: var(--color-text-3);
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

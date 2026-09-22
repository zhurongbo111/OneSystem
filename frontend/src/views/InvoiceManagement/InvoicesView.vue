<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { exportInvoices } from '@/api/export'
import {
  formatTaxRate,
  getInvoices,
  INVOICE_TYPE_META,
  INVOICE_TYPE_OPTIONS,
  toDateRange,
  voidInvoice,
  type InvoiceListItem,
  type InvoiceType,
} from '@/api/invoice'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconBan,
  IconDownload,
  IconEye,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

const auth = useAuthStore()

// —— types ——
type InvoiceTypeFilter = InvoiceType | undefined

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
/** 正在作废的发票 id（design §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)
const items = ref<InvoiceListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 类型 / 往来 / 日期范围：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const typeInput = ref<InvoiceTypeFilter>(undefined)
const partnerInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedType = ref<InvoiceTypeFilter>(undefined)
const appliedPartner = ref<string | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

/** 往来下拉数据源（全部启用往来，进项 / 销项共用） */
const partners = ref<Partner[]>([])

/** 可选列（序号与操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '发票号', value: 'invoiceNo' },
  { label: '类型', value: 'type' },
  { label: '往来单位', value: 'partnerName' },
  { label: '开票日期', value: 'invoiceDate' },
  { label: '不含税金额', value: 'amountExcludingTax' },
  { label: '税率', value: 'taxRate' },
  { label: '税额', value: 'taxAmount' },
  { label: '价税合计', value: 'totalAmount' },
  { label: '关联单据', value: 'orderNoSummary' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'invoiceNo',
  'type',
  'partnerName',
  'invoiceDate',
  'amountExcludingTax',
  'taxRate',
  'taxAmount',
  'totalAmount',
  'orderNoSummary',
  'status',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedKeyword.value}|${appliedType.value ?? ''}|${appliedPartner.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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

/** 往来下拉选项 */
const partnerOptions = computed(() => partners.value.map((p) => ({ label: p.name, value: p.id })))

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('invoiceNo')) {
    cols.push({ title: '发票号', dataIndex: 'invoiceNo', width: 160 })
  }
  if (visibleColumns.value.includes('type')) {
    cols.push({ title: '类型', slotName: 'type', width: 90, align: 'center' })
  }
  if (visibleColumns.value.includes('partnerName')) {
    cols.push({ title: '往来单位', dataIndex: 'partnerName', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('invoiceDate')) {
    cols.push({ title: '开票日期', slotName: 'invoiceDate', width: 110 })
  }
  if (visibleColumns.value.includes('amountExcludingTax')) {
    cols.push({ title: '不含税金额', slotName: 'amountExcludingTax', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('taxRate')) {
    cols.push({ title: '税率', slotName: 'taxRate', width: 90, align: 'right' })
  }
  if (visibleColumns.value.includes('taxAmount')) {
    cols.push({ title: '税额', slotName: 'taxAmount', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('totalAmount')) {
    cols.push({ title: '价税合计', slotName: 'totalAmount', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('orderNoSummary')) {
    cols.push({ title: '关联单据', slotName: 'orderNoSummary', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', slotName: 'status', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 操作列：2 个操作 ≤ 3 平铺（详情 / 作废），宽度按 specs/011-action-column §0 两操作取值 150
  cols.push({ title: '操作', slotName: 'action', width: 150, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动的最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(async () => {
  void fetchList()
  try {
    const result = await getPartners({ status: 1, page: 1, pageSize: 100 })
    partners.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
})

// —— methods ——
/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = appliedRange.value
      ? toDateRange(appliedRange.value[0], appliedRange.value[1])
      : {}
    const result = await getInvoices({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
      partnerId: appliedPartner.value,
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
  appliedType.value = typeInput.value
  appliedPartner.value = partnerInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  typeInput.value = undefined
  partnerInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedType.value = undefined
  appliedPartner.value = undefined
  appliedRange.value = null
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

/** 登记发票：独立页面（含关联单据子表格，specs/007-form-detail-showcase §0） */
function onCreate(): void {
  void router.push({ name: 'invoiceNew' })
}

function onDetail(row: InvoiceListItem): void {
  void router.push({ name: 'invoiceDetail', params: { id: row.id } })
}

/** 导出当前已应用筛选的全量发票（发票 + 关联明细两个工作表）；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    const { start, end } = appliedRange.value
      ? toDateRange(appliedRange.value[0], appliedRange.value[1])
      : {}
    await exportInvoices({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
      partnerId: appliedPartner.value,
      start,
      end,
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

/** 作废行整体置灰 */
function rowClassName(record: InvoiceListItem): string {
  return record.status === 0 ? 'row-voided' : ''
}

/** 作废：占用金额按聚合自动释放，仅改状态不删数据 */
async function onVoid(row: InvoiceListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidInvoice(row.id)
    Message.success('发票已作废，占用金额已释放')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        发票登记
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
          <a-col :span="5">
            <a-input
              v-model="keywordInput"
              placeholder="搜索发票号 / 往来单位 / 单据号"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="typeInput"
              :options="INVOICE_TYPE_OPTIONS"
              placeholder="类型"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="partnerInput"
              :options="partnerOptions"
              placeholder="全部往来"
              allow-clear
              allow-search
              :loading="partners.length === 0"
            />
          </a-col>
          <a-col :span="6">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              :allow-clear="true"
              style="width: 100%"
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

        <!-- 操作行：左组主操作（登记发票）靠左，右组视图操作（导出 / 列设置 / 刷新）靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('invoices.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              登记发票
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              v-if="auth.hasPermission('invoices.export')"
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
      </div>

      <a-table
        :key="tableKey"
        row-key="id"
        :row-class="rowClassName"
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
        <template #type="{ record }">
          <a-tag :color="INVOICE_TYPE_META[(record as InvoiceListItem).type].color">
            {{ INVOICE_TYPE_META[(record as InvoiceListItem).type].label }}
          </a-tag>
        </template>
        <template #invoiceDate="{ record }">
          {{ formatDateTime((record as InvoiceListItem).invoiceDate).slice(0, 10) }}
        </template>
        <template #amountExcludingTax="{ record }">
          <span class="amount">¥ {{ (record as InvoiceListItem).amountExcludingTax.toFixed(2) }}</span>
        </template>
        <template #taxRate="{ record }">
          {{ formatTaxRate((record as InvoiceListItem).taxRate) }}
        </template>
        <template #taxAmount="{ record }">
          <span class="amount">¥ {{ (record as InvoiceListItem).taxAmount.toFixed(2) }}</span>
        </template>
        <template #totalAmount="{ record }">
          <span class="amount">¥ {{ (record as InvoiceListItem).totalAmount.toFixed(2) }}</span>
        </template>
        <template #orderNoSummary="{ record }">
          {{ (record as InvoiceListItem).orderNoSummary || '-' }}
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as InvoiceListItem).status === 1 ? 'green' : 'red'">
            {{ (record as InvoiceListItem).status === 1 ? '正常' : '已作废' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as InvoiceListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：2 个操作平铺 详情 / 作废；作废仅正常发票显示 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as InvoiceListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
            <a-popconfirm
              v-if="(record as InvoiceListItem).status === 1"
              type="warning"
              content="确认作废该发票？作废后其占用金额自动释放，且不可恢复"
              @ok="onVoid(record as InvoiceListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="danger"
                :loading="voidingId === (record as InvoiceListItem).id"
              >
                <template #icon>
                  <IconBan />
                </template>
                作废
              </a-button>
            </a-popconfirm>
          </a-space>
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

/* 作废行整体置灰 */
:deep(.row-voided) {
  opacity: 0.55;
}
</style>

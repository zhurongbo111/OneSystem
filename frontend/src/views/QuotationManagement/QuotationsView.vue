<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { convertQuotation, getQuotations, toDateRange, voidQuotation } from '@/api/quotation'
import type { QuotationListItem } from '@/api/quotation'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { isQuotationExpired, quotationStatusColor, quotationStatusLabel, quotationStatusOptions } from '@/utils/quotation'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconArrowForwardUp,
  IconBan,
  IconDotsVertical,
  IconEdit,
  IconEye,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

const auth = useAuthStore()

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 状态下拉选项（文案取自 design.md §0.1） */
const statusOptions = quotationStatusOptions()

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 正在作废的报价单 id（design §4.4：voidingId） */
const voidingId = ref<string | undefined>(undefined)
/** 正在转单的报价单 id（design §4.4：convertingId） */
const convertingId = ref<string | undefined>(undefined)
const items = ref<QuotationListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 状态 / 日期范围：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const statusInput = ref<number | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedStatus = ref<number | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedStatus.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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

/** 可选列（序号与操作列固定显示，不参与列设置：specs/011-action-column §0） */
const columnOptions = [
  { label: '单号', value: 'quotationNo' },
  { label: '客户', value: 'partnerName' },
  { label: '报价日期', value: 'quotationDate' },
  { label: '有效期至', value: 'validUntil' },
  { label: '金额', value: 'totalAmount' },
  { label: '明细行数', value: 'itemCount' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'quotationNo',
  'partnerName',
  'quotationDate',
  'validUntil',
  'totalAmount',
  'status',
  'createdAt',
])

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('quotationNo')) {
    cols.push({ title: '单号', slotName: 'quotationNo', width: 160 })
  }
  if (visibleColumns.value.includes('partnerName')) {
    cols.push({
      title: '客户',
      dataIndex: 'partnerName',
      width: 180,
      ellipsis: true,
      tooltip: true,
    })
  }
  if (visibleColumns.value.includes('quotationDate')) {
    cols.push({ title: '报价日期', slotName: 'quotationDate', width: 110 })
  }
  if (visibleColumns.value.includes('validUntil')) {
    cols.push({ title: '有效期至', slotName: 'validUntil', width: 140 })
  }
  if (visibleColumns.value.includes('totalAmount')) {
    cols.push({ title: '金额', slotName: 'totalAmount', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('itemCount')) {
    cols.push({ title: '明细行数', slotName: 'itemCount', width: 100, align: 'right' })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', slotName: 'status', width: 110, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  cols.push({ title: '操作', slotName: 'action', width: 240, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §0 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(async () => {
  await fetchList()
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
    const result = await getQuotations({
      keyword: appliedKeyword.value.trim() || undefined,
      status: appliedStatus.value as never,
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

function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedStatus.value = statusInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedStatus.value = undefined
  appliedRange.value = null
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

function onCreate(): void {
  void router.push({ name: 'quotationCreate' })
}

function onDetail(row: QuotationListItem): void {
  void router.push({ name: 'quotationDetail', params: { id: row.id } })
}

function onEdit(row: QuotationListItem): void {
  void router.push({ name: 'quotationEdit', params: { id: row.id } })
}

/** 已作废行整体置灰（design.md §0.1） */
function rowClassName(record: QuotationListItem): string {
  return record.status === 2 ? 'row-voided' : ''
}

/** 转销售订单：一次性整单转，成功后报价单锁定为「已转订单」（同一报价单只能转一次） */
async function onConvert(row: QuotationListItem): Promise<void> {
  if (convertingId.value) return
  convertingId.value = row.id
  try {
    const result = await convertQuotation(row.id)
    Message.success(`已转销售订单 ${result.orderNo}`)
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理（40167 已转订单 / 非草稿）
  } finally {
    convertingId.value = undefined
  }
}

/** 转单不可逆（报价单锁定），列表内先二次确认再执行 */
function confirmConvert(row: QuotationListItem): void {
  Modal.warning({
    title: '转销售订单',
    content: `确认将报价单 ${row.quotationNo} 转为销售订单？转单后报价单锁定为「已转订单」，不可再编辑 / 转单 / 作废`,
    hideCancel: false,
    okText: '确认转单',
    onOk: () => onConvert(row),
  })
}

/** 作废报价单：仅改状态不删数据（无库存 / 资金影响） */
async function onVoid(row: QuotationListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidQuotation(row.id)
    Message.success('报价单已作废')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 「更多」内的作废无法用 a-popconfirm 包裹菜单项，改用函数式确认框（等价二次确认，specs/011-action-column §0） */
function confirmVoid(row: QuotationListItem): void {
  Modal.warning({
    title: '作废报价单',
    content: `确认作废报价单 ${row.quotationNo}？作废后不可恢复`,
    hideCancel: false,
    okText: '确认作废',
    onOk: () => onVoid(row),
  })
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        报价单
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
              placeholder="搜索单号 / 客户"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              :options="statusOptions"
              placeholder="报价状态"
              allow-clear
            />
          </a-col>
          <a-col :span="8">
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

        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('quotations.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建报价单
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
        <template #quotationNo="{ record }">
          {{ (record as QuotationListItem).quotationNo }}
        </template>
        <template #quotationDate="{ record }">
          {{ formatDateTime((record as QuotationListItem).quotationDate).slice(0, 10) }}
        </template>
        <template #validUntil="{ record }">
          <span>{{ (record as QuotationListItem).validUntil ?? '—' }}</span>
          <a-tag
            v-if="isQuotationExpired((record as QuotationListItem).status, (record as QuotationListItem).validUntil)"
            size="small"
            color="orange"
            class="expired-tag"
          >
            已过期
          </a-tag>
        </template>
        <template #totalAmount="{ record }">
          <span class="amount">
            ¥ {{ (record as QuotationListItem).totalAmount.toFixed(2) }}
          </span>
        </template>
        <template #itemCount="{ record }">
          {{ (record as QuotationListItem).itemCount }}
        </template>
        <template #status="{ record }">
          <a-tag :color="quotationStatusColor((record as QuotationListItem).status)">
            {{ quotationStatusLabel((record as QuotationListItem).status) }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as QuotationListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as QuotationListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              查看
            </a-button>

            <a-button
              v-if="(record as QuotationListItem).status === 0 && auth.hasPermission('quotations.update')"
              type="text"
              size="small"
              @click="onEdit(record as QuotationListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>

            <a-button
              v-if="(record as QuotationListItem).status === 0 && auth.hasPermission('quotations.convert')"
              type="text"
              size="small"
              status="success"
              :loading="convertingId === (record as QuotationListItem).id"
              @click="confirmConvert(record as QuotationListItem)"
            >
              <template #icon>
                <IconArrowForwardUp />
              </template>
              转订单
            </a-button>

            <a-dropdown
              v-if="(record as QuotationListItem).status === 0 && auth.hasPermission('quotations.void')"
              trigger="click"
            >
              <a-button
                type="text"
                size="small"
                :loading="voidingId === (record as QuotationListItem).id"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  :disabled="!!voidingId"
                  @click="confirmVoid(record as QuotationListItem)"
                >
                  <template #icon>
                    <IconBan />
                  </template>
                  作废
                </a-doption>
              </template>
            </a-dropdown>
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

.table-card {
  border-radius: var(--border-radius-medium);
}

.amount {
  font-variant-numeric: tabular-nums;
}

.expired-tag {
  margin-left: 6px;
}

.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}

.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
}

:deep(.row-voided) {
  opacity: 0.55;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { exportSalesReturns } from '@/api/export'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import {
  getSalesReturns,
  toDateRange,
  voidSalesReturn,
  type SalesReturnListItem,
} from '@/api/saleReturn'
import { formatDateTime } from '@/utils/datetime'
import {
  SETTLEMENT_STATE_OPTIONS,
  canStartSettlement,
  canVoidOrder,
  settlementStateColor,
  settlementStateLabel,
  VOID_SETTLED_HINT,
  type SettlementState,
} from '@/utils/settlement'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconBan,
  IconCash,
  IconDotsVertical,
  IconDownload,
  IconEye,
  IconPlus,
  IconPrinter,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
/** 正在作废的单据 id（design.md §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)
const items = ref<SalesReturnListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 客户 / 日期范围 / 结算：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const partnerInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const settlementInput = ref<SettlementState | undefined>(undefined)
const appliedKeyword = ref('')
const appliedPartner = ref<string | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)
const appliedSettlement = ref<SettlementState | undefined>(undefined)

/** 客户下拉数据源（全量拉取后前端筛「客户 / 两者」，后端查询仅支持单值 type） */
const partners = ref<Partner[]>([])
const customerOptions = computed(() =>
  partners.value
    .filter((p) => p.type === 2 || p.type === 3)
    .map((p) => ({ label: p.name, value: p.id })),
)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedPartner.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}|${appliedSettlement.value ?? ''}`,
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

/** 可选列（序号与操作列固定显示，不参与列设置：specs/011-action-column §5） */
const columnOptions = [
  { label: '单号', value: 'returnNo' },
  { label: '客户', value: 'partnerName' },
  { label: '退货日期', value: 'returnDate' },
  { label: '总金额', value: 'totalAmount' },
  { label: '结算状态', value: 'settlement' },
  { label: '单据状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'returnNo',
  'partnerName',
  'returnDate',
  'totalAmount',
  'settlement',
  'status',
  'createdAt',
])

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('returnNo')) {
    cols.push({ title: '单号', slotName: 'returnNo', width: 160 })
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
  if (visibleColumns.value.includes('returnDate')) {
    cols.push({ title: '退货日期', slotName: 'returnDate', width: 110 })
  }
  if (visibleColumns.value.includes('totalAmount')) {
    cols.push({ title: '总金额', slotName: 'totalAmount', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('settlement')) {
    cols.push({ title: '结算状态', slotName: 'settlement', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '单据状态', slotName: 'status', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  cols.push({ title: '操作', slotName: 'action', width: 280, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §2 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(async () => {
  void fetchList()
  try {
    const [customer, both] = await Promise.all([
      getPartners({ type: 2, status: 1, page: 1, pageSize: 100 }),
      getPartners({ type: 3, status: 1, page: 1, pageSize: 100 }),
    ])
    const seen = new Set<string>()
    partners.value = [...customer.items, ...both.items].filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)))
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
    const result = await getSalesReturns({
      keyword: appliedKeyword.value.trim() || undefined,
      partnerId: appliedPartner.value,
      start,
      end,
      settlementState: appliedSettlement.value,
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
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  appliedSettlement.value = settlementInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  partnerInput.value = undefined
  dateRangeInput.value = undefined
  settlementInput.value = undefined
  appliedKeyword.value = ''
  appliedPartner.value = undefined
  appliedRange.value = null
  appliedSettlement.value = undefined
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

function onCreate(): void {
  void router.push({ name: 'saleReturnNew' })
}

function onDetail(row: SalesReturnListItem): void {
  void router.push({ name: 'saleReturnDetail', params: { id: row.id } })
}

/** 导出当前已应用筛选的全量销售退货单（单据 + 明细两个工作表）；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    const { start, end } = appliedRange.value
      ? toDateRange(appliedRange.value[0], appliedRange.value[1])
      : {}
    await exportSalesReturns({
      keyword: appliedKeyword.value.trim() || undefined,
      partnerId: appliedPartner.value,
      start,
      end,
      settlementState: appliedSettlement.value,
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

/** 打印单据：同步路由跳转（瞬时动作不置 loading） */
function onPrint(row: SalesReturnListItem): void {
  void router.push({ name: 'saleReturnPrint', params: { id: row.id } })
}

/** 作废行整体置灰（design.md §4.4，同 021） */
function rowClassName(record: SalesReturnListItem): string {
  return record.status === 0 ? 'row-voided' : ''
}

/** 作废：回冲库存，仅改状态不删数据 */
async function onVoid(row: SalesReturnListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidSalesReturn(row.id)
    Message.success('已作废，库存已回冲')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 去收付款：销售退货单为付款方向（type=1，我们退客户钱），预置往来单位 */
function onGoSettlement(row: SalesReturnListItem): void {
  void router.push({ name: 'settlementNew', query: { type: '1', partnerId: row.partnerId } })
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（主操作已并入表格上方工具条左组） -->
    <div class="page-header">
      <h1 class="page-title">
        销售退货
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
              class="filter-bar__search"
              placeholder="搜索单号 / 客户"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="partnerInput"
              :options="customerOptions"
              placeholder="全部客户"
              allow-clear
              :loading="partners.length === 0"
            />
          </a-col>
          <a-col :span="7">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              :allow-clear="true"
              style="width: 100%"
            />
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="settlementInput"
              :options="SETTLEMENT_STATE_OPTIONS"
              placeholder="结算状态"
              allow-clear
            />
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

        <!-- 操作行：左组主操作（开退货单）靠左，右组视图操作（列设置/刷新）靠右，同一行 -->
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
              开退货单
            </a-button>
          </div>
          <div class="toolbar-actions__right">
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
        <template #returnNo="{ record }">
          {{ (record as SalesReturnListItem).returnNo }}
        </template>
        <template #returnDate="{ record }">
          {{ formatDateTime((record as SalesReturnListItem).returnDate).slice(0, 10) }}
        </template>
        <template #totalAmount="{ record }">
          <span class="amount">
            ¥ {{ (record as SalesReturnListItem).totalAmount.toFixed(2) }}
          </span>
        </template>
        <template #settlement="{ record }">
          <a-tag :color="settlementStateColor((record as SalesReturnListItem).settlementState)">
            {{ settlementStateLabel((record as SalesReturnListItem).settlementState, (record as SalesReturnListItem).unsettledAmount) }}
          </a-tag>
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as SalesReturnListItem).status === 1 ? 'green' : 'red'">
            {{ (record as SalesReturnListItem).status === 1 ? '正常' : '已作废' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as SalesReturnListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：4 个操作 > 3，平铺 详情/收付款/作废，「打印」收纳进「更多」；详情恒显，作废/收付款仅正常单显示 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as SalesReturnListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>

            <a-button
              v-if="canStartSettlement(record as SalesReturnListItem)"
              type="text"
              size="small"
              @click="onGoSettlement(record as SalesReturnListItem)"
            >
              <template #icon>
                <IconCash />
              </template>
              收付款
            </a-button>
            <!-- 已核销 → 禁用并提示处置顺序；未核销 → 二次确认（前端提前拦截，后端 40120 兜底） -->
            <a-tooltip
              v-if="(record as SalesReturnListItem).status === 1 && !canVoidOrder(record as SalesReturnListItem)"
              :content="VOID_SETTLED_HINT"
            >
              <span>
                <a-button
                  type="text"
                  size="small"
                  status="danger"
                  disabled
                >
                  <template #icon>
                    <IconBan />
                  </template>
                  作废
                </a-button>
              </span>
            </a-tooltip>
            <a-popconfirm
              v-else-if="canVoidOrder(record as SalesReturnListItem)"
              type="warning"
              content="确认作废该销售退货单？作废后库存将回冲，且不可恢复"
              @ok="onVoid(record as SalesReturnListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="danger"
                :loading="voidingId === (record as SalesReturnListItem).id"
              >
                <template #icon>
                  <IconBan />
                </template>
                作废
              </a-button>
            </a-popconfirm>
            <a-dropdown trigger="click">
              <a-button
                type="text"
                size="small"
                aria-label="更多操作"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  value="print"
                  @click="onPrint(record as SalesReturnListItem)"
                >
                  <template #icon>
                    <IconPrinter />
                  </template>
                  打印
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

/* 次要操作（如「改回未结算」）：降为次级文字色，与「详情」主题色区分 */
.row-actions :deep(.arco-btn-text.action-btn-secondary) {
  color: var(--color-text-2);
}

.row-actions :deep(.arco-btn-text.action-btn-secondary:hover) {
  color: var(--color-text-1);
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

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { toDateRange } from '@/api/purchase'
import {
  closePurchaseOrder,
  getPurchaseOrders,
  voidPurchaseOrder,
  type PurchaseOrderListItem,
} from '@/api/purchaseOrder'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { orderFlowStatusColor, orderFlowStatusLabel, orderFlowStatusOptions } from '@/utils/orderFlow'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconArchive,
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

/** 状态下拉选项（文案取自 design.md §0，采购侧为待收货 / 部分收货） */
const flowStatusOptions = orderFlowStatusOptions('purchase')

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 正在作废 / 关闭的订单 id（design §4.4：voidingId / closingId） */
const voidingId = ref<string | undefined>(undefined)
const closingId = ref<string | undefined>(undefined)
const items = ref<PurchaseOrderListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 供应商 / 状态 / 日期范围：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const partnerInput = ref<string | undefined>(undefined)
const flowStatusInput = ref<number | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedPartner = ref<string | undefined>(undefined)
const appliedFlowStatus = ref<number | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

/** 供应商下拉数据源（全量拉取后前端筛「供应商 / 两者」，后端查询仅支持单值 type） */
const partners = ref<Partner[]>([])
const supplierOptions = computed(() =>
  partners.value
    .filter((p) => p.type === 1 || p.type === 3)
    .map((p) => ({ label: p.name, value: p.id })),
)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedPartner.value ?? ''}|${appliedFlowStatus.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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
  { label: '单号', value: 'orderNo' },
  { label: '供应商', value: 'partnerName' },
  { label: '订单日期', value: 'orderDate' },
  { label: '预计到货', value: 'expectedDate' },
  { label: '总金额', value: 'totalAmount' },
  { label: '未收数量', value: 'unfulfilledQuantity' },
  { label: '订单状态', value: 'flowStatus' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'orderNo',
  'partnerName',
  'orderDate',
  'expectedDate',
  'totalAmount',
  'unfulfilledQuantity',
  'flowStatus',
  'createdAt',
])

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('orderNo')) {
    cols.push({ title: '单号', slotName: 'orderNo', width: 160 })
  }
  if (visibleColumns.value.includes('partnerName')) {
    cols.push({
      title: '供应商',
      dataIndex: 'partnerName',
      width: 180,
      ellipsis: true,
      tooltip: true,
    })
  }
  if (visibleColumns.value.includes('orderDate')) {
    cols.push({ title: '订单日期', slotName: 'orderDate', width: 110 })
  }
  if (visibleColumns.value.includes('expectedDate')) {
    cols.push({ title: '预计到货', slotName: 'expectedDate', width: 110 })
  }
  if (visibleColumns.value.includes('totalAmount')) {
    cols.push({ title: '总金额', slotName: 'totalAmount', width: 120, align: 'right' })
  }
  if (visibleColumns.value.includes('unfulfilledQuantity')) {
    cols.push({ title: '未收数量', slotName: 'unfulfilledQuantity', width: 100, align: 'right' })
  }
  if (visibleColumns.value.includes('flowStatus')) {
    cols.push({ title: '订单状态', slotName: 'flowStatus', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 4 个操作 → 前 3 平铺、作废收纳进「更多」（specs/011-action-column §0：含「更多」列宽 200~260）
  cols.push({ title: '操作', slotName: 'action', width: 240, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §2 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(async () => {
  void fetchList()
  try {
    const [supplier, both] = await Promise.all([
      getPartners({ type: 1, status: 1, page: 1, pageSize: 100 }),
      getPartners({ type: 3, status: 1, page: 1, pageSize: 100 }),
    ])
    const seen = new Set<string>()
    partners.value = [...supplier.items, ...both.items].filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)))
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
    const result = await getPurchaseOrders({
      keyword: appliedKeyword.value.trim() || undefined,
      partnerId: appliedPartner.value,
      flowStatus: appliedFlowStatus.value as never,
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
  appliedPartner.value = partnerInput.value
  appliedFlowStatus.value = flowStatusInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  partnerInput.value = undefined
  flowStatusInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedPartner.value = undefined
  appliedFlowStatus.value = undefined
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
  void router.push({ name: 'purchaseOrderNew' })
}

function onDetail(row: PurchaseOrderListItem): void {
  void router.push({ name: 'purchaseOrderDetail', params: { id: row.id } })
}

function onEdit(row: PurchaseOrderListItem): void {
  void router.push({ name: 'purchaseOrderEdit', params: { id: row.id } })
}

/** 已作废 / 已关闭行整体置灰（design §4.3） */
function rowClassName(record: PurchaseOrderListItem): string {
  return record.flowStatus === 0 || record.flowStatus === 4 ? 'row-voided' : ''
}

/** 关闭订单：剩余不再收货（保留累计量） */
async function onClose(row: PurchaseOrderListItem): Promise<void> {
  if (closingId.value) return
  closingId.value = row.id
  try {
    await closePurchaseOrder(row.id)
    Message.success('订单已关闭，剩余数量不再收货')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    closingId.value = undefined
  }
}

/** 作废订单：仅改状态不删数据（无库存影响） */
async function onVoid(row: PurchaseOrderListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidPurchaseOrder(row.id)
    Message.success('订单已作废')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 「更多」内的作废无法用 a-popconfirm 包裹菜单项，改用函数式确认框（等价二次确认，specs/011-action-column §0） */
function confirmVoid(row: PurchaseOrderListItem): void {
  Modal.warning({
    title: '作废订单',
    content: `确认作废订单 ${row.orderNo}？作废后不可恢复`,
    hideCancel: false,
    okText: '确认作废',
    onOk: () => onVoid(row),
  })
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（主操作已并入表格上方工具条左组） -->
    <div class="page-header">
      <h1 class="page-title">
        采购订单
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
              placeholder="搜索单号 / 供应商"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="partnerInput"
              :options="supplierOptions"
              placeholder="全部供应商"
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
              v-model="flowStatusInput"
              :options="flowStatusOptions"
              placeholder="订单状态"
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

        <!-- 操作行：左组主操作靠左，右组视图操作靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('purchaseOrders.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建订单
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
        <template #orderNo="{ record }">
          {{ (record as PurchaseOrderListItem).orderNo }}
        </template>
        <template #orderDate="{ record }">
          {{ formatDateTime((record as PurchaseOrderListItem).orderDate).slice(0, 10) }}
        </template>
        <template #expectedDate="{ record }">
          {{ (record as PurchaseOrderListItem).expectedDate ? formatDateTime((record as PurchaseOrderListItem).expectedDate as string).slice(0, 10) : '—' }}
        </template>
        <template #totalAmount="{ record }">
          <span class="amount">
            ¥ {{ (record as PurchaseOrderListItem).totalAmount.toFixed(2) }}
          </span>
        </template>
        <template #unfulfilledQuantity="{ record }">
          <span :class="{ 'qty-zero': (record as PurchaseOrderListItem).unfulfilledQuantity === 0 }">
            {{ (record as PurchaseOrderListItem).unfulfilledQuantity }}
          </span>
        </template>
        <template #flowStatus="{ record }">
          <a-tag :color="orderFlowStatusColor((record as PurchaseOrderListItem).flowStatus)">
            {{ orderFlowStatusLabel((record as PurchaseOrderListItem).flowStatus, 'purchase') }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as PurchaseOrderListItem).createdAt) }}
        </template>
        <!-- 操作列：详情恒显；编辑仅待收货；关闭待收货 / 部分收货；作废仅待收货（收纳进「更多」） -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="(record as PurchaseOrderListItem).flowStatus === 1"
              type="text"
              size="small"
              @click="onEdit(record as PurchaseOrderListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>

            <a-button
              type="text"
              size="small"
              @click="onDetail(record as PurchaseOrderListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>

            <a-popconfirm
              v-if="(record as PurchaseOrderListItem).flowStatus === 1 || (record as PurchaseOrderListItem).flowStatus === 2"
              type="warning"
              content="确认关闭该订单？关闭后剩余数量不再收货，且不可恢复"
              @ok="onClose(record as PurchaseOrderListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="warning"
                :loading="closingId === (record as PurchaseOrderListItem).id"
              >
                <template #icon>
                  <IconArchive />
                </template>
                关闭
              </a-button>
            </a-popconfirm>

            <a-dropdown
              v-if="(record as PurchaseOrderListItem).flowStatus === 1"
              trigger="click"
            >
              <a-button
                type="text"
                size="small"
                :loading="voidingId === (record as PurchaseOrderListItem).id"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  :disabled="!!voidingId"
                  @click="confirmVoid(record as PurchaseOrderListItem)"
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

/* 未收数量为 0 时（已收齐）降为次要色 */
.qty-zero {
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

/* 已作废 / 已关闭行整体置灰（design §4.3） */
:deep(.row-voided) {
  opacity: 0.55;
}
</style>

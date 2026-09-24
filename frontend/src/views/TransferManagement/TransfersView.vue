<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  getTransfers,
  toDateRange,
  voidTransfer,
  type TransferListItem,
} from '@/api/transfer'
import { getWarehousePickList } from '@/api/warehouse'
import type { WarehousePickItem } from '@/api/warehouse'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconBan,
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

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 正在作废的单据 id（design §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)
const items = ref<TransferListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 转出仓 / 转入仓 / 日期范围：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const fromWarehouseInput = ref<string | undefined>(undefined)
const toWarehouseInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedFromWarehouse = ref<string | undefined>(undefined)
const appliedToWarehouse = ref<string | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

/** 仓库下拉数据源（仅启用仓，038） */
const warehouses = ref<WarehousePickItem[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedFromWarehouse.value ?? ''}|${appliedToWarehouse.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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
  { label: '单号', value: 'transferNo' },
  { label: '转出仓', value: 'fromWarehouseName' },
  { label: '转入仓', value: 'toWarehouseName' },
  { label: '调拨日期', value: 'transferDate' },
  { label: '商品行数', value: 'itemCount' },
  { label: '数量合计', value: 'totalQuantity' },
  { label: '单据状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'transferNo',
  'fromWarehouseName',
  'toWarehouseName',
  'transferDate',
  'itemCount',
  'totalQuantity',
  'status',
  'createdAt',
])

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('transferNo')) {
    cols.push({ title: '单号', slotName: 'transferNo', width: 160 })
  }
  if (visibleColumns.value.includes('fromWarehouseName')) {
    // 转出仓：单据保存即固化，此处只读展示
    cols.push({ title: '转出仓', dataIndex: 'fromWarehouseName', width: 140, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('toWarehouseName')) {
    // 转入仓：单据保存即固化，此处只读展示
    cols.push({ title: '转入仓', dataIndex: 'toWarehouseName', width: 140, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('transferDate')) {
    cols.push({ title: '调拨日期', slotName: 'transferDate', width: 110 })
  }
  if (visibleColumns.value.includes('itemCount')) {
    cols.push({ title: '商品行数', dataIndex: 'itemCount', width: 100, align: 'right' })
  }
  if (visibleColumns.value.includes('totalQuantity')) {
    cols.push({ title: '数量合计', dataIndex: 'totalQuantity', width: 110, align: 'right' })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '单据状态', slotName: 'status', width: 100, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  cols.push({ title: '操作', slotName: 'action', width: 200, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度（specs/011-action-column §2 列宽策略） */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(async () => {
  void fetchList()
  try {
    warehouses.value = await getWarehousePickList()
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
    const result = await getTransfers({
      keyword: appliedKeyword.value.trim() || undefined,
      fromWarehouseId: appliedFromWarehouse.value,
      toWarehouseId: appliedToWarehouse.value,
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
  appliedFromWarehouse.value = fromWarehouseInput.value
  appliedToWarehouse.value = toWarehouseInput.value
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  fromWarehouseInput.value = undefined
  toWarehouseInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedFromWarehouse.value = undefined
  appliedToWarehouse.value = undefined
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

function onCreate(): void {
  void router.push({ name: 'transferNew' })
}

function onDetail(row: TransferListItem): void {
  void router.push({ name: 'transferDetail', params: { id: row.id } })
}

/** 作废行整体置灰（design §4.4） */
function rowClassName(record: TransferListItem): string {
  return record.status === 0 ? 'row-voided' : ''
}

/** 作废：双向回冲库存与流水，仅改状态不删数据 */
async function onVoid(row: TransferListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidTransfer(row.id)
    Message.success('已作废，库存已回冲')
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
    <!-- 页面头：仅标题（主操作已并入表格上方工具条左组） -->
    <div class="page-header">
      <h1 class="page-title">
        调拨单
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
          <a-col :span="4">
            <a-input
              v-model="keywordInput"
              class="filter-bar__search"
              placeholder="搜索单号"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="fromWarehouseInput"
              :options="warehouses.map((w) => ({ label: w.name, value: w.id }))"
              placeholder="全部转出仓"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="toWarehouseInput"
              :options="warehouses.map((w) => ({ label: w.name, value: w.id }))"
              placeholder="全部转入仓"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              :allow-clear="true"
              style="width: 100%"
            />
          </a-col>
          <a-col :span="7">
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

        <!-- 操作行：左组主操作（新调拨）靠左，右组视图操作（列设置/刷新）靠右，同一行 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('transfers.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新调拨
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
        <template #transferNo="{ record }">
          {{ (record as TransferListItem).transferNo }}
        </template>
        <template #transferDate="{ record }">
          {{ formatDateTime((record as TransferListItem).transferDate).slice(0, 10) }}
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as TransferListItem).status === 1 ? 'green' : 'red'">
            {{ (record as TransferListItem).status === 1 ? '正常' : '已作废' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as TransferListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：2 个操作 ≤ 3，平铺 详情/作废；详情恒显，作废仅正常单显示（design §4.4） -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as TransferListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
            <a-popconfirm
              v-if="(record as TransferListItem).status === 1"
              type="warning"
              content="确认作废该调拨单？作废后双仓库存将回冲，且不可恢复"
              @ok="onVoid(record as TransferListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="danger"
                :loading="voidingId === (record as TransferListItem).id"
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

.table-card {
  border-radius: var(--border-radius-medium);
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

/* 作废行整体置灰（design §4.4） */
:deep(.row-voided) {
  opacity: 0.55;
}
</style>

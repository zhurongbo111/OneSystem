<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { exportStockTakes } from '@/api/export'
import { getStockTakes, toDateRange, type StockTakeListItem, type StockTakeType } from '@/api/stockTake'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconDownload, IconListDetails, IconPlus, IconPrinter, IconRefresh, IconRestore, IconSearch, IconSettings } from '@tabler/icons-vue'

const auth = useAuthStore()

// —— constants ——
const typeOptions: { label: string; value: StockTakeType }[] = [
  { label: '期初建账', value: 0 },
  { label: '库存盘点', value: 1 },
]

/** 类型渲染（design §4.4：期初建账 purple / 库存盘点 gold） */
const TYPE_META: Record<StockTakeType, { label: string; color: string }> = {
  0: { label: '期初建账', color: 'purple' },
  1: { label: '库存盘点', color: 'gold' },
}

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const router = useRouter()

const loading = ref(false)
/** 导出（erp-export）：与查询 loading 分开，防重入 */
const exporting = ref(false)
const items = ref<StockTakeListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 单号关键词 / 类型 / 日期范围：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const typeInput = ref<StockTakeType | undefined>(undefined)
const dateRangeInput = ref<string[] | undefined>(undefined)
const appliedKeyword = ref('')
const appliedType = ref<StockTakeType | undefined>(undefined)
const appliedRange = ref<[string, string] | null>(null)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedType.value ?? ''}|${appliedRange.value?.[0] ?? ''}|${appliedRange.value?.[1] ?? ''}`,
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

/** 可选列（序号与操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '单号', value: 'takeNo' },
  { label: '类型', value: 'type' },
  { label: '盘点日期', value: 'takeDate' },
  { label: '明细行数', value: 'itemCount' },
  { label: '差异行数', value: 'diffItemCount' },
  { label: '备注', value: 'remark' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'takeNo',
  'type',
  'takeDate',
  'itemCount',
  'diffItemCount',
  'remark',
  'createdAt',
])

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('takeNo')) {
    cols.push({ title: '单号', slotName: 'takeNo', width: 160 })
  }
  if (visibleColumns.value.includes('type')) {
    cols.push({ title: '类型', slotName: 'type', width: 110, align: 'center' })
  }
  if (visibleColumns.value.includes('takeDate')) {
    cols.push({ title: '盘点日期', slotName: 'takeDate', width: 110 })
  }
  if (visibleColumns.value.includes('itemCount')) {
    cols.push({ title: '明细行数', dataIndex: 'itemCount', width: 90, align: 'right' })
  }
  if (visibleColumns.value.includes('diffItemCount')) {
    cols.push({ title: '差异行数', slotName: 'diffItemCount', width: 90, align: 'right' })
  }
  if (visibleColumns.value.includes('remark')) {
    cols.push({ title: '备注', dataIndex: 'remark', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 操作列：2 个操作 ≤ 3 平铺（详情 / 打印），宽度按 specs/011-action-column §0 取值 150
  cols.push({ title: '操作', slotName: 'action', width: 150, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
})

// —— methods ——
function takeNo(record: Record<string, unknown>): string {
  return (record.takeNo as string) ?? ''
}

function typeMeta(record: Record<string, unknown>): { label: string; color: string } {
  return TYPE_META[(record.type as StockTakeType) ?? 1]
}

function takeDate(record: Record<string, unknown>): string {
  return (record.takeDate as string) ?? ''
}

function diffCount(record: Record<string, unknown>): number {
  return (record.diffItemCount as number) ?? 0
}

function createdAt(record: Record<string, unknown>): string {
  return (record.createdAt as string) ?? ''
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { start, end } = appliedRange.value ? toDateRange(appliedRange.value[0], appliedRange.value[1]) : {}
    const result = await getStockTakes({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
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
  appliedRange.value = dateRangeInput.value ? (dateRangeInput.value as [string, string]) : null
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  typeInput.value = undefined
  dateRangeInput.value = undefined
  appliedKeyword.value = ''
  appliedType.value = undefined
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
  void router.push({ name: 'stockTakeNew' })
}

function onDetail(row: Record<string, unknown>): void {
  const id = row.id as string
  void router.push({ name: 'stockTakeDetail', params: { id } })
}

/** 导出当前已应用筛选的全量库存盘点单（单据 + 明细两个工作表）；失败提示由请求层统一处理 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    const { start, end } = appliedRange.value ? toDateRange(appliedRange.value[0], appliedRange.value[1]) : {}
    await exportStockTakes({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
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

/** 打印单据：同步路由跳转（瞬时动作不置 loading） */
function onPrint(row: Record<string, unknown>): void {
  const id = row.id as string
  void router.push({ name: 'stockTakePrint', params: { id } })
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（主操作已并入表格上方工具条左组） -->
    <div class="page-header">
      <h1 class="page-title">
        库存盘点
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
              placeholder="搜索单号"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="typeInput"
              class="filter-bar__type"
              :options="typeOptions"
              placeholder="全部类型"
              allow-clear
            />
          </a-col>
          <a-col :span="7">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              class="filter-bar__range"
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

        <!-- 操作行：左组主操作（新建）靠左，右组视图操作（列设置 / 刷新）靠右，同一行 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('stockTakes.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建盘点
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
        <template #takeNo="{ record }">
          {{ takeNo(record) }}
        </template>
        <template #type="{ record }">
          <a-tag :color="typeMeta(record).color">
            {{ typeMeta(record).label }}
          </a-tag>
        </template>
        <template #takeDate="{ record }">
          {{ formatDateTime(takeDate(record)).slice(0, 10) }}
        </template>
        <template #diffItemCount="{ record }">
          <span :class="diffCount(record) > 0 ? 'diff-count' : ''">
            {{ diffCount(record) }}
          </span>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime(createdAt(record)) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：2 个操作平铺 详情/打印 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record)"
            >
              <template #icon>
                <IconListDetails />
              </template>
              详情
            </a-button>
            <a-button
              type="text"
              size="small"
              @click="onPrint(record)"
            >
              <template #icon>
                <IconPrinter />
              </template>
              打印
            </a-button>
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
  justify-content: flex-end;
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

/* 差异行数 > 0 标橙（design §4.4） */
.diff-count {
  color: var(--color-warning-6);
  font-weight: 600;
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
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  closePeriod,
  getPeriods,
  getVouchers,
  reversePeriod,
  voidVoucher,
  PERIOD_STATUS_META,
  VOUCHER_SOURCE_TYPE_META,
  VOUCHER_SOURCE_TYPE_OPTIONS,
  VOUCHER_STATUS_META,
  type Period,
  type VoucherListItem,
  type VoucherSourceType,
} from '@/api/voucher'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconBan,
  IconCalendarStats,
  IconEye,
  IconListDetails,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

import AccountMappingsView from './AccountMappingsView.vue'

// —— constants ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

/** 可选列（序号与操作列固定显示，不参与列设置） */
const columnOptions = [
  { label: '凭证号', value: 'voucherNo' },
  { label: '记账日期', value: 'voucherDate' },
  { label: '摘要', value: 'summary' },
  { label: '来源', value: 'sourceType' },
  { label: '来源单据号', value: 'sourceNo' },
  { label: '借方合计', value: 'totalDebit' },
  { label: '贷方合计', value: 'totalCredit' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

// —— reactive state ——
const router = useRouter()
const auth = useAuthStore()

const loading = ref(false)
/** 正在作废的凭证 id（design §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)
/** 正在结账 / 反结账的期间 id（同一时刻只允许一次写操作） */
const closingId = ref<string | undefined>(undefined)
const items = ref<VoucherListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 来源 / 期间：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const sourceTypeInput = ref<VoucherSourceType | undefined>(undefined)
const periodInput = ref<string | undefined>(undefined)
const appliedKeyword = ref('')
const appliedSourceType = ref<VoucherSourceType | undefined>(undefined)
const appliedPeriod = ref<{ year: number; month: number } | null>(null)

/** 会计期间（期间筛选下拉 + 期间管理抽屉共用） */
const periods = ref<Period[]>([])
const periodsVisible = ref(false)
const mappingsVisible = ref(false)

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'voucherNo',
  'voucherDate',
  'summary',
  'sourceType',
  'sourceNo',
  'totalDebit',
  'totalCredit',
  'status',
])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () => `${appliedKeyword.value}|${appliedSourceType.value ?? ''}|${appliedPeriod.value ? `${appliedPeriod.value.year}-${appliedPeriod.value.month}` : ''}`,
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

/** 期间下拉选项（值为 `年-月`，最新期间在前） */
const periodOptions = computed(() =>
  [...periods.value]
    .sort((a, b) => (b.year - a.year) || (b.month - a.month))
    .map((p) => ({ label: `${p.year}-${String(p.month).padStart(2, '0')}`, value: `${p.year}-${p.month}` })),
)

/** 表格列：序号 + 可选列 + 操作（序号与操作固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('voucherNo')) {
    cols.push({ title: '凭证号', dataIndex: 'voucherNo', width: 160 })
  }
  if (visibleColumns.value.includes('voucherDate')) {
    cols.push({ title: '记账日期', slotName: 'voucherDate', width: 120 })
  }
  if (visibleColumns.value.includes('summary')) {
    cols.push({ title: '摘要', dataIndex: 'summary', width: 220, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('sourceType')) {
    cols.push({ title: '来源', slotName: 'sourceType', width: 130, align: 'center' })
  }
  if (visibleColumns.value.includes('sourceNo')) {
    cols.push({ title: '来源单据号', slotName: 'sourceNo', width: 160, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('totalDebit')) {
    cols.push({ title: '借方合计', slotName: 'totalDebit', width: 130, align: 'right' })
  }
  if (visibleColumns.value.includes('totalCredit')) {
    cols.push({ title: '贷方合计', slotName: 'totalCredit', width: 130, align: 'right' })
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
  await fetchPeriods()
})

// —— methods ——
/** 拉取会计期间（期间筛选与期间管理共用） */
async function fetchPeriods(): Promise<void> {
  try {
    periods.value = await getPeriods()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getVouchers({
      keyword: appliedKeyword.value.trim() || undefined,
      sourceType: appliedSourceType.value,
      year: appliedPeriod.value?.year,
      month: appliedPeriod.value?.month,
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
  appliedSourceType.value = sourceTypeInput.value
  appliedPeriod.value = parsePeriod(periodInput.value)
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  sourceTypeInput.value = undefined
  periodInput.value = undefined
  appliedKeyword.value = ''
  appliedSourceType.value = undefined
  appliedPeriod.value = null
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

/** 录入手工凭证：独立页面（主表 + 分录子表格，specs/007-form-detail-showcase §0） */
function onCreate(): void {
  void router.push({ name: 'voucherNew' })
}

function onDetail(row: VoucherListItem): void {
  void router.push({ name: 'voucherDetail', params: { id: row.id } })
}

/** 作废凭证：仅改状态不删数据，余额随之回退 */
async function onVoid(row: VoucherListItem): Promise<void> {
  if (voidingId.value) return
  voidingId.value = row.id
  try {
    await voidVoucher(row.id)
    Message.success('凭证已作废，余额已回退')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 期间结账 / 反结账：结账后该期间禁止新增 / 作废凭证 */
async function onTogglePeriod(period: Period): Promise<void> {
  if (closingId.value) return
  closingId.value = period.id
  try {
    if (period.status === 1) {
      await reversePeriod(period.id)
      Message.success(`期间 ${period.year}-${String(period.month).padStart(2, '0')} 已反结账`)
    } else {
      await closePeriod(period.id)
      Message.success(`期间 ${period.year}-${String(period.month).padStart(2, '0')} 已结账`)
    }
    await fetchPeriods()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    closingId.value = undefined
  }
}

/** 科目映射保存成功后提示（凭证生成依赖映射完整性） */
function onMappingsSaved(): void {
  Message.success('科目映射已保存')
}

/** 作废行整体置灰 */
function rowClassName(record: VoucherListItem): string {
  return record.status === 0 ? 'row-voided' : ''
}

/** 解析期间下拉值（`年-月`）为年月；非法值返回 null */
function parsePeriod(value: string | undefined): { year: number; month: number } | null {
  if (!value) return null
  const [year, month] = value.split('-').map(Number)
  if (!year || !month) return null
  return { year, month }
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        凭证
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
              placeholder="搜索凭证号 / 摘要"
              allow-clear
              @press-enter="onSearch"
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="periodInput"
              :options="periodOptions"
              placeholder="全部期间"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="sourceTypeInput"
              :options="VOUCHER_SOURCE_TYPE_OPTIONS"
              placeholder="全部来源"
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

        <!-- 操作行：左组主操作（手工凭证 / 科目映射 / 期间管理）靠左，右组视图操作（列设置 / 刷新）靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('vouchers.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              手工凭证
            </a-button>
            <a-button
              v-if="auth.hasPermission('vouchers.updateMapping')"
              size="small"
              @click="mappingsVisible = true"
            >
              <template #icon>
                <IconListDetails />
              </template>
              科目映射
            </a-button>
            <a-button
              size="small"
              @click="periodsVisible = true"
            >
              <template #icon>
                <IconCalendarStats />
              </template>
              期间管理
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
        <template #voucherDate="{ record }">
          {{ formatDateTime((record as VoucherListItem).voucherDate).slice(0, 10) }}
        </template>
        <template #sourceType="{ record }">
          <a-tag :color="VOUCHER_SOURCE_TYPE_META[(record as VoucherListItem).sourceType].color">
            {{ VOUCHER_SOURCE_TYPE_META[(record as VoucherListItem).sourceType].label }}
          </a-tag>
        </template>
        <template #sourceNo="{ record }">
          {{ (record as VoucherListItem).sourceNo || '-' }}
        </template>
        <template #totalDebit="{ record }">
          <span class="amount">¥ {{ (record as VoucherListItem).totalDebit.toFixed(2) }}</span>
        </template>
        <template #totalCredit="{ record }">
          <span class="amount">¥ {{ (record as VoucherListItem).totalCredit.toFixed(2) }}</span>
        </template>
        <template #status="{ record }">
          <a-tag :color="VOUCHER_STATUS_META[(record as VoucherListItem).status].color">
            {{ VOUCHER_STATUS_META[(record as VoucherListItem).status].label }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as VoucherListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column §0）：2 个操作平铺 详情 / 作废；作废仅已过账凭证显示 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as VoucherListItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
            <a-popconfirm
              v-if="(record as VoucherListItem).status === 1"
              type="warning"
              content="确认作废该凭证？作废后余额随之回退，且不可恢复"
              @ok="onVoid(record as VoucherListItem)"
            >
              <a-button
                type="text"
                size="small"
                status="danger"
                :loading="voidingId === (record as VoucherListItem).id"
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

    <!-- 科目映射（工具条入口，抽屉形态；design §4.2） -->
    <AccountMappingsView
      v-model:visible="mappingsVisible"
      @saved="onMappingsSaved"
    />

    <!-- 期间管理：结账 / 反结账（已结账期间禁止新增 / 作废凭证） -->
    <a-drawer
      :visible="periodsVisible"
      :width="560"
      unmount-on-close
      :footer="false"
      @cancel="periodsVisible = false"
    >
      <template #title>
        会计期间
      </template>
      <a-table
        row-key="id"
        :data="periods"
        :pagination="false"
        :bordered="false"
      >
        <template #columns>
          <a-table-column
            title="期间"
            :width="140"
          >
            <template #cell="{ record }">
              {{ (record as Period).year }}-{{ String((record as Period).month).padStart(2, '0') }}
            </template>
          </a-table-column>
          <a-table-column
            title="状态"
            :width="120"
            align="center"
          >
            <template #cell="{ record }">
              <a-tag :color="PERIOD_STATUS_META[(record as Period).status].color">
                {{ PERIOD_STATUS_META[(record as Period).status].label }}
              </a-tag>
            </template>
          </a-table-column>
          <a-table-column title="操作">
            <template #cell="{ record }">
              <a-popconfirm
                v-if="auth.hasPermission('vouchers.close')"
                type="warning"
                :content="
                  (record as Period).status === 1
                    ? '确认反结账该期间？反结账后可继续记账'
                    : '确认结账该期间？结账后该期间禁止新增 / 作废凭证'
                "
                @ok="onTogglePeriod(record as Period)"
              >
                <a-button
                  type="text"
                  size="small"
                  :loading="closingId === (record as Period).id"
                >
                  {{ (record as Period).status === 1 ? '反结账' : '结账' }}
                </a-button>
              </a-popconfirm>
            </template>
          </a-table-column>
        </template>
      </a-table>
    </a-drawer>
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

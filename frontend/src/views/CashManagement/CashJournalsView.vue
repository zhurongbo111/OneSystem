<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getBankAccounts, getCashJournal } from '@/api/bankAccount'
import type { BankAccountListItem, CashJournal, CashJournalEntry } from '@/api/bankAccount'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconRefresh,
  IconRestore,
  IconSearch,
} from '@tabler/icons-vue'

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— helpers ——
/** 今天（YYYY-MM-DD，本地日历日） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** 当月 1 日（YYYY-MM-DD） */
function monthStartLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-01`
}

/** 把本地日期范围（YYYY-MM-DD）转换为后端所需的 UTC ISO 闭区间 */
function toUtcRange(start: string, end: string): { start: string; end: string } {
  return {
    start: new Date(`${start}T00:00:00`).toISOString(),
    end: new Date(`${end}T23:59:59.999`).toISOString(),
  }
}

/** 金额展示（两位小数，千分位） */
function formatMoney(value: number): string {
  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// —— reactive state ——
const loading = ref(false)
const accounts = ref<BankAccountListItem[]>([])

/** 输入态与已应用态分离（点查询才生效） */
const accountInput = ref<string | undefined>(undefined)
const dateRangeInput = ref<string[]>([monthStartLocal(), todayLocal()])
const appliedAccountId = ref<string | undefined>(undefined)
const appliedRange = ref<string[]>([monthStartLocal(), todayLocal()])

/** 日记账结果（未查询时为 null，用于区分「空区间」与「未查询」） */
const journal = ref<CashJournal | null>(null)
/** 查询失败提示（错误提示由请求层统一给出，此处只保留空态） */
const queried = ref(false)

// —— computed ——
/** 账户下拉：编码 + 名称，便于按账户检索 */
const accountOptions = computed(() =>
  accounts.value.map((a) => ({ label: `${a.code} ${a.name}`, value: a.id })),
)

/** 流水表格列 */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '业务日期', slotName: 'date', width: 110 },
  { title: '单据号', dataIndex: 'settlementNo', width: 150 },
  { title: '摘要', slotName: 'summary', width: 240, ellipsis: true, tooltip: true },
  { title: '收（元）', slotName: 'debit', width: 120, align: 'right' },
  { title: '付（元）', slotName: 'credit', width: 120, align: 'right' },
  { title: '结余（元）', slotName: 'balance', width: 130, align: 'right' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

/** 区间收 / 付合计（由流水派生） */
const totalDebit = computed(() => (journal.value?.entries ?? []).reduce((sum, e) => sum + e.debit, 0))
const totalCredit = computed(() => (journal.value?.entries ?? []).reduce((sum, e) => sum + e.credit, 0))

// —— lifecycle ——
onMounted(() => {
  void fetchAccounts()
})

// —— methods ——
/** 加载资金账户下拉（一次取全量，账户为主数据规模可控） */
async function fetchAccounts(): Promise<void> {
  try {
    const result = await getBankAccounts({ page: 1, pageSize: 100 })
    accounts.value = result.items
    // 未选择账户时默认选中第一个，页面打开即可看到一本日记账
    if (!accountInput.value && result.items.length > 0) {
      accountInput.value = result.items[0].id
    }
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 查询日记账（请求序号防止乱序响应覆盖最新结果） */
async function fetchJournal(): Promise<void> {
  const accountId = appliedAccountId.value
  if (!accountId) {
    Message.warning('请先选择资金账户')
    return
  }
  const [startDate, endDate] = appliedRange.value
  if (!startDate || !endDate) {
    Message.warning('请选择查询日期范围')
    return
  }

  const seq = ++fetchSeq
  loading.value = true
  try {
    const range = toUtcRange(startDate, endDate)
    const result = await getCashJournal({ bankAccountId: accountId, start: range.start, end: range.end })
    if (seq !== fetchSeq) return
    journal.value = result
    queried.value = true
  } catch {
    if (seq === fetchSeq) {
      journal.value = null
      queried.value = true
    }
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

/** 查询：应用输入条件 */
function onSearch(): void {
  appliedAccountId.value = accountInput.value
  appliedRange.value = [...dateRangeInput.value]
  void fetchJournal()
}

/** 重置：恢复默认区间与首个账户 */
function onReset(): void {
  dateRangeInput.value = [monthStartLocal(), todayLocal()]
  if (accounts.value.length > 0) {
    accountInput.value = accounts.value[0].id
  }
  appliedAccountId.value = accountInput.value
  appliedRange.value = [...dateRangeInput.value]
  void fetchJournal()
}

/** 刷新当前条件 */
function onRefresh(): void {
  void fetchJournal()
}

/** 日期列展示（业务日期只到日） */
function formatDate(value: string): string {
  return formatDateTime(value).slice(0, 10)
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题 -->
    <div class="page-header">
      <h1 class="page-title">
        资金日记账
      </h1>
    </div>

    <a-card :bordered="false">
      <div class="toolbar">
        <!-- 筛选行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="6">
            <a-select
              v-model="accountInput"
              :options="accountOptions"
              placeholder="请选择资金账户"
              allow-search
              allow-clear
            />
          </a-col>
          <a-col :span="8">
            <a-range-picker
              v-model="dateRangeInput"
              value-format="YYYY-MM-DD"
              style="width: 100%"
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
                查询
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

        <!-- 操作行：右组视图操作 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left" />
          <div class="toolbar-actions__right">
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

      <!-- 期初 / 期末 -->
      <a-descriptions
        v-if="journal"
        :column="3"
        bordered
        size="medium"
        class="balance-panel"
      >
        <a-descriptions-item label="期初余额">
          <span class="amount">{{ formatMoney(journal.openingBalance) }}</span>
        </a-descriptions-item>
        <a-descriptions-item label="区间发生（收 / 付）">
          <span class="amount">
            {{ formatMoney(totalDebit) }} / {{ formatMoney(totalCredit) }}
          </span>
        </a-descriptions-item>
        <a-descriptions-item label="期末余额">
          <span class="amount amount--closing">{{ formatMoney(journal.closingBalance) }}</span>
        </a-descriptions-item>
      </a-descriptions>

      <a-table
        row-key="settlementNo"
        :loading="loading"
        :columns="columns"
        :data="journal?.entries ?? []"
        :pagination="false"
        :scroll="{ x: tableScrollX }"
      >
        <template #seq="{ rowIndex }">
          {{ rowIndex + 1 }}
        </template>
        <template #date="{ record }">
          {{ formatDate((record as CashJournalEntry).date) }}
        </template>
        <template #summary="{ record }">
          {{ (record as CashJournalEntry).summary }}
        </template>
        <template #debit="{ record }">
          <span class="amount amount--in">{{ formatMoney((record as CashJournalEntry).debit) }}</span>
        </template>
        <template #credit="{ record }">
          <span class="amount amount--out">{{ formatMoney((record as CashJournalEntry).credit) }}</span>
        </template>
        <template #balance="{ record }">
          <span class="amount">{{ formatMoney((record as CashJournalEntry).balance) }}</span>
        </template>
        <template #empty>
          <a-empty :description="queried ? '该区间内没有资金流水' : '请选择资金账户与日期范围后查询'" />
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

.balance-panel {
  margin-bottom: 12px;
}

.amount {
  font-variant-numeric: tabular-nums;
}

.amount--in {
  color: rgb(var(--green-6));
}

.amount--out {
  color: rgb(var(--red-6));
}

.amount--closing {
  font-weight: 600;
}
</style>

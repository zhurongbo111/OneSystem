<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'

import {
  getAccountBalance,
  getBalanceSheet,
  getIncomeStatement,
  type AccountBalanceItem,
  type BalanceSheet,
  type IncomeStatement,
} from '@/api/financialReport'
import { getPeriods, type Period } from '@/api/voucher'
import { IconRefresh } from '@tabler/icons-vue'

// —— types ——
type ReportTab = 'accountBalance' | 'balanceSheet' | 'incomeStatement'

// —— constants ——
/** 科目类别文案（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益） */
const CATEGORY_LABELS: Record<number, string> = { 1: '资产', 2: '负债', 3: '权益', 4: '成本', 5: '损益' }

/** 余额方向文案（1 借 / 2 贷） */
const DIRECTION_LABELS: Record<number, string> = { 1: '借', 2: '贷' }

// —— reactive state ——
const loading = ref(false)
const activeTab = ref<ReportTab>('accountBalance')
const periods = ref<Period[]>([])
const periodValue = ref<string | undefined>(undefined)
const balances = ref<AccountBalanceItem[]>([])
const balanceSheet = ref<BalanceSheet | null>(null)
const incomeStatement = ref<IncomeStatement | null>(null)

// —— computed ——
/** 期间下拉选项（值为 `年-月`，最新期间在前） */
const periodOptions = computed(() =>
  [...periods.value]
    .sort((a, b) => (b.year - a.year) || (b.month - a.month))
    .map((p) => ({ label: `${p.year}-${String(p.month).padStart(2, '0')}`, value: `${p.year}-${p.month}` })),
)

/** 当前所选期间的年月（未选择时为空） */
const selectedYearMonth = computed(() => {
  if (!periodValue.value) return null
  const [year, month] = periodValue.value.split('-').map(Number)
  return year && month ? { year, month } : null
})

/** 科目余额表借贷发生额合计 */
const balanceTotals = computed(() => ({
  opening: balances.value.reduce((sum, item) => sum + item.openingBalance, 0),
  debit: balances.value.reduce((sum, item) => sum + item.periodDebit, 0),
  credit: balances.value.reduce((sum, item) => sum + item.periodCredit, 0),
  closing: balances.value.reduce((sum, item) => sum + item.closingBalance, 0),
}))

/** 资产负债表：负债和所有者权益侧行（负债 + 权益 + 本年利润行） */
const liabilityAndEquityRows = computed(() => {
  const sheet = balanceSheet.value
  if (!sheet) return []
  return [
    ...sheet.liabilities,
    ...sheet.equities,
    ...sheet.profitLossItems,
  ]
})

// —— watch ——
watch([periodValue, activeTab], () => {
  void loadReport()
})

// —— lifecycle ——
onMounted(async () => {
  await loadPeriods()
  await loadReport()
})

// —— methods ——
/** 加载会计期间并默认选中**当前年月**（无当前期间时回退到最新期间） */
async function loadPeriods(): Promise<void> {
  try {
    periods.value = await getPeriods()
    const now = new Date()
    const current = `${now.getFullYear()}-${now.getMonth() + 1}`
    if (periods.value.some((period) => `${period.year}-${period.month}` === current)) {
      periodValue.value = current
      return
    }

    const latest = [...periods.value].sort((a, b) => (b.year - a.year) || (b.month - a.month))[0]
    if (latest) {
      periodValue.value = `${latest.year}-${latest.month}`
    }
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 按当前期间与 tab 加载报表（请求序号防止乱序响应覆盖最新结果） */
async function loadReport(): Promise<void> {
  const target = selectedYearMonth.value
  if (!target) return

  loading.value = true
  try {
    if (activeTab.value === 'accountBalance') {
      balances.value = await getAccountBalance(target.year, target.month)
    } else if (activeTab.value === 'balanceSheet') {
      balanceSheet.value = await getBalanceSheet(target.year, target.month)
    } else {
      incomeStatement.value = await getIncomeStatement(target.year, target.month)
    }
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

/** 刷新当前报表 */
function onRefresh(): void {
  void loadReport()
}
</script>

<template>
  <div class="report-page">
    <div class="page-header">
      <h1 class="page-title">
        财务报表
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="report-card"
    >
      <div class="toolbar-actions">
        <div class="toolbar-actions__left">
          <a-select
            v-model="periodValue"
            :options="periodOptions"
            placeholder="选择期间"
            style="width: 160px"
          />
        </div>
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

      <a-tabs v-model:active-key="activeTab">
        <!-- 科目余额表 -->
        <a-tab-pane
          key="accountBalance"
          title="科目余额表"
        >
          <a-table
            row-key="accountId"
            :loading="loading"
            :data="balances"
            :pagination="false"
            :scroll="{ x: 1000 }"
          >
            <template #columns>
              <a-table-column
                title="科目编码"
                :width="120"
              >
                <template #cell="{ record }">
                  {{ (record as AccountBalanceItem).code }}
                </template>
              </a-table-column>
              <a-table-column
                title="科目名称"
                :width="200"
              >
                <template #cell="{ record }">
                  {{ (record as AccountBalanceItem).name }}
                </template>
              </a-table-column>
              <a-table-column
                title="类别"
                :width="90"
                align="center"
              >
                <template #cell="{ record }">
                  {{ CATEGORY_LABELS[(record as AccountBalanceItem).category] }}
                </template>
              </a-table-column>
              <a-table-column
                title="方向"
                :width="80"
                align="center"
              >
                <template #cell="{ record }">
                  {{ DIRECTION_LABELS[(record as AccountBalanceItem).direction] }}
                </template>
              </a-table-column>
              <a-table-column
                title="期初余额"
                :width="140"
                align="right"
              >
                <template #cell="{ record }">
                  <span class="amount">¥ {{ (record as AccountBalanceItem).openingBalance.toFixed(2) }}</span>
                </template>
              </a-table-column>
              <a-table-column
                title="本期借方"
                :width="140"
                align="right"
              >
                <template #cell="{ record }">
                  <span class="amount">¥ {{ (record as AccountBalanceItem).periodDebit.toFixed(2) }}</span>
                </template>
              </a-table-column>
              <a-table-column
                title="本期贷方"
                :width="140"
                align="right"
              >
                <template #cell="{ record }">
                  <span class="amount">¥ {{ (record as AccountBalanceItem).periodCredit.toFixed(2) }}</span>
                </template>
              </a-table-column>
              <a-table-column
                title="期末余额"
                :width="140"
                align="right"
              >
                <template #cell="{ record }">
                  <span class="amount">¥ {{ (record as AccountBalanceItem).closingBalance.toFixed(2) }}</span>
                </template>
              </a-table-column>
            </template>
          </a-table>
          <div class="report-total report-total--balance">
            <span>本期借方合计 <b class="amount">¥ {{ balanceTotals.debit.toFixed(2) }}</b></span>
            <span>本期贷方合计 <b class="amount">¥ {{ balanceTotals.credit.toFixed(2) }}</b></span>
          </div>
        </a-tab-pane>

        <!-- 资产负债表 -->
        <a-tab-pane
          key="balanceSheet"
          title="资产负债表"
        >
          <a-row :gutter="16">
            <a-col :span="12">
              <div class="sheet-title">
                资产
              </div>
              <a-table
                row-key="accountId"
                :loading="loading"
                :data="balanceSheet?.assets ?? []"
                :pagination="false"
              >
                <template #columns>
                  <a-table-column
                    title="科目编码"
                    :width="120"
                  >
                    <template #cell="{ record }">
                      {{ record.code }}
                    </template>
                  </a-table-column>
                  <a-table-column title="科目名称">
                    <template #cell="{ record }">
                      {{ record.name }}
                    </template>
                  </a-table-column>
                  <a-table-column
                    title="期末余额"
                    :width="140"
                    align="right"
                  >
                    <template #cell="{ record }">
                      <span class="amount">¥ {{ (record.amount as number).toFixed(2) }}</span>
                    </template>
                  </a-table-column>
                </template>
              </a-table>
              <div class="report-total">
                <span>资产合计 <b class="amount">¥ {{ (balanceSheet?.totalAssets ?? 0).toFixed(2) }}</b></span>
              </div>
            </a-col>

            <a-col :span="12">
              <div class="sheet-title">
                负债和所有者权益
              </div>
              <a-table
                row-key="accountId"
                :loading="loading"
                :data="liabilityAndEquityRows"
                :pagination="false"
              >
                <template #columns>
                  <a-table-column
                    title="科目编码"
                    :width="120"
                  >
                    <template #cell="{ record }">
                      {{ record.code }}
                    </template>
                  </a-table-column>
                  <a-table-column title="科目名称">
                    <template #cell="{ record }">
                      {{ record.name }}
                    </template>
                  </a-table-column>
                  <a-table-column
                    title="期末余额"
                    :width="140"
                    align="right"
                  >
                    <template #cell="{ record }">
                      <span class="amount">¥ {{ (record.amount as number).toFixed(2) }}</span>
                    </template>
                  </a-table-column>
                </template>
              </a-table>
              <div class="report-total">
                <span>负债合计 <b class="amount">¥ {{ (balanceSheet?.totalLiabilities ?? 0).toFixed(2) }}</b></span>
                <span>权益合计 <b class="amount">¥ {{ (balanceSheet?.totalEquities ?? 0).toFixed(2) }}</b></span>
                <span>本年利润 <b class="amount">¥ {{ (balanceSheet?.currentProfit ?? 0).toFixed(2) }}</b></span>
                <span>
                  负债和权益合计
                  <b class="amount">¥ {{ (balanceSheet?.totalLiabilitiesAndEquity ?? 0).toFixed(2) }}</b>
                </span>
              </div>
            </a-col>
          </a-row>
        </a-tab-pane>

        <!-- 利润表 -->
        <a-tab-pane
          key="incomeStatement"
          title="利润表"
        >
          <a-row :gutter="16">
            <a-col :span="12">
              <div class="sheet-title">
                收入
              </div>
              <a-table
                row-key="accountId"
                :loading="loading"
                :data="incomeStatement?.revenueItems ?? []"
                :pagination="false"
              >
                <template #columns>
                  <a-table-column
                    title="科目编码"
                    :width="120"
                  >
                    <template #cell="{ record }">
                      {{ record.code }}
                    </template>
                  </a-table-column>
                  <a-table-column title="科目名称">
                    <template #cell="{ record }">
                      {{ record.name }}
                    </template>
                  </a-table-column>
                  <a-table-column
                    title="本期金额"
                    :width="140"
                    align="right"
                  >
                    <template #cell="{ record }">
                      <span class="amount">¥ {{ (record.amount as number).toFixed(2) }}</span>
                    </template>
                  </a-table-column>
                </template>
              </a-table>
            </a-col>

            <a-col :span="12">
              <div class="sheet-title">
                成本费用
              </div>
              <a-table
                row-key="accountId"
                :loading="loading"
                :data="incomeStatement?.costItems ?? []"
                :pagination="false"
              >
                <template #columns>
                  <a-table-column
                    title="科目编码"
                    :width="120"
                  >
                    <template #cell="{ record }">
                      {{ record.code }}
                    </template>
                  </a-table-column>
                  <a-table-column title="科目名称">
                    <template #cell="{ record }">
                      {{ record.name }}
                    </template>
                  </a-table-column>
                  <a-table-column
                    title="本期金额"
                    :width="140"
                    align="right"
                  >
                    <template #cell="{ record }">
                      <span class="amount">¥ {{ (record.amount as number).toFixed(2) }}</span>
                    </template>
                  </a-table-column>
                </template>
              </a-table>
            </a-col>
          </a-row>

          <div class="report-total report-total--income">
            <span>收入合计 <b class="amount">¥ {{ (incomeStatement?.totalRevenue ?? 0).toFixed(2) }}</b></span>
            <span>成本费用合计 <b class="amount">¥ {{ (incomeStatement?.totalCost ?? 0).toFixed(2) }}</b></span>
            <span>净利润 <b class="amount">¥ {{ (incomeStatement?.netProfit ?? 0).toFixed(2) }}</b></span>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<style scoped>
.report-page {
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

.report-card {
  border-radius: var(--border-radius-medium);
}

.toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 12px;
}

.toolbar-actions__left,
.toolbar-actions__right {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.sheet-title {
  margin-bottom: 8px;
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-1);
}

.report-total {
  display: flex;
  flex-wrap: wrap;
  gap: 24px;
  align-items: center;
  justify-content: flex-end;
  padding: 12px 0 0;
  font-size: 13px;
  color: var(--color-text-2);
}

.amount {
  font-variant-numeric: tabular-nums;
}
</style>

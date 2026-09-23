<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getBankAccounts } from '@/api/bankAccount'
import type { BankAccountListItem } from '@/api/bankAccount'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import {
  createSettlement,
  getUnsettledOrders,
  toUtcMidnight,
  type SettlementCandidate,
  type SettlementMethod,
  type SettlementOrderType,
  type SettlementType,
} from '@/api/settlement'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'

// —— types ——
/** 候选行本地类型：附加复合 key（orderType + orderId）供表格 row-key / 勾选使用 */
interface CandidateRow extends SettlementCandidate {
  key: string
}

// —— constants ——
/** 核销明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100

/** 收付款类型下拉（0 收款 / 1 付款） */
const typeOptions: { label: string; value: SettlementType }[] = [
  { label: '收款', value: 0 },
  { label: '付款', value: 1 },
]

/** 收付款方式下拉（0 现金 / 1 银行转账 / 2 其他） */
const methodOptions: { label: string; value: SettlementMethod }[] = [
  { label: '现金', value: 0 },
  { label: '银行转账', value: 1 },
  { label: '其他', value: 2 },
]

/** 被核销单据类型文案 */
const ORDER_TYPE_LABELS: Record<SettlementOrderType, string> = {
  0: '采购入库单',
  1: '销售出库单',
  2: '采购退货单',
  3: '销售退货单',
}

/** 核销明细表格列 */
const candidateColumns: TableColumnData[] = [
  { title: '单据类型', slotName: 'orderType', width: 120 },
  { title: '单号', dataIndex: 'orderNo', width: 160 },
  { title: '单据日期', slotName: 'orderDate', width: 110 },
  { title: '单据总额', slotName: 'totalAmount', width: 120, align: 'right' },
  { title: '已结金额', slotName: 'settledAmount', width: 120, align: 'right' },
  { title: '未结金额', slotName: 'unsettledAmount', width: 120, align: 'right' },
  { title: '本次核销金额', slotName: 'amount', width: 170 },
]

/** 当天本地日期 YYYY-MM-DD（业务日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const candidatesLoading = ref(false)

/** 表头：类型 / 往来单位 / 收付日期 / 方式 / 备注 */
const type = ref<SettlementType>(0)
const partnerId = ref<string | undefined>(undefined)
const settlementDate = ref(todayLocal())
const method = ref<SettlementMethod>(0)
/** 资金账户（034-erp-cash；现金 / 银行转账才需选账户，「其他」不关联） */
const bankAccountId = ref<string | undefined>(undefined)
const remark = ref('')

/** 往来下拉数据源（全部启用往来；不按类型过滤，收款可对供应商收回退货退款） */
const partners = ref<Partner[]>([])

/** 资金账户下拉数据源（全部启用账户，按结算方式过滤类型） */
const bankAccounts = ref<BankAccountListItem[]>([])

/** 可核销单据候选（未结 + 未作废）与已选核销行 */
const candidates = ref<CandidateRow[]>([])
const selectedKeys = ref<string[]>([])
/** 各候选行的本次核销金额（key → 金额，仅选中行有效） */
const amounts = ref<Record<string, number>>({})

const rules = {
  partnerId: [{ required: true, message: '请选择往来单位' }],
  settlementDate: [{ required: true, message: '请选择收付日期' }],
}

// —— computed ——
/** 往来下拉：全部启用往来（不按类型过滤——收款需支持「收供应商退款」，付款同理） */
const partnerOptions = computed(() => partners.value.map((p) => ({ label: p.name, value: p.id })))

/** 表格行选择配置 */
const rowSelection = computed(() => ({ type: 'checkbox' as const, showCheckedAll: true }))

/**
 * 资金账户下拉：按结算方式过滤账户类型（现金 → 现金账户、银行转账 → 银行账户，其他不关联账户）。
 * 与后端校验同源（不匹配 → 40162），避免用户选了不匹配的类型才在提交时报错。
 */
const bankAccountOptions = computed(() => {
  const expectedType = method.value === 0 ? 1 : method.value === 1 ? 2 : 0
  if (expectedType === 0) return []
  return bankAccounts.value
    .filter((a) => a.status === 1 && a.type === expectedType)
    .map((a) => ({ label: `${a.code} ${a.name}`, value: a.id }))
})

/** 收付款总额 = Σ 本次核销金额（仅展示，后端落库时重算） */
const totalAmount = computed(() =>
  candidates.value
    .filter((c) => selectedKeys.value.includes(c.key))
    .reduce((sum, c) => sum + (amounts.value[c.key] ?? 0), 0),
)

// —— lifecycle ——
onMounted(async () => {
  try {
    const result = await getPartners({ status: 1, page: 1, pageSize: 100 })
    partners.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }

  try {
    const accounts = await getBankAccounts({ status: 1, page: 1, pageSize: 100 })
    bankAccounts.value = accounts.items
  } catch {
    // 错误提示已由请求层统一处理
  }

  // 单据页「收付款」跳转预置：类型（0 收款 / 1 付款）与往来单位
  const queryType = route.query.type
  if (queryType === '0' || queryType === '1') {
    type.value = Number(queryType) as SettlementType
  }
  const queryPartnerId = route.query.partnerId
  if (typeof queryPartnerId === 'string' && queryPartnerId) {
    partnerId.value = queryPartnerId
  }
  if (partnerId.value) {
    void loadCandidates()
  }
})

// —— methods ——
/** 加载当前往来 + 方向下的可核销单据候选（未结且未作废） */
async function loadCandidates(): Promise<void> {
  if (!partnerId.value) {
    candidates.value = []
    return
  }
  candidatesLoading.value = true
  try {
    const result = await getUnsettledOrders({
      partnerId: partnerId.value,
      type: type.value,
      page: 1,
      pageSize: 100,
    })
    candidates.value = result.items.map((c) => ({ ...c, key: `${c.orderType}-${c.orderId}` }))
  } catch {
    candidates.value = []
  } finally {
    candidatesLoading.value = false
  }
}

/** 切类型 / 切往来时清空已选明细，防止串数据 */
function resetItems(): void {
  selectedKeys.value = []
  amounts.value = {}
  candidates.value = []
}

function onTypeChange(): void {
  // 往来不再按类型过滤（收款可对供应商），保留已选往来；仅清空核销明细并按新方向重载候选
  resetItems()
  void loadCandidates()
}

function onPartnerChange(): void {
  resetItems()
  void loadCandidates()
}

/** 切结算方式：现金 / 银行转账切换时账户类型不同，重置已选账户 */
function onMethodChange(): void {
  bankAccountId.value = undefined
}

/** 勾选变化：新选中行默认填入未结金额，取消选中行移除金额 */
function onSelectionChange(keys: (string | number)[]): void {
  const next = new Set(keys.map(String))
  const updated: Record<string, number> = {}
  for (const row of candidates.value) {
    if (next.has(row.key)) {
      updated[row.key] = amounts.value[row.key] ?? row.unsettledAmount
    }
  }
  amounts.value = updated
}

function onAmountChange(row: CandidateRow, value: number | undefined): void {
  amounts.value = { ...amounts.value, [row.key]: value ?? 0 }
}

/** 全部结清：把所选行金额一次性填为未结金额 */
function onSettleAll(): void {
  if (selectedKeys.value.length === 0) {
    Message.warning('请先勾选待核销单据')
    return
  }
  const updated: Record<string, number> = {}
  for (const row of candidates.value) {
    if (selectedKeys.value.includes(row.key)) {
      updated[row.key] = row.unsettledAmount
    }
  }
  amounts.value = updated
}

function goBack(): void {
  void router.push({ name: 'settlements' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result !== undefined) return
    if (selectedKeys.value.length === 0) {
      Message.error('请至少勾选一张待核销单据')
      return
    }
    if (selectedKeys.value.length > MAX_ITEMS) {
      Message.error(`核销明细不能超过 ${MAX_ITEMS} 行`)
      return
    }

    const selectedRows = candidates.value.filter((c) => selectedKeys.value.includes(c.key))
    const invalid = selectedRows.some((c) => {
      const amount = amounts.value[c.key] ?? 0
      return amount <= 0 || amount > c.unsettledAmount
    })
    if (invalid) {
      Message.error('核销金额需大于 0 且不超过该单据未结金额')
      return
    }

    const saved = await createSettlement({
      type: type.value,
      partnerId: partnerId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（裸日期会被后端按服务器本地时区解析导致入库失败）
      settlementDate: toUtcMidnight(settlementDate.value),
      method: method.value,
      bankAccountId: bankAccountId.value,
      items: selectedRows.map((c) => ({
        orderType: c.orderType,
        orderId: c.orderId,
        amount: amounts.value[c.key] as number,
      })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('收付款单已创建')
    void router.push({ name: 'settlementDetail', params: { id: saved.id } })
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      title="新建收付款"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="{ partnerId, settlementDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item label="类型">
              <a-radio-group
                v-model="type"
                :options="typeOptions"
                type="button"
                @change="onTypeChange"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="往来单位"
              field="partnerId"
            >
              <a-select
                v-model="partnerId"
                :options="partnerOptions"
                :placeholder="type === 0 ? '客户（销售回款）或供应商（收回退货退款）' : '供应商（采购付款）或客户（退出退款）'"
                allow-search
                allow-clear
                :loading="partners.length === 0"
                @change="onPartnerChange"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="收付日期"
              field="settlementDate"
            >
              <a-date-picker
                v-model="settlementDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择收付日期"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="方式">
              <a-select
                v-model="method"
                :options="methodOptions"
                placeholder="请选择收付款方式"
                @change="onMethodChange"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="资金账户"
              :extra="method === 2 ? '「其他」结算方式不关联资金账户' : undefined"
            >
              <a-select
                v-model="bankAccountId"
                :options="bankAccountOptions"
                :placeholder="method === 2 ? '「其他」结算方式无需选择账户' : '请选择资金账户（可空）'"
                :disabled="bankAccountOptions.length === 0"
                allow-search
                allow-clear
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">
          核销明细
        </a-divider>
        <div class="items-toolbar">
          <a-button
            size="small"
            :disabled="candidates.length === 0"
            @click="onSettleAll"
          >
            全部结清
          </a-button>
        </div>
        <a-table
          v-model:selected-keys="selectedKeys"
          row-key="key"
          size="small"
          :loading="candidatesLoading"
          :columns="candidateColumns"
          :data="candidates"
          :pagination="false"
          :row-selection="rowSelection"
          @selection-change="onSelectionChange"
        >
          <template #orderType="{ record }">
            {{ ORDER_TYPE_LABELS[(record as CandidateRow).orderType] }}
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as CandidateRow).orderDate).slice(0, 10) }}
          </template>
          <template #totalAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).totalAmount.toFixed(2) }}</span>
          </template>
          <template #settledAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).settledAmount.toFixed(2) }}</span>
          </template>
          <template #unsettledAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).unsettledAmount.toFixed(2) }}</span>
          </template>
          <template #amount="{ record }">
            <a-input-number
              :model-value="amounts[(record as CandidateRow).key] ?? 0"
              :min="0"
              :max="(record as CandidateRow).unsettledAmount"
              :precision="2"
              :disabled="!selectedKeys.includes((record as CandidateRow).key)"
              prefix="¥"
              style="width: 100%"
              @change="(v: number | undefined) => onAmountChange(record as CandidateRow, v)"
            />
          </template>
        </a-table>
        <a-alert
          v-if="partnerId && !candidatesLoading && candidates.length === 0"
          type="info"
          class="items-empty"
        >
          该往来单位当前方向下没有可核销的未结单据
        </a-alert>

        <a-divider orientation="left">
          其他
        </a-divider>
        <a-form-item label="备注">
          <a-textarea
            v-model="remark"
            placeholder="选填（不超过 200 字）"
            :max-length="200"
            :auto-size="{ minRows: 2, maxRows: 4 }"
          />
        </a-form-item>

        <div class="form-footer">
          <span class="form-footer__total">
            总额：<span class="form-footer__total-amount">{{ totalAmount.toFixed(2) }}</span>
          </span>
          <a-space>
            <a-button @click="goBack">
              取消
            </a-button>
            <a-button
              type="primary"
              :loading="submitting"
              @click="onSubmit"
            >
              提交
            </a-button>
          </a-space>
        </div>
      </a-form>
    </a-card>
  </div>
</template>

<style scoped>
.form-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.items-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.items-empty {
  margin-top: 8px;
}

.amount {
  font-variant-numeric: tabular-nums;
}

.form-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}

.form-footer__total {
  font-size: 14px;
  color: var(--color-text-2);
}

.form-footer__total-amount {
  font-size: 18px;
  font-weight: 600;
  color: rgb(var(--red-6));
  font-variant-numeric: tabular-nums;
}
</style>

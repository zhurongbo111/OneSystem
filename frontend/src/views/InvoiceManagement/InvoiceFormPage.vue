<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import {
  allowedPartnerTypes,
  createInvoice,
  CUSTOM_TAX_RATE,
  getInvoicableOrders,
  INVOICE_TYPE_OPTIONS,
  TAX_RATE_OPTIONS,
  toUtcMidnight,
  type InvoicableOrder,
  type InvoiceType,
} from '@/api/invoice'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { formatDateTime } from '@/utils/datetime'
import { settlementOrderTypeLabel } from '@/utils/settlement'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'

// —— types ——
/** 候选行本地类型：附加复合 key（orderType + orderId）供表格 row-key / 勾选使用 */
interface CandidateRow extends InvoicableOrder {
  key: string
}

// —— constants ——
/** 关联明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100

/** 税率下拉（标准税率 + 自定义） */
const taxRateOptions = [...TAX_RATE_OPTIONS, { label: '自定义', value: CUSTOM_TAX_RATE }]

/** 关联单据表格列 */
const candidateColumns: TableColumnData[] = [
  { title: '单据类型', slotName: 'orderType', width: 120 },
  { title: '单号', dataIndex: 'orderNo', width: 160 },
  { title: '单据日期', slotName: 'orderDate', width: 110 },
  { title: '单据总额', slotName: 'totalAmount', width: 120, align: 'right' },
  { title: '已开票金额', slotName: 'invoicedAmount', width: 120, align: 'right' },
  { title: '未开票金额', slotName: 'uninvoicedAmount', width: 120, align: 'right' },
  { title: '本次开票金额', slotName: 'amount', width: 170 },
]

/** 当天本地日期 YYYY-MM-DD（开票日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** 金额舍入到分（仅前端展示口径；落库金额由后端重算） */
function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const candidatesLoading = ref(false)

/** 表头：发票号 / 类型 / 往来单位 / 开票日期 / 不含税金额 / 税率 / 备注 */
const invoiceNo = ref('')
const type = ref<InvoiceType>(1)
const partnerId = ref<string | undefined>(undefined)
const invoiceDate = ref(todayLocal())
const amountExcludingTax = ref<number>(0)
const taxRateSelect = ref<number>(0.13)
/** 自定义税率（0–1 四位小数；仅选中「自定义」时生效） */
const customTaxRate = ref<number>(0.13)
const remark = ref('')

/** 往来下拉数据源（全部启用往来；按发票方向过滤档案类型） */
const partners = ref<Partner[]>([])

/** 可开票单据候选（未作废且未开票金额 > 0）与已选关联行 */
const candidates = ref<CandidateRow[]>([])
const selectedKeys = ref<string[]>([])
/** 各候选行的本次开票金额（key → 金额，仅选中行有效） */
const amounts = ref<Record<string, number>>({})

const rules = {
  invoiceNo: [{ required: true, message: '请输入发票号' }],
  partnerId: [{ required: true, message: '请选择往来单位' }],
  invoiceDate: [{ required: true, message: '请选择开票日期' }],
}

// —— computed ——
/** 往来下拉：按发票方向过滤档案类型（进项需供应商 / 两者，销项需客户 / 两者） */
const partnerOptions = computed(() =>
  partners.value
    .filter((p) => allowedPartnerTypes(type.value).includes(p.type))
    .map((p) => ({ label: p.name, value: p.id })),
)

/** 表格行选择配置 */
const rowSelection = computed(() => ({ type: 'checkbox' as const, showCheckedAll: true }))

/** 生效税率（0–1 小数口径） */
const taxRate = computed(() => (taxRateSelect.value === CUSTOM_TAX_RATE ? customTaxRate.value : taxRateSelect.value))

/** 税额 = 不含税金额 × 税率（四舍五入到分，仅展示；后端落库时重算） */
const taxAmount = computed(() => roundMoney(amountExcludingTax.value * taxRate.value))

/** 价税合计 = 不含税金额 + 税额 */
const totalAmount = computed(() => roundMoney(amountExcludingTax.value + taxAmount.value))

/** 已选关联行的开票金额合计 */
const selectedAmount = computed(() =>
  candidates.value
    .filter((c) => selectedKeys.value.includes(c.key))
    .reduce((sum, c) => sum + (amounts.value[c.key] ?? 0), 0),
)

/** 关联金额合计与不含税金额是否不一致（仅提示，不阻断，design.md §4.4） */
const amountMismatch = computed(
  () => selectedKeys.value.length > 0 && roundMoney(selectedAmount.value) !== roundMoney(amountExcludingTax.value),
)

// —— lifecycle ——
onMounted(async () => {
  try {
    const result = await getPartners({ status: 1, page: 1, pageSize: 100 })
    partners.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }

  // 往来页 / 单据页跳转预置：类型（0 进项 / 1 销项）与往来单位（未预置时默认销项）
  const queryType = route.query.type
  if (queryType === '0' || queryType === '1') {
    type.value = Number(queryType) as InvoiceType
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
/** 加载当前往来 + 方向下的可开票单据候选（未作废且未开票金额 > 0） */
async function loadCandidates(): Promise<void> {
  if (!partnerId.value) {
    candidates.value = []
    return
  }
  candidatesLoading.value = true
  try {
    const result = await getInvoicableOrders({
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

/** 关联金额合计超出不含税金额时提示（后端按行校验未开票金额，此处提前提醒） */
function selectedAmountInvalid(): boolean {
  return candidates.value
    .filter((c) => selectedKeys.value.includes(c.key))
    .some((c) => {
      const amount = amounts.value[c.key] ?? 0
      return amount <= 0 || amount > c.uninvoicedAmount
    })
}

/** 切类型 / 切往来时清空已选明细，防止串数据 */
function resetItems(): void {
  selectedKeys.value = []
  amounts.value = {}
  candidates.value = []
}

function onTypeChange(): void {
  // 往来档案类型受方向约束（进项 → 供应商），已选往来不再匹配时清空
  if (partnerId.value && !partnerOptions.value.some((o) => o.value === partnerId.value)) {
    partnerId.value = undefined
  }
  resetItems()
  if (partnerId.value) {
    void loadCandidates()
  }
}

function onPartnerChange(): void {
  resetItems()
  void loadCandidates()
}

/** 勾选变化：新选中行默认填入未开票金额，取消选中行移除金额 */
function onSelectionChange(keys: (string | number)[]): void {
  const next = new Set(keys.map(String))
  const updated: Record<string, number> = {}
  for (const row of candidates.value) {
    if (next.has(row.key)) {
      updated[row.key] = amounts.value[row.key] ?? row.uninvoicedAmount
    }
  }
  amounts.value = updated
}

function onAmountChange(row: CandidateRow, value: number | undefined): void {
  amounts.value = { ...amounts.value, [row.key]: value ?? 0 }
}

/** 全部开票：把所选行金额一次性填为未开票金额 */
function onInvoiceAll(): void {
  if (selectedKeys.value.length === 0) {
    Message.warning('请先勾选待开票单据')
    return
  }
  const updated: Record<string, number> = {}
  for (const row of candidates.value) {
    if (selectedKeys.value.includes(row.key)) {
      updated[row.key] = row.uninvoicedAmount
    }
  }
  amounts.value = updated
}

function goBack(): void {
  void router.push({ name: 'invoices' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result !== undefined) return
    if (selectedKeys.value.length === 0) {
      Message.error('请至少勾选一张关联单据')
      return
    }
    if (selectedKeys.value.length > MAX_ITEMS) {
      Message.error(`关联单据不能超过 ${MAX_ITEMS} 行`)
      return
    }
    if (amountExcludingTax.value <= 0) {
      Message.error('不含税金额必须大于 0')
      return
    }
    if (selectedAmountInvalid()) {
      Message.error('开票金额需大于 0 且不超过该单据未开票金额')
      return
    }

    const selectedRows = candidates.value.filter((c) => selectedKeys.value.includes(c.key))
    const saved = await createInvoice({
      invoiceNo: invoiceNo.value.trim(),
      type: type.value,
      partnerId: partnerId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（裸日期会被后端按服务器本地时区解析导致入库失败）
      invoiceDate: toUtcMidnight(invoiceDate.value),
      amountExcludingTax: amountExcludingTax.value,
      taxRate: taxRate.value,
      items: selectedRows.map((c) => ({
        orderType: c.orderType,
        orderId: c.orderId,
        amount: amounts.value[c.key] as number,
      })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('发票已登记')
    void router.push({ name: 'invoiceDetail', params: { id: saved.id } })
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
      title="登记发票"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="{ invoiceNo, partnerId, invoiceDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item
              label="发票号"
              field="invoiceNo"
            >
              <a-input
                v-model="invoiceNo"
                placeholder="1-50 字符，全局唯一"
                allow-clear
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="类型">
              <a-radio-group
                v-model="type"
                :options="INVOICE_TYPE_OPTIONS"
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
                :placeholder="type === 0 ? '供应商（采购取得）' : '客户（销售开出）'"
                allow-search
                allow-clear
                :loading="partners.length === 0"
                @change="onPartnerChange"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="开票日期"
              field="invoiceDate"
            >
              <a-date-picker
                v-model="invoiceDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择开票日期"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="不含税金额">
              <a-input-number
                v-model="amountExcludingTax"
                :min="0"
                :precision="2"
                placeholder="不含税金额"
                prefix="¥"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="税率">
              <a-space
                direction="vertical"
                fill
              >
                <a-select
                  v-model="taxRateSelect"
                  :options="taxRateOptions"
                  placeholder="请选择税率"
                />
                <a-input-number
                  v-if="taxRateSelect === CUSTOM_TAX_RATE"
                  v-model="customTaxRate"
                  :min="0"
                  :max="1"
                  :precision="4"
                  placeholder="自定义税率（0-1）"
                  style="width: 100%"
                />
              </a-space>
            </a-form-item>
          </a-col>
        </a-row>

        <div class="amount-summary">
          <span>税额：<span class="amount">¥ {{ taxAmount.toFixed(2) }}</span></span>
          <span>价税合计：<span class="amount amount--total">¥ {{ totalAmount.toFixed(2) }}</span></span>
        </div>

        <a-divider orientation="left">
          关联单据
        </a-divider>
        <div class="items-toolbar">
          <a-button
            size="small"
            :disabled="candidates.length === 0"
            @click="onInvoiceAll"
          >
            全部开票
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
            {{ settlementOrderTypeLabel((record as CandidateRow).orderType) }}
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as CandidateRow).orderDate).slice(0, 10) }}
          </template>
          <template #totalAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).totalAmount.toFixed(2) }}</span>
          </template>
          <template #invoicedAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).invoicedAmount.toFixed(2) }}</span>
          </template>
          <template #uninvoicedAmount="{ record }">
            <span class="amount">¥ {{ (record as CandidateRow).uninvoicedAmount.toFixed(2) }}</span>
          </template>
          <template #amount="{ record }">
            <a-input-number
              :model-value="amounts[(record as CandidateRow).key] ?? 0"
              :min="0"
              :max="(record as CandidateRow).uninvoicedAmount"
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
          该往来单位当前方向下没有可开票的单据
        </a-alert>
        <a-alert
          v-if="amountMismatch"
          type="warning"
          class="items-empty"
        >
          关联金额合计（{{ selectedAmount.toFixed(2) }}）与不含税金额（{{ amountExcludingTax.toFixed(2) }}）不一致
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
            关联金额合计：<span class="form-footer__total-amount">{{ selectedAmount.toFixed(2) }}</span>
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

.amount--total {
  font-weight: 600;
  color: rgb(var(--red-6));
}

.amount-summary {
  display: flex;
  gap: 32px;
  margin-bottom: 8px;
  font-size: 14px;
  color: var(--color-text-2);
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

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getAccounts, type AccountTreeNode } from '@/api/account'
import { createVoucher, toUtcMidnight, type CreateVoucherItemPayload } from '@/api/voucher'
import { Message } from '@arco-design/web-vue'

// —— types ——
/** 科目树选择项（非末级 / 停用科目不可选） */
interface AccountOption {
  key: string
  title: string
  disabled: boolean
  children: AccountOption[]
}

/** 分录行（借贷恰一个大于 0） */
interface EntryRow {
  rowKey: string
  accountId: string | undefined
  summary: string
  debit: number
  credit: number
}

// —— constants ——
/** 科目树字段映射 */
const treeFieldNames = { key: 'key', title: 'title', children: 'children', disabled: 'disabled' }

// —— helpers ——
/** 空分录行（默认一行供录入） */
function emptyRow(): EntryRow {
  return { rowKey: `row-${Date.now()}-${Math.random().toString(16).slice(2)}`, accountId: undefined, summary: '', debit: 0, credit: 0 }
}

// —— stores/composables ——
const router = useRouter()

// —— reactive state ——
const formRef = ref()
const submitting = ref(false)
const accountTree = ref<AccountOption[]>([])
const form = ref({ voucherDate: '', summary: '' })
const entries = ref<EntryRow[]>([emptyRow()])

// —— computed ——
const treeData = computed(() => accountTree.value)

/** 借方合计 */
const totalDebit = computed(() => entries.value.reduce((sum, row) => sum + (row.debit || 0), 0))
/** 贷方合计 */
const totalCredit = computed(() => entries.value.reduce((sum, row) => sum + (row.credit || 0), 0))
/** 借贷差额（0 表示平衡） */
const difference = computed(() => Number((totalDebit.value - totalCredit.value).toFixed(2)))

/** 表单校验规则（与后端约束一致） */
const rules = {
  voucherDate: [{ required: true, message: '请选择记账日期' }],
  summary: [{ required: true, message: '请输入凭证摘要' }, { maxLength: 200, message: '摘要长度不能超过 200' }],
}

// —— lifecycle ——
onMounted(async () => {
  try {
    accountTree.value = toOptions(await getAccounts())
  } catch {
    // 错误提示已由请求层统一处理
  }
})

// —— methods ——
/** 新增分录行 */
function onAddRow(): void {
  entries.value.push(emptyRow())
}

/** 删除分录行（至少保留一行） */
function onRemoveRow(row: EntryRow): void {
  if (entries.value.length <= 1) {
    Message.warning('至少保留一条分录')
    return
  }
  entries.value = entries.value.filter((item) => item.rowKey !== row.rowKey)
}

/** 借方录入：非 0 时清空同行贷方（保证每行恰有一个方向大于 0） */
function onDebitChange(row: EntryRow, value: number | undefined): void {
  row.debit = value ?? 0
  if (row.debit > 0) row.credit = 0
}

/** 贷方录入：非 0 时清空同行借方 */
function onCreditChange(row: EntryRow, value: number | undefined): void {
  row.credit = value ?? 0
  if (row.credit > 0) row.debit = 0
}

/** 提交：校验主表 + 分录合法性 + 借贷平衡 */
async function onSubmit(): Promise<void> {
  if (submitting.value) return

  const errors = await formRef.value?.validate()
  if (errors) return

  if (entries.value.length === 0) {
    Message.error('请至少添加一条分录')
    return
  }

  const invalidAccount = entries.value.find((row) => !row.accountId)
  if (invalidAccount) {
    Message.error('请为每条分录选择末级科目')
    return
  }

  const invalidAmount = entries.value.find((row) => (row.debit > 0) === (row.credit > 0))
  if (invalidAmount) {
    Message.error('每条分录的借方与贷方金额须恰有一个大于 0')
    return
  }

  if (difference.value !== 0) {
    Message.error(`借贷不平衡，差额 ${difference.value.toFixed(2)}`)
    return
  }

  const items: CreateVoucherItemPayload[] = entries.value.map((row) => ({
    accountId: row.accountId as string,
    summary: row.summary.trim() || undefined,
    debit: row.debit || 0,
    credit: row.credit || 0,
  }))

  submitting.value = true
  try {
    const created = await createVoucher({
      voucherDate: toUtcMidnight(form.value.voucherDate),
      summary: form.value.summary.trim(),
      items,
    })
    Message.success('凭证已录入')
    void router.replace({ name: 'voucherDetail', params: { id: created.id } })
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}

function onCancel(): void {
  void router.push({ name: 'vouchers' })
}

/** 科目树 → 选择项：末级且启用方可选 */
function toOptions(nodes: AccountTreeNode[]): AccountOption[] {
  return nodes.map((node) => {
    const children = toOptions(node.children)
    return {
      key: node.id,
      title: `${node.code} ${node.name}`,
      disabled: children.length > 0 || node.status === 0,
      children,
    }
  })
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      title="录入手工凭证"
      @back="onCancel"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item
              label="记账日期"
              field="voucherDate"
              extra="记账日期所在会计期间须未结账"
            >
              <a-date-picker
                v-model="form.voucherDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="摘要"
              field="summary"
            >
              <a-input
                v-model="form.summary"
                placeholder="如：计提折旧 / 期末调整"
                allow-clear
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">
          分录
        </a-divider>
        <a-button
          type="primary"
          size="small"
          style="margin-bottom: 12px"
          @click="onAddRow"
        >
          添加分录
        </a-button>
        <a-table
          row-key="rowKey"
          :data="entries"
          :pagination="false"
          :bordered="false"
          size="small"
        >
          <template #columns>
            <a-table-column
              title="科目"
              :width="260"
            >
              <template #cell="{ record }">
                <a-tree-select
                  :model-value="(record as EntryRow).accountId"
                  :data="treeData"
                  :field-names="treeFieldNames"
                  placeholder="请选择末级科目"
                  allow-search
                  allow-clear
                  @change="(value: unknown) => ((record as EntryRow).accountId = (value as string) || undefined)"
                />
              </template>
            </a-table-column>
            <a-table-column
              title="行摘要"
              :width="220"
            >
              <template #cell="{ record }">
                <a-input
                  :model-value="(record as EntryRow).summary"
                  placeholder="可空"
                  allow-clear
                  @change="(value: string) => ((record as EntryRow).summary = value)"
                />
              </template>
            </a-table-column>
            <a-table-column
              title="借方"
              :width="160"
              align="right"
            >
              <template #cell="{ record }">
                <a-input-number
                  :model-value="(record as EntryRow).debit"
                  :min="0"
                  :precision="2"
                  placeholder="0.00"
                  style="width: 100%"
                  @change="(value: number | undefined) => onDebitChange(record as EntryRow, value)"
                />
              </template>
            </a-table-column>
            <a-table-column
              title="贷方"
              :width="160"
              align="right"
            >
              <template #cell="{ record }">
                <a-input-number
                  :model-value="(record as EntryRow).credit"
                  :min="0"
                  :precision="2"
                  placeholder="0.00"
                  style="width: 100%"
                  @change="(value: number | undefined) => onCreditChange(record as EntryRow, value)"
                />
              </template>
            </a-table-column>
            <a-table-column
              title="操作"
              :width="90"
              align="center"
            >
              <template #cell="{ record }">
                <a-button
                  type="text"
                  size="small"
                  status="danger"
                  @click="onRemoveRow(record as EntryRow)"
                >
                  删除
                </a-button>
              </template>
            </a-table-column>
          </template>
        </a-table>

        <!-- 借贷合计与差额提示：不平衡时阻止提交 -->
        <div class="entry-total">
          <span>借方合计 <b class="amount">¥ {{ totalDebit.toFixed(2) }}</b></span>
          <span>贷方合计 <b class="amount">¥ {{ totalCredit.toFixed(2) }}</b></span>
          <span :class="difference === 0 ? 'entry-total__balanced' : 'entry-total__unbalanced'">
            {{ difference === 0 ? '借贷平衡' : `差额 ¥ ${difference.toFixed(2)}` }}
          </span>
        </div>
      </a-form>

      <div class="form-footer">
        <a-space>
          <a-button @click="onCancel">
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

.form-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 16px;
  margin-top: 16px;
  border-top: 1px solid var(--color-border);
}

.entry-total {
  display: flex;
  gap: 24px;
  align-items: center;
  justify-content: flex-end;
  padding: 12px 0 0;
  font-size: 13px;
  color: var(--color-text-2);
}

.entry-total__balanced {
  color: rgb(var(--green-6));
}

.entry-total__unbalanced {
  color: rgb(var(--red-6));
  font-weight: 600;
}

.amount {
  font-variant-numeric: tabular-nums;
}
</style>

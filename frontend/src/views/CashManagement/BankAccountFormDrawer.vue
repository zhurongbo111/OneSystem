<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createBankAccount, getBankAccount, updateBankAccount } from '@/api/bankAccount'
import type { BankAccountStatus, BankAccountType } from '@/api/bankAccount'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface BankAccountFormState {
  code: string
  name: string
  type: BankAccountType
  bankName: string
  accountNo: string
  initialBalance: number
  status: BankAccountStatus
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的资金账户 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): BankAccountFormState {
  return {
    code: '',
    name: '',
    type: 1,
    bankName: '',
    accountNo: '',
    initialBalance: 0,
    status: 1,
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<BankAccountFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增资金账户' : '编辑资金账户'))

const typeOptions = [
  { label: '现金', value: 1 },
  { label: '银行', value: 2 },
]

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 银行账户才需填写开户行 / 账号（与后端校验同源：type = Bank 时开户行必填） */
const isBank = computed(() => form.type === 2)

/** 校验规则：长度 / 区间与后端 BankAccountFieldConstraints 对齐 */
const rules: Record<string, FieldRule[]> = {
  code: [
    { required: true, message: '请输入账户编码' },
    { min: 1, max: 20, message: '账户编码长度必须在 1 到 20 之间' },
  ],
  name: [
    { required: true, message: '请输入账户名称' },
    { min: 1, max: 50, message: '账户名称长度必须在 1 到 50 之间' },
  ],
  bankName: [
    { required: isBank.value, message: '请输入开户行' },
    { max: 100, message: '开户行长度不能超过 100' },
  ],
  accountNo: [{ max: 30, message: '银行账号长度不能超过 30' }],
  initialBalance: [
    { required: true, type: 'number', min: 0, message: '初始余额必须大于等于 0' },
  ],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}

// —— watch ——
/** 打开抽屉时先重置（防数据串台），编辑态再拉取详情 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.editId) {
      void loadBankAccount(props.editId)
    }
  },
)

// —— methods ——
/** 加载资金账户详情并回填（编辑） */
async function loadBankAccount(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getBankAccount(id)
    form.code = detail.code
    form.name = detail.name
    form.type = detail.type
    form.bankName = detail.bankName ?? ''
    form.accountNo = detail.accountNo ?? ''
    form.initialBalance = detail.initialBalance
    form.status = detail.status
    form.remark = detail.remark ?? ''
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 切类型时清空不适用于该类型的字段，避免脏快照 */
function onTypeChange(): void {
  if (!isBank.value) {
    form.bankName = ''
    form.accountNo = ''
  }
}

/** 提交：新增 / 编辑共用 */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      code: form.code.trim(),
      name: form.name.trim(),
      type: form.type,
      bankName: isBank.value ? form.bankName.trim() || undefined : undefined,
      accountNo: isBank.value ? form.accountNo.trim() || undefined : undefined,
      initialBalance: form.initialBalance,
      status: form.status,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateBankAccount(props.editId as string, payload)
      Message.success('资金账户已更新')
    } else {
      await createBankAccount(payload)
      Message.success('资金账户已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码重复 40160）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="drawerTitle"
    :width="560"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <a-spin
      :loading="detailLoading"
      class="drawer-body"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-form-item
          label="账户编码"
          field="code"
        >
          <a-input
            v-model="form.code"
            placeholder="1-20 字符，全局唯一"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="账户名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="1-50 字符"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="账户类型"
          field="type"
          extra="现金账户对应现金结算、银行账户对应银行转账结算"
        >
          <a-radio-group
            v-model="form.type"
            :options="typeOptions"
            :disabled="detailLoading"
            @change="onTypeChange"
          />
        </a-form-item>

        <a-form-item
          v-if="isBank"
          label="开户行"
          field="bankName"
        >
          <a-input
            v-model="form.bankName"
            placeholder="≤ 100 字符，银行账户必填"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          v-if="isBank"
          label="银行账号"
          field="accountNo"
        >
          <a-input
            v-model="form.accountNo"
            placeholder="选填，≤ 30 字符"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="初始余额"
          field="initialBalance"
          extra="上线建账起点，≥ 0，最多 2 位小数"
        >
          <a-input-number
            v-model="form.initialBalance"
            :min="0"
            :precision="2"
            :disabled="detailLoading"
            class="drawer-number"
          />
        </a-form-item>

        <a-form-item
          label="状态"
          field="status"
        >
          <a-radio-group
            v-model="form.status"
            :options="statusOptions"
            :disabled="detailLoading"
          />
        </a-form-item>

        <a-form-item
          label="备注"
          field="remark"
        >
          <a-textarea
            v-model="form.remark"
            placeholder="选填，≤ 200 字符"
            :max-length="200"
            show-word-limit
            :auto-size="{ minRows: 2, maxRows: 4 }"
            :disabled="detailLoading"
          />
        </a-form-item>

        <div class="form-footer">
          <a-space>
            <a-button @click="onClose">
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
    </a-spin>
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  display: block;
  width: 100%;
}

.drawer-number {
  width: 100%;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

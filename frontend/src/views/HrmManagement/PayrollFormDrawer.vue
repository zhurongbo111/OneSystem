<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import type { Employee } from '@/api/employee'
import { createPayroll, updatePayroll } from '@/api/payroll'
import type { Payroll } from '@/api/payroll'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface PayrollFormState {
  employeeId: string | undefined
  year: number | undefined
  month: number | undefined
  baseSalary: number | undefined
  allowance: number | undefined
  deduction: number | undefined
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑（编辑时员工与期间不可改） */
  mode: 'create' | 'edit'
  /** 编辑时的列表行（列表已含全部字段，无需再请求详情） */
  record?: Payroll
  /** 员工下拉（由列表页传入） */
  employeeOptions: Employee[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): PayrollFormState {
  const now = new Date()
  return {
    employeeId: undefined,
    year: now.getFullYear(),
    month: now.getMonth() + 1,
    baseSalary: 0,
    allowance: 0,
    deduction: 0,
    remark: '',
  }
}

/** 金额上界（与后端 PayrollFieldConstraints.AmountMaxValue 对齐） */
const AMOUNT_MAX = 9999999.99

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive<PayrollFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增工资单' : '编辑工资单'))

/** 编辑态：员工与期间不可改（后端请求体亦不含这三个字段） */
const periodEditable = computed(() => props.mode === 'create')

const yearOptions = computed(() => {
  const current = new Date().getFullYear()
  return [current - 2, current - 1, current, current + 1].map((y) => ({ label: `${y} 年`, value: y }))
})

const monthOptions = Array.from({ length: 12 }, (_, i) => ({ label: `${i + 1} 月`, value: i + 1 }))

/** 员工下拉选项（姓名 + 工号） */
const employeeSelectOptions = computed(() =>
  props.employeeOptions.map((e) => ({ label: `${e.name}（${e.employeeNo}）`, value: e.id })),
)

/** 实发实时预览（口径与后端一致：基本工资 + 津贴 − 扣款） */
const netPayPreview = computed(
  () => Number(form.baseSalary ?? 0) + Number(form.allowance ?? 0) - Number(form.deduction ?? 0),
)

/** 实发预览文本（两位小数） */
const netPayText = computed(() => netPayPreview.value.toFixed(2))

/** 校验规则：金额区间 / 备注长度与后端 PayrollFieldConstraints 对齐 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  employeeId: [{ required: true, message: '请选择员工' }],
  year: [{ required: true, message: '请选择年份' }],
  month: [{ required: true, message: '请选择月份' }],
  baseSalary: [amountRule('基本工资')],
  allowance: [amountRule('津贴')],
  deduction: [amountRule('扣款')],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时重置（防数据串台），编辑态以列表行回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.record) {
      form.employeeId = props.record.employeeId
      form.year = props.record.year
      form.month = props.record.month
      form.baseSalary = props.record.baseSalary
      form.allowance = props.record.allowance
      form.deduction = props.record.deduction
      form.remark = props.record.remark ?? ''
    }
  },
)

// —— methods ——
/** 金额校验（0 到上界；空值按 0 处理） */
function amountRule(label: string): FieldRule {
  return {
    validator: (value: unknown, callback: (error?: string) => void) => {
      const amount = Number(value ?? 0)
      if (Number.isNaN(amount) || amount < 0 || amount > AMOUNT_MAX) {
        callback(`${label}必须在 0 到 ${AMOUNT_MAX} 之间`)
        return
      }
      callback()
    },
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交：新增 / 编辑共用（编辑为「薪酬调整」，员工与期间不可改） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const amounts = {
      baseSalary: Number(form.baseSalary ?? 0),
      allowance: Number(form.allowance ?? 0),
      deduction: Number(form.deduction ?? 0),
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updatePayroll(props.record?.id as string, amounts)
      Message.success('工资单已更新')
    } else {
      await createPayroll({
        employeeId: form.employeeId as string,
        year: Number(form.year),
        month: Number(form.month),
        ...amounts,
      })
      Message.success('工资单已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（期间重复 40170 / 已发放锁定 40171）
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
    <a-form
      ref="formRef"
      :model="form"
      :rules="rules"
      layout="vertical"
    >
      <a-form-item
        label="员工"
        field="employeeId"
        :extra="periodEditable ? '一个员工一个月只有一条工资单' : '员工不可修改'"
      >
        <a-select
          v-model="form.employeeId"
          :options="employeeSelectOptions"
          placeholder="请选择员工"
          allow-search
          :disabled="!periodEditable"
        />
      </a-form-item>

      <a-form-item
        label="期间"
        :extra="periodEditable ? '' : '期间不可修改'"
      >
        <a-space>
          <a-form-item
            field="year"
            :hide-label="true"
            class="period-item"
          >
            <a-select
              v-model="form.year"
              :options="yearOptions"
              placeholder="年份"
              :disabled="!periodEditable"
            />
          </a-form-item>
          <a-form-item
            field="month"
            :hide-label="true"
            class="period-item"
          >
            <a-select
              v-model="form.month"
              :options="monthOptions"
              placeholder="月份"
              :disabled="!periodEditable"
            />
          </a-form-item>
        </a-space>
      </a-form-item>

      <a-form-item
        label="基本工资"
        field="baseSalary"
      >
        <a-input-number
          v-model="form.baseSalary"
          :min="0"
          :max="AMOUNT_MAX"
          :precision="2"
          placeholder="请输入基本工资"
          class="amount-input"
        />
      </a-form-item>

      <a-form-item
        label="津贴"
        field="allowance"
      >
        <a-input-number
          v-model="form.allowance"
          :min="0"
          :max="AMOUNT_MAX"
          :precision="2"
          placeholder="请输入津贴"
          class="amount-input"
        />
      </a-form-item>

      <a-form-item
        label="扣款"
        field="deduction"
        extra="个税 / 社保由人工填入扣款，本期不做自动计税"
      >
        <a-input-number
          v-model="form.deduction"
          :min="0"
          :max="AMOUNT_MAX"
          :precision="2"
          placeholder="请输入扣款"
          class="amount-input"
        />
      </a-form-item>

      <a-form-item label="实发（自动计算）">
        <div class="net-pay-preview">
          {{ netPayText }}
        </div>
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
  </a-drawer>
</template>

<style scoped>
.period-item {
  width: 140px;
  margin-bottom: 0;
}

.amount-input {
  width: 100%;
}

.net-pay-preview {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-1);
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

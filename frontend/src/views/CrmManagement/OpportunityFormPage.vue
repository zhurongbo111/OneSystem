<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  OPPORTUNITY_STAGE_OPTIONS,
  createOpportunity,
  getOpportunity,
  updateOpportunity,
} from '@/api/opportunity'
import type { OpportunityStage } from '@/api/opportunity'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface OpportunityFormState {
  name: string
  partnerId: string | undefined
  amount: number
  stage: OpportunityStage
  /** 日期选择器绑定值：YYYY-MM-DD（undefined = 未设置） */
  expectedCloseDate: string | undefined
  ownerId: string | undefined
  remark: string
}

// —— constants ——
/**
 * 预计金额输入上限：仅作前端输入约束（后端 `OpportunityFieldConstraints.AmountMaxValue` 为准）。
 * 取 12 位整数上限，避免超出 JS 安全整数范围导致的精度丢失（`no-loss-of-precision`）。
 */
const AMOUNT_MAX = 999999999999.99

// —— helpers ——
function emptyForm(): OpportunityFormState {
  return {
    name: '',
    partnerId: undefined,
    amount: 0,
    stage: 0,
    expectedCloseDate: undefined,
    ownerId: undefined,
    remark: '',
  }
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 新建 / 编辑共用本页（design.md §4.1 / §4.2） */
const isEdit = computed(() => route.name === 'opportunityEdit')
const opportunityId = computed(() => (isEdit.value ? (route.params.id as string) : undefined))

const formRef = ref<FormInstance>()
const submitting = ref(false)
const loading = ref(false)
const form = reactive<OpportunityFormState>(emptyForm())

/** 客户 / 负责人下拉数据源（仅启用） */
const partners = ref<Partner[]>([])
const employees = ref<Employee[]>([])

// —— computed ——
/** 客户下拉：仅启用 + 客户 / 两者（与报价单同口径） */
const partnerOptions = computed(() =>
  partners.value.filter((p) => p.type === 2 || p.type === 3).map((p) => ({ label: p.name, value: p.id })),
)

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 校验规则：与后端 OpportunityFieldConstraints / Create·UpdateOpportunityRequestValidator 同源 */
const rules: Record<string, FieldRule[]> = {
  name: [
    { required: true, message: '请输入商机名称' },
    { max: 50, message: '商机名称长度不能超过 50' },
  ],
  amount: [{ required: true, message: '请输入预计金额' }],
  stage: [{ required: true, message: '请选择阶段' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}

// —— lifecycle ——
onMounted(async () => {
  await loadOptions()
  if (isEdit.value && opportunityId.value) {
    await loadDetail(opportunityId.value)
  }
})

// —— methods ——
async function loadOptions(): Promise<void> {
  try {
    const [customer, both, employeeResult] = await Promise.all([
      getPartners({ type: 2, status: 1, page: 1, pageSize: 100 }),
      getPartners({ type: 3, status: 1, page: 1, pageSize: 100 }),
      getEmployees({ page: 1, pageSize: 100, status: 1 }),
    ])
    const seen = new Set<string>()
    partners.value = [...customer.items, ...both.items].filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)))
    employees.value = employeeResult.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function loadDetail(id: string): Promise<void> {
  loading.value = true
  try {
    const detail = await getOpportunity(id)
    form.name = detail.name
    form.partnerId = detail.partnerId ?? undefined
    form.amount = detail.amount
    form.stage = detail.stage
    form.expectedCloseDate = detail.expectedCloseDate ?? undefined
    form.ownerId = detail.ownerId ?? undefined
    form.remark = detail.remark ?? ''
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  if (isEdit.value && opportunityId.value) {
    void router.push({ name: 'opportunityDetail', params: { id: opportunityId.value } })
    return
  }
  void router.push({ name: 'opportunities' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      name: form.name.trim(),
      partnerId: form.partnerId || undefined,
      amount: form.amount,
      stage: form.stage,
      expectedCloseDate: form.expectedCloseDate || undefined,
      ownerId: form.ownerId || undefined,
      remark: form.remark.trim() || undefined,
    }
    const saved = isEdit.value
      ? await updateOpportunity(opportunityId.value as string, payload)
      : await createOpportunity(payload)
    Message.success(isEdit.value ? '商机已保存' : '商机已创建')
    void router.push({ name: 'opportunityDetail', params: { id: saved.id } })
  } catch {
    // 错误提示已由请求层统一处理（40169 终态不可改阶段 / 40400 客户或负责人不存在）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      :title="isEdit ? '编辑商机' : '新建商机'"
      @back="goBack"
    />

    <a-card
      :bordered="false"
      :loading="loading"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="商机名称"
              field="name"
            >
              <a-input
                v-model="form.name"
                placeholder="1-50 字符"
                allow-clear
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="客户"
              field="partnerId"
            >
              <a-select
                v-model="form.partnerId"
                :options="partnerOptions"
                placeholder="选填（线索阶段可能尚无正式客户）"
                allow-clear
                allow-search
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="预计金额"
              field="amount"
            >
              <a-input-number
                v-model="form.amount"
                :min="0"
                :max="AMOUNT_MAX"
                :precision="2"
                placeholder="0.00"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="阶段"
              field="stage"
            >
              <a-select
                v-model="form.stage"
                :options="OPPORTUNITY_STAGE_OPTIONS"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="预计成交日期"
              field="expectedCloseDate"
            >
              <a-date-picker
                v-model="form.expectedCloseDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="选填"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="负责人"
              field="ownerId"
            >
              <a-select
                v-model="form.ownerId"
                :options="ownerOptions"
                placeholder="选填，仅在职员工"
                allow-clear
                allow-search
              />
            </a-form-item>
          </a-col>
        </a-row>

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

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

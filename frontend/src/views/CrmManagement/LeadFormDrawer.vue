<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  LEAD_SOURCE_OPTIONS,
  LEAD_STATUS_META,
  createLead,
  getLead,
  updateLead,
} from '@/api/lead'
import type { LeadSource, LeadStatus } from '@/api/lead'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface LeadFormState {
  name: string
  contact: string
  phone: string
  source: LeadSource
  status: LeadStatus
  ownerId: string | undefined
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑（编辑时状态终态不可选，其余字段全量覆盖） */
  mode: 'create' | 'edit'
  /** 编辑时的线索 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): LeadFormState {
  return {
    name: '',
    contact: '',
    phone: '',
    source: 4,
    status: 0,
    ownerId: undefined,
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<LeadFormState>(emptyForm())

/** 负责人下拉数据源（启用员工，一次取前 100 条） */
const employees = ref<Employee[]>([])

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增线索' : '编辑线索'))

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/**
 * 状态下拉：新建只允许「新线索 / 跟进中」；
 * 编辑可选「新线索 / 跟进中 / 已废弃」——「已转化」只能由转商机产生（design.md §0.1）。
 */
const statusOptions = computed(() =>
  (props.mode === 'create' ? ([0, 1] as LeadStatus[]) : ([0, 1, 3] as LeadStatus[])).map((value) => ({
    label: LEAD_STATUS_META[value].label,
    value,
  })),
)

/** 校验规则：与后端 LeadFieldConstraints / Create·UpdateLeadRequestValidator 同源 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  name: [
    { required: true, message: '请输入线索名称' },
    { max: 50, message: '线索名称长度不能超过 50' },
  ],
  contact: [{ max: 30, message: '联系人长度不能超过 30' }],
  phone: [{ max: 20, message: '电话长度不能超过 20' }],
  source: [{ required: true, message: '请选择来源' }],
  status: [{ required: true, message: '请选择状态' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    void loadEmployees()
    if (props.mode === 'edit' && props.editId) {
      void loadDetail(props.editId)
    }
  },
)

// —— methods ——
async function loadEmployees(): Promise<void> {
  try {
    const result = await getEmployees({ page: 1, pageSize: 100, status: 1 })
    employees.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function loadDetail(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getLead(id)
    form.name = detail.name
    form.contact = detail.contact ?? ''
    form.phone = detail.phone ?? ''
    form.source = detail.source
    form.status = detail.status
    form.ownerId = detail.ownerId ?? undefined
    form.remark = detail.remark ?? ''
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

function onClose(): void {
  emit('update:visible', false)
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      name: form.name.trim(),
      contact: form.contact.trim() || undefined,
      phone: form.phone.trim() || undefined,
      source: form.source,
      status: form.status,
      ownerId: form.ownerId || undefined,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateLead(props.editId as string, payload)
      Message.success('线索已保存')
    } else {
      await createLead(payload)
      Message.success('线索已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（40168 终态不可改状态 / 40400 负责人不存在）
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
          label="线索名称 / 公司"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="1-50 字符"
            allow-clear
            :disabled="detailLoading"
          />
        </a-form-item>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="联系人"
              field="contact"
            >
              <a-input
                v-model="form.contact"
                placeholder="选填，≤ 30 字符"
                allow-clear
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="联系电话"
              field="phone"
            >
              <a-input
                v-model="form.phone"
                placeholder="选填，≤ 20 字符"
                allow-clear
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="来源"
              field="source"
            >
              <a-select
                v-model="form.source"
                :options="LEAD_SOURCE_OPTIONS"
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="状态"
              field="status"
            >
              <a-select
                v-model="form.status"
                :options="statusOptions"
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
        </a-row>

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

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

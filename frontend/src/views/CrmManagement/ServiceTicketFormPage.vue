<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import {
  SERVICE_TICKET_PRIORITY_OPTIONS,
  createServiceTicket,
  getServiceTicket,
  updateServiceTicket,
} from '@/api/serviceTicket'
import type { TicketPriority } from '@/api/serviceTicket'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface ServiceTicketFormState {
  partnerId: string | undefined
  contact: string
  phone: string
  title: string
  description: string
  priority: TicketPriority
  ownerId: string | undefined
  remark: string
}

// —— helpers ——
function emptyForm(): ServiceTicketFormState {
  return {
    partnerId: undefined,
    contact: '',
    phone: '',
    title: '',
    description: '',
    priority: 1,
    ownerId: undefined,
    remark: '',
  }
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 登记 / 编辑共用本页：编辑态由查询参数 id 表达（design.md §4.2 的路由表只有「登记」与「详情」两条表单相关路由） */
const editId = computed(() => (typeof route.query.id === 'string' && route.query.id !== '' ? route.query.id : undefined))
const isEdit = computed(() => editId.value !== undefined)

const formRef = ref<FormInstance>()
const submitting = ref(false)
const loading = ref(false)
const form = reactive<ServiceTicketFormState>(emptyForm())

/** 客户 / 负责人下拉数据源（仅启用客户、仅在职员工） */
const partners = ref<Partner[]>([])
const employees = ref<Employee[]>([])

// —— computed ——
/** 客户下拉：客户 / 两者且启用（与商机、报价单同口径） */
const partnerOptions = computed(() =>
  partners.value.filter((p) => p.type === 2 || p.type === 3).map((p) => ({ label: p.name, value: p.id })),
)

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 校验规则：与后端 ServiceTicketFieldConstraints / Create·UpdateServiceTicketRequestValidator 同源 */
const rules: Record<string, FieldRule[]> = {
  partnerId: [{ required: true, message: '请选择客户' }],
  title: [
    { required: true, message: '请输入工单标题' },
    { max: 50, message: '工单标题长度不能超过 50' },
  ],
  description: [{ max: 500, message: '问题描述长度不能超过 500' }],
  contact: [{ max: 30, message: '联系人长度不能超过 30' }],
  phone: [{ max: 20, message: '联系电话长度不能超过 20' }],
  priority: [{ required: true, message: '请选择优先级' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}

// —— lifecycle ——
onMounted(async () => {
  await loadOptions()
  if (editId.value) {
    await loadDetail(editId.value)
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
    const detail = await getServiceTicket(id)
    form.partnerId = detail.partnerId
    form.contact = detail.contact ?? ''
    form.phone = detail.phone ?? ''
    form.title = detail.title
    form.description = detail.description ?? ''
    form.priority = detail.priority
    form.ownerId = detail.ownerId ?? undefined
    form.remark = detail.remark ?? ''
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  if (editId.value) {
    void router.push({ name: 'serviceTicketDetail', params: { id: editId.value } })
    return
  }
  void router.push({ name: 'serviceTickets' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      partnerId: form.partnerId as string,
      contact: form.contact.trim() || undefined,
      phone: form.phone.trim() || undefined,
      title: form.title.trim(),
      description: form.description.trim() || undefined,
      priority: form.priority,
      ownerId: form.ownerId || undefined,
      remark: form.remark.trim() || undefined,
    }
    const saved = isEdit.value
      ? await updateServiceTicket(editId.value as string, payload)
      : await createServiceTicket(payload)
    Message.success(isEdit.value ? '工单已保存' : '工单已创建')
    void router.push({ name: 'serviceTicketDetail', params: { id: saved.id } })
  } catch {
    // 错误提示已由请求层统一处理（40172 已关闭不可编辑 / 40400 客户或负责人不存在）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      :title="isEdit ? '编辑服务工单' : '登记服务工单'"
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
              label="客户"
              field="partnerId"
            >
              <a-select
                v-model="form.partnerId"
                :options="partnerOptions"
                placeholder="请选择客户"
                allow-search
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="工单标题"
              field="title"
            >
              <a-input
                v-model="form.title"
                placeholder="1-50 字符"
                allow-clear
              />
            </a-form-item>
          </a-col>
        </a-row>

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
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="优先级"
              field="priority"
            >
              <a-select
                v-model="form.priority"
                :options="SERVICE_TICKET_PRIORITY_OPTIONS"
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
          label="问题描述"
          field="description"
        >
          <a-textarea
            v-model="form.description"
            placeholder="选填，≤ 500 字符"
            :max-length="500"
            show-word-limit
            :auto-size="{ minRows: 3, maxRows: 6 }"
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

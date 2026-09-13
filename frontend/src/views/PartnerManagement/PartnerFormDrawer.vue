<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createPartner, getPartner, updatePartner } from '@/api/partner'
import type { PartnerType } from '@/api/partner'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface PartnerFormState {
  name: string
  type: PartnerType | undefined
  contact: string
  phone: string
  address: string
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 / 查看（详情，disabled 展示） */
  mode: 'create' | 'edit' | 'view'
  /** 编辑 / 查看时的往来单位 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— constants ——
const typeOptions: { label: string; value: PartnerType }[] = [
  { label: '供应商', value: 1 },
  { label: '客户', value: 2 },
  { label: '两者', value: 3 },
]

// —— helpers ——
function emptyForm(): PartnerFormState {
  return {
    name: '',
    type: undefined,
    contact: '',
    phone: '',
    address: '',
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<PartnerFormState>(emptyForm())

/** 查看态审计信息（不参与表单提交） */
const detailCreatedAt = ref<string | null>(null)
const detailUpdatedAt = ref<string | null>(null)

// —— computed ——
const drawerTitle = computed(() =>
  props.mode === 'create' ? '新增往来单位' : props.mode === 'edit' ? '编辑往来单位' : '往来单位详情',
)

/** 查看态：整表 disabled 展示 */
const isView = computed(() => props.mode === 'view')

/** 校验规则：名称仅新增时校验（创建后不可改） */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  name: [
    { required: true, message: '请输入单位名称' },
    { min: 1, max: 50, message: '单位名称长度必须在 1 到 50 之间' },
  ],
  type: [{ required: true, message: '请选择单位类型' }],
  contact: [{ max: 20, message: '联系人长度不能超过 20' }],
  phone: [
    { max: 20, message: '联系电话长度不能超过 20' },
    { match: /^1[3-9]\d{9}$/, message: '联系电话格式不正确（11 位手机号）' },
  ],
  address: [{ max: 100, message: '地址长度不能超过 100' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式拉取详情 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    detailCreatedAt.value = null
    detailUpdatedAt.value = null
    if (props.mode !== 'create' && props.editId) {
      void loadPartner(props.editId)
    }
  },
)

// —— methods ——
/** 加载往来单位详情并回填（编辑 / 查看） */
async function loadPartner(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getPartner(id)
    form.name = detail.name
    form.type = detail.type
    form.contact = detail.contact ?? ''
    form.phone = detail.phone ?? ''
    form.address = detail.address ?? ''
    form.remark = detail.remark ?? ''
    detailCreatedAt.value = detail.createdAt
    detailUpdatedAt.value = detail.updatedAt
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

/** 提交（仅新增 / 编辑态渲染提交按钮） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      type: form.type as PartnerType,
      contact: form.contact.trim() || undefined,
      phone: form.phone.trim() || undefined,
      address: form.address.trim() || undefined,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updatePartner(props.editId as string, payload)
      Message.success('往来单位已更新')
    } else {
      await createPartner({ name: form.name.trim(), ...payload })
      Message.success('往来单位已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（名称已存在 40102）
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
        <!-- 名称：新增可输入；编辑 / 查看只读展示（创建后不可修改） -->
        <a-form-item
          v-if="mode === 'create'"
          label="单位名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="1-50 字符，创建后不可修改"
            :disabled="isView || detailLoading"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          v-else
          label="单位名称"
        >
          <a-input
            :model-value="form.name"
            disabled
          />
        </a-form-item>

        <a-form-item
          label="单位类型"
          field="type"
        >
          <a-radio-group
            v-model="form.type"
            :options="typeOptions"
            :disabled="isView || detailLoading"
          />
        </a-form-item>

        <a-form-item
          label="联系人"
          field="contact"
        >
          <a-input
            v-model="form.contact"
            placeholder="选填，≤ 20 字符"
            :disabled="isView || detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="联系电话"
          field="phone"
        >
          <a-input
            v-model="form.phone"
            placeholder="选填，11 位手机号"
            :disabled="isView || detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="地址"
          field="address"
        >
          <a-input
            v-model="form.address"
            placeholder="选填，≤ 100 字符"
            :disabled="isView || detailLoading"
            allow-clear
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
            :disabled="isView || detailLoading"
          />
        </a-form-item>

        <!-- 查看态：附加只读审计信息 -->
        <a-descriptions
          v-if="isView && !detailLoading"
          class="view-meta"
          :column="1"
          size="small"
        >
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detailCreatedAt) }}
          </a-descriptions-item>
          <a-descriptions-item label="更新时间">
            {{ formatDateTime(detailUpdatedAt) }}
          </a-descriptions-item>
        </a-descriptions>

        <div class="form-footer">
          <a-space>
            <a-button @click="onClose">
              {{ isView ? '关闭' : '取消' }}
            </a-button>
            <a-button
              v-if="!isView"
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

.view-meta {
  margin-bottom: 8px;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

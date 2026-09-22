<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createTaxRate, getTaxRate, updateTaxRate } from '@/api/taxRate'
import type { TaxRateStatus } from '@/api/taxRate'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface TaxRateFormState {
  code: string
  name: string
  /** 税率百分比数值（13 表示 13%） */
  rate: number
  status: TaxRateStatus
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的税率 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): TaxRateFormState {
  return {
    code: '',
    name: '',
    rate: 0,
    status: 1,
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<TaxRateFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增税率' : '编辑税率'))

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 校验规则：长度 / 区间与后端 TaxRateFieldConstraints 对齐（税率 0–100，4 位小数） */
const rules: Record<string, FieldRule[]> = {
  code: [
    { required: true, message: '请输入税率编码' },
    { min: 1, max: 20, message: '税率编码长度必须在 1 到 20 之间' },
  ],
  name: [
    { required: true, message: '请输入税率名称' },
    { min: 1, max: 50, message: '税率名称长度必须在 1 到 50 之间' },
  ],
  rate: [{ required: true, type: 'number', min: 0, max: 100, message: '税率必须在 0 到 100 之间' }],
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
      void loadTaxRate(props.editId)
    }
  },
)

// —— methods ——
/** 加载税率详情并回填（编辑） */
async function loadTaxRate(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getTaxRate(id)
    form.code = detail.code
    form.name = detail.name
    form.rate = detail.rate
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
      rate: form.rate,
      status: form.status,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateTaxRate(props.editId as string, payload)
      Message.success('税率已更新')
    } else {
      await createTaxRate(payload)
      Message.success('税率已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码 40151 / 名称 40152）
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
          label="税率编码"
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
          label="税率名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="1-50 字符，全局唯一"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="税率（%）"
          field="rate"
          extra="0–100 的百分比数值，如 13、13.5，最多 4 位小数"
        >
          <a-input-number
            v-model="form.rate"
            :min="0"
            :max="100"
            :precision="4"
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

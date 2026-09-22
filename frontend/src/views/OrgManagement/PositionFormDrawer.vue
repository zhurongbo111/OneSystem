<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createPosition, getPosition, updatePosition } from '@/api/position'
import type { PositionStatus } from '@/api/position'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface PositionFormState {
  code: string
  name: string
  status: PositionStatus
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的岗位 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): PositionFormState {
  return {
    code: '',
    name: '',
    status: 1,
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<PositionFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增岗位' : '编辑岗位'))

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 校验规则：长度与后端 PositionFieldConstraints 对齐 */
const rules: Record<string, FieldRule[]> = {
  code: [
    { required: true, message: '请输入岗位编码' },
    { min: 1, max: 20, message: '岗位编码长度必须在 1 到 20 之间' },
  ],
  name: [
    { required: true, message: '请输入岗位名称' },
    { min: 1, max: 50, message: '岗位名称长度必须在 1 到 50 之间' },
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
      void loadPosition(props.editId)
    }
  },
)

// —— methods ——
/** 加载岗位详情并回填（编辑） */
async function loadPosition(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getPosition(id)
    form.code = detail.code
    form.name = detail.name
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
      status: form.status,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updatePosition(props.editId as string, payload)
      Message.success('岗位已更新')
    } else {
      await createPosition(payload)
      Message.success('岗位已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码 40142 / 名称 40143）
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
          label="岗位编码"
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
          label="岗位名称"
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

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

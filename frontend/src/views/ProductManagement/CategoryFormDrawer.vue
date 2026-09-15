<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createCategory, updateCategory } from '@/api/product'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface CategoryFormState {
  name: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的分类 id */
  editId?: string
  /** 编辑时的分类名称（回填用，分类无独立详情接口） */
  editName?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— constants ——
/** 分类名称长度限制（对应后端 NameMinLength / NameMaxLength） */
const NAME_MIN = 1
const NAME_MAX = 20

// —— helpers ——
function emptyForm(): CategoryFormState {
  return { name: '' }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive<CategoryFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增分类' : '编辑分类'))

const rules = computed<Record<string, FieldRule[]>>(() => ({
  name: [
    { required: true, message: '请输入分类名称' },
    { min: NAME_MIN, max: NAME_MAX, message: '名称长度必须在 1 到 20 之间' },
  ],
}))

// —— watch ——
/** 打开抽屉时重置并按模式回填名称（新增为空 / 编辑回填） */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.editName) {
      form.name = props.editName
    }
  },
)

// —— methods ——
/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交（新增 / 编辑） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const name = form.name.trim()
    if (props.mode === 'edit') {
      await updateCategory(props.editId as string, name)
      Message.success('分类已更新')
    } else {
      await createCategory(name)
      Message.success('分类已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（重名 40105）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="drawerTitle"
    :width="480"
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
        label="分类名称"
        field="name"
      >
        <a-input
          v-model="form.name"
          :placeholder="`1-${NAME_MAX} 字符`"
          :max-length="NAME_MAX"
          show-word-limit
          allow-clear
          @press-enter="onSubmit"
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
.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createWarehouse, getWarehouse, updateWarehouse } from '@/api/warehouse'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface WarehouseFormState {
  code: string
  name: string
  address: string
  contact: string
  phone: string
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑（编码创建后不可修改，编辑态只读展示） */
  mode: 'create' | 'edit'
  /** 编辑时的仓库 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): WarehouseFormState {
  return {
    code: '',
    name: '',
    address: '',
    contact: '',
    phone: '',
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<WarehouseFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增仓库' : '编辑仓库'))

/** 校验规则：编码仅新增时校验（编辑只读展示），规则与后端 WarehouseFieldConstraints 同源 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  code: [
    { required: true, message: '请输入仓库编码' },
    { match: /^[A-Za-z0-9_-]{2,20}$/, message: '编码为 2-20 位字母、数字、下划线或连字符' },
  ],
  name: [
    { required: true, message: '请输入仓库名称' },
    { max: 50, message: '名称长度不能超过 50' },
  ],
  address: [{ max: 100, message: '地址长度不能超过 100' }],
  contact: [{ max: 20, message: '联系人长度不能超过 20' }],
  phone: [{ match: /^1[3-9]\d{9}$/, message: '请输入 11 位手机号' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式拉取详情回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.editId) {
      void loadWarehouse(props.editId)
    }
  },
)

// —— methods ——
/** 加载仓库详情并回填（编辑） */
async function loadWarehouse(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getWarehouse(id)
    form.code = detail.code
    form.name = detail.name
    form.address = detail.address ?? ''
    form.contact = detail.contact ?? ''
    form.phone = detail.phone ?? ''
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

/** 提交（仅新增 / 编辑态渲染提交按钮） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      name: form.name.trim(),
      address: form.address.trim() || undefined,
      contact: form.contact.trim() || undefined,
      phone: form.phone.trim() || undefined,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateWarehouse(props.editId as string, payload)
      Message.success('仓库已更新')
    } else {
      await createWarehouse({ code: form.code.trim(), ...payload })
      Message.success('仓库已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码已存在 40122 / 名称已存在 40125）
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
        <!-- 编码：新增可输入；编辑只读展示（创建后不可修改） -->
        <a-form-item
          v-if="mode === 'create'"
          label="仓库编码"
          field="code"
        >
          <a-input
            v-model="form.code"
            placeholder="2-20 位字母、数字、下划线或连字符"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          v-else
          label="仓库编码"
        >
          <a-input
            :model-value="form.code"
            disabled
          />
        </a-form-item>

        <a-form-item
          label="仓库名称"
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
          label="地址"
          field="address"
        >
          <a-input
            v-model="form.address"
            placeholder="选填，≤ 100 字符"
            :disabled="detailLoading"
            allow-clear
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
                placeholder="选填，≤ 20 字符"
                :disabled="detailLoading"
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
                placeholder="选填，11 位手机号"
                :disabled="detailLoading"
                allow-clear
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

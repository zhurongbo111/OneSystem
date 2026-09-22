<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'

import { getRoles } from '@/api/role'
import { createUser, getUser, updateUser } from '@/api/user'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface UserFormState {
  username: string
  displayName: string
  email: string
  phone: string
  password: string
  /** 角色 id 集合（全量提交，至少一个） */
  roleIds: string[]
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的用户 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): UserFormState {
  return { username: '', displayName: '', email: '', phone: '', password: '', roleIds: [] }
}

/** 邮箱：选填；填写时校验格式 */
function validateEmail(value: unknown, callback: (error?: string) => void): void {
  const v = String(value ?? '').trim()
  if (!v) {
    callback()
    return
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) {
    callback('邮箱格式不正确')
    return
  }
  callback()
}

/** 手机号：选填；填写时校验格式 */
function validatePhone(value: unknown, callback: (error?: string) => void): void {
  const v = String(value ?? '').trim()
  if (!v) {
    callback()
    return
  }
  if (!/^1[3-9]\d{9}$/.test(v)) {
    callback('手机号格式不正确')
    return
  }
  callback()
}

/** 角色：必选且至少一个（后端 CreateUser / UpdateUser 全量覆盖语义） */
function validateRoles(value: unknown, callback: (error?: string) => void): void {
  const list = Array.isArray(value) ? value : []
  if (list.length === 0) {
    callback('请至少选择一个角色')
    return
  }
  callback()
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<UserFormState>(emptyForm())
/** 角色下拉选项（用户必选至少一个角色，故一次性拉取较大分页） */
const roleOptions = ref<{ label: string; value: string }[]>([])

// —— computed ——
/** 校验规则：密码仅新增时必填（编辑不渲染该字段） */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  username: [
    { required: true, message: '请输入用户名' },
    { match: /^[a-zA-Z0-9_]{3,50}$/, message: '用户名只能由 3-50 位字母、数字或下划线组成' },
  ],
  displayName: [
    { required: true, message: '请输入显示名' },
    { maxLength: 50, message: '显示名长度不能超过 50' },
  ],
  email: [{ validator: validateEmail }],
  phone: [{ validator: validatePhone }],
  roleIds: [{ validator: validateRoles }],
  password:
    props.mode === 'create'
      ? [
          { required: true, message: '请输入初始密码' },
          { minLength: 6, maxLength: 32, message: '密码长度必须在 6 到 32 之间' },
        ]
      : [],
}))

// —— watch ——
/** 打开抽屉时初始化：先清空（避免残留上次数据），编辑再按 id 拉取详情回填（不污染列表数据源） */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.editId) {
      void loadUser(props.editId)
    }
  },
)

// —— lifecycle ——
onMounted(() => {
  void loadRoleOptions()
})

// —— methods ——
/** 加载角色下拉选项 */
async function loadRoleOptions(): Promise<void> {
  if (roleOptions.value.length > 0) return
  try {
    const result = await getRoles({ page: 1, pageSize: 100 })
    roleOptions.value = result.items.map((item) => ({ label: item.name, value: item.id }))
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 加载待编辑用户详情并回填 */
async function loadUser(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getUser(id)
    form.username = detail.username
    form.displayName = detail.displayName
    form.email = detail.email ?? ''
    form.phone = detail.phone ?? ''
    form.password = ''
    form.roleIds = detail.roles.map((role) => role.id)
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

/** 提交：手动校验，失败保持打开；成功后通知列表刷新并关闭 */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const email = form.email.trim() || undefined
    const phone = form.phone.trim() || undefined
    if (props.mode === 'edit') {
      await updateUser(props.editId as string, {
        displayName: form.displayName.trim(),
        email,
        phone,
        roleIds: [...form.roleIds],
      })
      Message.success('用户已更新')
    } else {
      await createUser({
        username: form.username.trim(),
        displayName: form.displayName.trim(),
        email,
        phone,
        password: form.password,
        roleIds: [...form.roleIds],
      })
      Message.success('用户已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="mode === 'edit' ? '编辑用户' : '新增用户'"
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
          label="用户名"
          field="username"
        >
          <a-input
            v-model="form.username"
            placeholder="3-50 位字母、数字或下划线"
            :readonly="mode === 'edit'"
            :disabled="mode === 'edit' || detailLoading"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          label="显示名"
          field="displayName"
        >
          <a-input
            v-model="form.displayName"
            placeholder="请输入显示名"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          label="角色"
          field="roleIds"
        >
          <a-select
            v-model="form.roleIds"
            :options="roleOptions"
            :disabled="detailLoading"
            placeholder="请选择角色（至少一个）"
            multiple
            allow-clear
          />
        </a-form-item>
        <a-form-item
          label="邮箱"
          field="email"
        >
          <a-input
            v-model="form.email"
            placeholder="选填"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          label="手机号"
          field="phone"
        >
          <a-input
            v-model="form.phone"
            placeholder="选填"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          v-if="mode === 'create'"
          label="初始密码"
          field="password"
        >
          <a-input-password
            v-model="form.password"
            placeholder="6-32 位密码"
            :disabled="detailLoading"
            allow-clear
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

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'

import { createRole, getPermissions, getRole, updateRole } from '@/api/role'
import type { PermissionGroup } from '@/api/role'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface RoleFormState {
  name: string
  remark: string
}

/** 权限树节点（分组为父节点，权限点为叶子） */
interface PermissionNode {
  key: string
  title: string
  disabled?: boolean
  children?: PermissionNode[]
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的角色 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): RoleFormState {
  return { name: '', remark: '' }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const permissionLoading = ref(false)
const form = reactive<RoleFormState>(emptyForm())

/** 权限点分组（中文名称由后端返回，前端不硬编码） */
const groups = ref<PermissionGroup[]>([])
/** 已勾选节点 key（含分组节点，提交前过滤为权限点） */
const checkedKeys = ref<string[]>([])
/** 内置角色：名称与权限不可改（后端 40175 兜底） */
const readonly = ref(false)

// —— computed ——
/** 校验规则：长度约束与后端 RoleFieldConstraints 一致 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  name: [
    { required: true, message: '请输入角色名称' },
    { maxLength: 20, message: '角色名称长度不能超过 20' },
  ],
  remark: [{ maxLength: 100, message: '备注长度不能超过 100' }],
}))

/** 权限树数据（内置角色只读时整树禁用） */
const treeData = computed<PermissionNode[]>(() =>
  groups.value.map((group) => ({
    key: group.groupName,
    title: group.groupName,
    disabled: readonly.value,
    children: group.items.map((item) => ({
      key: item.key,
      title: item.name,
      disabled: readonly.value,
    })),
  })),
)

/** 提交用权限点集合（过滤掉分组节点 key） */
const selectedPermissions = computed<string[]>(() => {
  const groupKeys = new Set(groups.value.map((group) => group.groupName))
  return checkedKeys.value.filter((key) => !groupKeys.has(key))
})

// —— watch ——
/** 打开抽屉时初始化：先清空（避免残留上次数据），编辑再按 id 拉取详情回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    checkedKeys.value = []
    readonly.value = false
    if (props.mode === 'edit' && props.editId) {
      void loadRole(props.editId)
    }
  },
)

// —— lifecycle ——
onMounted(() => {
  void loadPermissions()
})

// —— methods ——
/** 加载权限点分组清单（随抽屉挂载一次，清单不随角色变化） */
async function loadPermissions(): Promise<void> {
  if (groups.value.length > 0) return
  permissionLoading.value = true
  try {
    groups.value = await getPermissions()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    permissionLoading.value = false
  }
}

/** 加载待编辑角色详情并回填 */
async function loadRole(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getRole(id)
    form.name = detail.name
    form.remark = detail.remark ?? ''
    checkedKeys.value = [...detail.permissionKeys]
    readonly.value = detail.isBuiltin
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
    const payload = {
      name: form.name.trim(),
      remark: form.remark.trim() || undefined,
      permissionKeys: selectedPermissions.value,
    }
    if (props.mode === 'edit') {
      await updateRole(props.editId as string, payload)
      Message.success('角色已更新')
    } else {
      await createRole(payload)
      Message.success('角色已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（40173 重名 / 40000 非法权限点）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="mode === 'edit' ? '编辑角色' : '新增角色'"
    :width="640"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <a-spin
      :loading="detailLoading || permissionLoading"
      class="drawer-body"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-form-item
          label="角色名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="请输入角色名称（最多 20 字）"
            :disabled="readonly || detailLoading"
            :max-length="20"
            show-word-limit
            allow-clear
          />
        </a-form-item>
        <a-form-item
          label="备注"
          field="remark"
        >
          <a-textarea
            v-model="form.remark"
            placeholder="选填（最多 100 字，留空表示清空）"
            :disabled="readonly || detailLoading"
            :max-length="100"
            :auto-size="{ minRows: 2, maxRows: 4 }"
            show-word-limit
          />
        </a-form-item>
        <a-form-item
          label="权限点"
          field="permissionKeys"
        >
          <div class="permission-panel">
            <a-tree
              v-model:checked-keys="checkedKeys"
              :data="treeData"
              :disabled="readonly || detailLoading"
              checkable
              default-expand-all
            />
          </div>
        </a-form-item>

        <div class="form-footer">
          <a-space>
            <a-button @click="onClose">
              取消
            </a-button>
            <a-button
              type="primary"
              :disabled="readonly"
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

.permission-panel {
  width: 100%;
  max-height: 420px;
  padding: 8px 12px;
  overflow: auto;
  background: var(--color-fill-1);
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-small);
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

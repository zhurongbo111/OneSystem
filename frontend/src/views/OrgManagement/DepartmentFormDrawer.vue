<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createDepartment, getDepartment, updateDepartment } from '@/api/department'
import type { DepartmentStatus, DepartmentTreeNode } from '@/api/department'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface DepartmentFormState {
  code: string
  name: string
  parentId: string | undefined
  sortOrder: number
  status: DepartmentStatus
  remark: string
}

/** 上级部门下拉的树节点（Arco TreeSelect：key / title / children） */
interface ParentOption {
  key: string
  title: string
  children: ParentOption[]
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的部门 id */
  editId?: string
  /** 新增下级时的默认上级 id（新增顶级为 undefined） */
  defaultParentId?: string
  /** 部门树（上级选择数据源，由列表页传入，避免重复请求） */
  tree: DepartmentTreeNode[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): DepartmentFormState {
  return {
    code: '',
    name: '',
    parentId: undefined,
    sortOrder: 0,
    status: 1,
    remark: '',
  }
}

/** 递归构造上级下拉节点；excludeId 命中时整棵子树剪掉（编辑时上级不得为自身或自身后代） */
function buildParentOptions(nodes: DepartmentTreeNode[], excludeId?: string): ParentOption[] {
  return nodes
    .filter((node) => node.id !== excludeId)
    .map((node) => ({
      key: node.id,
      title: node.code ? `${node.name}（${node.code}）` : node.name,
      children: buildParentOptions(node.children, excludeId),
    }))
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<DepartmentFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增部门' : '编辑部门'))

/** 上级下拉数据：编辑时排除自身及后代（防环，后端 40141 兜底） */
const parentOptions = computed<ParentOption[]>(() => buildParentOptions(props.tree, props.mode === 'edit' ? props.editId : undefined))

/** Arco TreeSelect 字段映射（显式声明，避免默认值差异） */
const parentFieldNames = { key: 'key', title: 'title', children: 'children' }

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 校验规则：长度 / 区间与后端 DepartmentFieldConstraints 对齐 */
const rules: Record<string, FieldRule[]> = {
  code: [
    { required: true, message: '请输入部门编码' },
    { min: 1, max: 20, message: '部门编码长度必须在 1 到 20 之间' },
  ],
  name: [
    { required: true, message: '请输入部门名称' },
    { min: 1, max: 50, message: '部门名称长度必须在 1 到 50 之间' },
  ],
  sortOrder: [{ required: true, type: 'number', min: 0, max: 9999, message: '排序必须在 0 到 9999 之间' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式拉取详情 / 套用默认上级 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'create') {
      form.parentId = props.defaultParentId
      return
    }
    if (props.editId) {
      void loadDepartment(props.editId)
    }
  },
)

// —— methods ——
/** 加载部门详情并回填（编辑） */
async function loadDepartment(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getDepartment(id)
    form.code = detail.code
    form.name = detail.name
    form.parentId = detail.parentId ?? undefined
    form.sortOrder = detail.sortOrder
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
      parentId: form.parentId,
      sortOrder: form.sortOrder,
      status: form.status,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateDepartment(props.editId as string, payload)
      Message.success('部门已更新')
    } else {
      await createDepartment(payload)
      Message.success('部门已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码 40138 / 同级重名 40139 / 防环 40141）
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
          label="部门编码"
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
          label="部门名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="1-50 字符，同一上级下唯一"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="上级部门"
          field="parentId"
          extra="不选表示顶级部门；不可选择自身或自身下级"
        >
          <a-tree-select
            v-model="form.parentId"
            :data="parentOptions"
            :field-names="parentFieldNames"
            placeholder="不选表示顶级部门"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="排序"
          field="sortOrder"
        >
          <a-input-number
            v-model="form.sortOrder"
            :min="0"
            :max="9999"
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

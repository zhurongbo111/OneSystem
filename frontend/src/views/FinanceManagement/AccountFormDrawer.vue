<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createAccount, getAccount, updateAccount } from '@/api/account'
import type { AccountCategory, AccountDirection, AccountStatus, AccountTreeNode } from '@/api/account'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface AccountFormState {
  code: string
  name: string
  category: AccountCategory
  direction: AccountDirection
  parentId: string | undefined
  sortOrder: number
  status: AccountStatus
  remark: string
}

/** 上级科目下拉的树节点（Arco TreeSelect：key / title / children） */
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
  /** 编辑时的科目 id */
  editId?: string
  /** 新增下级时的默认上级 id（新增一级科目为 undefined） */
  defaultParentId?: string
  /** 科目树（上级选择数据源，由列表页传入，避免重复请求） */
  tree: AccountTreeNode[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): AccountFormState {
  return {
    code: '',
    name: '',
    category: 1,
    direction: 1,
    parentId: undefined,
    sortOrder: 0,
    status: 1,
    remark: '',
  }
}

/** 递归构造上级下拉节点；excludeId 命中时整棵子树剪掉（编辑时上级不得为自身或自身下级） */
function buildParentOptions(nodes: AccountTreeNode[], excludeId?: string): ParentOption[] {
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
const form = reactive<AccountFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增科目' : '编辑科目'))

/** 上级下拉数据：编辑时排除自身及下级（防环，后端 40141 兜底） */
const parentOptions = computed<ParentOption[]>(() => buildParentOptions(props.tree, props.mode === 'edit' ? props.editId : undefined))

/** Arco TreeSelect 字段映射（显式声明，避免默认值差异） */
const parentFieldNames = { key: 'key', title: 'title', children: 'children' }

/** 科目类别（唯一事实源 specs/031-erp-finance-master/design.md §0.1） */
const categoryOptions = [
  { label: '资产', value: 1 },
  { label: '负债', value: 2 },
  { label: '权益', value: 3 },
  { label: '成本', value: 4 },
  { label: '损益', value: 5 },
]

/** 余额方向（唯一事实源 specs/031-erp-finance-master/design.md §0.1） */
const directionOptions = [
  { label: '借', value: 1 },
  { label: '贷', value: 2 },
]

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 校验规则：长度 / 区间与后端 AccountFieldConstraints 对齐 */
const rules: Record<string, FieldRule[]> = {
  code: [
    { required: true, message: '请输入科目编码' },
    { min: 1, max: 20, message: '科目编码长度必须在 1 到 20 之间' },
  ],
  name: [
    { required: true, message: '请输入科目名称' },
    { min: 1, max: 50, message: '科目名称长度必须在 1 到 50 之间' },
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
      void loadAccount(props.editId)
    }
  },
)

// —— methods ——
/** 加载科目详情并回填（编辑） */
async function loadAccount(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getAccount(id)
    form.code = detail.code
    form.name = detail.name
    form.category = detail.category
    form.direction = detail.direction
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

/**
 * 类别变更时套用默认余额方向（可改）：资产 / 成本 → 借，负债 / 权益 / 损益 → 贷
 * （specs/031-erp-finance-master/design.md §0.1）
 */
function onCategoryChange(): void {
  const creditCategories: number[] = [2, 3, 5]
  form.direction = creditCategories.includes(form.category) ? 2 : 1
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
      category: form.category,
      direction: form.direction,
      parentId: form.parentId,
      sortOrder: form.sortOrder,
      status: form.status,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateAccount(props.editId as string, payload)
      Message.success('科目已更新')
    } else {
      await createAccount(payload)
      Message.success('科目已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码 40149 / 防环 40141）
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
          label="科目编码"
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
          label="科目名称"
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
          label="科目类别"
          field="category"
        >
          <a-select
            v-model="form.category"
            :options="categoryOptions"
            :disabled="detailLoading"
            @change="onCategoryChange"
          />
        </a-form-item>

        <a-form-item
          label="余额方向"
          field="direction"
          extra="按类别默认取值（资产 / 成本 → 借，负债 / 权益 / 损益 → 贷），可改"
        >
          <a-radio-group
            v-model="form.direction"
            :options="directionOptions"
            :disabled="detailLoading"
          />
        </a-form-item>

        <a-form-item
          label="上级科目"
          field="parentId"
          extra="不选表示一级科目；不可选择自身或自身下级"
        >
          <a-tree-select
            v-model="form.parentId"
            :data="parentOptions"
            :field-names="parentFieldNames"
            placeholder="不选表示一级科目"
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

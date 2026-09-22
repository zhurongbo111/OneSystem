<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import type { DepartmentTreeNode } from '@/api/department'
import { createEmployee, getAvailableUsers, getEmployee, updateEmployee } from '@/api/employee'
import type { EmployeePickUser, EmployeeStatus, Gender } from '@/api/employee'
import type { PositionPick } from '@/api/position'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface EmployeeFormState {
  employeeNo: string
  name: string
  gender: Gender | undefined
  phone: string
  email: string
  departmentId: string | undefined
  positionId: string | undefined
  hireDate: string
  resignDate: string
  status: EmployeeStatus
  userId: string | undefined
  remark: string
}

/** 部门下拉节点（Arco TreeSelect：key / title / children） */
interface DepartmentTreeOption {
  key: string
  title: string
  children: DepartmentTreeOption[]
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的员工 id */
  editId?: string
  /** 部门树（选项数据源由列表页传入，避免重复请求） */
  departmentTree: DepartmentTreeNode[]
  /** 岗位下拉（仅启用岗位，由列表页传入） */
  positionOptions: PositionPick[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): EmployeeFormState {
  return {
    employeeNo: '',
    name: '',
    gender: undefined,
    phone: '',
    email: '',
    departmentId: undefined,
    positionId: undefined,
    hireDate: '',
    resignDate: '',
    status: 1,
    userId: undefined,
    remark: '',
  }
}

/** 递归构造部门下拉节点：规格 §0.4 停用部门不参与员工选择 */
function buildDepartmentOptions(nodes: DepartmentTreeNode[]): DepartmentTreeOption[] {
  return nodes
    .filter((node) => node.status === 1)
    .map((node) => ({
      key: node.id,
      title: node.name,
      children: buildDepartmentOptions(node.children),
    }))
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<EmployeeFormState>(emptyForm())

/** 关联账号候选（启用未绑定 ∪ 当前员工已绑定） */
const userOptions = ref<EmployeePickUser[]>([])

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增员工' : '编辑员工'))

const departmentOptions = computed<DepartmentTreeOption[]>(() => buildDepartmentOptions(props.departmentTree))

const departmentFieldNames = { key: 'key', title: 'title', children: 'children' }

const genderOptions = [
  { label: '男', value: 1 },
  { label: '女', value: 2 },
]

const statusOptions = [
  { label: '在职', value: 1 },
  { label: '离职', value: 0 },
]

const positionSelectOptions = computed(() =>
  props.positionOptions.map((p) => ({ label: `${p.name}（${p.code}）`, value: p.id })),
)

const userSelectOptions = computed(() =>
  userOptions.value.map((u) => ({ label: `${u.displayName}（${u.username}）`, value: u.id })),
)

/** 校验规则：长度 / 格式与后端 EmployeeFieldConstraints 对齐 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  employeeNo:
    props.mode === 'create'
      ? [
          { required: true, message: '请输入工号' },
          { min: 1, max: 20, message: '工号长度必须在 1 到 20 之间' },
        ]
      : [],
  name: [
    { required: true, message: '请输入姓名' },
    { min: 1, max: 50, message: '姓名长度必须在 1 到 50 之间' },
  ],
  phone: [
    { max: 20, message: '手机号长度不能超过 20' },
    { match: /^1[3-9]\d{9}$/, message: '手机号格式不正确（11 位手机号）' },
  ],
  email: [
    { max: 100, message: '邮箱长度不能超过 100' },
    { match: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: '邮箱格式不正确' },
  ],
  hireDate: [{ required: true, message: '请选择入职日期' }],
  resignDate: [
    {
      validator: (value: unknown, callback: (error?: string) => void) => {
        const resignDate = typeof value === 'string' ? value : ''
        if (resignDate && form.hireDate && resignDate < form.hireDate) {
          callback('离职日期不能早于入职日期')
          return
        }
        callback()
      },
    },
  ],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），编辑态拉取详情，两种模式都拉取可选账号 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    void loadUserOptions(props.mode === 'edit' ? props.editId : undefined)
    if (props.mode === 'edit' && props.editId) {
      void loadEmployee(props.editId)
    }
  },
)

// —— methods ——
/** 加载员工详情并回填（编辑） */
async function loadEmployee(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getEmployee(id)
    form.employeeNo = detail.employeeNo
    form.name = detail.name
    form.gender = detail.gender ?? undefined
    form.phone = detail.phone ?? ''
    form.email = detail.email ?? ''
    form.departmentId = detail.departmentId ?? undefined
    form.positionId = detail.positionId ?? undefined
    form.hireDate = detail.hireDate
    form.resignDate = detail.resignDate ?? ''
    form.status = detail.status
    form.userId = detail.userId ?? undefined
    form.remark = detail.remark ?? ''
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

/** 加载可选账号（启用且未被绑定 ∪ 当前员工已绑定） */
async function loadUserOptions(employeeId?: string): Promise<void> {
  try {
    userOptions.value = await getAvailableUsers(employeeId)
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交：新增 / 编辑共用（编辑不含工号，AGENTS.md §4.5） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      name: form.name.trim(),
      gender: form.gender,
      phone: form.phone.trim() || undefined,
      email: form.email.trim() || undefined,
      departmentId: form.departmentId,
      positionId: form.positionId,
      hireDate: form.hireDate,
      resignDate: form.resignDate || undefined,
      status: form.status,
      userId: form.userId,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateEmployee(props.editId as string, payload)
      Message.success('员工已更新')
    } else {
      await createEmployee({ employeeNo: form.employeeNo.trim(), ...payload })
      Message.success('员工已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（工号 40145 / 绑定冲突 40146 / 手机 40147 / 邮箱 40148）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="drawerTitle"
    :width="640"
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
          label="工号"
          field="employeeNo"
          extra="工号创建后不可修改"
        >
          <a-input
            v-model="form.employeeNo"
            placeholder="1-20 字符，全局唯一"
            :disabled="mode === 'edit' || detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="姓名"
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
          label="性别"
          field="gender"
        >
          <a-select
            v-model="form.gender"
            :options="genderOptions"
            placeholder="未填"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="手机号"
          field="phone"
          extra="选填，非空时全局唯一"
        >
          <a-input
            v-model="form.phone"
            placeholder="选填，11 位手机号"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="邮箱"
          field="email"
          extra="选填，非空时全局唯一"
        >
          <a-input
            v-model="form.email"
            placeholder="选填，≤ 100 字符"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="部门"
          field="departmentId"
          extra="仅可选择启用部门"
        >
          <a-tree-select
            v-model="form.departmentId"
            :data="departmentOptions"
            :field-names="departmentFieldNames"
            placeholder="不选表示未分配部门"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="岗位"
          field="positionId"
          extra="仅可选择启用岗位"
        >
          <a-select
            v-model="form.positionId"
            :options="positionSelectOptions"
            placeholder="不选表示未分配岗位"
            :disabled="detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="入职日期"
          field="hireDate"
        >
          <a-date-picker
            v-model="form.hireDate"
            value-format="YYYY-MM-DD"
            placeholder="请选择入职日期"
            :disabled="detailLoading"
            class="drawer-date"
          />
        </a-form-item>

        <a-form-item
          label="离职日期"
          field="resignDate"
          extra="状态为离职且留空时由后端补当天"
        >
          <a-date-picker
            v-model="form.resignDate"
            value-format="YYYY-MM-DD"
            placeholder="选填"
            :disabled="detailLoading"
            class="drawer-date"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="在职状态"
          field="status"
        >
          <a-radio-group
            v-model="form.status"
            :options="statusOptions"
            :disabled="detailLoading"
          />
        </a-form-item>

        <a-form-item
          label="关联账号"
          field="userId"
          extra="一个账号最多绑定一个员工；员工本身不是账号，登录仍走用户管理"
        >
          <a-select
            v-model="form.userId"
            :options="userSelectOptions"
            placeholder="不绑定账号"
            :disabled="detailLoading"
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

.drawer-date {
  width: 100%;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

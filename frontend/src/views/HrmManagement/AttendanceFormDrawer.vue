<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createAttendance, updateAttendance } from '@/api/attendance'
import type { Attendance, AttendanceType } from '@/api/attendance'
import type { Employee } from '@/api/employee'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface AttendanceFormState {
  employeeId: string | undefined
  type: AttendanceType
  startDate: string
  endDate: string
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的列表行（列表已含全部字段，无需再请求详情） */
  record?: Attendance
  /** 员工下拉（仅在职，由列表页传入） */
  employeeOptions: Employee[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): AttendanceFormState {
  return {
    employeeId: undefined,
    type: 0,
    startDate: '',
    endDate: '',
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive<AttendanceFormState>(emptyForm())

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增考勤登记' : '编辑考勤登记'))

const typeOptions = [
  { label: '请假', value: 0 },
  { label: '加班', value: 1 },
]

/** 员工下拉选项（姓名 + 工号） */
const employeeSelectOptions = computed(() =>
  props.employeeOptions.map((e) => ({ label: `${e.name}（${e.employeeNo}）`, value: e.id })),
)

/** 校验规则：长度 / 日期区间与后端 AttendanceFieldConstraints 对齐 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  employeeId: [{ required: true, message: '请选择员工' }],
  type: [{ required: true, message: '请选择类型' }],
  startDate: [{ required: true, message: '请选择起始日' }],
  endDate: [
    { required: true, message: '请选择结束日' },
    {
      validator: (value: unknown, callback: (error?: string) => void) => {
        const endDate = typeof value === 'string' ? value : ''
        if (endDate && form.startDate && endDate < form.startDate) {
          callback('结束日不能早于起始日')
          return
        }
        callback()
      },
    },
  ],
  remark: [{ max: 200, message: '事由长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时重置（防数据串台），编辑态以列表行回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    if (props.mode === 'edit' && props.record) {
      form.employeeId = props.record.employeeId
      form.type = props.record.type
      form.startDate = props.record.startDate
      form.endDate = props.record.endDate
      form.remark = props.record.remark ?? ''
    }
  },
)

// —— methods ——
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
      employeeId: form.employeeId as string,
      type: form.type,
      startDate: form.startDate,
      endDate: form.endDate,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateAttendance(props.record?.id as string, payload)
      Message.success('考勤记录已更新')
    } else {
      await createAttendance(payload)
      Message.success('考勤记录已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（员工离职 / 区间重叠均返回 40000）
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
    <a-form
      ref="formRef"
      :model="form"
      :rules="rules"
      layout="vertical"
    >
      <a-form-item
        label="员工"
        field="employeeId"
        extra="仅可选择在职员工；离职员工不可登记考勤"
      >
        <a-select
          v-model="form.employeeId"
          :options="employeeSelectOptions"
          placeholder="请选择员工"
          allow-search
        />
      </a-form-item>

      <a-form-item
        label="类型"
        field="type"
      >
        <a-radio-group
          v-model="form.type"
          :options="typeOptions"
        />
      </a-form-item>

      <a-form-item
        label="起始日"
        field="startDate"
      >
        <a-date-picker
          v-model="form.startDate"
          value-format="YYYY-MM-DD"
          placeholder="请选择起始日"
          class="drawer-date"
        />
      </a-form-item>

      <a-form-item
        label="结束日"
        field="endDate"
        extra="同一员工同一类型的日期区间不可重叠"
      >
        <a-date-picker
          v-model="form.endDate"
          value-format="YYYY-MM-DD"
          placeholder="请选择结束日"
          class="drawer-date"
        />
      </a-form-item>

      <a-form-item
        label="事由"
        field="remark"
      >
        <a-textarea
          v-model="form.remark"
          placeholder="选填，≤ 200 字符"
          :max-length="200"
          show-word-limit
          :auto-size="{ minRows: 2, maxRows: 4 }"
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

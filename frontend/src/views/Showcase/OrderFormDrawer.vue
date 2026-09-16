<script setup lang="ts">
import { reactive, ref, watch } from 'vue'

import { ORDER_STATUS_OPTIONS, useOrderData, type OrderRow } from '@/composables/useOrderStore'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

/** 抽屉表单数据（提交后映射为 OrderRow） */
interface OrderFormState {
  orderNo: string
  customer: string
  product: string
  amount: number | undefined
  status: OrderRow['status']
  createdAt: string
  remark: string
}

const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的行 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

const { findById, upsert } = useOrderData()

// —— constants ——
const rules: Record<string, FieldRule[]> = {
  customer: [{ required: true, message: '请输入客户' }],
  product: [{ required: true, message: '请输入商品' }],
  amount: [
    { required: true, message: '请输入金额' },
    { positive: true, message: '金额必须大于 0' },
  ],
  status: [{ required: true, message: '请选择状态' }],
  createdAt: [{ required: true, message: '请选择创建时间' }],
}

// —— helpers ——
function emptyForm(): OrderFormState {
  return {
    orderNo: '',
    customer: '',
    product: '',
    amount: undefined,
    status: 'pending',
    createdAt: '',
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive<OrderFormState>(emptyForm())

// —— watch ——
/** 打开抽屉时按模式初始化：新增清空、编辑深拷贝回填（不污染数据源） */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    if (props.mode === 'edit' && props.editId) {
      const row = findById(props.editId)
      if (row) {
        form.orderNo = row.orderNo
        form.customer = row.customer
        form.product = row.product
        form.amount = row.amount
        form.status = row.status
        form.createdAt = row.createdAt
        form.remark = row.remark
        return
      }
    }
    Object.assign(form, emptyForm())
  },
)

// —— methods ——
/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交：手动校验，失败保持打开；成功写入数据源、提示并关闭 */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result) return
    const editing = props.mode === 'edit' ? findById(props.editId as string) : undefined
    upsert({
      id: props.mode === 'edit' ? (props.editId as string) : '',
      orderNo: form.orderNo,
      customer: form.customer.trim(),
      product: form.product.trim(),
      amount: form.amount as number,
      status: form.status,
      createdAt: form.createdAt,
      remark: form.remark,
      createdBy: editing?.createdBy ?? '',
      updatedAt: '',
      items: editing ? editing.items.map((i) => ({ ...i })) : [{ key: 'i1', productName: form.product.trim(), quantity: 1 }],
    })
    Message.success(props.mode === 'edit' ? '订单已更新' : '订单已创建')
    emit('saved')
    emit('update:visible', false)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="mode === 'edit' ? '编辑订单' : '新增订单'"
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
      class="drawer-form"
    >
      <a-form-item
        label="订单号"
        field="orderNo"
      >
        <a-input
          v-model="form.orderNo"
          placeholder="保存时自动生成"
          :readonly="mode === 'create'"
        />
      </a-form-item>
      <a-form-item
        label="客户"
        field="customer"
      >
        <a-input
          v-model="form.customer"
          placeholder="请输入客户"
          allow-clear
        />
      </a-form-item>
      <a-form-item
        label="商品"
        field="product"
      >
        <a-input
          v-model="form.product"
          placeholder="请输入商品"
          allow-clear
        />
      </a-form-item>
      <a-form-item
        label="金额"
        field="amount"
      >
        <a-input-number
          v-model="form.amount"
          :min="0"
          :precision="2"
          prefix="¥"
          style="width: 100%"
          placeholder="请输入金额"
        />
      </a-form-item>
      <a-form-item
        label="状态"
        field="status"
      >
        <a-select
          v-model="form.status"
          :options="ORDER_STATUS_OPTIONS"
          placeholder="请选择状态"
        />
      </a-form-item>
      <a-form-item
        label="创建时间"
        field="createdAt"
      >
        <a-date-picker
          v-model="form.createdAt"
          style="width: 100%"
          value-format="YYYY-MM-DD"
          placeholder="请选择日期"
        />
      </a-form-item>
      <a-form-item
        label="备注"
        field="remark"
      >
        <a-textarea
          v-model="form.remark"
          placeholder="选填"
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
.drawer-form {
  display: flex;
  flex-direction: column;
  min-height: 100%;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

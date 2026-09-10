<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus } from '@arco-design/web-vue/es/icon'
import {
  ORDER_STATUS_OPTIONS,
  useOrderData,
  type OrderFormLike,
} from '@/composables/useOrderStore'

const route = useRoute()
const router = useRouter()
const { findById, upsert } = useOrderData()

/** 编辑模式：路由为 formEdit 且带 id */
const editId = computed<string | null>(() =>
  route.name === 'formEdit' ? (String(route.params.id ?? '') || null) : null,
)
const isEdit = computed(() => Boolean(editId.value))
const pageTitle = computed(() => (isEdit.value ? '编辑订单' : '新增订单'))

let itemSeq = 0
function newKey(): string {
  itemSeq += 1
  return `i-${Date.now()}-${itemSeq}`
}

interface FormState extends OrderFormLike {
  items: { key: string; productName: string; quantity: number }[]
}

function emptyForm(): FormState {
  return {
    orderNo: '',
    customer: '',
    product: '',
    amount: undefined,
    status: 'pending',
    createdAt: '',
    remark: '',
    items: [{ key: newKey(), productName: '', quantity: 1 }],
  }
}

const form = reactive<FormState>(emptyForm())

/** 编辑模式：挂载后从数据源深拷贝回填 */
const editingRow = editId.value ? findById(editId.value) : undefined
if (editingRow) {
  form.orderNo = editingRow.orderNo
  form.customer = editingRow.customer
  form.product = editingRow.product
  form.amount = editingRow.amount
  form.status = editingRow.status
  form.createdAt = editingRow.createdAt
  form.remark = editingRow.remark
  form.items = editingRow.items.map((i) => ({ key: newKey(), productName: i.productName, quantity: i.quantity }))
} else {
  Object.assign(form, emptyForm())
}

const formRef = ref<FormInstance>()
const submitting = ref(false)

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

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除（computed 派生） */
const itemsInvalid = computed(
  () => form.items.length === 0 || form.items.some((i) => i.productName.trim() === '' || i.quantity < 1),
)
const itemsErrorShown = ref(false)
function validateItems(): boolean {
  const ok = !itemsInvalid.value
  itemsErrorShown.value = true
  return ok
}

/** 商品明细子表格列（Arco：columns 数组 + slotName 插槽） */
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品名称', slotName: 'productName' },
  { title: '数量', slotName: 'quantity', width: 160 },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

function addItem(): void {
  form.items.push({ key: newKey(), productName: '', quantity: 1 })
}

function removeItem(key: string): void {
  form.items = form.items.filter((i) => i.key !== key)
}

function goBack(): void {
  void router.push({ name: 'form' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const itemsOk = validateItems()
    const result = await formRef.value?.validate()
    if (!itemsOk || result !== undefined) {
      Message.error('请检查表单填写')
      return
    }
    const saved = upsert({
      id: editId.value ?? '',
      orderNo: form.orderNo,
      customer: form.customer.trim(),
      product: form.product.trim(),
      amount: form.amount as number,
      status: form.status,
      createdAt: form.createdAt,
      remark: form.remark,
      createdBy: editingRow?.createdBy ?? '',
      updatedAt: '',
      items: form.items.map((i) => ({ key: i.key, productName: i.productName.trim(), quantity: i.quantity })),
    })
    Message.success(isEdit.value ? '订单已更新' : '订单已创建')
    void router.push({ name: 'formDetail', params: { id: saved.id } })
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      class="form-header"
      :title="pageTitle"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item
              label="订单号"
              field="orderNo"
            >
              <a-input
                v-model="form.orderNo"
                placeholder="保存时自动生成"
                :readonly="!isEdit"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
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
          </a-col>
          <a-col :span="12">
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
          </a-col>
          <a-col :span="12">
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
          </a-col>
          <a-col :span="12">
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
          </a-col>
          <a-col :span="12">
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
          </a-col>
        </a-row>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <div class="items-toolbar">
          <a-button
            size="small"
            @click="addItem"
          >
            <template #icon>
              <IconPlus />
            </template>
            添加行
          </a-button>
        </div>
        <a-table
          row-key="key"
          size="small"
          :columns="itemColumns"
          :data="form.items"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #productName="{ record }">
            <a-input
              v-model="(record as FormState['items'][number]).productName"
              placeholder="请输入明细商品名称"
              allow-clear
            />
          </template>
          <template #quantity="{ record }">
            <a-input-number
              v-model="(record as FormState['items'][number]).quantity"
              :min="1"
              style="width: 100%"
            />
          </template>
          <template #itemAction="{ record }">
            <a-button
              type="text"
              status="danger"
              size="small"
              @click="removeItem((record as FormState['items'][number]).key)"
            >
              删除
            </a-button>
          </template>
        </a-table>
        <a-alert
          v-if="itemsErrorShown && itemsInvalid"
          type="error"
          class="items-error"
        >
          请至少添加一条商品明细并填写商品名称
        </a-alert>

        <a-divider orientation="left">
          其他
        </a-divider>
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
            <a-button @click="goBack">
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
    </a-card>
  </div>
</template>

<style scoped>
.form-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.form-header {
  background: var(--color-bg-2);
  border-radius: var(--border-radius-medium);
  padding: 12px 20px;
}

.items-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

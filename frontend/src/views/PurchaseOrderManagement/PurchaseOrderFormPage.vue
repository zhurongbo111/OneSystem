<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { toUtcMidnight } from '@/api/purchase'
import { createPurchaseOrder, getPurchaseOrder, updatePurchaseOrder } from '@/api/purchaseOrder'
import type { PurchaseOrderFormLine } from '@/api/purchaseOrder'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus } from '@tabler/icons-vue'

// —— constants ——
/** 明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100
/** 明细数量边界（ProductFieldConstraints.QuantityMinValue / MaxValue） */
const QUANTITY_MIN = 1
const QUANTITY_MAX = 999999

const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', slotName: 'product' },
  { title: '数量', slotName: 'quantity', width: 150 },
  { title: '单价', slotName: 'unitPrice', width: 170 },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

// —— helpers ——
let itemSeq = 0
function newKey(): string {
  itemSeq += 1
  return `i-${Date.now()}-${itemSeq}`
}

function newLine(): PurchaseOrderFormLine {
  return { key: newKey(), productId: undefined, productName: '', unit: '', quantity: 1, unitPrice: 0, subtotal: 0 }
}

/** 当天本地日期 YYYY-MM-DD（订单日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 新建 / 编辑共用本页（design §4.3） */
const isEdit = computed(() => route.name === 'purchaseOrderEdit')
const orderId = computed(() => (isEdit.value ? (route.params.id as string) : undefined))

const formRef = ref<FormInstance>()
const submitting = ref(false)
const loading = ref(false)
const itemsErrorShown = ref(false)

/** 表头 */
const partnerId = ref<string | undefined>(undefined)
const orderDate = ref(todayLocal())
const expectedDate = ref<string | undefined>(undefined)
const remark = ref('')

/** 供应商 / 商品下拉数据源（远程全量拉取，仅启用） */
const partners = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 明细行（subtotal 为前端实时计算，仅展示） */
const lines = ref<PurchaseOrderFormLine[]>([newLine()])

const rules = {
  partnerId: [{ required: true, message: '请选择供应商' }],
  orderDate: [{ required: true, message: '请选择订单日期' }],
}

// —— computed ——
/** 供应商下拉：仅启用 + 供应商 / 两者 */
const supplierOptions = computed(() =>
  partners.value.filter((p) => p.type === 1 || p.type === 3).map((p) => ({ label: p.name, value: p.id })),
)

/** 商品下拉：显示「编码 名称（库存 x）」 */
const productOptions = computed(() =>
  products.value.map((p) => ({
    label: `${p.code} ${p.name}（库存 ${p.stockQuantity}）`,
    value: p.id,
  })),
)

/** 总金额 = Σ 小计（仅展示，后端落库时重算） */
const totalAmount = computed(() => lines.value.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0))

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除 */
const itemsInvalid = computed(
  () =>
    lines.value.length === 0 ||
    lines.value.length > MAX_ITEMS ||
    lines.value.some((l) => !l.productId || l.quantity < QUANTITY_MIN || l.quantity > QUANTITY_MAX),
)

// —— lifecycle ——
onMounted(async () => {
  try {
    const [supplier, both, pick] = await Promise.all([
      getPartners({ type: 1, status: 1, page: 1, pageSize: 100 }),
      getPartners({ type: 3, status: 1, page: 1, pageSize: 100 }),
      getProductPickList(),
    ])
    const seen = new Set<string>()
    partners.value = [...supplier.items, ...both.items].filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)))
    products.value = pick
  } catch {
    // 错误提示已由请求层统一处理
  }

  if (isEdit.value && orderId.value) {
    await loadOrder(orderId.value)
  }
})

// —— methods ——
/** 编辑回填：字段逐个赋值 + 明细深拷贝（不污染接口返回对象） */
async function loadOrder(id: string): Promise<void> {
  loading.value = true
  try {
    const detail = await getPurchaseOrder(id)
    partnerId.value = detail.partnerId
    orderDate.value = detail.orderDate.slice(0, 10)
    expectedDate.value = detail.expectedDate ? detail.expectedDate.slice(0, 10) : undefined
    remark.value = detail.remark ?? ''
    lines.value = detail.items.map((item) => ({
      key: newKey(),
      productId: item.productId,
      productName: item.productName,
      unit: item.unit,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      subtotal: item.subtotal,
    }))
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

function onLineProductChange(line: PurchaseOrderFormLine, value?: string): void {
  line.productId = value
  const p = products.value.find((it) => it.id === value)
  line.productName = p?.name ?? ''
  line.unit = p?.unit ?? ''
  line.unitPrice = p?.purchasePrice ?? 0
}

function onLineQuantityChange(line: PurchaseOrderFormLine, value: number | undefined): void {
  line.quantity = value ?? 0
}

function onLineUnitPriceChange(line: PurchaseOrderFormLine, value: number | undefined): void {
  line.unitPrice = value ?? 0
}

function addItem(): void {
  if (lines.value.length >= MAX_ITEMS) {
    Message.warning(`最多添加 ${MAX_ITEMS} 行明细`)
    return
  }
  lines.value.push(newLine())
}

function removeItem(key: string): void {
  lines.value = lines.value.filter((l) => l.key !== key)
}

function validateItems(): boolean {
  const ok = !itemsInvalid.value
  itemsErrorShown.value = true
  return ok
}

function goBack(): void {
  void router.push({ name: 'purchaseOrders' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result !== undefined) return
    if (!validateItems()) {
      Message.error('请检查明细：至少一行，且每行需选择商品并填写有效数量')
      return
    }
    if (expectedDate.value && expectedDate.value < orderDate.value) {
      Message.error('预计到货日期不能早于订单日期')
      return
    }

    const payload = {
      partnerId: partnerId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（避免后端按服务器本地时区解析）
      orderDate: toUtcMidnight(orderDate.value),
      expectedDate: expectedDate.value ? toUtcMidnight(expectedDate.value) : undefined,
      items: lines.value.map((l) => ({
        productId: l.productId as string,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
      })),
      remark: remark.value.trim() || undefined,
    }

    const saved =
      isEdit.value && orderId.value
        ? await updatePurchaseOrder(orderId.value, payload)
        : await createPurchaseOrder(payload)
    Message.success(isEdit.value ? '采购订单已保存' : '采购订单已创建')
    void router.push({ name: 'purchaseOrderDetail', params: { id: saved.id } })
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      :title="isEdit ? '编辑采购订单' : '新建采购订单'"
      @back="goBack"
    />

    <a-card
      :bordered="false"
      :loading="loading"
    >
      <a-form
        ref="formRef"
        :model="{ partnerId, orderDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="8">
            <a-form-item
              label="供应商"
              field="partnerId"
            >
              <a-select
                v-model="partnerId"
                :options="supplierOptions"
                placeholder="请选择供应商（仅供应商 / 两者类型）"
                allow-search
                allow-clear
                :loading="partners.length === 0"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item
              label="订单日期"
              field="orderDate"
            >
              <a-date-picker
                v-model="orderDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择订单日期"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="预计到货">
              <a-date-picker
                v-model="expectedDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="选填"
                allow-clear
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
          :data="lines"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #product="{ record }">
            <a-select
              :value="(record as PurchaseOrderFormLine).productId"
              :options="productOptions"
              placeholder="请选择商品"
              allow-search
              allow-clear
              @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as PurchaseOrderFormLine, v as string | undefined)"
            />
          </template>
          <template #quantity="{ record }">
            <a-input-number
              :model-value="(record as PurchaseOrderFormLine).quantity"
              :min="QUANTITY_MIN"
              :max="QUANTITY_MAX"
              style="width: 100%"
              @change="(v: number | undefined) => onLineQuantityChange(record as PurchaseOrderFormLine, v)"
            />
          </template>
          <template #unitPrice="{ record }">
            <a-input-number
              :model-value="(record as PurchaseOrderFormLine).unitPrice"
              :min="0"
              :precision="2"
              prefix="¥"
              style="width: 100%"
              @change="(v: number | undefined) => onLineUnitPriceChange(record as PurchaseOrderFormLine, v)"
            />
          </template>
          <template #subtotal="{ record }">
            <span class="line-subtotal">
              {{ ((record as PurchaseOrderFormLine).quantity * (record as PurchaseOrderFormLine).unitPrice).toFixed(2) }}
            </span>
          </template>
          <template #itemAction="{ record }">
            <a-button
              type="text"
              status="danger"
              size="small"
              @click="removeItem((record as PurchaseOrderFormLine).key)"
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
          请至少添加一行明细，且每行需选择商品、填写有效数量
        </a-alert>

        <a-divider orientation="left">
          其他
        </a-divider>
        <a-form-item label="备注">
          <a-textarea
            v-model="remark"
            placeholder="选填（不超过 200 字）"
            :max-length="200"
            :auto-size="{ minRows: 2, maxRows: 4 }"
          />
        </a-form-item>

        <div class="form-footer">
          <span class="form-footer__total">
            总金额：<span class="form-footer__total-amount">{{ totalAmount.toFixed(2) }}</span>
          </span>
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

.items-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.items-error {
  margin-top: 8px;
}

.line-subtotal {
  font-variant-numeric: tabular-nums;
}

.form-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}

.form-footer__total {
  font-size: 14px;
  color: var(--color-text-2);
}

.form-footer__total-amount {
  font-size: 18px;
  font-weight: 600;
  color: rgb(var(--red-6));
  font-variant-numeric: tabular-nums;
}
</style>

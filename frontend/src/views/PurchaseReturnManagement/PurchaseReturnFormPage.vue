<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { createPurchaseReturn, toUtcMidnight } from '@/api/purchaseReturn'
import type { PurchaseReturnFormLine } from '@/api/purchaseReturn'
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
  { title: '数量', slotName: 'quantity', width: 170 },
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

function newLine(): PurchaseReturnFormLine {
  return { key: newKey(), productId: undefined, productName: '', unit: '', stockQuantity: 0, quantity: 1, unitPrice: 0, subtotal: 0 }
}

/** 当天本地日期 YYYY-MM-DD（退货日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const router = useRouter()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const itemsErrorShown = ref(false)

/** 表头 */
const partnerId = ref<string | undefined>(undefined)
const returnDate = ref(todayLocal())
const remark = ref('')

/** 供应商 / 商品下拉数据源（远程全量拉取，仅启用） */
const partners = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 明细行（subtotal 为前端实时计算，仅展示；提交不含小计 / 总额） */
const lines = ref<PurchaseReturnFormLine[]>([newLine()])

const rules = {
  partnerId: [{ required: true, message: '请选择供应商' }],
  returnDate: [{ required: true, message: '请选择退货日期' }],
}

// —— computed ——
/** 供应商下拉：仅启用 + 供应商 / 两者（design §4.4） */
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

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除（computed 派生） */
const itemsInvalid = computed(
  () =>
    lines.value.length === 0 ||
    lines.value.length > MAX_ITEMS ||
    lines.value.some((l) => !l.productId || l.quantity < QUANTITY_MIN || l.quantity > QUANTITY_MAX),
)

/** 任一行「退货数量 > 当前库存」：前端预警（最终以后端 40103 为准），提交前拦截 */
const hasOverStock = computed(() => lines.value.some((l) => isOverStock(l)))

// —— lifecycle ——
onMounted(async () => {
  try {
    // 供应商查询仅支持单值 type，分两次拉取后前端取并集
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
})

// —— methods ——
/** 退货数量是否超出该商品当前库存（design §4.4 行内预警） */
function isOverStock(line: PurchaseReturnFormLine): boolean {
  return line.productId !== undefined && line.quantity > line.stockQuantity
}

function onLineProductChange(line: PurchaseReturnFormLine, value?: string): void {
  line.productId = value
  const p = products.value.find((it) => it.id === value)
  line.productName = p?.name ?? ''
  line.unit = p?.unit ?? ''
  line.stockQuantity = p?.stockQuantity ?? 0
  line.unitPrice = p?.purchasePrice ?? 0
}

function onLineQuantityChange(line: PurchaseReturnFormLine, value: number | undefined): void {
  line.quantity = value ?? 0
}

function onLineUnitPriceChange(line: PurchaseReturnFormLine, value: number | undefined): void {
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
  void router.push({ name: 'purchaseReturns' })
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
    if (hasOverStock.value) {
      Message.error('存在退货数量大于当前库存的明细行，请修正后提交')
      return
    }
    const saved = await createPurchaseReturn({
      partnerId: partnerId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（design §4.2；裸日期会被后端按服务器本地时区解析导致入库失败）
      returnDate: toUtcMidnight(returnDate.value),
      items: lines.value.map((l) => ({ productId: l.productId as string, quantity: l.quantity, unitPrice: l.unitPrice })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('采购退货单已创建')
    void router.push({ name: 'purchaseReturnDetail', params: { id: saved.id } })
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
      title="开退货单"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="{ partnerId, returnDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
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
          <a-col :span="12">
            <a-form-item
              label="退货日期"
              field="returnDate"
            >
              <a-date-picker
                v-model="returnDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择退货日期"
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
              :model-value="(record as PurchaseReturnFormLine).productId"
              :options="productOptions"
              placeholder="请选择商品"
              allow-search
              allow-clear
              @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as PurchaseReturnFormLine, v as string | undefined)"
            />
          </template>
          <template #quantity="{ record }">
            <a-input-number
              :model-value="(record as PurchaseReturnFormLine).quantity"
              :min="QUANTITY_MIN"
              :max="QUANTITY_MAX"
              :class="{ 'qty-over-stock': isOverStock(record as PurchaseReturnFormLine) }"
              style="width: 100%"
              @change="(v: number | undefined) => onLineQuantityChange(record as PurchaseReturnFormLine, v)"
            />
            <!-- 退货数量 > 当前库存：行内预警（最终以后端 40103 为准，design §4.4） -->
            <div
              v-if="isOverStock(record as PurchaseReturnFormLine)"
              class="qty-stock-error"
            >
              库存不足，当前库存 {{ (record as PurchaseReturnFormLine).stockQuantity }}
            </div>
          </template>
          <template #unitPrice="{ record }">
            <a-input-number
              :model-value="(record as PurchaseReturnFormLine).unitPrice"
              :min="0"
              :precision="2"
              prefix="¥"
              style="width: 100%"
              @change="(v: number | undefined) => onLineUnitPriceChange(record as PurchaseReturnFormLine, v)"
            />
          </template>
          <template #subtotal="{ record }">
            <span class="line-subtotal">
              {{ ((record as PurchaseReturnFormLine).quantity * (record as PurchaseReturnFormLine).unitPrice).toFixed(2) }}
            </span>
          </template>
          <template #itemAction="{ record }">
            <a-button
              type="text"
              status="danger"
              size="small"
              @click="removeItem((record as PurchaseReturnFormLine).key)"
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
            placeholder="选填（不超过 200 字，可写原采购单号 / 退货原因）"
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

/* 退货数量 > 当前库存：输入框标红 + 行内文案（design §4.4） */
.qty-over-stock :deep(.arco-input-inner-wrapper) {
  border-color: rgb(var(--red-6));
}

.qty-stock-error {
  margin-top: 4px;
  font-size: 12px;
  color: rgb(var(--red-6));
  line-height: 1.4;
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

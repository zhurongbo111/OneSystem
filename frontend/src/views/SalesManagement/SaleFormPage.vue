<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { createSalesShipment, getSalesOrderLines, getSalesOrderPicks, toUtcMidnight } from '@/api/sale'
import type { SalesFormLine, SalesOrderPick } from '@/api/sale'
import { getSalesOrder } from '@/api/saleOrder'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus } from '@tabler/icons-vue'

// —— constants ——
/** 明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100
/** 明细数量边界（ProductFieldConstraints.QuantityMinValue / MaxValue） */
const QUANTITY_MIN = 1
const QUANTITY_MAX = 999999

/** 未关联订单时的明细列（与既有一步式开单完全一致） */
const plainItemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', slotName: 'product' },
  { title: '数量', slotName: 'quantity', width: 150 },
  { title: '单价', slotName: 'unitPrice', width: 170 },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

/** 关联订单时的明细列：固定为订单明细，展示订购 / 已发 / 未发（design §4.3） */
const linkedItemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', slotName: 'product' },
  { title: '订购数量', slotName: 'orderedQuantity', width: 100, align: 'right' },
  { title: '已发', slotName: 'fulfilledQuantity', width: 90, align: 'right' },
  { title: '未发', slotName: 'remainingQuantity', width: 90, align: 'right' },
  { title: '本次数量', slotName: 'quantity', width: 150 },
  { title: '单价', slotName: 'unitPrice', width: 140, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

// —— helpers ——
let itemSeq = 0
function newKey(): string {
  itemSeq += 1
  return `i-${Date.now()}-${itemSeq}`
}

function newLine(): SalesFormLine {
  return { key: newKey(), productId: undefined, productName: '', unit: '', quantity: 1, unitPrice: 0, subtotal: 0 }
}

/** 当天本地日期 YYYY-MM-DD（单据日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const itemsErrorShown = ref(false)

/** 表头 */
const partnerId = ref<string | undefined>(undefined)
const orderDate = ref(todayLocal())
const remark = ref('')

/** 关联销售订单（可选；选择后明细固定为订单明细，design §4.3） */
const orderId = ref<string | undefined>(undefined)
const orderPicks = ref<SalesOrderPick[]>([])
const orderLinesLoading = ref(false)

/** 客户 / 商品下拉数据源（远程全量拉取，仅启用） */
const partners = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 明细行（subtotal 为前端实时计算，仅展示；提交不含小计 / 总额） */
const lines = ref<SalesFormLine[]>([newLine()])

const rules = {
  partnerId: [{ required: true, message: '请选择客户' }],
  orderDate: [{ required: true, message: '请选择单据日期' }],
}

// —— computed ——
/** 客户下拉：仅启用 + 客户 / 两者 */
const customerOptions = computed(() =>
  partners.value.filter((p) => p.type === 2 || p.type === 3).map((p) => ({ label: p.name, value: p.id })),
)

/** 商品下拉：显示「编码 名称（库存 x）」 */
const productOptions = computed(() =>
  products.value.map((p) => ({
    label: `${p.code} ${p.name}（库存 ${p.stockQuantity}）`,
    value: p.id,
  })),
)

/** 是否已关联订单（关联模式下：明细固定为订单明细、单价只读、数量上限为未发数量） */
const isLinked = computed(() => !!orderId.value)

/** 关联订单下拉选项：单号 + 下单日期 */
const orderPickOptions = computed(() =>
  orderPicks.value.map((o) => ({
    label: `${o.orderNo}（${o.orderDate.slice(0, 10)}）`,
    value: o.id,
  })),
)

/** 明细列随关联状态切换 */
const itemColumns = computed<TableColumnData[]>(() => (isLinked.value ? linkedItemColumns : plainItemColumns))

/** 总金额 = Σ 小计（仅展示，后端落库时重算） */
const totalAmount = computed(() => lines.value.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0))

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除（computed 派生） */
const itemsInvalid = computed(
  () =>
    lines.value.length === 0 ||
    lines.value.length > MAX_ITEMS ||
    lines.value.some(
      (l) =>
        !l.productId ||
        l.quantity < QUANTITY_MIN ||
        l.quantity > QUANTITY_MAX ||
        (isLinked.value && l.remainingQuantity !== undefined && l.quantity > l.remainingQuantity),
    ),
)

// —— lifecycle ——
onMounted(async () => {
  try {
    // 客户查询仅支持单值 type，分两次拉取后前端取并集
    const [customer, both, pick] = await Promise.all([
      getPartners({ type: 2, status: 1, page: 1, pageSize: 100 }),
      getPartners({ type: 3, status: 1, page: 1, pageSize: 100 }),
      getProductPickList(),
    ])
    const seen = new Set<string>()
    partners.value = [...customer.items, ...both.items].filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)))
    products.value = pick
  } catch {
    // 错误提示已由请求层统一处理
  }

  // 订单详情「去出库」跳转时预置关联订单（design §4.3）
  const presetOrderId = route.query.orderId as string | undefined
  if (presetOrderId) {
    await applyPresetOrder(presetOrderId)
  }
})

// —— methods ——
/** 客户变化：清空已关联订单并重置明细，再按新客户拉候选订单 */
async function onPartnerChange(value?: string): Promise<void> {
  partnerId.value = value
  orderId.value = undefined
  lines.value = [newLine()]
  orderPicks.value = []
  if (!value) return
  await loadOrderPicks(value)
}

async function loadOrderPicks(value: string): Promise<void> {
  try {
    orderPicks.value = await getSalesOrderPicks(value)
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 订单详情「去出库」预置：先取订单头拿客户，再按客户拉候选并按订单带出明细 */
async function applyPresetOrder(value: string): Promise<void> {
  // 进入加载即清空占位行：避免「去出库」跳转后异步带出明细前，测试 fill 命中占位行被后续覆盖
  lines.value = []
  try {
    const order = await getSalesOrder(value)
    partnerId.value = order.partnerId
    await loadOrderPicks(order.partnerId)
    await onOrderChange(value)
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 关联订单变化：选择订单后带出明细；清空则恢复自由开单 */
async function onOrderChange(value?: string): Promise<void> {
  orderId.value = value
  if (!value) {
    lines.value = [newLine()]
    return
  }

  // 加载期间清空占位行，避免旧行残留与并发 fill 竞争
  lines.value = []
  orderLinesLoading.value = true
  try {
    const detail = await getSalesOrderLines(value)
    // 明细固定为订单明细：每行对应一个订单行，默认本次数量取未发数量（可改小）
    lines.value = detail.items.map((item) => ({
      key: newKey(),
      productId: item.productId,
      productName: item.productName,
      unit: item.unit,
      quantity: item.remainingQuantity,
      unitPrice: item.unitPrice,
      subtotal: item.remainingQuantity * item.unitPrice,
      orderItemId: item.orderItemId,
      remainingQuantity: item.remainingQuantity,
      orderedQuantity: item.quantity,
      fulfilledQuantity: item.fulfilledQuantity,
    }))
    itemsErrorShown.value = false
  } catch {
    orderId.value = undefined
    lines.value = [newLine()]
  } finally {
    orderLinesLoading.value = false
  }
}

function onLineProductChange(line: SalesFormLine, value?: string): void {
  line.productId = value
  const p = products.value.find((it) => it.id === value)
  line.productName = p?.name ?? ''
  line.unit = p?.unit ?? ''
  // 单价默认带出商品销售价（开单时可改）
  line.unitPrice = p?.salePrice ?? 0
}

function onLineQuantityChange(line: SalesFormLine, value: number | undefined): void {
  line.quantity = value ?? 0
}

function onLineUnitPriceChange(line: SalesFormLine, value: number | undefined): void {
  line.unitPrice = value ?? 0
}

/** 行数量是否超库存（前端预警标红，仅提示不拦截，最终以后端 40103 为准；design §4.4） */
function isOverStock(line: SalesFormLine): boolean {
  if (!line.productId) return false
  const p = products.value.find((it) => it.id === line.productId)
  return p !== undefined && line.quantity > p.stockQuantity
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
  void router.push({ name: 'sales' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result !== undefined) return
    if (!validateItems()) {
      Message.error(
        isLinked.value
          ? '请检查明细：每行本次数量不得超过未发数量'
          : '请检查明细：至少一行，且每行需选择商品并填写有效数量',
      )
      return
    }
    const saved = await createSalesShipment({
      partnerId: partnerId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（同采购单约定；裸日期会被后端按服务器本地时区解析导致入库失败）
      orderDate: toUtcMidnight(orderDate.value),
      orderId: orderId.value,
      items: lines.value.map((l) => ({
        productId: l.productId as string,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        orderItemId: l.orderItemId,
      })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('销售单已创建')
    void router.push({ name: 'salesDetail', params: { id: saved.id } })
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
      title="开销售出库单"
      @back="goBack"
    />

    <a-card :bordered="false">
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
          <a-col :span="12">
            <a-form-item
              label="客户"
              field="partnerId"
            >
              <a-select
                v-model="partnerId"
                :options="customerOptions"
                placeholder="请选择客户（仅客户 / 两者类型）"
                allow-search
                allow-clear
                :loading="partners.length === 0"
                @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onPartnerChange(v as string | undefined)"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="单据日期"
              field="orderDate"
            >
              <a-date-picker
                v-model="orderDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择单据日期"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item label="关联销售订单">
              <a-select
                :model-value="orderId"
                class="order-select"
                :options="orderPickOptions"
                placeholder="不关联（货到即入账）"
                allow-search
                allow-clear
                :disabled="!partnerId"
                :loading="orderLinesLoading"
                @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onOrderChange(v as string | undefined)"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <div class="items-toolbar">
          <a-button
            v-if="!isLinked"
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
          :loading="orderLinesLoading"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #product="{ record }">
            <a-select
              v-if="!isLinked"
              :value="(record as SalesFormLine).productId"
              :options="productOptions"
              placeholder="请选择商品"
              allow-search
              allow-clear
              @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as SalesFormLine, v as string | undefined)"
            />
            <span v-else>{{ (record as SalesFormLine).productName }}</span>
          </template>
          <template #orderedQuantity="{ record }">
            {{ (record as SalesFormLine).orderedQuantity ?? 0 }}
          </template>
          <template #fulfilledQuantity="{ record }">
            {{ (record as SalesFormLine).fulfilledQuantity ?? 0 }}
          </template>
          <template #remainingQuantity="{ record }">
            {{ (record as SalesFormLine).remainingQuantity ?? 0 }}
          </template>
          <template #quantity="{ record }">
            <a-input-number
              :model-value="(record as SalesFormLine).quantity"
              :min="QUANTITY_MIN"
              :max="(record as SalesFormLine).remainingQuantity ?? QUANTITY_MAX"
              :status="isOverStock(record as SalesFormLine) ? 'error' : undefined"
              style="width: 100%"
              @change="(v: number | undefined) => onLineQuantityChange(record as SalesFormLine, v)"
            />
          </template>
          <template #unitPrice="{ record }">
            <a-input-number
              v-if="!isLinked"
              :model-value="(record as SalesFormLine).unitPrice"
              :min="0"
              :precision="2"
              prefix="¥"
              style="width: 100%"
              @change="(v: number | undefined) => onLineUnitPriceChange(record as SalesFormLine, v)"
            />
            <span
              v-else
              class="line-unit-price"
            >¥ {{ (record as SalesFormLine).unitPrice.toFixed(2) }}</span>
          </template>
          <template #subtotal="{ record }">
            <div>
              <span class="line-subtotal">
                {{ ((record as SalesFormLine).quantity * (record as SalesFormLine).unitPrice).toFixed(2) }}
              </span>
              <div
                v-if="isOverStock(record as SalesFormLine)"
                class="stock-warning"
              >
                库存不足，当前库存 {{ products.find((it) => it.id === (record as SalesFormLine).productId)?.stockQuantity ?? 0 }}
              </div>
            </div>
          </template>
          <template #itemAction="{ record }">
            <a-button
              type="text"
              status="danger"
              size="small"
              @click="removeItem((record as SalesFormLine).key)"
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
          {{ isLinked ? '每行本次数量不得超过未发数量' : '请至少添加一行明细，且每行需选择商品、填写有效数量' }}
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

.order-select {
  width: 100%;
}

.items-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
  min-height: 24px;
}

.items-error {
  margin-top: 8px;
}

.line-subtotal,
.line-unit-price {
  font-variant-numeric: tabular-nums;
}

.stock-warning {
  margin-top: 2px;
  font-size: 12px;
  line-height: 1.4;
  color: rgb(var(--red-6));
  text-align: right;
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

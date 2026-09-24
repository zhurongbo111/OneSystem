<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { createTransfer, toUtcMidnight } from '@/api/transfer'
import { getWarehousePickList } from '@/api/warehouse'
import type { WarehousePickItem } from '@/api/warehouse'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus } from '@tabler/icons-vue'

// —— types ——
/** 开单明细行本地类型（无 unitPrice / subtotal：调拨无价格概念，design §0） */
interface TransferFormLine {
  key: string
  productId?: string
  productName: string
  unit: string
  /** 转出仓可用库存（取自 getProductPickList(fromWarehouseId) 的同口径快照，design §4.4） */
  stockQuantity: number
  quantity: number
}

// —— constants ——
/** 明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100
/** 明细数量边界（ProductFieldConstraints.QuantityMinValue / MaxValue） */
const QUANTITY_MIN = 1
const QUANTITY_MAX = 999999
/** 商品下拉请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新仓数据（038） */
let productFetchSeq = 0

const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', slotName: 'product' },
  { title: '单位', dataIndex: 'unit', width: 80, align: 'center' },
  { title: '可用库存', slotName: 'stock', width: 110, align: 'right' },
  { title: '数量', slotName: 'quantity', width: 170 },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

// —— helpers ——
let itemSeq = 0
function newKey(): string {
  itemSeq += 1
  return `i-${Date.now()}-${itemSeq}`
}

function newLine(): TransferFormLine {
  return { key: newKey(), productId: undefined, productName: '', unit: '', stockQuantity: 0, quantity: 1 }
}

/** 当天本地日期 YYYY-MM-DD（调拨日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const router = useRouter()

const formRef = ref<FormInstance>()
const submitting = ref(false)
/** 明细区可用库存加载（design §4.5：stockLoading，a-spin 包裹明细区；转出仓切换时重载商品下拉同口径库存） */
const stockLoading = ref(false)
const itemsErrorShown = ref(false)

/** 表头 */
const fromWarehouseId = ref<string | undefined>(undefined)
const toWarehouseId = ref<string | undefined>(undefined)
const transferDate = ref(todayLocal())
const remark = ref('')

/** 仓库 / 商品下拉数据源（远程全量拉取，仅启用） */
const warehouses = ref<WarehousePickItem[]>([])
const products = ref<ProductPickItem[]>([])

/** 明细行 */
const lines = ref<TransferFormLine[]>([newLine()])

const rules = {
  fromWarehouseId: [{ required: true, message: '请选择转出仓' }],
  toWarehouseId: [{ required: true, message: '请选择转入仓' }],
  transferDate: [{ required: true, message: '请选择调拨日期' }],
}

// —— computed ——
/** 转出仓下拉：仅启用仓 */
const fromWarehouseOptions = computed(() =>
  warehouses.value.map((w) => ({ label: w.name, value: w.id })),
)

/**
 * 转入仓下拉：禁用已选转出仓，避免同仓调拨（design §4.4：前端禁用 + 后端 40126 兜底）。
 * 已选的转入仓若与转出仓相同，会在 watch(fromWarehouseId) 中清空。
 */
const toWarehouseOptions = computed(() =>
  warehouses.value.map((w) => ({
    label: w.name,
    value: w.id,
    disabled: w.id === fromWarehouseId.value,
  })),
)

/** 商品下拉：显示「编码 名称（库存 x）」，x 为转出仓口径（design §4.4） */
const productOptions = computed(() =>
  products.value.map((p) => ({
    label: `${p.code} ${p.name}（库存 ${p.stockQuantity}）`,
    value: p.id,
  })),
)

/** 数量合计 = Σ 行数量（仅展示，后端落库时重算） */
const totalQuantity = computed(() => lines.value.reduce((sum, l) => sum + l.quantity, 0))

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除（computed 派生） */
const itemsInvalid = computed(
  () =>
    lines.value.length === 0 ||
    lines.value.length > MAX_ITEMS ||
    lines.value.some((l) => !l.productId || l.quantity < QUANTITY_MIN || l.quantity > QUANTITY_MAX),
)

/** 任一行「调拨数量 > 转出仓可用库存」：前端预警（最终以后端 40103 为准），提交前拦截 */
const hasOverStock = computed(() => lines.value.some((l) => isOverStock(l)))

// —— watch ——
/**
 * 转出仓变化：商品下拉「库存」与明细行的库存快照都换成所选仓口径（038）；
 * 同时清空已选的转入仓（若与转出仓相同），避免同仓调拨。
 */
watch(fromWarehouseId, (v) => {
  if (toWarehouseId.value === v) {
    toWarehouseId.value = undefined
  }
  void refreshProducts(v)
})

// —— lifecycle ——
onMounted(async () => {
  try {
    const warehousePicks = await getWarehousePickList()
    warehouses.value = warehousePicks
    // 默认仓预选（038）：转出仓与转入仓都默认预选默认仓（转入仓会因同仓禁用自动清空）
    const defaultId = warehousePicks.find((w) => w.isDefault)?.id
    fromWarehouseId.value = defaultId
    toWarehouseId.value = defaultId
    // 商品下拉「库存」为转出仓口径（与 watch 同源，序号守卫只采纳最后一次响应）
    await refreshProducts(fromWarehouseId.value)
  } catch {
    // 错误提示已由请求层统一处理
  }
})

// —— methods ——
/** 按所选转出仓刷新商品下拉，并把已选明细行的「可用库存」快照同步为所选仓口径（038） */
async function refreshProducts(warehouse?: string): Promise<void> {
  const seq = ++productFetchSeq
  stockLoading.value = true
  try {
    const picks = await getProductPickList(warehouse)
    // 序号守卫：预选默认仓与用户改仓会各发一次请求，慢的旧响应不得覆盖新仓数据
    if (seq !== productFetchSeq) return
    products.value = picks
    lines.value.forEach((line) => {
      if (!line.productId) return
      line.stockQuantity = picks.find((p) => p.id === line.productId)?.stockQuantity ?? 0
    })
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    if (seq === productFetchSeq) stockLoading.value = false
  }
}

/** 调拨数量是否超出转出仓可用库存（design §4.4 行内预警） */
function isOverStock(line: TransferFormLine): boolean {
  return line.productId !== undefined && line.quantity > line.stockQuantity
}

/** 选中商品：同步单位 / 转出仓口径库存快照（design §4.4） */
function onLineProductChange(line: TransferFormLine, value?: string): void {
  line.productId = value
  const p = products.value.find((it) => it.id === value)
  line.productName = p?.name ?? ''
  line.unit = p?.unit ?? ''
  line.stockQuantity = p?.stockQuantity ?? 0
}

function onLineQuantityChange(line: TransferFormLine, value: number | undefined): void {
  line.quantity = value ?? 0
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
  void router.push({ name: 'transfers' })
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
      Message.error('存在调拨数量大于转出仓可用库存的明细行，请修正后提交')
      return
    }
    const saved = await createTransfer({
      fromWarehouseId: fromWarehouseId.value as string,
      toWarehouseId: toWarehouseId.value as string,
      // 所选日期 → UTC 午夜 ISO 串（design §4.2；裸日期会被后端按服务器本地时区解析导致入库失败）
      transferDate: toUtcMidnight(transferDate.value),
      items: lines.value.map((l) => ({ productId: l.productId as string, quantity: l.quantity })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('调拨单已创建')
    void router.push({ name: 'transferDetail', params: { id: saved.id } })
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
      title="开调拨单"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="{ fromWarehouseId, toWarehouseId, transferDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="8">
            <!-- 转出仓（038）：仅启用仓，默认仓预选；单据保存即固化 -->
            <a-form-item
              label="转出仓"
              field="fromWarehouseId"
            >
              <a-select
                v-model="fromWarehouseId"
                :options="fromWarehouseOptions"
                placeholder="请选择转出仓"
                allow-search
                :loading="warehouses.length === 0"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <!-- 转入仓：默认仓预选；同仓禁用，避免同仓调拨（design §4.4） -->
            <a-form-item
              label="转入仓"
              field="toWarehouseId"
            >
              <a-select
                v-model="toWarehouseId"
                :options="toWarehouseOptions"
                placeholder="请选择转入仓"
                allow-search
                :loading="warehouses.length === 0"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item
              label="调拨日期"
              field="transferDate"
            >
              <a-date-picker
                v-model="transferDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择调拨日期"
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
        <a-spin :loading="stockLoading">
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
                :model-value="(record as TransferFormLine).productId"
                :options="productOptions"
                placeholder="请选择商品"
                allow-search
                allow-clear
                @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as TransferFormLine, v as string | undefined)"
              />
            </template>
            <template #stock="{ record }">
              {{ (record as TransferFormLine).stockQuantity }}
            </template>
            <template #quantity="{ record }">
              <a-input-number
                :model-value="(record as TransferFormLine).quantity"
                :min="QUANTITY_MIN"
                :max="QUANTITY_MAX"
                :class="{ 'qty-over-stock': isOverStock(record as TransferFormLine) }"
                style="width: 100%"
                @change="(v: number | undefined) => onLineQuantityChange(record as TransferFormLine, v)"
              />
              <!-- 调拨数量 > 转出仓可用库存：行内预警（最终以后端 40103 为准，design §4.4） -->
              <div
                v-if="isOverStock(record as TransferFormLine)"
                class="qty-stock-error"
              >
                库存不足，转出仓可用库存 {{ (record as TransferFormLine).stockQuantity }}
              </div>
            </template>
            <template #itemAction="{ record }">
              <a-button
                type="text"
                status="danger"
                size="small"
                @click="removeItem((record as TransferFormLine).key)"
              >
                删除
              </a-button>
            </template>
          </a-table>
        </a-spin>
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
            placeholder="选填（不超过 200 字，可写调拨原因）"
            :max-length="200"
            :auto-size="{ minRows: 2, maxRows: 4 }"
          />
        </a-form-item>

        <div class="form-footer">
          <span class="form-footer__total">
            数量合计：<span class="form-footer__total-amount">{{ totalQuantity }}</span>
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

/* 调拨数量 > 转出仓可用库存：输入框标红 + 行内文案（design §4.4） */
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
  color: var(--color-text-1);
  font-variant-numeric: tabular-nums;
}
</style>

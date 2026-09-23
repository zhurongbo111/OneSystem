<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { getEffectivePrices, PRICE_SOURCE_META } from '@/api/partnerPrice'
import type { PriceSource } from '@/api/partnerPrice'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { createQuotation, getQuotation, toUtcMidnight, updateQuotation } from '@/api/quotation'
import type { QuotationFormLine } from '@/api/quotation'
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
  { title: '单价', slotName: 'unitPrice', width: 200 },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

// —— helpers ——
let itemSeq = 0
function newKey(): string {
  itemSeq += 1
  return `i-${Date.now()}-${itemSeq}`
}

function newLine(): QuotationFormLine {
  return { key: newKey(), productId: undefined, productName: '', unit: '', quantity: 1, unitPrice: 0, subtotal: 0 }
}

/** 当天本地日期 YYYY-MM-DD（报价日期默认值） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 新建 / 编辑共用本页（design §4.3） */
const isEdit = computed(() => route.name === 'quotationEdit')
const quotationId = computed(() => (isEdit.value ? (route.params.id as string) : undefined))

const formRef = ref<FormInstance>()
const submitting = ref(false)
const loading = ref(false)
const itemsErrorShown = ref(false)

/** 表头 */
const partnerId = ref<string | undefined>(undefined)
const quotationDate = ref(todayLocal())
const validUntil = ref<string | undefined>(undefined)
const remark = ref('')

/** 客户 / 商品下拉数据源（远程全量拉取，仅启用） */
const partners = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 明细行（subtotal 为前端实时计算，仅展示） */
const lines = ref<QuotationFormLine[]>([newLine()])

/** 明细区批量取价中（036 §4.5：绑明细区 a-spin，非按钮） */
const pricesLoading = ref(false)

/** 各行单价来源（协议价 / 默认价）；手工改价后清除，表示议价（design.md §0.2） */
const priceSources = ref<Record<string, PriceSource>>({})

/** 手工改过单价的行（批量取价默认跳过，切客户时按「覆盖 + 提示」处理） */
const manualPriceKeys = ref<Record<string, boolean>>({})

const rules = {
  partnerId: [{ required: true, message: '请选择客户' }],
  quotationDate: [{ required: true, message: '请选择报价日期' }],
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

  if (isEdit.value && quotationId.value) {
    await loadQuotation(quotationId.value)
  }
})

// —— methods ——
/**
 * 批量取价：对已选商品一次请求取「协议价优先、未配置时取商品销售价」，回填单价并标注来源（036 §4.4 / 本规格 §0.2）。
 *
 * - 手工改过单价的行默认跳过（保留议价）；`overrideManual` 时覆盖并提示——切换客户场景按「覆盖 + Message.info」处理。
 */
async function refreshEffectivePrices(options?: { overrideManual?: boolean }): Promise<void> {
  if (!partnerId.value) return
  const targets = lines.value.filter(
    (l) => l.productId && (options?.overrideManual === true || !manualPriceKeys.value[l.key]),
  )
  const productIds = [...new Set(targets.map((l) => l.productId as string))]
  if (productIds.length === 0) return

  if (options?.overrideManual === true && Object.keys(manualPriceKeys.value).length > 0) {
    Message.info('已按新客户重新取价，手工填写的单价将被覆盖')
  }

  pricesLoading.value = true
  try {
    const prices = await getEffectivePrices({ partnerId: partnerId.value, productIds })
    const map = new Map(prices.map((p) => [p.productId, p]))
    lines.value.forEach((l) => {
      const hit = l.productId ? map.get(l.productId) : undefined
      if (!hit) return
      l.unitPrice = hit.unitPrice
      priceSources.value[l.key] = hit.source
      delete manualPriceKeys.value[l.key]
    })
  } catch {
    // 错误提示已由请求层统一处理（商品不存在 40400）
  } finally {
    pricesLoading.value = false
  }
}

/** 客户变化：按新客户重新取价（覆盖手工价并提示） */
async function onPartnerChange(value?: string): Promise<void> {
  partnerId.value = value
  if (!value) return
  await refreshEffectivePrices({ overrideManual: true })
}

/** 编辑回填：字段逐个赋值 + 明细深拷贝（不污染接口返回对象） */
async function loadQuotation(id: string): Promise<void> {
  loading.value = true
  try {
    const detail = await getQuotation(id)
    partnerId.value = detail.partnerId
    quotationDate.value = detail.quotationDate.slice(0, 10)
    validUntil.value = detail.validUntil ?? undefined
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

function onLineProductChange(line: QuotationFormLine, value?: string): void {
  line.productId = value
  const p = products.value.find((it) => it.id === value)
  line.productName = p?.name ?? ''
  line.unit = p?.unit ?? ''
  // 单价默认带出商品销售价（可改）；已选客户时按批量取价覆盖为协议价
  line.unitPrice = p?.salePrice ?? 0
  delete priceSources.value[line.key]
  delete manualPriceKeys.value[line.key]
  void refreshEffectivePrices()
}

function onLineQuantityChange(line: QuotationFormLine, value: number | undefined): void {
  line.quantity = value ?? 0
}

function onLineUnitPriceChange(line: QuotationFormLine, value: number | undefined): void {
  line.unitPrice = value ?? 0
  // 手工改价视为议价：来源标注消失，后续批量取价跳过该行
  manualPriceKeys.value[line.key] = true
  delete priceSources.value[line.key]
}

/** 行单价来源（协议价 / 默认价）；手工改价后为 undefined（议价，不标注） */
function priceSource(row: QuotationFormLine): PriceSource | undefined {
  return priceSources.value[row.key]
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
  void router.push({ name: 'quotations' })
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
    if (validUntil.value && validUntil.value < quotationDate.value) {
      Message.error('有效期不能早于报价日期')
      return
    }

    const payload = {
      partnerId: partnerId.value as string,
      quotationDate: toUtcMidnight(quotationDate.value),
      validUntil: validUntil.value || undefined,
      items: lines.value.map((l) => ({
        productId: l.productId as string,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
      })),
      remark: remark.value.trim() || undefined,
    }

    const saved =
      isEdit.value && quotationId.value
        ? await updateQuotation(quotationId.value, payload)
        : await createQuotation(payload)
    Message.success(isEdit.value ? '报价单已保存' : '报价单已创建')
    void router.push({ name: 'quotationDetail', params: { id: saved.id } })
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
      :title="isEdit ? '编辑报价单' : '新建报价单'"
      @back="goBack"
    />

    <a-card
      :bordered="false"
      :loading="loading"
    >
      <a-form
        ref="formRef"
        :model="{ partnerId, quotationDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="8">
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
          <a-col :span="8">
            <a-form-item
              label="报价日期"
              field="quotationDate"
            >
              <a-date-picker
                v-model="quotationDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择报价日期"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="有效期至">
              <a-date-picker
                v-model="validUntil"
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
        <!-- 批量取价期间明细区整体加载（036 §4.5：pricesLoading 绑明细区，非按钮） -->
        <a-spin
          :loading="pricesLoading"
          class="items-area"
        >
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
                :model-value="(record as QuotationFormLine).productId"
                :options="productOptions"
                placeholder="请选择商品"
                allow-search
                allow-clear
                @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as QuotationFormLine, v as string | undefined)"
              />
            </template>
            <template #quantity="{ record }">
              <a-input-number
                :model-value="(record as QuotationFormLine).quantity"
                :min="QUANTITY_MIN"
                :max="QUANTITY_MAX"
                style="width: 100%"
                @change="(v: number | undefined) => onLineQuantityChange(record as QuotationFormLine, v)"
              />
            </template>
            <template #unitPrice="{ record }">
              <div>
                <a-input-number
                  :model-value="(record as QuotationFormLine).unitPrice"
                  :min="0"
                  :precision="2"
                  prefix="¥"
                  style="width: 100%"
                  @change="(v: number | undefined) => onLineUnitPriceChange(record as QuotationFormLine, v)"
                />
                <!-- 来源标注：协议价 / 默认价；手工改价后消失（视为议价） -->
                <div
                  v-if="priceSource(record as QuotationFormLine) !== undefined"
                  class="price-source"
                >
                  <a-tag
                    size="small"
                    :color="PRICE_SOURCE_META[priceSource(record as QuotationFormLine) as PriceSource].color"
                  >
                    {{ PRICE_SOURCE_META[priceSource(record as QuotationFormLine) as PriceSource].label }}
                  </a-tag>
                </div>
              </div>
            </template>
            <template #subtotal="{ record }">
              <span class="line-subtotal">
                {{ ((record as QuotationFormLine).quantity * (record as QuotationFormLine).unitPrice).toFixed(2) }}
              </span>
            </template>
            <template #itemAction="{ record }">
              <a-button
                type="text"
                status="danger"
                size="small"
                @click="removeItem((record as QuotationFormLine).key)"
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

.price-source {
  margin-top: 4px;
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

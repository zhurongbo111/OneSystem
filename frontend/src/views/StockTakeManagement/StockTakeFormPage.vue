<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  createStockTake,
  getStockTakePickProducts,
  toUtcMidnight,
  type StockTakeProductPick,
  type StockTakeType,
} from '@/api/stockTake'
import { Message } from '@arco-design/web-vue'
import type { FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus } from '@tabler/icons-vue'

// —— constants ——
/** 明细行上限（OrderFieldConstraints.ItemsMaxCount） */
const MAX_ITEMS = 100
/** 实盘数量边界（StockTakeFieldConstraints.ActualQuantityMinValue / MaxValue） */
const ACTUAL_MIN = 0
const ACTUAL_MAX = 999999

/** 类型选项（design §4.4：默认「库存盘点」） */
const typeOptions: { label: string; value: StockTakeType }[] = [
  { label: '库存盘点', value: 1 },
  { label: '期初建账', value: 0 },
]

const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', slotName: 'product' },
  { title: '账面数量', slotName: 'book', width: 110, align: 'right' },
  { title: '实盘数量', slotName: 'actual', width: 160 },
  { title: '差异', slotName: 'diff', width: 90, align: 'right' },
  { title: '操作', slotName: 'itemAction', width: 90, align: 'center' },
]

// —— helpers ——
let lineSeq = 0
function newKey(): string {
  lineSeq += 1
  return `i-${Date.now()}-${lineSeq}`
}

/** 当天本地日期 YYYY-MM-DD（盘点日期默认值） */
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

/** 表头（design §4.4：类型默认「库存盘点」、日期默认当天） */
const takeType = ref<StockTakeType>(1)
const takeDate = ref(todayLocal())
const remark = ref('')

/** 商品下拉数据源（启用商品 + 当前库存 + 是否已发生库存变动） */
const products = ref<StockTakeProductPick[]>([])

/** 明细行（账面 / 差异为前端实时计算，仅展示；提交只传实盘数量，后端重算差异） */
interface StockTakeFormLine {
  key: string
  productId?: string
  actualQuantity: number | undefined
}

function newLine(): StockTakeFormLine {
  return { key: newKey(), productId: undefined, actualQuantity: undefined }
}

const lines = ref<StockTakeFormLine[]>([newLine()])

const rules = {
  takeDate: [{ required: true, message: '请选择盘点日期' }],
}

// —— computed ——
/**
 * 商品下拉：显示「编码 名称（账面 x）」；
 * 期初建账模式下 hasMovements 的商品禁用并标注「已建账」（design §4.4）。
 */
const productOptions = computed(() =>
  products.value.map((p) => ({
    label: takeType.value === 0 && p.hasMovements ? `${p.code} ${p.name}（已建账）` : `${p.code} ${p.name}（账面 ${p.stockQuantity}）`,
    value: p.id,
    disabled: takeType.value === 0 && p.hasMovements,
  })),
)

/** 行账面数量（按所选商品带出；提交时由后端在事务内重读，此处仅供录入参考） */
function rowBook(line: StockTakeFormLine): number {
  return products.value.find((p) => p.id === line.productId)?.stockQuantity ?? 0
}

/** 行实盘数量（未填按 0） */
function rowActual(line: StockTakeFormLine): number {
  return line.actualQuantity ?? 0
}

/** 行差异 = 实盘 − 账面（前端预览；提交以后端事务内重算为准） */
function rowDiff(line: StockTakeFormLine): number {
  return rowActual(line) - rowBook(line)
}

/** 差异着色（design §4.4：正绿负红，0 中性） */
function rowDiffClass(line: StockTakeFormLine): string {
  const d = rowDiff(line)
  if (d > 0) return 'diff-pos'
  if (d < 0) return 'diff-neg'
  return 'diff-zero'
}

/** 明细行校验：非表单字段，提交时手动校验，错误提示随输入自动清除（computed 派生） */
const itemsInvalid = computed(
  () =>
    lines.value.length === 0 ||
    lines.value.length > MAX_ITEMS ||
    lines.value.some((l) => !l.productId || l.actualQuantity === undefined || l.actualQuantity < ACTUAL_MIN || l.actualQuantity > ACTUAL_MAX),
)

// —— lifecycle ——
onMounted(async () => {
  try {
    products.value = await getStockTakePickProducts()
  } catch {
    // 错误提示已由请求层统一处理
  }
})

// —— methods ——
function onLineProductChange(line: StockTakeFormLine, value?: string): void {
  line.productId = value
  // 切换商品后实盘需按新账面重新录入
  line.actualQuantity = undefined
}

function onLineActualChange(line: StockTakeFormLine, value: number | undefined): void {
  line.actualQuantity = value
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
  void router.push({ name: 'stockTakes' })
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    const result = await formRef.value?.validate()
    if (result !== undefined) return
    if (!validateItems()) {
      Message.error('请检查明细：至少一行，且每行需选择商品并填写有效实盘数量')
      return
    }
    const saved = await createStockTake({
      type: takeType.value,
      // 所选日期 → UTC 午夜 ISO 串（design §4.2；裸日期会被后端按服务器本地时区解析导致入库失败）
      takeDate: toUtcMidnight(takeDate.value),
      items: lines.value.map((l) => ({ productId: l.productId as string, actualQuantity: l.actualQuantity as number })),
      remark: remark.value.trim() || undefined,
    })
    Message.success('盘点单已生效')
    void router.push({ name: 'stockTakeDetail', params: { id: saved.id } })
  } catch {
    // 错误提示已由请求层统一处理（如 40111 期初限制）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="form-page">
    <a-page-header
      title="新建盘点单"
      @back="goBack"
    />

    <a-card :bordered="false">
      <a-form
        ref="formRef"
        :model="{ takeDate }"
        :rules="rules"
        layout="vertical"
      >
        <a-divider orientation="left">
          基本信息
        </a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item
              label="类型"
              field="takeType"
            >
              <a-radio-group
                v-model="takeType"
                :options="typeOptions"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="盘点日期"
              field="takeDate"
            >
              <a-date-picker
                v-model="takeDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="请选择盘点日期"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item
          v-if="takeType === 0"
          label=" "
        >
          <a-alert type="info">
            期初建账仅可选未发生库存变动的商品（已建账 / 有单据业务的商品请用库存盘点调整）
          </a-alert>
        </a-form-item>

        <a-divider orientation="left">
          盘点明细
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
              :value="(record as StockTakeFormLine).productId"
              :options="productOptions"
              placeholder="请选择商品"
              allow-search
              allow-clear
              :loading="products.length === 0"
              @change="(v: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[]) => onLineProductChange(record as StockTakeFormLine, v as string | undefined)"
            />
          </template>
          <template #book="{ record }">
            <span class="num">
              {{ rowBook(record as StockTakeFormLine) }}
            </span>
          </template>
          <template #actual="{ record }">
            <a-input-number
              :model-value="(record as StockTakeFormLine).actualQuantity"
              :min="ACTUAL_MIN"
              :max="ACTUAL_MAX"
              :precision="0"
              placeholder="实盘数量"
              style="width: 100%"
              @change="(v: number | undefined) => onLineActualChange(record as StockTakeFormLine, v)"
            />
          </template>
          <template #diff="{ record }">
            <span :class="rowDiffClass(record as StockTakeFormLine)">
              {{ rowDiff(record as StockTakeFormLine) }}
            </span>
          </template>
          <template #itemAction="{ record }">
            <a-button
              type="text"
              status="danger"
              size="small"
              @click="removeItem((record as StockTakeFormLine).key)"
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
          请至少添加一行明细，且每行需选择商品、填写有效实盘数量（0–999999）
        </a-alert>

        <a-divider orientation="left">
          其他
        </a-divider>
        <a-form-item label="备注">
          <a-textarea
            v-model="remark"
            placeholder="选填（盘点说明 / 差异原因，不超过 200 字）"
            :max-length="200"
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

.items-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.items-error {
  margin-top: 8px;
}

.num {
  font-variant-numeric: tabular-nums;
}

/* 差异着色（design §4.4：正绿负红，0 中性） */
.diff-pos {
  color: rgb(var(--green-6));
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.diff-neg {
  color: rgb(var(--red-6));
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.diff-zero {
  color: var(--color-text-3);
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
</style>

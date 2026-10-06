<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { getBatchPickList } from '@/api/batch'
import type { BatchPickItem, BatchPickValue } from '@/api/batch'
import { IconPlus } from '@tabler/icons-vue'

// 开单页批次选择控件（040）：
// - 选定商品 + 仓库后加载该仓启用批次（到期日升序），展示可用库存 / 到期 / 近效期 / 过期标记；
// - 「就地新建批次」仅创建类单据可开（allowCreate），展开 batchNo / 生产日期 / 到期日三个输入；
// - 已过期批次在「必选」时下拉中置灰（出库类），入库类可放行但标注。
//
// 值（v-model，类型 BatchPickValue）：开单页明细行持有，batchId 与「就地新建三字段」互斥（规格 §3.4）：
// - 选中已有批次：仅 batchId / batchNo 有值；
// - 就地新建：仅 newBatchNo 等三字段有值（batchId 为空），父组件提交时直接取用，
//   后端在同一事务内建批次并使用。

// —— props/emits ——
const props = defineProps<{
  /** 商品 id（变化时重新加载批次） */
  productId?: string
  /** 仓库 id（变化时重新加载批次） */
  warehouseId: string
  /** 是否允许就地新建批次（采购入库 / 销售退货开单） */
  allowCreate?: boolean
  /** 是否必选（按批次商品为 true；非批次商品不渲染本控件） */
  required?: boolean
}>()

// —— reactive state ——
const modelValue = defineModel<BatchPickValue>({ default: () => ({}) })
const items = ref<BatchPickItem[]>([])
const loading = ref(false)
/** 就地新建输入区是否展开（allowCreate 时才显示「新建」按钮） */
const expanded = ref(false)

let fetchSeq = 0

// —— computed ——
const selectOptions = computed(() =>
  items.value.map((b) => ({
    label: buildLabel(b),
    value: b.batchId,
    // 必选单据（出库类）禁选已过期批次；入库类不置灰但标注
    disabled: b.isExpired && props.required,
    raw: b,
  })),
)

/** 下拉标签：批次号 + 可用库存 + 到期标记 */
function buildLabel(b: BatchPickItem): string {
  const parts = [b.batchNo, `可用 ${b.availableQuantity}`]
  if (b.expiryDate) {
    const d = new Date(b.expiryDate)
    const pad = (n: number) => String(n).padStart(2, '0')
    parts.push(`${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}${b.isExpired ? '（已过期）' : b.isNearExpiry ? '（近效期）' : ''}`)
  }
  return parts.join(' · ')
}

/** 选中批次的展示行（可用库存 / 到期 / 近效期） */
const selected = computed(() => items.value.find((b) => b.batchId === modelValue.value?.batchId))

/** 是否处于「就地新建」态（展开且已输入批次号） */
const isCreatingNew = computed(() => expanded.value && !!modelValue.value?.newBatchNo)

// —— watch ——
// immediate：控件经 v-if 在「已选商品」后才挂载（productId 初始即有值），非 immediate 的 watch 不会在
// 挂载时触发，导致批次下拉首次为空（无 /batches/pick 请求）；immediate 保证挂载即加载，且商品 / 仓变化重载。
watch(
  [() => props.productId, () => props.warehouseId],
  () => {
    modelValue.value = {}
    expanded.value = false
    void load()
  },
  { immediate: true },
)

// —— methods ——
async function load(): Promise<void> {
  if (!props.productId || !props.warehouseId) {
    items.value = []
    return
  }
  const seq = ++fetchSeq
  loading.value = true
  try {
    const list = await getBatchPickList({ productId: props.productId, warehouseId: props.warehouseId })
    if (seq !== fetchSeq) return
    items.value = list
    // FEFO 辅助：未选批次且未在建新时，默认选中最早到期且未过期且有可用库存的批次（到期日升序，后端已排好）
    if (!modelValue.value?.batchId && !modelValue.value?.newBatchNo) {
      const defaultPick = list.find((b) => !b.isExpired && b.availableQuantity > 0)
      if (defaultPick) modelValue.value = { batchId: defaultPick.batchId, batchNo: defaultPick.batchNo }
    }
  } catch {
    // 错误提示已由请求层统一处理
    if (seq === fetchSeq) items.value = []
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

function onSelect(value: string | number | boolean | Record<string, unknown> | (string | number | boolean | Record<string, unknown>)[] | undefined): void {
  const batchId = typeof value === 'string' ? value : undefined
  const item = items.value.find((b) => b.batchId === batchId)
  // 选中已有批次：收起新建区并清掉新建字段（batchId 与 newBatchNo 互斥）
  expanded.value = false
  modelValue.value = item ? { batchId: item.batchId, batchNo: item.batchNo } : {}
}

/** 就地新建：展开 / 收起输入区（仅 allowCreate）；展开即清空已选批次，保证互斥 */
function onCreateNew(): void {
  expanded.value = !expanded.value
  if (expanded.value) {
    modelValue.value = {}
  }
}

// 暴露给父组件：切换商品 / 仓库后手动重载（一般无需，watch 已覆盖）
defineExpose({ reload: load })
</script>

<template>
  <div class="batch-pick">
    <a-row
      :gutter="8"
      :wrap="false"
    >
      <a-col :flex="'1'">
        <a-select
          :model-value="modelValue?.batchId"
          :options="selectOptions"
          :loading="loading"
          :placeholder="isCreatingNew ? '就地新建批次' : props.productId ? '选择批次' : '先选商品'"
          :disabled="!props.productId"
          allow-clear
          allow-search
          :filter-option="true"
          @update:model-value="onSelect"
        />
      </a-col>
      <a-col
        v-if="props.allowCreate"
        :flex="'none'"
      >
        <a-button
          type="text"
          size="small"
          :disabled="!props.productId"
          @click="onCreateNew"
        >
          <template #icon>
            <IconPlus />
          </template>
          新建
        </a-button>
      </a-col>
    </a-row>

    <!-- 就地新建批次输入区 -->
    <a-row
      v-if="expanded && props.allowCreate"
      :gutter="8"
      class="new-batch-panel"
    >
      <a-col :span="10">
        <a-input
          v-model="modelValue!.newBatchNo"
          placeholder="批次号"
          size="small"
          allow-clear
        />
      </a-col>
      <a-col :span="7">
        <a-date-picker
          v-model="modelValue!.newProductionDate"
          value-format="YYYY-MM-DD"
          size="small"
          placeholder="生产日期"
          style="width: 100%"
        />
      </a-col>
      <a-col :span="7">
        <a-date-picker
          v-model="modelValue!.newExpiryDate"
          value-format="YYYY-MM-DD"
          size="small"
          placeholder="到期日"
          style="width: 100%"
        />
      </a-col>
    </a-row>

    <!-- 选中批次信息 -->
    <div
      v-if="selected"
      class="batch-meta"
    >
      <span>可用 {{ selected.availableQuantity }}</span>
      <a-tag
        v-if="selected.isExpired"
        color="red"
      >
        已过期
      </a-tag>
      <a-tag
        v-else-if="selected.isNearExpiry"
        color="orange"
      >
        近效期
      </a-tag>
    </div>
  </div>
</template>

<style scoped>
.batch-pick {
  display: flex;
  flex-direction: column;
  gap: 6px;
  width: 100%;
}

.new-batch-panel {
  margin-top: 2px;
}

.batch-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-3);
}
</style>

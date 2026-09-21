<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { getSettlements } from '@/api/settlement'
import type { SettlementListItem, SettlementOrderType } from '@/api/settlement'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'

// —— props/emits ——
const props = defineProps<{
  /** 被核销单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货） */
  orderType: SettlementOrderType
  /** 被核销单据 id */
  orderId: string
}>()

// —— constants ——
/** 收付款方向文案与颜色（收款绿 / 付款橙，与收付款列表口径一致） */
const TYPE_META: Record<number, { label: string; color: string }> = {
  0: { label: '收款', color: 'green' },
  1: { label: '付款', color: 'orange' },
}

const columns: TableColumnData[] = [
  { title: '单号', slotName: 'settlementNo', width: 190 },
  { title: '类型', slotName: 'type', width: 90, align: 'center' },
  { title: '收付日期', slotName: 'settlementDate', width: 120 },
  { title: '本次核销金额', slotName: 'orderAmount', width: 140, align: 'right' },
  { title: '状态', slotName: 'status', width: 90, align: 'center' },
]

// —— stores/composables ——
const router = useRouter()

// —— reactive state ——
const loading = ref(false)
const items = ref<SettlementListItem[]>([])

// —— watch ——
/** 按被核销单据反查收付款单（含已作废；单据 id 就绪即拉取，变更时重取） */
watch(
  () => [props.orderType, props.orderId] as const,
  () => {
    void fetchRecords()
  },
  { immediate: true },
)

// —— methods ——
async function fetchRecords(): Promise<void> {
  if (!props.orderId) return
  loading.value = true
  try {
    // 单张单据的收付款单数量有限，一次取足（pageSize 上限 100，不翻页）
    const result = await getSettlements({
      page: 1,
      pageSize: 100,
      orderType: props.orderType,
      orderId: props.orderId,
    })
    items.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
    items.value = []
  } finally {
    loading.value = false
  }
}

/** 作废行整体置灰（同收付款列表口径） */
function rowClassName(record: SettlementListItem): string {
  return record.status === 0 ? 'row-voided' : ''
}

/** 收付款单详情路径（单号超链接 href；实际跳转走 router.push，避免整页刷新） */
function settlementHref(record: SettlementListItem): string {
  return router.resolve({ name: 'settlementDetail', params: { id: record.id } }).href
}

/** 打开收付款单详情（同步路由跳转不置 loading） */
function onDetail(record: SettlementListItem): void {
  void router.push({ name: 'settlementDetail', params: { id: record.id } })
}
</script>

<template>
  <div class="settlement-records">
    <a-table
      row-key="id"
      size="small"
      :row-class="rowClassName"
      :loading="loading"
      :columns="columns"
      :data="items"
      :pagination="false"
    >
      <template #settlementNo="{ record }">
        <a-link
          :href="settlementHref(record as SettlementListItem)"
          @click.prevent="onDetail(record as SettlementListItem)"
        >
          {{ (record as SettlementListItem).settlementNo }}
        </a-link>
      </template>
      <template #type="{ record }">
        <a-tag :color="TYPE_META[(record as SettlementListItem).type]?.color">
          {{ TYPE_META[(record as SettlementListItem).type]?.label }}
        </a-tag>
      </template>
      <template #settlementDate="{ record }">
        {{ formatDateTime((record as SettlementListItem).settlementDate).slice(0, 10) }}
      </template>
      <template #orderAmount="{ record }">
        <span class="amount">
          ¥ {{ ((record as SettlementListItem).orderAmount ?? 0).toFixed(2) }}
        </span>
      </template>
      <template #status="{ record }">
        <a-tag :color="(record as SettlementListItem).status === 1 ? 'green' : 'red'">
          {{ (record as SettlementListItem).status === 1 ? '正常' : '已作废' }}
        </a-tag>
      </template>
    </a-table>
  </div>
</template>

<style scoped>
.amount {
  font-variant-numeric: tabular-nums;
}

/* 作废行整体置灰（与收付款列表一致） */
:deep(.row-voided) {
  opacity: 0.55;
}
</style>

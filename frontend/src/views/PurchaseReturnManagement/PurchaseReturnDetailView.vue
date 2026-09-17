<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getPurchaseReturn, updatePurchaseReturnSettlement, voidPurchaseReturn } from '@/api/purchaseReturn'
import type { PurchaseReturnDetail, PurchaseReturnItem, SettlementStatus } from '@/api/purchaseReturn'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载；notFound = 已加载但不存在 → 404 结果页） */
const detail = ref<PurchaseReturnDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

/** 作废 / 结算切换 loading（design §4.5：voidingId / settlingId） */
const voidingId = ref<string | undefined>(undefined)
const settlingId = ref<string | undefined>(undefined)

// —— constants ——
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', dataIndex: 'productName', width: 220, ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80, align: 'center' },
  { title: '数量', slotName: 'quantity', width: 110, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 130, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 130, align: 'right' },
]

// —— computed ——
/** 是否已作废（决定底部操作是否显示） */
const isVoided = computed(() => detail.value?.status === 0)
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))

// —— lifecycle ——
onMounted(async () => {
  await fetchDetail()
})

// —— methods ——
async function fetchDetail(): Promise<void> {
  if (!id.value) {
    notFound.value = true
    return
  }
  loading.value = true
  try {
    detail.value = await getPurchaseReturn(id.value)
    notFound.value = false
    creatorName.value = null
    if (detail.value.createdBy) {
      try {
        creatorName.value = (await getUser(detail.value.createdBy)).displayName
      } catch {
        // 创建人已删除等异常：回退显示原始 id（不阻断详情展示）
        creatorName.value = detail.value.createdBy
      }
    }
  } catch {
    notFound.value = true
    detail.value = null
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'purchaseReturns' })
}

/** 作废：回冲库存，仅改状态不删数据 */
async function onVoid(): Promise<void> {
  if (voidingId.value || !detail.value) return
  voidingId.value = detail.value.id
  try {
    await voidPurchaseReturn(detail.value.id)
    Message.success('已作废，库存已回冲')
    detail.value = await getPurchaseReturn(detail.value.id)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 结算切换：未结算 ↔ 已结算（库存不变） */
async function onToggleSettlement(): Promise<void> {
  if (settlingId.value || !detail.value) return
  settlingId.value = detail.value.id
  try {
    const next: SettlementStatus = detail.value.settlementStatus === 1 ? 0 : 1
    await updatePurchaseReturnSettlement(detail.value.id, next)
    Message.success(next === 1 ? '已标记为已结算' : '已改回未结算')
    detail.value = await getPurchaseReturn(detail.value.id)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    settlingId.value = undefined
  }
}
</script>

<template>
  <div
    v-loading="loading"
    class="detail-page"
  >
    <a-result
      v-if="notFound && !loading"
      status="404"
      title="单据不存在"
      subtitle="该采购退货单可能已被删除，请返回列表查看"
    >
      <template #extra>
        <a-button
          type="primary"
          @click="goBack"
        >
          返回列表
        </a-button>
      </template>
    </a-result>

    <template v-else-if="detail">
      <a-page-header
        class="detail-header"
        title="采购退货单详情"
        @back="goBack"
      />

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="单号">
            {{ detail.returnNo }}
          </a-descriptions-item>
          <a-descriptions-item label="供应商">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="退货日期">
            {{ formatDateTime(detail.returnDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="结算状态">
            <a-tag :color="detail.settlementStatus === 1 ? 'green' : 'gray'">
              {{ detail.settlementStatus === 1 ? '已结算' : '未结算' }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="单据状态">
            <a-tag :color="detail.status === 1 ? 'green' : 'red'">
              {{ detail.status === 1 ? '正常' : '已作废' }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="创建人">
            {{ creatorName ?? '-' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item
            label="备注"
            :span="2"
          >
            {{ detail.remark || '-' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <a-table
          row-key="id"
          size="small"
          :columns="itemColumns"
          :data="detail.items"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #quantity="{ record }">
            {{ (record as PurchaseReturnItem).quantity }}
          </template>
          <template #unitPrice="{ record }">
            ¥ {{ (record as PurchaseReturnItem).unitPrice.toFixed(2) }}
          </template>
          <template #subtotal="{ record }">
            ¥ {{ (record as PurchaseReturnItem).subtotal.toFixed(2) }}
          </template>
        </a-table>
      </a-card>

      <!-- 底部操作：仅正常单显示（作废后操作消失） -->
      <div
        v-if="!isVoided"
        class="detail-actions"
      >
        <a-space>
          <a-popconfirm
            type="warning"
            content="确认作废该采购退货单？作废后库存将回冲，且不可恢复"
            @ok="onVoid"
          >
            <a-button
              status="danger"
              :loading="voidingId === detail.id"
            >
              作废
            </a-button>
          </a-popconfirm>
          <a-popconfirm
            type="info"
            :content="detail.settlementStatus === 1 ? '确认改回未结算？' : '确认标记为已结算？'"
            @ok="onToggleSettlement"
          >
            <a-button
              :type="detail.settlementStatus === 1 ? 'outline' : 'primary'"
              :loading="settlingId === detail.id"
            >
              {{ detail.settlementStatus === 1 ? '改回未结算' : '标记已结算' }}
            </a-button>
          </a-popconfirm>
        </a-space>
      </div>
    </template>
  </div>
</template>

<style scoped>
.detail-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.detail-header {
  background: var(--color-bg-2);
  border-radius: var(--border-radius-medium);
  padding: 12px 20px;
}

.detail-desc {
  max-width: 960px;
}

.amount {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.detail-actions {
  display: flex;
  justify-content: flex-end;
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getSalesOrder, updateSalesOrderSettlement, voidSalesOrder } from '@/api/sale'
import type { SalesOrderDetail, SalesOrderItem, SettlementStatus } from '@/api/sale'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载；undefined = 已加载但不存在 → 404 结果页） */
const detail = ref<SalesOrderDetail | null>(null)
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
    detail.value = await getSalesOrder(id.value)
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
  void router.push({ name: 'sales' })
}

/** 作废：回冲库存，仅改状态不删数据 */
async function onVoid(): Promise<void> {
  if (voidingId.value || !detail.value) return
  voidingId.value = detail.value.id
  try {
    await voidSalesOrder(detail.value.id)
    Message.success('已作废，库存已回冲')
    detail.value = await getSalesOrder(detail.value.id)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 结算切换：未收 ↔ 已收（库存不变） */
async function onToggleSettlement(): Promise<void> {
  if (settlingId.value || !detail.value) return
  settlingId.value = detail.value.id
  try {
    const next: SettlementStatus = detail.value.settlementStatus === 1 ? 0 : 1
    await updateSalesOrderSettlement(detail.value.id, next)
    Message.success(next === 1 ? '已标记为已收' : '已改回未收')
    detail.value = await getSalesOrder(detail.value.id)
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
      subtitle="该销售单可能已被删除，请返回列表查看"
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
        title="销售单详情"
        @back="goBack"
      />

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="单号">
            {{ detail.orderNo }}
          </a-descriptions-item>
          <a-descriptions-item label="客户">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="单据日期">
            {{ formatDateTime(detail.orderDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="结算状态">
            <a-tag :color="detail.settlementStatus === 1 ? 'green' : 'orange'">
              {{ detail.settlementStatus === 1 ? '已收' : '未收' }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="单据状态">
            <a-tag :color="detail.status === 1 ? 'green' : 'gray'">
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
            {{ (record as SalesOrderItem).quantity }}
          </template>
          <template #unitPrice="{ record }">
            ¥ {{ (record as SalesOrderItem).unitPrice.toFixed(2) }}
          </template>
          <template #subtotal="{ record }">
            ¥ {{ (record as SalesOrderItem).subtotal.toFixed(2) }}
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
            content="确认作废该销售单？作废后库存将回冲，且不可恢复"
            @ok="onVoid"
          >
            <a-button
              status="warning"
              :loading="voidingId === detail.id"
            >
              作废
            </a-button>
          </a-popconfirm>
          <a-popconfirm
            type="info"
            :content="detail.settlementStatus === 1 ? '确认改回未收？' : '确认标记为已收？'"
            @ok="onToggleSettlement"
          >
            <a-button
              :type="detail.settlementStatus === 1 ? 'outline' : 'primary'"
              :loading="settlingId === detail.id"
            >
              {{ detail.settlementStatus === 1 ? '改回未收' : '标记已收' }}
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

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { formatTaxRate, getInvoice, INVOICE_TYPE_META, voidInvoice } from '@/api/invoice'
import type { InvoiceDetail, InvoiceItem } from '@/api/invoice'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { settlementOrderTypeLabel, settlementOrderTypeRouteName } from '@/utils/settlement'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'

// —— constants ——
/** 关联单据表格列（只读，快照字段原样展示；单号为超链接 → 单据详情） */
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '单据类型', slotName: 'orderType', width: 130 },
  { title: '单号', slotName: 'orderNo', width: 180 },
  { title: '单据日期', slotName: 'orderDate', width: 120 },
  { title: '单据总额', slotName: 'orderTotalAmount', width: 130, align: 'right' },
  { title: '本次开票金额', slotName: 'amount', width: 140, align: 'right' },
]

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载；notFound = 已加载但不存在 → 404 结果页） */
const detail = ref<InvoiceDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

/** 作废 loading（design §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)

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
    detail.value = await getInvoice(id.value)
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
  void router.push({ name: 'invoices' })
}

/** 关联单据详情路径（单号超链接 href；未知单据类型返回空串） */
function orderHref(item: InvoiceItem): string {
  const routeName = settlementOrderTypeRouteName(item.orderType)
  return routeName ? router.resolve({ name: routeName, params: { id: item.orderId } }).href : ''
}

/** 打开关联单据详情（同步路由跳转不置 loading） */
function onOrderDetail(item: InvoiceItem): void {
  const routeName = settlementOrderTypeRouteName(item.orderType)
  if (routeName) void router.push({ name: routeName, params: { id: item.orderId } })
}

/** 作废：占用金额按聚合自动释放，仅改状态不删数据 */
async function onVoid(): Promise<void> {
  if (voidingId.value || !detail.value) return
  voidingId.value = detail.value.id
  try {
    await voidInvoice(detail.value.id)
    Message.success('发票已作废，占用金额已释放')
    detail.value = await getInvoice(detail.value.id)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
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
      title="发票不存在"
      subtitle="该发票可能已被删除，请返回列表查看"
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
        title="发票详情"
        @back="goBack"
      />

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="发票号">
            {{ detail.invoiceNo }}
          </a-descriptions-item>
          <a-descriptions-item label="类型">
            <a-tag :color="INVOICE_TYPE_META[detail.type].color">
              {{ INVOICE_TYPE_META[detail.type].label }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="往来单位">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="开票日期">
            {{ formatDateTime(detail.invoiceDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="不含税金额">
            <span class="amount">¥ {{ detail.amountExcludingTax.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="税率">
            {{ formatTaxRate(detail.taxRate) }}
          </a-descriptions-item>
          <a-descriptions-item label="税额">
            <span class="amount">¥ {{ detail.taxAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="价税合计">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
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
          <a-descriptions-item label="备注">
            {{ detail.remark || '-' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          关联单据
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
          <template #orderType="{ record }">
            {{ settlementOrderTypeLabel((record as InvoiceItem).orderType) }}
          </template>
          <template #orderNo="{ record }">
            <a-link
              :href="orderHref(record as InvoiceItem)"
              @click.prevent="onOrderDetail(record as InvoiceItem)"
            >
              {{ (record as InvoiceItem).orderNo }}
            </a-link>
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as InvoiceItem).orderDate).slice(0, 10) }}
          </template>
          <template #orderTotalAmount="{ record }">
            ¥ {{ (record as InvoiceItem).orderTotalAmount.toFixed(2) }}
          </template>
          <template #amount="{ record }">
            ¥ {{ (record as InvoiceItem).amount.toFixed(2) }}
          </template>
        </a-table>
      </a-card>

      <!-- 底部操作：仅正常发票显示（作废后操作消失） -->
      <div
        v-if="!isVoided"
        class="detail-actions"
      >
        <a-popconfirm
          type="warning"
          content="确认作废该发票？作废后其占用金额自动释放，且不可恢复"
          @ok="onVoid"
        >
          <a-button
            status="danger"
            :loading="voidingId === detail.id"
          >
            作废
          </a-button>
        </a-popconfirm>
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

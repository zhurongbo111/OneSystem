<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { convertQuotation, getQuotation, voidQuotation } from '@/api/quotation'
import type { QuotationDetail, QuotationItem } from '@/api/quotation'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { isQuotationExpired, quotationStatusColor, quotationStatusLabel } from '@/utils/quotation'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconArrowForwardUp, IconBan, IconEdit } from '@tabler/icons-vue'

// —— constants ——
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', dataIndex: 'productName', ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80 },
  { title: '数量', dataIndex: 'quantity', width: 100, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 120, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 130, align: 'right' },
]

// —— reactive state ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<QuotationDetail>()

/** 正在作废 / 转单（design §4.4：voidingId / convertingId） */
const voidingId = ref<string | undefined>(undefined)
const convertingId = ref<string | undefined>(undefined)

// —— computed ——
/** 仅草稿可编辑 / 作废 / 转单（design.md §0.1） */
const isDraft = computed(() => detail.value?.status === 0)

/** 明细数量合计（仅展示） */
const totalQuantity = computed(() => detail.value?.items.reduce((sum, i) => sum + i.quantity, 0) ?? 0)

// —— lifecycle ——
onMounted(async () => {
  await loadDetail()
})

// —— methods ——
async function loadDetail(): Promise<void> {
  const id = route.params.id as string
  loading.value = true
  try {
    detail.value = await getQuotation(id)
    notFound.value = false
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'quotations' })
}

function onEdit(): void {
  void router.push({ name: 'quotationEdit', params: { id: route.params.id as string } })
}

/** 转出的销售订单详情（单号超链接；实际跳转走 router.push，避免整页刷新） */
function orderHref(): string {
  return router.resolve({ name: 'salesOrderDetail', params: { id: detail.value?.convertedOrderId ?? '' } }).href
}

function onOrderDetail(): void {
  if (!detail.value?.convertedOrderId) return
  void router.push({ name: 'salesOrderDetail', params: { id: detail.value.convertedOrderId } })
}

/** 转销售订单：一次性整单转，成功后报价单锁定为「已转订单」（本页刷新展示转单信息） */
async function onConvert(): Promise<void> {
  if (!detail.value || convertingId.value) return
  convertingId.value = detail.value.id
  try {
    const result = await convertQuotation(detail.value.id)
    Message.success(`已转销售订单 ${result.orderNo}`)
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理（40167 非草稿 / 已转订单）
  } finally {
    convertingId.value = undefined
  }
}

function confirmConvert(): void {
  if (!detail.value) return
  Modal.warning({
    title: '转销售订单',
    content: `确认将报价单 ${detail.value.quotationNo} 转为销售订单？转单后报价单锁定为「已转订单」，不可再编辑 / 转单 / 作废`,
    hideCancel: false,
    okText: '确认转单',
    onOk: () => onConvert(),
  })
}

async function onVoid(): Promise<void> {
  if (!detail.value || voidingId.value) return
  voidingId.value = detail.value.id
  try {
    detail.value = await voidQuotation(detail.value.id)
    Message.success('报价单已作废')
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

function confirmVoid(): void {
  if (!detail.value) return
  Modal.warning({
    title: '作废报价单',
    content: `确认作废报价单 ${detail.value.quotationNo}？作废后不可恢复`,
    hideCancel: false,
    okText: '确认作废',
    onOk: () => onVoid(),
  })
}
</script>

<template>
  <div class="detail-page">
    <a-result
      v-if="notFound"
      status="404"
      title="报价单不存在"
      subtitle="该报价单可能已被删除，或链接有误"
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

    <template v-else>
      <a-page-header
        :title="detail ? `报价单 ${detail.quotationNo}` : '报价单详情'"
        @back="goBack"
      />

      <a-card
        :bordered="false"
        :loading="loading"
      >
        <a-descriptions
          v-if="detail"
          class="detail-desc"
          :column="3"
          bordered
          size="medium"
        >
          <a-descriptions-item label="单号">
            {{ detail.quotationNo }}
          </a-descriptions-item>
          <a-descriptions-item label="客户">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="报价状态">
            <a-tag :color="quotationStatusColor(detail.status)">
              {{ quotationStatusLabel(detail.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="报价日期">
            {{ formatDateTime(detail.quotationDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="有效期至">
            <span>{{ detail.validUntil ?? '—' }}</span>
            <a-tag
              v-if="isQuotationExpired(detail.status, detail.validUntil)"
              size="small"
              color="orange"
              class="expired-tag"
            >
              已过期
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="明细行数">
            {{ detail.items.length }}
          </a-descriptions-item>
          <a-descriptions-item label="数量合计">
            {{ totalQuantity }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item
            label="备注"
            :span="3"
          >
            {{ detail.remark || '—' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <a-table
          row-key="id"
          size="small"
          :columns="itemColumns"
          :data="detail?.items ?? []"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #unitPrice="{ record }">
            <span class="amount">¥ {{ (record as QuotationItem).unitPrice.toFixed(2) }}</span>
          </template>
          <template #subtotal="{ record }">
            <span class="amount">¥ {{ (record as QuotationItem).subtotal.toFixed(2) }}</span>
          </template>
        </a-table>

        <a-divider orientation="left">
          转单信息
        </a-divider>
        <a-descriptions
          v-if="detail"
          :column="2"
          bordered
          size="medium"
        >
          <a-descriptions-item label="转出销售订单">
            <a-link
              v-if="detail.convertedOrderId"
              :href="orderHref()"
              @click.prevent="onOrderDetail"
            >
              {{ detail.convertedOrderNo }}
            </a-link>
            <span v-else>—</span>
          </a-descriptions-item>
          <a-descriptions-item label="说明">
            报价单为意向单据，不锁库存、不产生应收；成交以转单后的销售订单为准
          </a-descriptions-item>
        </a-descriptions>

        <div class="detail-footer">
          <a-space>
            <a-button
              v-if="isDraft && auth.hasPermission('quotations.convert')"
              type="primary"
              :loading="!!convertingId"
              @click="confirmConvert"
            >
              <template #icon>
                <IconArrowForwardUp />
              </template>
              转销售订单
            </a-button>
            <a-button
              v-if="isDraft && auth.hasPermission('quotations.update')"
              @click="onEdit"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-button
              v-if="isDraft && auth.hasPermission('quotations.void')"
              status="danger"
              :loading="!!voidingId"
              @click="confirmVoid"
            >
              <template #icon>
                <IconBan />
              </template>
              作废
            </a-button>
          </a-space>
        </div>
      </a-card>
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

.amount {
  font-variant-numeric: tabular-nums;
}

.expired-tag {
  margin-left: 6px;
}

.detail-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}
</style>

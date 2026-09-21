<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getPurchaseReceipts } from '@/api/purchase'
import type { PurchaseReceiptListItem } from '@/api/purchase'
import { closePurchaseOrder, getPurchaseOrder, voidPurchaseOrder } from '@/api/purchaseOrder'
import type { PurchaseOrderDetail, PurchaseOrderItem } from '@/api/purchaseOrder'
import { formatDateTime } from '@/utils/datetime'
import { orderFlowStatusColor, orderFlowStatusLabel } from '@/utils/orderFlow'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconArchive, IconBan, IconEdit, IconPackageImport } from '@tabler/icons-vue'

// —— constants ——
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', dataIndex: 'productName', ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80 },
  { title: '订购数量', slotName: 'quantity', width: 100, align: 'right' },
  { title: '已收数量', slotName: 'fulfilledQuantity', width: 100, align: 'right' },
  { title: '未收数量', slotName: 'remainingQuantity', width: 100, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 110, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
]

/** 关联入库单列表列（按 orderId 查入库单，跟单用；单号为超链接 → 入库单详情） */
const receiptColumns: TableColumnData[] = [
  { title: '单号', slotName: 'receiptNo', width: 180 },
  { title: '单据日期', slotName: 'orderDate', width: 120 },
  { title: '数量合计', slotName: 'totalQuantity', width: 100, align: 'right' },
  { title: '状态', slotName: 'status', width: 100, align: 'center' },
]

// —— reactive state ——
const route = useRoute()
const router = useRouter()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<PurchaseOrderDetail>()
const receipts = ref<PurchaseReceiptListItem[]>([])

/** 正在作废 / 关闭（design §4.4：voidingId / closingId） */
const voidingId = ref<string | undefined>(undefined)
const closingId = ref<string | undefined>(undefined)

// —— computed ——
/** 可编辑 / 可作废：仅待收货；可关闭：待收货 / 部分收货 */
const canEdit = computed(() => detail.value?.flowStatus === 1)
const canClose = computed(() => detail.value?.flowStatus === 1 || detail.value?.flowStatus === 2)
const canVoid = computed(() => detail.value?.flowStatus === 1)
/** 去入库：待收货 / 部分收货 */
const canReceive = computed(() => detail.value?.flowStatus === 1 || detail.value?.flowStatus === 2)

const totalQuantity = computed(() => detail.value?.items.reduce((sum, i) => sum + i.quantity, 0) ?? 0)
const totalFulfilled = computed(() => detail.value?.items.reduce((sum, i) => sum + i.fulfilledQuantity, 0) ?? 0)

// —— lifecycle ——
onMounted(async () => {
  await loadDetail()
})

// —— methods ——
async function loadDetail(): Promise<void> {
  const id = route.params.id as string
  loading.value = true
  try {
    detail.value = await getPurchaseOrder(id)
    notFound.value = false
    // 关联入库单列表（跟单：该订单已收了多少货）
    const result = await getPurchaseReceipts({ orderId: id, page: 1, pageSize: 100 })
    receipts.value = result.items
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'purchaseOrders' })
}

function onEdit(): void {
  void router.push({ name: 'purchaseOrderEdit', params: { id: route.params.id } })
}

/** 去入库：跳转采购入库开单页并预置关联订单（同步路由跳转不置 loading） */
function onReceive(): void {
  void router.push({ name: 'purchaseNew', query: { orderId: route.params.id as string } })
}

/** 关联入库单详情路径（单号超链接 href；实际跳转走 router.push，避免整页刷新） */
function receiptHref(record: PurchaseReceiptListItem): string {
  return router.resolve({ name: 'purchaseDetail', params: { id: record.id } }).href
}

/** 查看关联入库单详情：逐商品数量在入库单详情查看（同步路由跳转不置 loading） */
function onReceiptDetail(record: PurchaseReceiptListItem): void {
  void router.push({ name: 'purchaseDetail', params: { id: record.id } })
}

/** 关闭订单：剩余不再收货 */
async function onClose(): Promise<void> {
  if (!detail.value || closingId.value) return
  closingId.value = detail.value.id
  try {
    detail.value = await closePurchaseOrder(detail.value.id)
    Message.success('订单已关闭，剩余数量不再收货')
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    closingId.value = undefined
  }
}

/** 作废订单：仅改状态不删数据 */
async function onVoid(): Promise<void> {
  if (!detail.value || voidingId.value) return
  voidingId.value = detail.value.id
  try {
    detail.value = await voidPurchaseOrder(detail.value.id)
    Message.success('订单已作废')
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
    title: '作废订单',
    content: `确认作废订单 ${detail.value.orderNo}？作废后不可恢复`,
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
      title="订单不存在"
      subtitle="该采购订单可能已被删除，或链接有误"
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
        :title="detail ? `采购订单 ${detail.orderNo}` : '采购订单详情'"
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
            {{ detail.orderNo }}
          </a-descriptions-item>
          <a-descriptions-item label="供应商">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="订单状态">
            <a-tag :color="orderFlowStatusColor(detail.flowStatus)">
              {{ orderFlowStatusLabel(detail.flowStatus, 'purchase') }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="订单日期">
            {{ formatDateTime(detail.orderDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="预计到货">
            {{ detail.expectedDate ? formatDateTime(detail.expectedDate).slice(0, 10) : '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="订购总数">
            {{ totalQuantity }}
          </a-descriptions-item>
          <a-descriptions-item label="已收总数">
            {{ totalFulfilled }}
          </a-descriptions-item>
          <a-descriptions-item label="未收总数">
            {{ totalQuantity - totalFulfilled }}
          </a-descriptions-item>
          <a-descriptions-item label="备注">
            {{ detail.remark || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建人">
            {{ detail.createdBy || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
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
          <template #quantity="{ record }">
            {{ (record as PurchaseOrderItem).quantity }}
          </template>
          <template #fulfilledQuantity="{ record }">
            {{ (record as PurchaseOrderItem).fulfilledQuantity }}
          </template>
          <template #remainingQuantity="{ record }">
            {{ (record as PurchaseOrderItem).remainingQuantity }}
          </template>
          <template #unitPrice="{ record }">
            <span class="amount">¥ {{ (record as PurchaseOrderItem).unitPrice.toFixed(2) }}</span>
          </template>
          <template #subtotal="{ record }">
            <span class="amount">¥ {{ (record as PurchaseOrderItem).subtotal.toFixed(2) }}</span>
          </template>
        </a-table>

        <a-divider orientation="left">
          关联入库单
        </a-divider>
        <a-table
          row-key="id"
          size="small"
          :columns="receiptColumns"
          :data="receipts"
          :pagination="false"
        >
          <template #receiptNo="{ record }">
            <a-link
              :href="receiptHref(record as PurchaseReceiptListItem)"
              @click.prevent="onReceiptDetail(record as PurchaseReceiptListItem)"
            >
              {{ (record as PurchaseReceiptListItem).receiptNo }}
            </a-link>
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as PurchaseReceiptListItem).orderDate).slice(0, 10) }}
          </template>
          <template #totalQuantity="{ record }">
            {{ (record as PurchaseReceiptListItem).totalQuantity }}
          </template>
          <template #status="{ record }">
            <a-tag :color="(record as PurchaseReceiptListItem).status === 1 ? 'green' : 'red'">
              {{ (record as PurchaseReceiptListItem).status === 1 ? '正常' : '已作废' }}
            </a-tag>
          </template>
        </a-table>

        <div class="detail-footer">
          <a-space>
            <a-button
              v-if="canReceive"
              type="primary"
              @click="onReceive"
            >
              <template #icon>
                <IconPackageImport />
              </template>
              去入库
            </a-button>
            <a-button
              v-if="canEdit"
              @click="onEdit"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="canClose"
              type="warning"
              content="确认关闭该订单？关闭后剩余数量不再收货，且不可恢复"
              @ok="onClose"
            >
              <a-button
                status="warning"
                :loading="!!closingId"
              >
                <template #icon>
                  <IconArchive />
                </template>
                关闭
              </a-button>
            </a-popconfirm>
            <a-button
              v-if="canVoid"
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

.detail-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}
</style>

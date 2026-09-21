<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getSalesShipments } from '@/api/sale'
import type { SalesShipmentListItem } from '@/api/sale'
import { closeSalesOrder, getSalesOrder, voidSalesOrder } from '@/api/saleOrder'
import type { SalesOrderDetail, SalesOrderItem } from '@/api/saleOrder'
import { formatDateTime } from '@/utils/datetime'
import { orderFlowStatusColor, orderFlowStatusLabel } from '@/utils/orderFlow'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconArchive, IconBan, IconEdit, IconTruckDelivery } from '@tabler/icons-vue'

// —— constants ——
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', dataIndex: 'productName', ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80 },
  { title: '订购数量', slotName: 'quantity', width: 100, align: 'right' },
  { title: '已发数量', slotName: 'fulfilledQuantity', width: 100, align: 'right' },
  { title: '未发数量', slotName: 'remainingQuantity', width: 100, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 110, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 120, align: 'right' },
]

/** 关联出库单列表列（按 orderId 查出库单，跟单用；单号为超链接 → 出库单详情） */
const shipmentColumns: TableColumnData[] = [
  { title: '单号', slotName: 'shipmentNo', width: 180 },
  { title: '单据日期', slotName: 'orderDate', width: 120 },
  { title: '数量合计', slotName: 'totalQuantity', width: 100, align: 'right' },
  { title: '状态', slotName: 'status', width: 100, align: 'center' },
]

// —— reactive state ——
const route = useRoute()
const router = useRouter()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<SalesOrderDetail>()
const shipments = ref<SalesShipmentListItem[]>([])

/** 正在作废 / 关闭（design §4.4：voidingId / closingId） */
const voidingId = ref<string | undefined>(undefined)
const closingId = ref<string | undefined>(undefined)

// —— computed ——
const canEdit = computed(() => detail.value?.flowStatus === 1)
const canClose = computed(() => detail.value?.flowStatus === 1 || detail.value?.flowStatus === 2)
const canVoid = computed(() => detail.value?.flowStatus === 1)
/** 去出库：待发货 / 部分发货 */
const canShip = computed(() => detail.value?.flowStatus === 1 || detail.value?.flowStatus === 2)

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
    detail.value = await getSalesOrder(id)
    notFound.value = false
    const result = await getSalesShipments({ orderId: id, page: 1, pageSize: 100 })
    shipments.value = result.items
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'salesOrders' })
}

function onEdit(): void {
  void router.push({ name: 'salesOrderEdit', params: { id: route.params.id } })
}

/** 去出库：跳转销售出库开单页并预置关联订单（同步路由跳转不置 loading） */
function onShip(): void {
  void router.push({ name: 'salesNew', query: { orderId: route.params.id as string } })
}

/** 关联出库单详情路径（单号超链接 href；实际跳转走 router.push，避免整页刷新） */
function shipmentHref(record: SalesShipmentListItem): string {
  return router.resolve({ name: 'salesDetail', params: { id: record.id } }).href
}

/** 查看关联出库单详情：逐商品数量在出库单详情查看（同步路由跳转不置 loading） */
function onShipmentDetail(record: SalesShipmentListItem): void {
  void router.push({ name: 'salesDetail', params: { id: record.id } })
}

async function onClose(): Promise<void> {
  if (!detail.value || closingId.value) return
  closingId.value = detail.value.id
  try {
    detail.value = await closeSalesOrder(detail.value.id)
    Message.success('订单已关闭，剩余数量不再发货')
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    closingId.value = undefined
  }
}

async function onVoid(): Promise<void> {
  if (!detail.value || voidingId.value) return
  voidingId.value = detail.value.id
  try {
    detail.value = await voidSalesOrder(detail.value.id)
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
      subtitle="该销售订单可能已被删除，或链接有误"
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
        :title="detail ? `销售订单 ${detail.orderNo}` : '销售订单详情'"
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
          <a-descriptions-item label="客户">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="订单状态">
            <a-tag :color="orderFlowStatusColor(detail.flowStatus)">
              {{ orderFlowStatusLabel(detail.flowStatus, 'sales') }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="订单日期">
            {{ formatDateTime(detail.orderDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="预计发货">
            {{ detail.expectedDate ? formatDateTime(detail.expectedDate).slice(0, 10) : '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="订购总数">
            {{ totalQuantity }}
          </a-descriptions-item>
          <a-descriptions-item label="已发总数">
            {{ totalFulfilled }}
          </a-descriptions-item>
          <a-descriptions-item label="未发总数">
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
            {{ (record as SalesOrderItem).quantity }}
          </template>
          <template #fulfilledQuantity="{ record }">
            {{ (record as SalesOrderItem).fulfilledQuantity }}
          </template>
          <template #remainingQuantity="{ record }">
            {{ (record as SalesOrderItem).remainingQuantity }}
          </template>
          <template #unitPrice="{ record }">
            <span class="amount">¥ {{ (record as SalesOrderItem).unitPrice.toFixed(2) }}</span>
          </template>
          <template #subtotal="{ record }">
            <span class="amount">¥ {{ (record as SalesOrderItem).subtotal.toFixed(2) }}</span>
          </template>
        </a-table>

        <a-divider orientation="left">
          关联出库单
        </a-divider>
        <a-table
          row-key="id"
          size="small"
          :columns="shipmentColumns"
          :data="shipments"
          :pagination="false"
        >
          <template #shipmentNo="{ record }">
            <a-link
              :href="shipmentHref(record as SalesShipmentListItem)"
              @click.prevent="onShipmentDetail(record as SalesShipmentListItem)"
            >
              {{ (record as SalesShipmentListItem).shipmentNo }}
            </a-link>
          </template>
          <template #orderDate="{ record }">
            {{ formatDateTime((record as SalesShipmentListItem).orderDate).slice(0, 10) }}
          </template>
          <template #totalQuantity="{ record }">
            {{ (record as SalesShipmentListItem).totalQuantity }}
          </template>
          <template #status="{ record }">
            <a-tag :color="(record as SalesShipmentListItem).status === 1 ? 'green' : 'red'">
              {{ (record as SalesShipmentListItem).status === 1 ? '正常' : '已作废' }}
            </a-tag>
          </template>
        </a-table>

        <div class="detail-footer">
          <a-space>
            <a-button
              v-if="canShip"
              type="primary"
              @click="onShip"
            >
              <template #icon>
                <IconTruckDelivery />
              </template>
              去出库
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
              content="确认关闭该订单？关闭后剩余数量不再发货，且不可恢复"
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

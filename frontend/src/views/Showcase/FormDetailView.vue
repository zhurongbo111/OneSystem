<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import {
  ORDER_STATUS_COLOR,
  ORDER_STATUS_LABEL,
  formatAmount,
  useOrderData,
} from '@/composables/useOrderStore'
import { Message } from '@arco-design/web-vue'

import OrderFormDrawer from './OrderFormDrawer.vue'

const route = useRoute()
const router = useRouter()
const { findById, remove } = useOrderData()

// —— constants ——
const itemColumns = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' as const },
  { title: '商品名称', dataIndex: 'productName' },
  { title: '数量', dataIndex: 'quantity', width: 120 },
]

// —— reactive state ——
const drawerVisible = ref(false)

// —— computed ——
/** 当前订单（id 变化时重新查找） */
const order = computed(() => {
  const id = String(route.params.id ?? '')
  return id ? findById(id) : undefined
})

const notFound = computed(() => !order.value)

function goBack(): void {
  void router.push({ name: 'form' })
}

function onEdit(): void {
  if (order.value) drawerVisible.value = true
}

function onDelete(): void {
  if (!order.value) return
  remove(order.value.id)
  Message.success('订单已删除')
  goBack()
}
</script>

<template>
  <div class="detail-page">
    <template v-if="!notFound && order">
      <a-page-header
        class="detail-header"
        title="订单详情"
        :subtitle="`订单号：${order.orderNo}`"
        @back="goBack"
      >
        <template #extra>
          <a-space>
            <a-button
              type="primary"
              size="small"
              @click="onEdit"
            >
              编辑
            </a-button>
            <a-popconfirm
              type="warning"
              content="确认删除该订单？"
              @ok="onDelete"
            >
              <a-button
                status="danger"
                size="small"
              >
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-page-header>

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          bordered
        >
          <a-descriptions-item label="订单号">
            {{ order.orderNo }}
          </a-descriptions-item>
          <a-descriptions-item label="客户">
            {{ order.customer }}
          </a-descriptions-item>
          <a-descriptions-item label="商品">
            {{ order.product }}
          </a-descriptions-item>
          <a-descriptions-item label="金额">
            {{ formatAmount(order.amount) }}
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="ORDER_STATUS_COLOR[order.status]">
              {{ ORDER_STATUS_LABEL[order.status] }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ order.createdAt }}
          </a-descriptions-item>
          <a-descriptions-item label="备注">
            {{ order.remark || '-' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建人">
            {{ order.createdBy }}
          </a-descriptions-item>
          <a-descriptions-item label="更新时间">
            {{ order.updatedAt }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <a-table
          row-key="key"
          size="small"
          :columns="itemColumns"
          :data="order.items"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
        </a-table>
      </a-card>

      <OrderFormDrawer
        v-model:visible="drawerVisible"
        mode="edit"
        :edit-id="order.id"
      />
    </template>

    <a-result
      v-else
      status="404"
      title="订单不存在"
      subtitle="订单不存在或已被删除"
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
</style>

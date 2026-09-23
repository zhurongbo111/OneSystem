<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getSalesReturn } from '@/api/saleReturn'
import type { SalesReturnDetail } from '@/api/saleReturn'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { IconArrowLeft, IconPrinter } from '@tabler/icons-vue'

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载） */
const detail = ref<SalesReturnDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

/** 打印时间（取点击打印当刻） */
const printedAt = ref('')

// —— computed ——
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))
const totalQuantity = computed(() =>
  (detail.value?.items ?? []).reduce((sum, item) => sum + item.quantity, 0),
)

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
    detail.value = await getSalesReturn(id.value)
    notFound.value = false
    creatorName.value = null
    if (detail.value.createdBy) {
      try {
        creatorName.value = (await getUser(detail.value.createdBy)).displayName
      } catch {
        // 创建人已删除等异常：回退显示原始 id（不阻断打印）
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

function onBack(): void {
  router.back()
}

function goToList(): void {
  void router.push({ name: 'salesReturns' })
}

/** 打印时间取点击当刻，待 DOM 刷新后再调起打印 */
async function onPrint(): Promise<void> {
  printedAt.value = new Date().toISOString()
  await nextTick()
  window.print()
}
</script>

<template>
  <div class="print-view">
    <div class="print-toolbar">
      <a-space>
        <a-button
          type="primary"
          @click="onPrint"
        >
          <template #icon>
            <IconPrinter />
          </template>
          打印
        </a-button>
        <a-button @click="onBack">
          <template #icon>
            <IconArrowLeft />
          </template>
          返回
        </a-button>
      </a-space>
    </div>

    <a-spin
      :loading="loading"
      class="print-spin"
    >
      <template v-if="!notFound && detail">
        <div class="print-page">
          <h1 class="print-title">
            销售退货单
          </h1>

          <div class="print-meta">
            <span>
              <span class="print-meta-label">往来单位：</span>
              <span>{{ detail.partnerName }}</span>
            </span>
            <span>
              <span class="print-meta-label">入库仓：</span>
              <span>{{ detail.warehouseName }}</span>
            </span>
            <span>
              <span class="print-meta-label">单据日期：</span>
              <span>{{ formatDateTime(detail.returnDate).slice(0, 10) }}</span>
            </span>
            <span>
              <span class="print-meta-label">单号：</span>
              <span>{{ detail.returnNo }}</span>
            </span>
            <span>
              <span class="print-meta-label">备注：</span>
              <span>{{ detail.remark || '-' }}</span>
            </span>
          </div>

          <table class="print-table">
            <colgroup>
              <col style="width: 8%">
              <col style="width: 32%">
              <col style="width: 12%">
              <col style="width: 16%">
              <col style="width: 16%">
              <col style="width: 16%">
            </colgroup>
            <thead>
              <tr>
                <th>序号</th>
                <th>商品名称</th>
                <th>单位</th>
                <th class="is-right">
                  数量
                </th>
                <th class="is-right">
                  单价
                </th>
                <th class="is-right">
                  小计
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(item, index) in detail.items"
                :key="item.id"
              >
                <td class="is-center">
                  {{ index + 1 }}
                </td>
                <td>{{ item.productName }}</td>
                <td class="is-center">
                  {{ item.unit }}
                </td>
                <td class="is-right">
                  {{ item.quantity }}
                </td>
                <td class="is-right">
                  {{ item.unitPrice.toFixed(2) }}
                </td>
                <td class="is-right">
                  {{ item.subtotal.toFixed(2) }}
                </td>
              </tr>
            </tbody>
          </table>

          <div class="print-summary">
            <span>数量合计：{{ totalQuantity }}</span>
            <span>金额合计：¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </div>

          <div class="print-footer">
            <span>操作人：{{ creatorName ?? '-' }}</span>
            <span>打印时间：{{ formatDateTime(printedAt) }}</span>
            <span>第 1 页</span>
          </div>
        </div>
      </template>

      <a-result
        v-else-if="!loading"
        status="404"
        title="单据不存在"
        subtitle="该销售退货单可能已被删除，请返回列表查看"
      >
        <template #extra>
          <a-button
            type="primary"
            @click="goToList"
          >
            返回列表
          </a-button>
        </template>
      </a-result>
    </a-spin>
  </div>
</template>

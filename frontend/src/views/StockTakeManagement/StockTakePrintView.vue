<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getStockTakeById } from '@/api/stockTake'
import type { StockTakeDetail, StockTakeType } from '@/api/stockTake'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { IconArrowLeft, IconPrinter } from '@tabler/icons-vue'

// —— constants ——
/** 类型文案（同盘点详情页口径：0 期初建账 / 1 库存盘点） */
const TYPE_LABELS: Record<StockTakeType, string> = { 0: '期初建账', 1: '库存盘点' }

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载） */
const detail = ref<StockTakeDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

/** 打印时间（取点击打印当刻） */
const printedAt = ref('')

// —— computed ——
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))
const bookQuantityTotal = computed(() =>
  (detail.value?.items ?? []).reduce((sum, item) => sum + item.bookQuantity, 0),
)
const actualQuantityTotal = computed(() =>
  (detail.value?.items ?? []).reduce((sum, item) => sum + item.actualQuantity, 0),
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
    detail.value = await getStockTakeById(id.value)
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
  void router.push({ name: 'stockTakes' })
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
            库存盘点单
          </h1>

          <div class="print-meta">
            <span>
              <span class="print-meta-label">单据日期：</span>
              <span>{{ formatDateTime(detail.takeDate).slice(0, 10) }}</span>
            </span>
            <span>
              <span class="print-meta-label">单号：</span>
              <span>{{ detail.takeNo }}</span>
            </span>
            <span>
              <span class="print-meta-label">类型：</span>
              <span>{{ TYPE_LABELS[detail.type] }}</span>
            </span>
            <span>
              <span class="print-meta-label">备注：</span>
              <span>{{ detail.remark || '-' }}</span>
            </span>
          </div>

          <table class="print-table">
            <colgroup>
              <col style="width: 7%">
              <col style="width: 13%">
              <col style="width: 24%">
              <col style="width: 8%">
              <col style="width: 15%">
              <col style="width: 15%">
              <col style="width: 18%">
            </colgroup>
            <thead>
              <tr>
                <th>序号</th>
                <th>商品编码</th>
                <th>商品名称</th>
                <th>单位</th>
                <th class="is-right">
                  账面数量
                </th>
                <th class="is-right">
                  实盘数量
                </th>
                <th class="is-right">
                  差异
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
                <td>{{ item.productCode }}</td>
                <td>{{ item.productName }}</td>
                <td class="is-center">
                  {{ item.unit }}
                </td>
                <td class="is-right">
                  {{ item.bookQuantity }}
                </td>
                <td class="is-right">
                  {{ item.actualQuantity }}
                </td>
                <td class="is-right">
                  {{ item.difference }}
                </td>
              </tr>
            </tbody>
          </table>

          <div class="print-summary">
            <span>账面数量合计：{{ bookQuantityTotal }}</span>
            <span>实盘数量合计：{{ actualQuantityTotal }}</span>
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
        subtitle="该盘点单可能已被删除，请返回列表查看"
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

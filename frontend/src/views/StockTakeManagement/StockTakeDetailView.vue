<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getStockTakeById, type StockTakeDetail, type StockTakeItem, type StockTakeType } from '@/api/stockTake'
import { getUser } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconPrinter, IconStack2 } from '@tabler/icons-vue'

// —— constants ——
/** 类型渲染（design §4.4：期初建账 purple / 库存盘点 gold） */
const TYPE_META: Record<StockTakeType, { label: string; color: string }> = {
  0: { label: '期初建账', color: 'purple' },
  1: { label: '库存盘点', color: 'gold' },
}

const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品编码', dataIndex: 'productCode', width: 140 },
  { title: '商品名称', dataIndex: 'productName', width: 220, ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80, align: 'center' },
  { title: '账面数量', dataIndex: 'bookQuantity', width: 110, align: 'right' },
  { title: '实盘数量', dataIndex: 'actualQuantity', width: 110, align: 'right' },
  { title: '差异', slotName: 'diff', width: 100, align: 'right' },
]

// —— reactive state ——
const route = useRoute()
const router = useRouter()

/** 详情数据（null = 尚未加载；undefined = 已加载但不存在 → 404 结果页） */
const detail = ref<StockTakeDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

// —— computed ——
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))

// —— lifecycle ——
onMounted(async () => {
  await fetchDetail()
})

// —— methods ——
/** 行差异着色（正绿负红，0 中性，同新建页口径） */
function rowDiff(item: StockTakeItem): number {
  return item.difference
}

function rowDiffClass(item: StockTakeItem): string {
  const d = item.difference
  if (d > 0) return 'diff-pos'
  if (d < 0) return 'diff-neg'
  return 'diff-zero'
}

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
  void router.push({ name: 'stockTakes' })
}

/** 打开打印视图（specs/027-erp-export §4.3：详情页头部打印入口） */
function onPrint(): void {
  void router.push({ name: 'stockTakePrint', params: { id: id.value } })
}

/** 查看库存流水：跳流水页并预置本单号关键词（design §4.4，复用 019 页面） */
function onShowMovements(): void {
  if (!detail.value) return
  void router.push({ name: 'stockMovements', query: { keyword: detail.value.takeNo } })
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
      subtitle="该盘点单可能已被删除，请返回列表查看"
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
        title="盘点单详情"
        @back="goBack"
      >
        <template #extra>
          <a-button
            size="small"
            @click="onPrint"
          >
            <template #icon>
              <IconPrinter />
            </template>
            打印
          </a-button>
        </template>
      </a-page-header>

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="单号">
            {{ detail.takeNo }}
          </a-descriptions-item>
          <a-descriptions-item label="类型">
            <a-tag :color="TYPE_META[detail.type].color">
              {{ TYPE_META[detail.type].label }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="盘点日期">
            {{ formatDateTime(detail.takeDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="明细行数">
            {{ detail.itemCount }}
          </a-descriptions-item>
          <a-descriptions-item label="差异行数">
            <span :class="detail.diffItemCount > 0 ? 'diff-count' : ''">
              {{ detail.diffItemCount }}
            </span>
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
          盘点明细
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
          <template #diff="{ record }">
            <span :class="rowDiffClass(record as StockTakeItem)">
              {{ rowDiff(record as StockTakeItem) }}
            </span>
          </template>
        </a-table>
      </a-card>

      <!-- 底部操作：查看库存流水（预置本单号关键词，复用 019 流水页） -->
      <div class="detail-actions">
        <a-space>
          <a-button @click="onShowMovements">
            <template #icon>
              <IconStack2 />
            </template>
            查看库存流水
          </a-button>
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

/* 差异行数 > 0 标橙（design §4.4） */
.diff-count {
  color: var(--color-warning-6);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

/* 差异着色（正绿负红，0 中性，同新建页口径） */
.diff-pos {
  color: rgb(var(--green-6));
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.diff-neg {
  color: rgb(var(--red-6));
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.diff-zero {
  color: var(--color-text-3);
  font-variant-numeric: tabular-nums;
}

.detail-actions {
  display: flex;
  justify-content: flex-end;
}
</style>

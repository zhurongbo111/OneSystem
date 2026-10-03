<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getBatches, updateBatchStatus } from '@/api/batch'
import type { BatchListItem, BatchStatus } from '@/api/batch'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime, toDateInput } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconEdit,
  IconPlayerPlay,
  IconPlus,
  IconPower,
  IconRefresh,
  IconRestore,
  IconSearch,
} from '@tabler/icons-vue'

import BatchFormDrawer from './BatchFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 近效期窗口（天）：与后端 BatchFieldConstraints.NearExpiryDays 同源 */
const NEAR_EXPIRY_DAYS = 30

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const togglingId = ref<string | undefined>(undefined)
const items = ref<BatchListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 状态 / 仅看近效期或过期：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const statusInput = ref<BatchStatus | undefined>(undefined)
const onlyExpiringInput = ref(false)
const appliedKeyword = ref('')
const appliedStatus = ref<BatchStatus | undefined>(undefined)
const appliedOnlyExpiring = ref(false)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedStatus.value ?? ''}|${appliedOnlyExpiring.value}`)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

const columns = computed<TableColumnData[]>(() => [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '批次号', slotName: 'batchNo', width: 180 },
  { title: '商品编码', dataIndex: 'productCode', width: 140 },
  { title: '商品名称', dataIndex: 'productName', width: 200, ellipsis: true, tooltip: true },
  { title: '生产日期', slotName: 'productionDate', width: 120 },
  { title: '到期日', slotName: 'expiryDate', width: 160 },
  { title: '库存', dataIndex: 'totalStock', width: 100, align: 'right' },
  { title: '状态', slotName: 'status', width: 80, align: 'center' },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  // 操作列：2 个操作（编辑 / 停用或启用）
  { title: '操作', slotName: 'action', width: 160, bodyCellClass: 'action-cell' },
])

const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
})

// —— methods ——
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getBatches({
      keyword: appliedKeyword.value.trim() || undefined,
      status: appliedStatus.value,
      onlyExpiring: appliedOnlyExpiring.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    if (seq !== fetchSeq) return
    items.value = result.items
    total.value = result.total
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedStatus.value = statusInput.value
  appliedOnlyExpiring.value = onlyExpiringInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  onlyExpiringInput.value = false
  appliedKeyword.value = ''
  appliedStatus.value = undefined
  appliedOnlyExpiring.value = false
  page.value = 1
  void fetchList()
}

function onRefresh(): void {
  void fetchList()
}

function onPageChange(current: number): void {
  page.value = current
  void fetchList()
}

function onPageSizeChange(size: number): void {
  pageSize.value = size
  page.value = 1
  void fetchList()
}

function onCreate(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

function onEdit(row: BatchListItem): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

async function onToggleStatus(row: BatchListItem): Promise<void> {
  if (togglingId.value) return
  const next: 0 | 1 = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateBatchStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已停用')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 到期日渲染：永久 / 已过期（红）/ 近效期（橙）/ 正常（文本） */
function renderExpiry(row: BatchListItem): { text: string; color?: 'red' | 'orange' } {
  if (!row.expiryDate) return { text: '永久' }
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const expiry = new Date(row.expiryDate)
  const label = toDateInput(expiry)
  if (expiry < today) return { text: `${label}（已过期）`, color: 'red' }
  const near = new Date(today.getTime() + NEAR_EXPIRY_DAYS * 24 * 60 * 60 * 1000)
  if (expiry <= near) return { text: `${label}（近效期）`, color: 'orange' }
  return { text: label }
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        批次管理
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="8">
            <a-input
              v-model="keywordInput"
              placeholder="搜索批次号 / 商品编码 / 商品名称"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              class="filter-bar__status"
              :options="statusOptions"
              placeholder="状态"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-checkbox
              v-model="onlyExpiringInput"
              style="align-self: center"
            >
              仅看近效期或过期
            </a-checkbox>
          </a-col>
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                :loading="loading"
                @click="onSearch"
              >
                <template #icon>
                  <IconSearch />
                </template>
                搜索
              </a-button>
              <a-button
                :loading="loading"
                @click="onReset"
              >
                <template #icon>
                  <IconRestore />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('batches.create')"
              type="primary"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              size="small"
              :loading="loading"
              @click="onRefresh"
            >
              <template #icon>
                <IconRefresh />
              </template>
              刷新
            </a-button>
          </div>
        </div>
      </div>

      <a-table
        :key="tableKey"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="items"
        :pagination="pagination"
        :scroll="{ x: tableScrollX }"
        @page-change="onPageChange"
        @page-size-change="onPageSizeChange"
      >
        <template #seq="{ rowIndex }">
          {{ (page - 1) * pageSize + rowIndex + 1 }}
        </template>
        <template #batchNo="{ record }">
          <span class="batch-no">{{ (record as BatchListItem).batchNo }}</span>
        </template>
        <template #productionDate="{ record }">
          {{ (record as BatchListItem).productionDate ? toDateInput(new Date((record as BatchListItem).productionDate as string)) : '-' }}
        </template>
        <template #expiryDate="{ record }">
          <a-tag
            v-if="renderExpiry(record as BatchListItem).color"
            :color="renderExpiry(record as BatchListItem).color"
          >
            {{ renderExpiry(record as BatchListItem).text }}
          </a-tag>
          <span v-else>{{ renderExpiry(record as BatchListItem).text }}</span>
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as BatchListItem).status === 1 ? 'green' : 'red'">
            {{ (record as BatchListItem).status === 1 ? '启用' : '停用' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as BatchListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onEdit(record as BatchListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              type="warning"
              :content="`确认${(record as BatchListItem).status === 1 ? '停用' : '启用'}批次 ${(record as BatchListItem).batchNo}？`"
              @ok="onToggleStatus(record as BatchListItem)"
            >
              <a-button
                type="text"
                :status="(record as BatchListItem).status === 1 ? 'warning' : 'normal'"
                size="small"
                :loading="togglingId === (record as BatchListItem).id"
              >
                <template #icon>
                  <IconPower v-if="(record as BatchListItem).status === 1" />
                  <IconPlayerPlay v-else />
                </template>
                {{ (record as BatchListItem).status === 1 ? '停用' : '启用' }}
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <BatchFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      @saved="fetchList"
    />
  </div>
</template>

<style scoped>
.list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
}

.batch-no {
  font-family: var(--font-mono, monospace);
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.page-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-1);
}

.toolbar-filter {
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--color-border);
}

.toolbar-filter .arco-col {
  display: flex;
}

.toolbar-filter .arco-col > .arco-input,
.toolbar-filter .arco-col > .arco-select {
  flex: 1;
}

.toolbar-filter__actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.toolbar-actions__left,
.toolbar-actions__right {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

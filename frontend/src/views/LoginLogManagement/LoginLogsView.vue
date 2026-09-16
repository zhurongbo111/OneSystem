<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getLoginLogs, toUtcRange } from '@/api/loginLog'
import type { LoginLogListItem } from '@/api/loginLog'
import { formatDateTime } from '@/utils/datetime'
import { IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

// —— constants ——
const columns = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' as const },
  { title: '登录名', dataIndex: 'username', width: 160 },
  { title: '显示名', dataIndex: 'displayName', width: 140 },
  { title: '登录时间', dataIndex: 'loginAt', width: 190, slotName: 'loginAt' },
  { title: '登录 IP', dataIndex: 'ipAddress', width: 160, slotName: 'ipAddress' },
  { title: 'User-Agent', dataIndex: 'userAgent', ellipsis: true, tooltip: true },
]

// —— reactive state ——
/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

const loading = ref(false)
const items = ref<LoginLogListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 筛选：输入态 与 已应用态分离（点搜索才生效） */
const usernameInput = ref('')
const dateRange = ref<string[]>([])
const appliedUsername = ref('')
const appliedRange = ref<string[]>([])

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedUsername.value}|${appliedRange.value.join('~')}`)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
})

// —— methods ——
/** 拉取当前条件下的登录日志（时间范围在接口层转为 UTC ISO；请求序号防止乱序响应覆盖） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const { startTime, endTime } = toUtcRange(appliedRange.value[0], appliedRange.value[1])
    const result = await getLoginLogs({
      username: appliedUsername.value.trim() || undefined,
      startTime,
      endTime,
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

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedUsername.value = usernameInput.value
  appliedRange.value = [...(dateRange.value ?? [])]
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  usernameInput.value = ''
  dateRange.value = []
  appliedUsername.value = ''
  appliedRange.value = []
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
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        登录日志
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
              v-model="usernameInput"
              class="filter-bar__search"
              placeholder="搜索登录名"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="8">
            <a-range-picker
              v-model="dateRange"
              value-format="YYYY-MM-DD"
              class="filter-bar__range"
              allow-clear
            />
          </a-col>
          <a-col :span="6">
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

      <a-table
        :key="tableKey"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="items"
        :pagination="pagination"
        @page-change="onPageChange"
        @page-size-change="onPageSizeChange"
      >
        <template #seq="{ rowIndex }">
          {{ (page - 1) * pageSize + rowIndex + 1 }}
        </template>
        <template #loginAt="{ record }">
          {{ formatDateTime((record as LoginLogListItem).loginAt) }}
        </template>
        <template #ipAddress="{ record }">
          {{ (record as LoginLogListItem).ipAddress || '-' }}
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<style scoped>
.list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
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
.toolbar-filter .arco-col > .arco-range-picker {
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
  justify-content: flex-end;
  gap: 8px;
  margin-bottom: 8px;
}

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { NOTIFICATION_TYPE_META, NOTIFICATION_TYPE_OPTIONS, getNotifications, scanStockAlerts } from '@/api/notification'
import type { NotificationItem, NotificationType } from '@/api/notification'
import { useAuthStore } from '@/stores/auth'
import { useNotificationStore } from '@/stores/notification'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconChecks, IconEye, IconRadar, IconRefresh, IconRestore, IconSearch } from '@tabler/icons-vue'

const router = useRouter()
const auth = useAuthStore()
const notification = useNotificationStore()

// —— constants ——
/** 已读状态下拉（全部由 allow-clear 表达，不单列「全部」选项） */
const readStatusOptions = [
  { label: '未读', value: 'unread' },
  { label: '已读', value: 'read' },
]

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<NotificationItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 类型 / 已读状态 / 关键词：输入态与已应用态分离（点搜索才生效） */
const typeInput = ref<NotificationType | undefined>(undefined)
const readStatusInput = ref<'unread' | 'read' | undefined>(undefined)
const keywordInput = ref('')
const appliedType = ref<NotificationType | undefined>(undefined)
const appliedReadStatus = ref<'unread' | 'read' | undefined>(undefined)
const appliedKeyword = ref('')

/** 单条「查看」进行中的消息 id（行内 loading + 防重入） */
const readingId = ref<string | undefined>(undefined)
/** 全部已读进行中 */
const markingAll = ref(false)
/** 立即扫描进行中 */
const scanning = ref(false)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedType.value ?? ''}|${appliedReadStatus.value ?? ''}|${appliedKeyword.value}`)

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
  { title: '类型', slotName: 'type', width: 100, align: 'center' },
  { title: '标题', dataIndex: 'title', width: 200, ellipsis: true, tooltip: true },
  { title: '内容', dataIndex: 'content', width: 360, ellipsis: true, tooltip: true },
  { title: '时间', slotName: 'createdAt', width: 172 },
  { title: '状态', slotName: 'status', width: 90, align: 'center' },
  // 操作列：1 个操作（查看 = 标记已读 + 跳转）
  { title: '操作', slotName: 'action', width: 100, bodyCellClass: 'action-cell' },
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
    const result = await getNotifications({
      type: appliedType.value,
      isRead: appliedReadStatus.value === undefined ? undefined : appliedReadStatus.value === 'read',
      keyword: appliedKeyword.value.trim() || undefined,
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
  appliedType.value = typeInput.value
  appliedReadStatus.value = readStatusInput.value
  appliedKeyword.value = keywordInput.value
  page.value = 1
  void fetchList()
}

function onReset(): void {
  typeInput.value = undefined
  readStatusInput.value = undefined
  keywordInput.value = ''
  appliedType.value = undefined
  appliedReadStatus.value = undefined
  appliedKeyword.value = ''
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

/** 全部已读：走共享状态以同步顶栏未读数，再刷新列表 */
async function onMarkAllRead(): Promise<void> {
  if (markingAll.value) return
  markingAll.value = true
  try {
    const affectedCount = await notification.markAllRead()
    Message.success(affectedCount > 0 ? `已标记 ${affectedCount} 条为已读` : '没有未读消息')
    await fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    markingAll.value = false
  }
}

/** 立即扫描（手动触发库存预警扫描，与定时宿主共用同一实现） */
async function onScan(): Promise<void> {
  if (scanning.value) return
  scanning.value = true
  try {
    const result = await scanStockAlerts()
    Message.success(result.messageCount > 0 ? `扫描完成，新增 ${result.messageCount} 条消息` : '扫描完成，暂无新告警')
    await fetchList()
    // 扫描会新增本人消息，同步顶栏未读数（不依赖路由切换）
    await notification.fetchSummary()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    scanning.value = false
  }
}

/** 查看：标记已读（未读时）并按跳转目标跳转；无链接时仅标记已读 */
async function onView(row: NotificationItem): Promise<void> {
  if (readingId.value) return
  readingId.value = row.id
  try {
    if (!row.isRead) {
      await notification.markRead(row.id)
      row.isRead = true
    }
    await pushLink(row)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    readingId.value = undefined
  }
}

/** 按消息携带的路由名 + query 跳转；路由不存在时仅标记已读（不跳转） */
async function pushLink(row: NotificationItem): Promise<void> {
  if (!row.linkRouteName) return
  const routeExists = router.getRoutes().some((record) => record.name === row.linkRouteName)
  if (!routeExists) return
  const query: Record<string, string> = row.linkQuery
    ? (JSON.parse(row.linkQuery) as Record<string, string>)
    : {}
  await router.push({ name: row.linkRouteName, query })
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        站内消息
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
              placeholder="搜索消息标题"
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
              v-model="typeInput"
              :options="NOTIFICATION_TYPE_OPTIONS"
              placeholder="类型"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="readStatusInput"
              :options="readStatusOptions"
              placeholder="已读状态"
              allow-clear
            />
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
            <a-popconfirm
              type="warning"
              content="确认把全部未读消息标记为已读？"
              @ok="onMarkAllRead"
            >
              <a-button
                size="small"
                :loading="markingAll"
              >
                <template #icon>
                  <IconChecks />
                </template>
                全部已读
              </a-button>
            </a-popconfirm>
            <a-button
              v-if="auth.hasPermission('notifications.scan')"
              type="primary"
              size="small"
              :loading="scanning"
              @click="onScan"
            >
              <template #icon>
                <IconRadar />
              </template>
              立即扫描
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
        <template #type="{ record }">
          <a-tag :color="NOTIFICATION_TYPE_META[(record as NotificationItem).type].color">
            {{ NOTIFICATION_TYPE_META[(record as NotificationItem).type].label }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as NotificationItem).createdAt) }}
        </template>
        <template #status="{ record }">
          <a-tag
            v-if="!(record as NotificationItem).isRead"
            color="arcoblue"
          >
            未读
          </a-tag>
          <span v-else>已读</span>
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              :loading="readingId === (record as NotificationItem).id"
              @click="onView(record as NotificationItem)"
            >
              <template #icon>
                <IconEye />
              </template>
              查看
            </a-button>
          </a-space>
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

.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
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

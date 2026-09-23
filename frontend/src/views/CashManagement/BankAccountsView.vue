<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import {
  deleteBankAccount,
  getBankAccountSummary,
  getBankAccounts,
  updateBankAccountStatus,
} from '@/api/bankAccount'
import type {
  BankAccountBalanceItem,
  BankAccountListItem,
  BankAccountStatus,
  BankAccountType,
} from '@/api/bankAccount'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
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
  IconTrash,
} from '@tabler/icons-vue'

import BankAccountFormDrawer from './BankAccountFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

const typeOptions = [
  { label: '现金', value: 1 },
  { label: '银行', value: 2 },
]

const TYPE_LABELS: Record<BankAccountType, string> = {
  1: '现金',
  2: '银行',
}

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在启停的账户 id：行内按钮 loading 与写操作互斥用 */
const togglingId = ref<string | undefined>(undefined)
/** 正在删除的账户 id */
const deletingId = ref<string | undefined>(undefined)
const items = ref<BankAccountListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
/** 余额总览（账户数 / 总余额） */
const summary = ref<BankAccountBalanceItem[]>([])

/** 关键词 / 类型 / 状态：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const typeInput = ref<BankAccountType | undefined>(undefined)
const statusInput = ref<BankAccountStatus | undefined>(undefined)
const appliedKeyword = ref('')
const appliedType = ref<BankAccountType | undefined>(undefined)
const appliedStatus = ref<BankAccountStatus | undefined>(undefined)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedType.value ?? ''}|${appliedStatus.value ?? ''}`)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

/** 启用账户数与总余额（总览卡片） */
const enabledCount = computed(() => summary.value.filter((a) => a.status === 1).length)
const totalBalance = computed(() =>
  summary.value.filter((a) => a.status === 1).reduce((sum, a) => sum + a.balance, 0),
)

/** 列定义（操作列 3 个操作平铺，宽 210，specs/011-action-column §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '账户编码', dataIndex: 'code', width: 120 },
  { title: '账户名称', dataIndex: 'name', width: 160, ellipsis: true, tooltip: true },
  { title: '类型', slotName: 'type', width: 80, align: 'center' },
  { title: '开户行', dataIndex: 'bankName', width: 180, ellipsis: true, tooltip: true },
  { title: '账号', dataIndex: 'accountNo', width: 160, ellipsis: true, tooltip: true },
  { title: '初始余额', slotName: 'initialBalance', width: 110, align: 'right' },
  { title: '当前余额', slotName: 'balance', width: 110, align: 'right' },
  { title: '状态', slotName: 'status', width: 80, align: 'center' },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  { title: '操作', slotName: 'action', width: 210, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchSummary()
})

// —— methods ——
/** 金额展示（两位小数，千分位） */
function formatMoney(value: number): string {
  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getBankAccounts({
      keyword: appliedKeyword.value.trim() || undefined,
      type: appliedType.value,
      status: appliedStatus.value,
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

/** 余额总览（与列表分开取，不阻塞列表渲染） */
async function fetchSummary(): Promise<void> {
  try {
    summary.value = await getBankAccountSummary()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedType.value = typeInput.value
  appliedStatus.value = statusInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  typeInput.value = undefined
  statusInput.value = undefined
  appliedKeyword.value = ''
  appliedType.value = undefined
  appliedStatus.value = undefined
  page.value = 1
  void fetchList()
}

/** 刷新当前页与总览 */
function onRefresh(): void {
  void fetchList()
  void fetchSummary()
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

/** 新增（抽屉） */
function onCreate(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉） */
function onEdit(row: BankAccountListItem): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 启用 / 停用 */
async function onToggleStatus(row: BankAccountListItem): Promise<void> {
  if (togglingId.value) return
  const next: BankAccountStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateBankAccountStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已停用')
    void fetchList()
    void fetchSummary()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 删除 */
async function onDelete(row: BankAccountListItem): Promise<void> {
  if (deletingId.value) return
  deletingId.value = row.id
  try {
    await deleteBankAccount(row.id)
    Message.success('资金账户已删除')
    void fetchList()
    void fetchSummary()
  } catch {
    // 错误提示已由请求层统一处理（被收付款单引用 → 40161）
  } finally {
    deletingId.value = undefined
  }
}

/** 抽屉保存后刷新列表与总览 */
function onSaved(): void {
  void fetchList()
  void fetchSummary()
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        资金账户
      </h1>
    </div>

    <!-- 余额总览 -->
    <a-row
      :gutter="16"
      class="summary-row"
    >
      <a-col :span="12">
        <a-card :bordered="false">
          <a-statistic
            title="启用账户数"
            :value="enabledCount"
            :value-style="{ color: 'var(--color-text-1)' }"
          />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card :bordered="false">
          <a-statistic
            title="资金总余额（元）"
            :value="totalBalance"
            :precision="2"
            :value-style="{ color: 'rgb(var(--green-6))' }"
          />
        </a-card>
      </a-col>
    </a-row>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <!-- 筛选行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="8">
            <a-input
              v-model="keywordInput"
              placeholder="搜索账户编码或名称"
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
              :options="typeOptions"
              placeholder="类型"
              allow-clear
            />
          </a-col>
          <a-col :span="4">
            <a-select
              v-model="statusInput"
              :options="statusOptions"
              placeholder="状态"
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

        <!-- 操作行：左组主操作靠左，右组视图操作靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('bankAccounts.create')"
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
        <template #type="{ record }">
          <a-tag :color="(record as BankAccountListItem).type === 1 ? 'orange' : 'arcoblue'">
            {{ TYPE_LABELS[(record as BankAccountListItem).type] }}
          </a-tag>
        </template>
        <template #initialBalance="{ record }">
          {{ formatMoney((record as BankAccountListItem).initialBalance) }}
        </template>
        <template #balance="{ record }">
          {{ formatMoney((record as BankAccountListItem).balance) }}
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as BankAccountListItem).status === 1 ? 'green' : 'red'">
            {{ (record as BankAccountListItem).status === 1 ? '启用' : '停用' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as BankAccountListItem).createdAt) }}
        </template>
        <!-- 操作列（specs/011-action-column）：3 个操作平铺 编辑 / 启停 / 删除 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('bankAccounts.update')"
              type="text"
              size="small"
              @click="onEdit(record as BankAccountListItem)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('bankAccounts.status')"
              type="warning"
              :content="`确认${(record as BankAccountListItem).status === 1 ? '停用' : '启用'}该资金账户？`"
              @ok="onToggleStatus(record as BankAccountListItem)"
            >
              <a-button
                type="text"
                :status="(record as BankAccountListItem).status === 1 ? 'warning' : 'normal'"
                size="small"
                :loading="togglingId === (record as BankAccountListItem).id"
              >
                <template #icon>
                  <IconPower v-if="(record as BankAccountListItem).status === 1" />
                  <IconPlayerPlay v-else />
                </template>
                {{ (record as BankAccountListItem).status === 1 ? '停用' : '启用' }}
              </a-button>
            </a-popconfirm>
            <a-popconfirm
              v-if="auth.hasPermission('bankAccounts.delete')"
              type="warning"
              content="确认删除该资金账户？"
              @ok="onDelete(record as BankAccountListItem)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
                :loading="deletingId === (record as BankAccountListItem).id"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <BankAccountFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      @saved="onSaved"
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

.summary-row {
  margin: 0;
}

/* 操作列密度（specs/011-action-column §0）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

/* 操作列兜底：按钮组不折行 */
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

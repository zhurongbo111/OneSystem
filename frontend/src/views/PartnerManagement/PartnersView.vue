<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getPartners, updatePartnerStatus } from '@/api/partner'
import type { Partner, PartnerStatus, PartnerType } from '@/api/partner'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconEdit,
  IconEye,
  IconPlayerPlay,
  IconPlus,
  IconPower,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSettings,
} from '@tabler/icons-vue'

import PartnerFormDrawer from './PartnerFormDrawer.vue'

// —— constants ——
const typeOptions = [
  { label: '供应商', value: 1 },
  { label: '客户', value: 2 },
  { label: '两者', value: 3 },
]

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
]

/** 类型 → tag 颜色（design §4.4：供应商蓝 / 客户绿 / 两者紫） */
const typeTagColor: Record<PartnerType, string> = {
  1: 'blue',
  2: 'green',
  3: 'purple',
}

const typeTagLabel: Record<PartnerType, string> = {
  1: '供应商',
  2: '客户',
  3: '两者',
}

const columnOptions = [
  { label: '名称', value: 'name' },
  { label: '类型', value: 'type' },
  { label: '联系人', value: 'contact' },
  { label: '电话', value: 'phone' },
  { label: '地址', value: 'address' },
  { label: '状态', value: 'status' },
  { label: '创建时间', value: 'createdAt' },
]

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在启停的往来单位 id：行内按钮 loading 与写操作互斥用 */
const togglingId = ref<string | undefined>(undefined)
const items = ref<Partner[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 类型 / 状态：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const typeInput = ref<PartnerType | undefined>(undefined)
const statusInput = ref<PartnerStatus | undefined>(undefined)
const appliedKeyword = ref('')
const appliedType = ref<PartnerType | undefined>(undefined)
const appliedStatus = ref<PartnerStatus | undefined>(undefined)

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'name',
  'type',
  'contact',
  'phone',
  'address',
  'status',
  'createdAt',
])

/** 新增 / 编辑 / 详情抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit' | 'view'>('create')
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

/** 依据列显示设置动态拼列（序号与操作列固定显示） */
const columns = computed<TableColumnData[]>(() => {
  const cols: TableColumnData[] = [{ title: '序号', slotName: 'seq', width: 64, align: 'center' }]
  if (visibleColumns.value.includes('name')) {
    cols.push({ title: '名称', dataIndex: 'name', width: 180, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('type')) {
    cols.push({ title: '类型', slotName: 'type', width: 90, align: 'center' })
  }
  if (visibleColumns.value.includes('contact')) {
    cols.push({ title: '联系人', dataIndex: 'contact', width: 100 })
  }
  if (visibleColumns.value.includes('phone')) {
    cols.push({ title: '电话', dataIndex: 'phone', width: 130 })
  }
  if (visibleColumns.value.includes('address')) {
    cols.push({ title: '地址', dataIndex: 'address', width: 200, ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', slotName: 'status', width: 80, align: 'center' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', slotName: 'createdAt', width: 172 })
  }
  // 操作列：3 个操作 ≤ 3 平铺（编辑 / 停用或启用 / 详情），宽度按实测取 210（specs/action-column §2）
  cols.push({ title: '操作', slotName: 'action', width: 210, bodyCellClass: 'action-cell' })
  return cols
})

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = computed(() => columns.value.reduce((sum, c) => sum + (c.width ?? 0), 0))

// —— lifecycle ——
onMounted(() => {
  void fetchList()
})

// —— methods ——
/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getPartners({
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

/** 刷新当前页 */
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

/** 新增（抽屉） */
function onCreate(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉） */
function onEdit(row: Partner): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 详情（抽屉查看态） */
function onDetail(row: Partner): void {
  drawerMode.value = 'view'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 启用 / 停用 */
async function onToggleStatus(row: Partner): Promise<void> {
  if (togglingId.value) return
  const next: PartnerStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updatePartnerStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已停用')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        往来单位
      </h1>
    </div>

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
              class="filter-bar__search"
              placeholder="搜索单位名称或联系人"
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
              class="filter-bar__type"
              :options="typeOptions"
              placeholder="全部类型"
              allow-clear
            />
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

        <!-- 操作行：左组主操作（新增）靠左，右组视图操作（列设置/刷新）靠右，同一行 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
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
            <a-dropdown trigger="click">
              <a-button size="small">
                <template #icon>
                  <IconSettings />
                </template>
                列设置
              </a-button>
              <template #content>
                <div class="col-settings">
                  <a-checkbox-group v-model="visibleColumns">
                    <a-space direction="vertical">
                      <a-checkbox
                        v-for="opt in columnOptions"
                        :key="opt.value"
                        :value="opt.value"
                      >
                        {{ opt.label }}
                      </a-checkbox>
                    </a-space>
                  </a-checkbox-group>
                </div>
              </template>
            </a-dropdown>
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
          <a-tag :color="typeTagColor[(record as Partner).type]">
            {{ typeTagLabel[(record as Partner).type] }}
          </a-tag>
        </template>
        <template #status="{ record }">
          <a-tag :color="(record as Partner).status === 1 ? 'green' : 'red'">
            {{ (record as Partner).status === 1 ? '启用' : '停用' }}
          </a-tag>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as Partner).createdAt) }}
        </template>
        <!-- 操作列（specs/action-column）：3 个操作 ≤ 3，平铺 编辑 / 停用或启用 / 详情 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onEdit(record as Partner)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              type="warning"
              :content="`确认${(record as Partner).status === 1 ? '停用' : '启用'}该往来单位？`"
              @ok="onToggleStatus(record as Partner)"
            >
              <a-button
                type="text"
                :status="(record as Partner).status === 1 ? 'warning' : 'normal'"
                size="small"
                :loading="togglingId === (record as Partner).id"
              >
                <template #icon>
                  <IconPower v-if="(record as Partner).status === 1" />
                  <IconPlayerPlay v-else />
                </template>
                {{ (record as Partner).status === 1 ? '停用' : '启用' }}
              </a-button>
            </a-popconfirm>
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as Partner)"
            >
              <template #icon>
                <IconEye />
              </template>
              详情
            </a-button>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <PartnerFormDrawer
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

/* 操作列密度（specs/action-column §5）：收窄 Arco 文本按钮默认水平 padding */
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

.col-settings {
  min-width: 160px;
  padding: 8px 12px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-small);
  box-shadow: var(--box-shadow-2);
}
</style>

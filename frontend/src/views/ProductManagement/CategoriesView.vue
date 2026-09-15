<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { deleteCategory, getCategoriesPaged } from '@/api/product'
import type { Category } from '@/api/product'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconDelete,
  IconEdit,
  IconPlus,
  IconRefresh,
  IconSearch,
  IconUndo,
} from '@arco-design/web-vue/es/icon'

import CategoryFormDrawer from './CategoryFormDrawer.vue'

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
const items = ref<Category[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词：输入态与已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const appliedKeyword = ref('')

/** 行内删除中的分类 id */
const deletingCategoryId = ref<string | undefined>(undefined)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)
const drawerEditName = ref('')

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => appliedKeyword.value)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

/** 表格列（序号 + 名称 + 创建时间 + 操作） */
const columns = computed<TableColumnData[]>(() => [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '分类名称', dataIndex: 'name', width: 240, ellipsis: true, tooltip: true },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  // 操作列：2 个操作 ≤ 3 平铺（编辑 / 删除），宽度 150（specs/action-column §2）
  { title: '操作', slotName: 'action', width: 150, bodyCellClass: 'action-cell' },
])

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
    const result = await getCategoriesPaged({
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

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  appliedKeyword.value = ''
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
  drawerEditName.value = ''
  drawerVisible.value = true
}

/** 编辑（抽屉，分类无独立详情接口，名称取当前行回填） */
function onEdit(row: Category): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerEditName.value = row.name
  drawerVisible.value = true
}

/** 抽屉保存成功：刷新当前列表 */
function onDrawerSaved(): void {
  void fetchList()
}

/** 删除分类（被商品引用时后端返回 40106，统一错误提示） */
async function onDelete(row: Category): Promise<void> {
  if (deletingCategoryId.value) return
  deletingCategoryId.value = row.id
  try {
    await deleteCategory(row.id)
    Message.success('分类已删除')
    // 删空当前页时回退到上一页，避免停留在空页
    if (items.value.length === 1 && page.value > 1) {
      page.value -= 1
    }
    await fetchList()
  } catch {
    // 错误提示已由请求层统一处理（40106 有引用 / 40400 不存在）
  } finally {
    deletingCategoryId.value = undefined
  }
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        分类管理
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
              placeholder="搜索分类名称"
              allow-clear
              @press-enter="onSearch"
            >
              <template #prefix>
                <IconSearch />
              </template>
            </a-input>
          </a-col>
          <a-col :span="4">
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
                  <IconUndo />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：左组主操作（新增）靠左，右组视图操作（刷新）靠右，同一行 -->
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
        <template #createdAt="{ record }">
          {{ formatDateTime((record as Category).createdAt) }}
        </template>
        <!-- 操作列（specs/action-column）：2 个操作 ≤ 3，平铺 编辑 / 删除 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              type="text"
              size="small"
              @click="onEdit(record as Category)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              type="warning"
              content="确认删除该分类？已被商品引用的分类无法删除"
              @ok="onDelete(record as Category)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
                :loading="deletingCategoryId === (record as Category).id"
              >
                <template #icon>
                  <IconDelete />
                </template>
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <CategoryFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      :edit-name="drawerEditName"
      @saved="onDrawerSaved"
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

.toolbar-filter .arco-col > .arco-input {
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

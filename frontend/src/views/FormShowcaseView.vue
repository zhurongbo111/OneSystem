<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  ORDER_STATUS_COLOR,
  ORDER_STATUS_LABEL,
  ORDER_STATUS_OPTIONS,
  formatAmount,
  useOrderData,
  type OrderRow,
} from '@/composables/useOrderStore'
import OrderFormDrawer from '@/views/FormShowcase/components/OrderFormDrawer.vue'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconPlus, IconRotateLeft, IconSearch } from '@arco-design/web-vue/es/icon'

// —— constants ——
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '订单号', dataIndex: 'orderNo', width: 170 },
  { title: '客户', dataIndex: 'customer', width: 110 },
  { title: '商品', dataIndex: 'product', ellipsis: true, tooltip: true },
  { title: '金额', dataIndex: 'amount', width: 110, slotName: 'amount' },
  { title: '状态', dataIndex: 'status', width: 100, slotName: 'status' },
  { title: '创建时间', dataIndex: 'createdAt', width: 120 },
  { title: '操作', slotName: 'action', width: 220 },
]

const pagination = {
  defaultPageSize: 10,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}

const router = useRouter()
const { orders, remove } = useOrderData()

// —— reactive state ——
/** 关键词：输入态 / 已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const appliedKeyword = ref('')
/** 状态筛选：输入态 / 已应用态 */
const statusInput = ref<string | undefined>(undefined)
const appliedStatus = ref<string | undefined>(undefined)

/** 抽屉表单状态（新增/编辑共用） */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

// —— computed ——
const tableData = computed<OrderRow[]>(() => {
  const kw = appliedKeyword.value.trim().toLowerCase()
  return orders.value.filter((o) => {
    const okKw = !kw || o.orderNo.toLowerCase().includes(kw) || o.customer.toLowerCase().includes(kw)
    const okSt = !appliedStatus.value || o.status === appliedStatus.value
    return okKw && okSt
  })
})

/** 表格重挂载 key：条件变化回第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedStatus.value ?? ''}`)

// —— methods ——
function onSearch(): void {
  appliedKeyword.value = keywordInput.value
  appliedStatus.value = statusInput.value
}

function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  appliedKeyword.value = ''
  appliedStatus.value = undefined
}

/** 新增（抽屉形态） */
function onCreateDrawer(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerVisible.value = true
}

/** 新增（独立页面形态） */
function onPageCreate(): void {
  void router.push({ name: 'formNew' })
}

/** 编辑（形态菜单）：用抽屉表单 */
function onEditByDrawer(row: OrderRow): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 编辑（形态菜单）：用独立页面表单 */
function onEditByPage(row: OrderRow): void {
  void router.push({ name: 'formEdit', params: { id: row.id } })
}

/** 编辑形态菜单选择：drawer 用抽屉表单，page 用独立页面表单 */
function onEditFormSelect(key: string | number | Record<string, unknown> | undefined, row: OrderRow): void {
  if (key === 'drawer') onEditByDrawer(row)
  else onEditByPage(row)
}

/** 查看：进统一详情页 */
function onDetail(row: OrderRow): void {
  void router.push({ name: 'formDetail', params: { id: row.id } })
}

/** 删除（Popconfirm 确认后） */
function onDelete(row: OrderRow): void {
  remove(row.id)
  Message.success('已删除 1 条')
}
</script>

<template>
  <div class="list-page">
    <div class="page-header">
      <h1 class="page-title">
        订单列表（表单与详情示例）
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
              placeholder="搜索订单号或客户"
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
              :options="ORDER_STATUS_OPTIONS"
              placeholder="状态"
              allow-clear
            />
          </a-col>
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                @click="onSearch"
              >
                <template #icon>
                  <IconSearch />
                </template>
                搜索
              </a-button>
              <a-button @click="onReset">
                <template #icon>
                  <IconRotateLeft />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：主操作（两种新增形态）靠左，本页无视图操作 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              type="primary"
              size="small"
              @click="onCreateDrawer"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增·抽屉
            </a-button>
            <a-button
              size="small"
              @click="onPageCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增·页面
            </a-button>
          </div>
        </div>
      </div>

      <a-table
        :key="tableKey"
        row-key="id"
        :columns="columns"
        :data="tableData"
        :pagination="pagination"
      >
        <template #seq="{ rowIndex }">
          {{ rowIndex + 1 }}
        </template>
        <template #amount="{ record }">
          {{ formatAmount((record as OrderRow).amount) }}
        </template>
        <template #status="{ record }">
          <a-tag :color="ORDER_STATUS_COLOR[(record as OrderRow).status]">
            {{ ORDER_STATUS_LABEL[(record as OrderRow).status] }}
          </a-tag>
        </template>
        <template #action="{ record }">
          <a-space :size="4">
            <a-button
              type="text"
              size="small"
              @click="onDetail(record)"
            >
              查看
            </a-button>
            <a-dropdown
              trigger="click"
              @select="(key: string | number | Record<string, unknown> | undefined) => onEditFormSelect(key, record as OrderRow)"
            >
              <a-button
                type="text"
                size="small"
              >
                编辑
              </a-button>
              <template #content>
                <a-doption value="drawer">
                  用抽屉
                </a-doption>
                <a-doption value="page">
                  用页面
                </a-doption>
              </template>
            </a-dropdown>
            <a-popconfirm
              type="warning"
              content="确认删除该订单？"
              @ok="onDelete(record)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
              >
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <a-alert
      type="info"
      class="example-hint"
    >
      本示例：同一列表提供两种表单形态——字段较少的抽屉表单，与含商品明细子表格的独立页面表单；详情页为统一独立页面。
    </a-alert>

    <OrderFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
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

.example-hint {
  max-width: 720px;
}
</style>

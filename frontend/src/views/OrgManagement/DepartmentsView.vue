<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { deleteDepartment, getDepartments, updateDepartmentStatus } from '@/api/department'
import type { DepartmentTreeNode } from '@/api/department'
import { useAuthStore } from '@/stores/auth'
import { Message, Modal } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconDotsVertical,
  IconEdit,
  IconGitBranch,
  IconPlayerPlay,
  IconPlus,
  IconPower,
  IconRefresh,
  IconTrash,
  IconZoomIn,
  IconZoomOut,
} from '@tabler/icons-vue'

import DepartmentFormDrawer from './DepartmentFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
/**
 * 列定义：树列（部门名称）必须为第 1 列——Arco 表格的展开图标只渲染在第 1 列
 * （`es/table/table.js` 中 `index === 0` 才传 `showExpandBtn`），故树形表格不设序号列。
 */
const columns: TableColumnData[] = [
  { title: '部门名称', dataIndex: 'name', width: 240, ellipsis: true, tooltip: true },
  { title: '编码', dataIndex: 'code', width: 140 },
  { title: '排序', dataIndex: 'sortOrder', width: 80, align: 'center' },
  { title: '在职人数', dataIndex: 'employeeCount', width: 96, align: 'center' },
  { title: '状态', slotName: 'status', width: 90, align: 'center' },
  { title: '备注', dataIndex: 'remark', width: 200, ellipsis: true, tooltip: true },
  // 操作列：4 个操作 > 3，平铺「新增下级 / 编辑 / 删除」（主操作 + 危险操作优先），启停收纳进「更多」
  { title: '操作', slotName: 'action', width: 260, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— reactive state ——
const loading = ref(false)
/** 正在启停的部门 id（行内写操作 loading 与互斥用） */
const togglingId = ref<string | undefined>(undefined)
/** 正在删除的部门 id */
const deletingId = ref<string | undefined>(undefined)
const tree = ref<DepartmentTreeNode[]>([])
/** 展开的部门 id 集合（受控，支撑「展开全部 / 收起全部」） */
const expandedKeys = ref<string[]>([])

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)
/** 新增下级时的默认上级 id（新增顶级为 undefined） */
const drawerParentId = ref<string | undefined>(undefined)

// —— computed ——
/** 全部有子部门的部门 id（展开全部用） */
const expandableKeys = computed<string[]>(() => {
  const keys: string[] = []
  const collect = (nodes: DepartmentTreeNode[]): void => {
    nodes.forEach((node) => {
      if (node.children.length > 0) {
        keys.push(node.id)
        collect(node.children)
      }
    })
  }
  collect(tree.value)
  return keys
})

// —— lifecycle ——
onMounted(() => {
  void fetchTree()
})

// —— methods ——
/** 拉取部门树（全量，含各节点在职人数） */
async function fetchTree(): Promise<void> {
  loading.value = true
  try {
    tree.value = await getDepartments()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

/** 刷新（保留当前展开态） */
function onRefresh(): void {
  void fetchTree()
}

function onExpandAll(): void {
  expandedKeys.value = expandableKeys.value
}

function onCollapseAll(): void {
  expandedKeys.value = []
}

/** 新增顶级部门 */
function onCreateRoot(): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerParentId.value = undefined
  drawerVisible.value = true
}

/** 新增下级部门 */
function onCreateChild(row: DepartmentTreeNode): void {
  drawerMode.value = 'create'
  drawerEditId.value = undefined
  drawerParentId.value = row.id
  drawerVisible.value = true
  // 新增下级后展开该节点，便于看到结果
  if (!expandedKeys.value.includes(row.id)) {
    expandedKeys.value = [...expandedKeys.value, row.id]
  }
}

/** 编辑部门 */
function onEdit(row: DepartmentTreeNode): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerParentId.value = undefined
  drawerVisible.value = true
}

/** 启用 / 停用 */
async function onToggleStatus(row: DepartmentTreeNode): Promise<void> {
  if (togglingId.value) return
  const next = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateDepartmentStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已停用')
    void fetchTree()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 删除（有子部门或员工时后端拒绝并提示 40140） */
async function onDelete(row: DepartmentTreeNode): Promise<void> {
  if (deletingId.value) return
  deletingId.value = row.id
  try {
    await deleteDepartment(row.id)
    Message.success('部门已删除')
    void fetchTree()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    deletingId.value = undefined
  }
}

/** 「更多」内的启停无法用 a-popconfirm 包裹菜单项，改用函数式确认框（等价二次确认，specs/011-action-column §0） */
function confirmToggleStatus(row: DepartmentTreeNode): void {
  Modal.warning({
    title: row.status === 1 ? '停用部门' : '启用部门',
    content: `确认${row.status === 1 ? '停用' : '启用'}部门「${row.name}」？停用后不参与新增下级与员工选择`,
    hideCancel: false,
    okText: '确认',
    onOk: () => onToggleStatus(row),
  })
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        部门管理
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <!-- 操作行：左组主操作（新增顶级部门）靠左，右组视图操作（展开 / 收起 / 刷新）靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('departments.create')"
              type="primary"
              size="small"
              @click="onCreateRoot"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增顶级部门
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              size="small"
              @click="onExpandAll"
            >
              <template #icon>
                <IconZoomIn />
              </template>
              展开全部
            </a-button>
            <a-button
              size="small"
              @click="onCollapseAll"
            >
              <template #icon>
                <IconZoomOut />
              </template>
              收起全部
            </a-button>
            <a-divider
              direction="vertical"
              class="toolbar-actions__divider"
            />
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

      <!-- 部门树：全量返回（组织量级小），不分页 -->
      <a-table
        v-model:expanded-keys="expandedKeys"
        row-key="id"
        children-key="children"
        :loading="loading"
        :columns="columns"
        :data="tree"
        :pagination="false"
        :scroll="{ x: tableScrollX }"
      >
        <template #status="{ record }">
          <a-tag :color="(record as DepartmentTreeNode).status === 1 ? 'green' : 'red'">
            {{ (record as DepartmentTreeNode).status === 1 ? '启用' : '停用' }}
          </a-tag>
        </template>
        <!-- 操作列（specs/011-action-column）：4 个操作，平铺 新增下级 / 编辑 / 删除，启停入「更多」 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('departments.create')"
              type="text"
              size="small"
              @click="onCreateChild(record as DepartmentTreeNode)"
            >
              <template #icon>
                <IconGitBranch />
              </template>
              新增下级
            </a-button>
            <a-button
              v-if="auth.hasPermission('departments.update')"
              type="text"
              size="small"
              @click="onEdit(record as DepartmentTreeNode)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('departments.delete')"
              type="warning"
              content="确认删除该部门？存在子部门或员工时不可删除"
              @ok="onDelete(record as DepartmentTreeNode)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
                :loading="deletingId === (record as DepartmentTreeNode).id"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
            <a-dropdown
              v-if="auth.hasPermission('departments.status')"
              trigger="click"
            >
              <a-button
                type="text"
                size="small"
                aria-label="更多操作"
              >
                <template #icon>
                  <IconDotsVertical />
                </template>
              </a-button>
              <template #content>
                <a-doption
                  :disabled="!!togglingId"
                  @click="confirmToggleStatus(record as DepartmentTreeNode)"
                >
                  <template #icon>
                    <IconPower v-if="(record as DepartmentTreeNode).status === 1" />
                    <IconPlayerPlay v-else />
                  </template>
                  {{ (record as DepartmentTreeNode).status === 1 ? '停用' : '启用' }}
                </a-doption>
              </template>
            </a-dropdown>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <DepartmentFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      :default-parent-id="drawerParentId"
      :tree="tree"
      @saved="fetchTree"
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

/* 操作列密度（specs/011-action-column §0）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

.row-actions :deep(.arco-btn-only-icon) {
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

.toolbar-actions__divider {
  margin: 0;
}

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

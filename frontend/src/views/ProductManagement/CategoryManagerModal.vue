<script setup lang="ts">
import { ref, watch } from 'vue'

import { createCategory, deleteCategory, getCategories, updateCategory } from '@/api/product'
import type { Category } from '@/api/product'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconCheck, IconClose, IconDelete, IconEdit, IconPlus } from '@arco-design/web-vue/es/icon'

// —— props/emits ——
const props = defineProps<{
  /** 弹窗可见性（v-model） */
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  /** 分类列表有变化（新增 / 编辑 / 删除），父级刷新自己的分类数据源 */
  (e: 'saved'): void
  /** 新建分类成功，父级可回填选中新分类 */
  (e: 'created', category: Category): void
}>()

// —— reactive state ——
const loading = ref(false)
const categories = ref<Category[]>([])
const newName = ref('')
/** 新增 / 编辑保存中（行内保存按钮 loading） */
const categorySubmitting = ref(false)
/** 正在删除的分类 id（popconfirm 期间行内删除按钮 loading） */
const deletingCategoryId = ref<string | undefined>(undefined)
/** 行内编辑：当前编辑行 id 与输入值 */
const editingId = ref<string | undefined>(undefined)
const editingName = ref('')

// —— constants ——
const columns: TableColumnData[] = [
  { title: '分类名称', slotName: 'name', width: 200 },
  { title: '创建时间', slotName: 'createdAt', width: 172 },
  { title: '操作', slotName: 'action', width: 180, bodyCellClass: 'action-cell' },
]

// —— watch ——
/** 打开弹窗时重置交互态并加载列表 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    newName.value = ''
    editingId.value = undefined
    editingName.value = ''
    void fetchCategories()
  },
)

// —— methods ——
/** 加载分类列表（创建时间正序，由后端保证） */
async function fetchCategories(): Promise<void> {
  loading.value = true
  try {
    categories.value = await getCategories()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

/** 关闭弹窗 */
function onClose(): void {
  emit('update:visible', false)
}

/** 行内新增分类 */
async function onCreate(): Promise<void> {
  const name = newName.value.trim()
  if (!name) {
    Message.warning('请输入分类名称')
    return
  }
  if (categorySubmitting.value) return
  categorySubmitting.value = true
  try {
    const created = await createCategory(name)
    Message.success('分类已创建')
    newName.value = ''
    emit('created', created)
    emit('saved')
    await fetchCategories()
  } catch {
    // 错误提示已由请求层统一处理（重名 40105）
  } finally {
    categorySubmitting.value = false
  }
}

/** 进入行内编辑 */
function onStartEdit(row: Category): void {
  if (editingId.value) return
  editingId.value = row.id
  editingName.value = row.name
}

/** 退出行内编辑（取消） */
function onCancelEdit(): void {
  editingId.value = undefined
  editingName.value = ''
}

/** 保存行内编辑 */
async function onEditSave(row: Category): Promise<void> {
  const name = editingName.value.trim()
  if (!name) {
    Message.warning('请输入分类名称')
    return
  }
  if (categorySubmitting.value) return
  categorySubmitting.value = true
  try {
    await updateCategory(row.id, name)
    Message.success('分类已更新')
    editingId.value = undefined
    editingName.value = ''
    emit('saved')
    await fetchCategories()
  } catch {
    // 错误提示已由请求层统一处理（重名 40105）
  } finally {
    categorySubmitting.value = false
  }
}

/** 删除分类（被商品引用时后端返回 40106，统一错误提示） */
async function onDelete(row: Category): Promise<void> {
  if (deletingCategoryId.value) return
  deletingCategoryId.value = row.id
  try {
    await deleteCategory(row.id)
    Message.success('分类已删除')
    emit('saved')
    await fetchCategories()
  } catch {
    // 错误提示已由请求层统一处理（40106 有引用 / 40400 不存在）
  } finally {
    deletingCategoryId.value = undefined
  }
}
</script>

<template>
  <a-modal
    :visible="props.visible"
    title="分类管理"
    :width="560"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <div class="category-manager">
      <!-- 行内新增 -->
      <div class="category-manager__add">
        <a-input
          v-model="newName"
          class="category-manager__add-input"
          placeholder="输入新分类名称（1-20 字符）"
          allow-clear
          @press-enter="onCreate"
        />
        <a-button
          type="primary"
          :loading="categorySubmitting"
          @click="onCreate"
        >
          <template #icon>
            <IconPlus />
          </template>
          新增
        </a-button>
      </div>

      <a-table
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="categories"
        :pagination="false"
        size="small"
      >
        <template #name="{ record }">
          <template v-if="editingId === (record as Category).id">
            <div class="category-manager__edit">
              <a-input
                v-model="editingName"
                class="category-manager__edit-input"
                allow-clear
                @press-enter="onEditSave(record as Category)"
              />
              <a-button
                type="text"
                size="small"
                :loading="categorySubmitting"
                @click="onEditSave(record as Category)"
              >
                <template #icon>
                  <IconCheck />
                </template>
                保存
              </a-button>
              <a-button
                type="text"
                size="small"
                @click="onCancelEdit"
              >
                <template #icon>
                  <IconClose />
                </template>
                取消
              </a-button>
            </div>
          </template>
          <span v-else>{{ (record as Category).name }}</span>
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as Category).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="editingId !== (record as Category).id"
              type="text"
              size="small"
              @click="onStartEdit(record as Category)"
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
    </div>
  </a-modal>
</template>

<style scoped>
.category-manager {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.category-manager__add {
  display: flex;
  gap: 8px;
}

.category-manager__add-input {
  flex: 1;
}

.category-manager__edit {
  display: flex;
  align-items: center;
  gap: 4px;
}

.category-manager__edit-input {
  width: 160px;
}

/* 操作列密度（同 UsersView）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

:deep(.action-cell) {
  white-space: nowrap;
}
</style>

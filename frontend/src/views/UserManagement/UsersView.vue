<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getUsers, resetUserPassword, updateUserStatus } from '@/api/user'
import type { UserListItem, UserStatus } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance, TableColumnData } from '@arco-design/web-vue'
import { IconPlus, IconRefresh, IconSearch, IconSettings } from '@arco-design/web-vue/es/icon'

import UserFormDrawer from './UserFormDrawer.vue'

// —— types ——
interface ResetPasswordForm {
  newPassword: string
}

// —— constants ——
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '禁用', value: 0 },
]

const columnOptions = [
  { label: '用户名', value: 'username' },
  { label: '显示名', value: 'displayName' },
  { label: '邮箱', value: 'email' },
  { label: '手机号', value: 'phone' },
  { label: '状态', value: 'status' },
  { label: '最近登录时间', value: 'lastLoginAt' },
  { label: '创建时间', value: 'createdAt' },
]

const resetRules: Record<string, FieldRule[]> = {
  newPassword: [
    { required: true, message: '请输入新密码' },
    { minLength: 6, maxLength: 32, message: '密码长度必须在 6 到 32 之间' },
  ],
}

const router = useRouter()

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在启停的用户 id：行内按钮 loading 与写操作互斥用 */
const togglingId = ref<string | undefined>(undefined)
const items = ref<UserListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 关键词 / 状态：输入态 与 已应用态分离（点搜索才生效） */
const keywordInput = ref('')
const statusInput = ref<UserStatus | undefined>(undefined)
const appliedKeyword = ref('')
const appliedStatus = ref<UserStatus | undefined>(undefined)

/** 列显示设置（不持久化） */
const visibleColumns = ref<string[]>([
  'username',
  'displayName',
  'email',
  'phone',
  'status',
  'lastLoginAt',
  'createdAt',
])

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerEditId = ref<string | undefined>(undefined)

/** 重置密码模态 */
const resetVisible = ref(false)
const resetSubmitting = ref(false)
const resetFormRef = ref<FormInstance>()
const resetTarget = ref<UserListItem | null>(null)
const resetForm = reactive<ResetPasswordForm>({ newPassword: '' })

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(() => `${appliedKeyword.value}|${appliedStatus.value ?? ''}`)

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
  if (visibleColumns.value.includes('username')) {
    cols.push({ title: '用户名', dataIndex: 'username', width: 140 })
  }
  if (visibleColumns.value.includes('displayName')) {
    cols.push({ title: '显示名', dataIndex: 'displayName', width: 120 })
  }
  if (visibleColumns.value.includes('email')) {
    cols.push({ title: '邮箱', dataIndex: 'email', ellipsis: true, tooltip: true })
  }
  if (visibleColumns.value.includes('phone')) {
    cols.push({ title: '手机号', dataIndex: 'phone', width: 140 })
  }
  if (visibleColumns.value.includes('status')) {
    cols.push({ title: '状态', dataIndex: 'status', width: 90, slotName: 'status' })
  }
  if (visibleColumns.value.includes('lastLoginAt')) {
    cols.push({ title: '最近登录时间', dataIndex: 'lastLoginAt', width: 180, slotName: 'lastLoginAt' })
  }
  if (visibleColumns.value.includes('createdAt')) {
    cols.push({ title: '创建时间', dataIndex: 'createdAt', width: 180, slotName: 'createdAt' })
  }
  cols.push({ title: '操作', slotName: 'action', width: 260 })
  return cols
})

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
    const result = await getUsers({
      keyword: appliedKeyword.value.trim() || undefined,
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
  appliedStatus.value = statusInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  keywordInput.value = ''
  statusInput.value = undefined
  appliedKeyword.value = ''
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
function onEdit(row: UserListItem): void {
  drawerMode.value = 'edit'
  drawerEditId.value = row.id
  drawerVisible.value = true
}

/** 详情（独立页面） */
function onDetail(row: UserListItem): void {
  void router.push({ name: 'userDetail', params: { id: row.id } })
}

/** 启用 / 禁用 */
async function onToggleStatus(row: UserListItem): Promise<void> {
  if (togglingId.value) return
  const next: UserStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateUserStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已禁用')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}

/** 打开重置密码模态 */
function onOpenResetPassword(row: UserListItem): void {
  resetTarget.value = row
  resetForm.newPassword = ''
  resetVisible.value = true
}

/** 提交重置密码 */
async function onSubmitResetPassword(): Promise<void> {
  if (resetSubmitting.value || !resetTarget.value) return
  const errors = await resetFormRef.value?.validate()
  if (errors) return
  resetSubmitting.value = true
  try {
    await resetUserPassword(resetTarget.value.id, resetForm.newPassword)
    Message.success('密码已重置')
    resetVisible.value = false
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    resetSubmitting.value = false
  }
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        用户管理
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
              placeholder="搜索用户名或显示名"
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
          <a-col :span="8">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                :loading="loading"
                @click="onSearch"
              >
                搜索
              </a-button>
              <a-button
                :loading="loading"
                @click="onReset"
              >
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行 -->
        <div class="toolbar-actions">
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
          <a-divider
            direction="vertical"
            class="toolbar-actions__divider"
          />
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
        <template #status="{ record }">
          <a-tag :color="(record as UserListItem).status === 1 ? 'green' : 'red'">
            {{ (record as UserListItem).status === 1 ? '启用' : '禁用' }}
          </a-tag>
        </template>
        <template #lastLoginAt="{ record }">
          {{ formatDateTime((record as UserListItem).lastLoginAt) }}
        </template>
        <template #createdAt="{ record }">
          {{ formatDateTime((record as UserListItem).createdAt) }}
        </template>
        <template #action="{ record }">
          <a-space :size="4">
            <a-button
              type="text"
              size="small"
              @click="onEdit(record as UserListItem)"
            >
              编辑
            </a-button>
            <a-button
              type="text"
              size="small"
              @click="onOpenResetPassword(record as UserListItem)"
            >
              重置密码
            </a-button>
            <a-popconfirm
              type="warning"
              :content="`确认${(record as UserListItem).status === 1 ? '禁用' : '启用'}该用户？`"
              @ok="onToggleStatus(record as UserListItem)"
            >
              <a-button
                type="text"
                size="small"
                :loading="togglingId === (record as UserListItem).id"
              >
                {{ (record as UserListItem).status === 1 ? '禁用' : '启用' }}
              </a-button>
            </a-popconfirm>
            <a-button
              type="text"
              size="small"
              @click="onDetail(record as UserListItem)"
            >
              详情
            </a-button>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <UserFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :edit-id="drawerEditId"
      @saved="fetchList"
    />

    <a-modal
      v-model:visible="resetVisible"
      title="重置密码"
      :ok-loading="resetSubmitting"
      @ok="onSubmitResetPassword"
    >
      <a-form
        ref="resetFormRef"
        :model="resetForm"
        :rules="resetRules"
        layout="vertical"
      >
        <a-form-item
          :label="`为 ${resetTarget?.username ?? ''} 设置新密码`"
          field="newPassword"
        >
          <a-input-password
            v-model="resetForm.newPassword"
            placeholder="请输入 6-32 位新密码"
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-modal>
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
  justify-content: flex-end;
  gap: 8px;
  margin-bottom: 8px;
}

.toolbar-actions__divider {
  margin: 0;
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

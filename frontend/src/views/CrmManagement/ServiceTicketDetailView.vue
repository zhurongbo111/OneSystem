<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { Component } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import {
  allowedTicketTransitions,
  assignServiceTicket,
  getServiceTicket,
  ticketPriorityColor,
  ticketPriorityLabel,
  ticketStatusColor,
  ticketStatusLabel,
  ticketTransitionLabel,
  updateServiceTicketStatus,
} from '@/api/serviceTicket'
import type { ServiceTicketDetail, TicketStatus } from '@/api/serviceTicket'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message, Modal } from '@arco-design/web-vue'
import { IconArchive, IconCircleCheck, IconEdit, IconPlayerPlay, IconRefresh, IconRotate, IconUserCheck } from '@tabler/icons-vue'

// —— constants ——
/** 已关闭（终态）：只读，不可编辑 / 改状态 / 指派 */
const CLOSED_STATUS: TicketStatus = 3

/** 状态流转动作的展示顺序（受理 / 重开 → 解决 → 关闭） */
const TRANSITION_ORDER: TicketStatus[] = [1, 2, 3]

// —— reactive state ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<ServiceTicketDetail>()

/** 负责人下拉数据源（仅在职员工） */
const employees = ref<Employee[]>([])
const assignOwnerId = ref<string | undefined>(undefined)
/** 正在改状态 / 指派（前端规则 §4.6：statusSubmitting / assignSubmitting） */
const statusSubmitting = ref(false)
const assignSubmitting = ref(false)

// —— computed ——
/** 已关闭：整页只读 */
const isClosed = computed(() => detail.value?.status === CLOSED_STATUS)

/** 可推进的目标状态（按 design.md §0.1 白名单派生，已关闭为空） */
const availableTransitions = computed<TicketStatus[]>(() => {
  if (!detail.value || isClosed.value) return []
  const allowed = allowedTicketTransitions(detail.value.status)
  return TRANSITION_ORDER.filter((target) => allowed.includes(target))
})

const ownerOptions = computed(() => employees.value.map((e) => ({ label: e.name, value: e.id })))

/** 状态流转按钮图标（按动作语义取，Tabler 首选，前端规则 §4.7） */
const transitionIcons: Record<string, Component> = {
  受理: IconPlayerPlay,
  解决: IconCircleCheck,
  重开: IconRotate,
  关闭: IconArchive,
}

// —— lifecycle ——
onMounted(async () => {
  await Promise.all([loadDetail(), loadEmployees()])
})

// —— methods ——
async function loadDetail(): Promise<void> {
  const id = route.params.id as string
  loading.value = true
  try {
    const result = await getServiceTicket(id)
    detail.value = result
    assignOwnerId.value = result.ownerId ?? undefined
    notFound.value = false
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

/** 负责人下拉（仅在职员工） */
async function loadEmployees(): Promise<void> {
  try {
    const result = await getEmployees({ page: 1, pageSize: 100, status: 1 })
    employees.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

function goBack(): void {
  void router.push({ name: 'serviceTickets' })
}

function goEdit(): void {
  if (!detail.value) return
  void router.push({ name: 'serviceTicketCreate', query: { id: detail.value.id } })
}

/** 状态流转按钮文案（受理 / 解决 / 重开 / 关闭） */
function transitionLabel(target: TicketStatus): string {
  return detail.value ? ticketTransitionLabel(detail.value.status, target) : '变更状态'
}

/** 状态流转：关闭为终态，二次确认后执行（其余直接提交） */
function onTransition(target: TicketStatus): void {
  const label = transitionLabel(target)
  if (target !== CLOSED_STATUS) {
    void changeStatus(target)
    return
  }

  Modal.warning({
    title: `${label}工单`,
    content: `确认${label}工单 ${detail.value?.ticketNo ?? ''}？关闭后为终态，不可再编辑、改状态或指派`,
    hideCancel: false,
    okText: `确认${label}`,
    onOk: () => changeStatus(target),
  })
}

/** 状态流转（白名单由后端兜底，非法组合返回 40172） */
async function changeStatus(target: TicketStatus): Promise<void> {
  if (!detail.value || statusSubmitting.value) return
  statusSubmitting.value = true
  try {
    await updateServiceTicketStatus(detail.value.id, target)
    Message.success(`工单已${transitionLabel(target)}`)
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理（40172 非法流转 / 已关闭终态）
  } finally {
    statusSubmitting.value = false
  }
}

/** 指派负责人（已关闭不可指派，40172；负责人不存在 40400） */
async function onAssign(): Promise<void> {
  if (!detail.value || assignSubmitting.value || !assignOwnerId.value) {
    if (!assignOwnerId.value) Message.warning('请选择负责人')
    return
  }
  assignSubmitting.value = true
  try {
    await assignServiceTicket(detail.value.id, assignOwnerId.value)
    Message.success('工单已指派')
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    assignSubmitting.value = false
  }
}
</script>

<template>
  <div class="detail-page">
    <a-result
      v-if="notFound"
      status="404"
      title="工单不存在"
      subtitle="该工单可能已被删除，或链接有误"
    >
      <template #extra>
        <a-button
          type="primary"
          @click="goBack"
        >
          返回列表
        </a-button>
      </template>
    </a-result>

    <template v-else>
      <a-page-header
        :title="detail ? `服务工单 ${detail.ticketNo}` : '服务工单详情'"
        @back="goBack"
      />

      <a-card
        :bordered="false"
        :loading="loading"
      >
        <a-descriptions
          v-if="detail"
          class="detail-desc"
          :column="3"
          bordered
          size="medium"
        >
          <a-descriptions-item label="工单号">
            {{ detail.ticketNo }}
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="ticketStatusColor(detail.status)">
              {{ ticketStatusLabel(detail.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="优先级">
            <a-tag :color="ticketPriorityColor(detail.priority)">
              {{ ticketPriorityLabel(detail.priority) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="客户">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="联系人">
            {{ detail.contact || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="联系电话">
            {{ detail.phone || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="负责人">
            {{ detail.ownerName || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="解决时间">
            {{ detail.resolvedAt ? formatDateTime(detail.resolvedAt) : '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item
            label="工单标题"
            :span="3"
          >
            {{ detail.title }}
          </a-descriptions-item>
          <a-descriptions-item
            label="问题描述"
            :span="3"
          >
            {{ detail.description || '—' }}
          </a-descriptions-item>
          <a-descriptions-item
            label="备注"
            :span="3"
          >
            {{ detail.remark || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="更新时间">
            {{ formatDateTime(detail.updatedAt) }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          负责人指派
        </a-divider>
        <div class="assign-bar">
          <a-select
            v-model="assignOwnerId"
            :options="ownerOptions"
            placeholder="选择负责人（仅在职员工）"
            allow-search
            :disabled="isClosed || !auth.hasPermission('serviceTickets.assign')"
            class="assign-bar__select"
          />
          <a-button
            v-if="!isClosed && auth.hasPermission('serviceTickets.assign')"
            type="primary"
            :loading="assignSubmitting"
            @click="onAssign"
          >
            <template #icon>
              <IconUserCheck />
            </template>
            指派
          </a-button>
          <span
            v-else-if="isClosed"
            class="assign-bar__hint"
          >
            工单已关闭（终态），不可再指派
          </span>
        </div>

        <div class="detail-footer">
          <a-space>
            <a-button
              v-if="!isClosed && auth.hasPermission('serviceTickets.update')"
              @click="goEdit"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>

            <a-button
              v-for="target in availableTransitions"
              :key="target"
              :type="target === 2 ? 'primary' : undefined"
              :status="target === 3 ? 'danger' : undefined"
              :loading="statusSubmitting"
              @click="onTransition(target)"
            >
              <template #icon>
                <component :is="transitionIcons[transitionLabel(target)]" />
              </template>
              {{ transitionLabel(target) }}
            </a-button>

            <a-button
              :loading="loading"
              @click="loadDetail"
            >
              <template #icon>
                <IconRefresh />
              </template>
              刷新
            </a-button>
          </a-space>
        </div>
      </a-card>
    </template>
  </div>
</template>

<style scoped>
.detail-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.assign-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}

.assign-bar__select {
  max-width: 320px;
}

.assign-bar__hint {
  color: var(--color-text-3);
}

.detail-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}
</style>

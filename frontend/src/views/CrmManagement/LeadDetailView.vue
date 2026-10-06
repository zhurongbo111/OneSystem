<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import {
  ACTIVITY_TYPE_LABELS,
  ACTIVITY_TYPE_OPTIONS,
  LEAD_SOURCE_LABELS,
  convertLead,
  createLeadActivity,
  getLead,
  getLeadActivities,
  leadStatusColor,
  leadStatusLabel,
  updateLeadStatus,
} from '@/api/lead'
import type { ActivityItem, ActivityType, LeadDetail } from '@/api/lead'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime, nowInput, toUtcIso } from '@/utils/datetime'
import { Message, Modal } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'
import { IconArrowForwardUp, IconBan, IconPlus, IconRefresh } from '@tabler/icons-vue'

// —— types ——
interface ActivityFormState {
  type: ActivityType
  content: string
  /** 日期时间选择器绑定值：本地 `YYYY-MM-DDTHH:mm:ss` */
  activityTime: string
}

// —— constants ——
/** 终态（已转化 / 已废弃）：不可转商机 / 废弃（design.md §0.1） */
const TERMINAL_STATUSES = [2, 3]

// —— reactive state ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<LeadDetail>()
const activities = ref<ActivityItem[]>([])

/** 正在转商机 / 废弃（前端规则 §4.6：convertingId / updatingId） */
const convertingId = ref<string | undefined>(undefined)
const updatingId = ref<string | undefined>(undefined)

/** 跟进活动抽屉（新增） */
const activityDrawerVisible = ref(false)
const activityFormRef = ref<FormInstance>()
const activitySubmitting = ref(false)
const activityForm = reactive<ActivityFormState>({ type: 0, content: '', activityTime: nowInput() })

/** 活动表单校验：与后端 ActivityFieldConstraints / CreateLeadActivityRequestValidator 同源 */
const activityRules: Record<string, FieldRule[]> = {
  type: [{ required: true, message: '请选择跟进方式' }],
  content: [
    { required: true, message: '请输入跟进内容' },
    { max: 200, message: '跟进内容长度不能超过 200' },
  ],
  activityTime: [{ required: true, message: '请选择跟进时间' }],
}

// —— computed ——
/** 终态线索锁定：不可转商机 / 废弃 */
const isTerminal = computed(() => detail.value !== undefined && TERMINAL_STATUSES.includes(detail.value.status))

// —— lifecycle ——
onMounted(async () => {
  await loadDetail()
})

// —— methods ——
async function loadDetail(): Promise<void> {
  const id = route.params.id as string
  loading.value = true
  try {
    const [lead, list] = await Promise.all([getLead(id), getLeadActivities(id)])
    detail.value = lead
    activities.value = list
    notFound.value = false
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'leads' })
}

/** 转商机：一次性整转，成功后线索置「已转化」（本页刷新展示转出商机） */
async function onConvert(): Promise<void> {
  if (!detail.value || convertingId.value) return
  convertingId.value = detail.value.id
  try {
    const result = await convertLead(detail.value.id)
    Message.success(`已转商机 ${result.opportunityNo}`)
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理（40168 终态线索）
  } finally {
    convertingId.value = undefined
  }
}

function confirmConvert(): void {
  if (!detail.value) return
  Modal.warning({
    title: '转商机',
    content: `确认将线索 ${detail.value.leadNo} 转为商机？转商机后线索置「已转化」，不可再编辑 / 转商机 / 废弃`,
    hideCancel: false,
    okText: '确认转商机',
    onOk: () => onConvert(),
  })
}

/** 废弃线索：仅改状态不删数据（无库存 / 资金影响） */
async function onAbandon(): Promise<void> {
  if (!detail.value || updatingId.value) return
  updatingId.value = detail.value.id
  try {
    await updateLeadStatus(detail.value.id, 3)
    Message.success('线索已废弃')
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理（40168 终态线索）
  } finally {
    updatingId.value = undefined
  }
}

function confirmAbandon(): void {
  if (!detail.value) return
  Modal.warning({
    title: '废弃线索',
    content: `确认废弃线索 ${detail.value.leadNo}？废弃后不可恢复`,
    hideCancel: false,
    okText: '确认废弃',
    onOk: () => onAbandon(),
  })
}

function onAddActivity(): void {
  activityForm.type = 0
  activityForm.content = ''
  activityForm.activityTime = nowInput()
  activityDrawerVisible.value = true
}

/** 新增跟进活动（活动只增不改不删：提交后仅刷新时间线） */
async function onSubmitActivity(): Promise<void> {
  if (!detail.value || activitySubmitting.value) return
  const errors = await activityFormRef.value?.validate()
  if (errors) return
  activitySubmitting.value = true
  try {
    await createLeadActivity(detail.value.id, {
      type: activityForm.type,
      content: activityForm.content.trim(),
      activityTime: toUtcIso(activityForm.activityTime),
    })
    Message.success('跟进记录已添加')
    activityDrawerVisible.value = false
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    activitySubmitting.value = false
  }
}
</script>

<template>
  <div class="detail-page">
    <a-result
      v-if="notFound"
      status="404"
      title="线索不存在"
      subtitle="该线索可能已被删除，或链接有误"
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
        :title="detail ? `线索 ${detail.leadNo}` : '线索详情'"
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
          <a-descriptions-item label="线索号">
            {{ detail.leadNo }}
          </a-descriptions-item>
          <a-descriptions-item label="名称">
            {{ detail.name }}
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="leadStatusColor(detail.status)">
              {{ leadStatusLabel(detail.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="联系人">
            {{ detail.contact || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="联系电话">
            {{ detail.phone || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="来源">
            {{ LEAD_SOURCE_LABELS[detail.source] }}
          </a-descriptions-item>
          <a-descriptions-item label="负责人">
            {{ detail.ownerName || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="转出商机">
            {{ detail.opportunityNo || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item
            label="备注"
            :span="3"
          >
            {{ detail.remark || '—' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          跟进记录
        </a-divider>
        <div class="activity-toolbar">
          <a-button
            v-if="auth.hasPermission('leads.update')"
            type="primary"
            size="small"
            @click="onAddActivity"
          >
            <template #icon>
              <IconPlus />
            </template>
            新增跟进
          </a-button>
          <a-button
            size="small"
            :loading="loading"
            @click="loadDetail"
          >
            <template #icon>
              <IconRefresh />
            </template>
            刷新
          </a-button>
        </div>

        <a-empty
          v-if="activities.length === 0"
          description="暂无跟进记录"
        />
        <a-timeline v-else>
          <a-timeline-item
            v-for="item in activities"
            :key="item.id"
            :label="formatDateTime(item.activityTime)"
          >
            <div class="activity-item">
              <span class="activity-item__type">{{ ACTIVITY_TYPE_LABELS[item.type] }}</span>
              <span class="activity-item__content">{{ item.content }}</span>
              <span class="activity-item__meta">记录人：{{ item.recorderName || '—' }}</span>
            </div>
          </a-timeline-item>
        </a-timeline>

        <div class="detail-footer">
          <a-space>
            <a-button
              v-if="!isTerminal && auth.hasPermission('leads.convert')"
              type="primary"
              :loading="!!convertingId"
              @click="confirmConvert"
            >
              <template #icon>
                <IconArrowForwardUp />
              </template>
              转商机
            </a-button>
            <a-button
              v-if="!isTerminal && auth.hasPermission('leads.status')"
              status="danger"
              :loading="!!updatingId"
              @click="confirmAbandon"
            >
              <template #icon>
                <IconBan />
              </template>
              废弃
            </a-button>
          </a-space>
        </div>
      </a-card>

      <a-drawer
        v-model:visible="activityDrawerVisible"
        title="新增跟进记录"
        :width="480"
        :footer="false"
        unmount-on-close
      >
        <a-form
          ref="activityFormRef"
          :model="activityForm"
          :rules="activityRules"
          layout="vertical"
        >
          <a-form-item
            label="跟进方式"
            field="type"
          >
            <a-select
              v-model="activityForm.type"
              :options="ACTIVITY_TYPE_OPTIONS"
            />
          </a-form-item>
          <a-form-item
            label="跟进时间"
            field="activityTime"
          >
            <a-date-picker
              v-model="activityForm.activityTime"
              show-time
              value-format="YYYY-MM-DDTHH:mm:ss"
              style="width: 100%"
            />
          </a-form-item>
          <a-form-item
            label="跟进内容"
            field="content"
          >
            <a-textarea
              v-model="activityForm.content"
              placeholder="1-200 字符"
              :max-length="200"
              show-word-limit
              :auto-size="{ minRows: 3, maxRows: 5 }"
            />
          </a-form-item>
          <div class="form-footer">
            <a-space>
              <a-button @click="activityDrawerVisible = false">
                取消
              </a-button>
              <a-button
                type="primary"
                :loading="activitySubmitting"
                @click="onSubmitActivity"
              >
                提交
              </a-button>
            </a-space>
          </div>
        </a-form>
      </a-drawer>
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

.activity-toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.activity-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.activity-item__type {
  font-weight: 600;
  color: var(--color-text-1);
}

.activity-item__content {
  color: var(--color-text-1);
  word-break: break-all;
}

.activity-item__meta {
  font-size: 12px;
  color: var(--color-text-3);
}

.detail-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

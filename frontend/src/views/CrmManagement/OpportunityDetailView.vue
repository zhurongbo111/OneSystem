<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { ACTIVITY_TYPE_LABELS, ACTIVITY_TYPE_OPTIONS } from '@/api/lead'
import type { ActivityItem, ActivityType } from '@/api/lead'
import {
  OPPORTUNITY_STAGE_OPTIONS,
  createOpportunityActivity,
  getOpportunity,
  getOpportunityActivities,
  isOpportunityStageTerminal,
  opportunityStageColor,
  opportunityStageLabel,
  updateOpportunityStage,
} from '@/api/opportunity'
import type { OpportunityDetail, OpportunityStage } from '@/api/opportunity'
import { useAuthStore } from '@/stores/auth'
import { formatDate, formatDateTime, nowInput, toUtcIso } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'
import { IconEdit, IconPlus, IconRefresh, IconTargetArrow } from '@tabler/icons-vue'

// —— types ——
interface ActivityFormState {
  type: ActivityType
  content: string
  /** 日期时间选择器绑定值：本地 `YYYY-MM-DDTHH:mm:ss` */
  activityTime: string
}

// —— reactive state ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(false)
const notFound = ref(false)
const detail = ref<OpportunityDetail>()
const activities = ref<ActivityItem[]>([])

/** 目标阶段（阶段推进下拉，初始 = 当前阶段） */
const stageInput = ref<OpportunityStage>()
/** 正在推进阶段（前端规则 §4.6：advancing） */
const advancing = ref(false)

/** 跟进活动抽屉（新增） */
const activityDrawerVisible = ref(false)
const activityFormRef = ref<FormInstance>()
const activitySubmitting = ref(false)
const activityForm = reactive<ActivityFormState>({ type: 0, content: '', activityTime: nowInput() })

/** 活动表单校验：与后端 ActivityFieldConstraints / CreateOpportunityActivityRequestValidator 同源 */
const activityRules: Record<string, FieldRule[]> = {
  type: [{ required: true, message: '请选择跟进方式' }],
  content: [
    { required: true, message: '请输入跟进内容' },
    { max: 200, message: '跟进内容长度不能超过 200' },
  ],
  activityTime: [{ required: true, message: '请选择跟进时间' }],
}

// —— computed ——
/** 终态（赢单 / 输单）：不可再改阶段（design.md §0.1 / 40169） */
const isTerminal = computed(() => detail.value !== undefined && isOpportunityStageTerminal(detail.value.stage))

/** 阶段推进按钮可用：非终态且目标阶段与当前不同 */
const canAdvance = computed(
  () => !isTerminal.value && stageInput.value !== undefined && stageInput.value !== detail.value?.stage,
)

// —— lifecycle ——
onMounted(async () => {
  await loadDetail()
})

// —— methods ——
async function loadDetail(): Promise<void> {
  const id = route.params.id as string
  loading.value = true
  try {
    const [opportunity, list] = await Promise.all([getOpportunity(id), getOpportunityActivities(id)])
    detail.value = opportunity
    activities.value = list
    stageInput.value = opportunity.stage
    notFound.value = false
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'opportunities' })
}

function onEdit(): void {
  void router.push({ name: 'opportunityEdit', params: { id: route.params.id as string } })
}

/** 阶段推进：终态商机不可再改（后端 40169 兜底） */
async function onAdvance(): Promise<void> {
  if (!detail.value || stageInput.value === undefined || advancing.value) return
  advancing.value = true
  try {
    await updateOpportunityStage(detail.value.id, stageInput.value)
    Message.success(`阶段已推进至${opportunityStageLabel(stageInput.value)}`)
    await loadDetail()
  } catch {
    // 错误提示已由请求层统一处理（40169 终态不可改阶段）
  } finally {
    advancing.value = false
  }
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
    await createOpportunityActivity(detail.value.id, {
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
      title="商机不存在"
      subtitle="该商机可能已被删除，或链接有误"
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
        :title="detail ? `商机 ${detail.opportunityNo}` : '商机详情'"
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
          <a-descriptions-item label="商机号">
            {{ detail.opportunityNo }}
          </a-descriptions-item>
          <a-descriptions-item label="名称">
            {{ detail.name }}
          </a-descriptions-item>
          <a-descriptions-item label="阶段">
            <a-tag :color="opportunityStageColor(detail.stage)">
              {{ opportunityStageLabel(detail.stage) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="客户">
            {{ detail.partnerName || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="预计金额">
            <span class="amount">¥ {{ detail.amount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="预计成交日期">
            {{ detail.expectedCloseDate ? formatDate(detail.expectedCloseDate) : '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="负责人">
            {{ detail.ownerName || '—' }}
          </a-descriptions-item>
          <a-descriptions-item label="来源线索">
            {{ detail.leadId ? '由线索转入' : '手工新建' }}
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
          阶段推进
        </a-divider>
        <a-space>
          <a-select
            v-model="stageInput"
            :options="OPPORTUNITY_STAGE_OPTIONS"
            :disabled="isTerminal || loading"
            style="width: 180px"
          />
          <a-button
            v-if="auth.hasPermission('opportunities.stage')"
            type="primary"
            :disabled="!canAdvance"
            :loading="advancing"
            @click="onAdvance"
          >
            <template #icon>
              <IconTargetArrow />
            </template>
            推进阶段
          </a-button>
          <span
            v-if="isTerminal"
            class="stage-hint"
          >
            已赢单 / 输单（终态），不可再改阶段
          </span>
        </a-space>

        <a-divider orientation="left">
          跟进记录
        </a-divider>
        <div class="activity-toolbar">
          <a-button
            v-if="auth.hasPermission('opportunities.update')"
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
              v-if="auth.hasPermission('opportunities.update')"
              @click="onEdit"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
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

.amount {
  font-variant-numeric: tabular-nums;
}

.stage-hint {
  color: var(--color-text-3);
  font-size: 12px;
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

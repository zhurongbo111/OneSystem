<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { APPROVAL_ORDER_TYPE_META, APPROVAL_STATUS_META, approveOrder, getApproval, rejectApproval } from '@/api/approval'
import type { ApprovalDetail, ApprovalItem } from '@/api/approval'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance, TableColumnData } from '@arco-design/web-vue'

// —— types ——
interface DecideFormState {
  /** 审批意见（通过可空、驳回必填，≤ 200 字符） */
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 待查看 / 审批的审批记录 id */
  approvalId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  /** 审批成功（列表刷新 + 关闭抽屉） */
  (e: 'decided'): void
  /** 审批失败（保留抽屉 + 列表刷新，如库存不足 40103） */
  (e: 'failed'): void
}>()

// —— constants ——
/** 审批意见校验：仅长度（驳回的必填在提交时校验，通过可空） */
const remarkRules: Record<string, FieldRule[]> = {
  remark: [{ max: 200, message: '审批意见不能超过 200 个字符' }],
}

/** 被审批单据明细列（只读） */
const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 56, align: 'center' },
  { title: '商品', dataIndex: 'productName', width: 150, ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 56, align: 'center' },
  { title: '批次', slotName: 'batchNo', width: 96 },
  { title: '数量', dataIndex: 'quantity', width: 80, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 96, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 96, align: 'right' },
]

/** 明细表横向滚动宽度（各列固定宽度之和） */
const itemScrollX = itemColumns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— reactive state ——
const formRef = ref<FormInstance>()
const detail = ref<ApprovalDetail | null>(null)
const loading = ref(false)
const form = ref<DecideFormState>({ remark: '' })
/** 通过 / 驳回：各自独立的 loading，且互相防重入（specs/010-button-loading §0） */
const approving = ref(false)
const rejecting = ref(false)

// —— computed ——
/** 非待审批（或已查看决定结果）时为只读：隐藏通过 / 驳回按钮 */
const readonly = computed(() => Boolean(detail.value) && detail.value?.status !== 1)

/** 明细行（附行键；Arco a-table 的 row-key 仅接受字段名，而明细无业务 id） */
const itemRows = computed(() =>
  (detail.value?.items ?? []).map((item, index) => ({ ...item, key: `item-${index}` })),
)

// —— watch ——
/** 打开抽屉时加载详情并重置审批意见（防数据串台） */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    form.value = { remark: '' }
    detail.value = null
    if (props.approvalId) {
      void fetchDetail(props.approvalId)
    }
  },
)

// —— methods ——
/** 加载审批详情（审批记录快照 + 被审批单据摘要与明细） */
async function fetchDetail(id: string): Promise<void> {
  loading.value = true
  try {
    detail.value = await getApproval(id)
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    loading.value = false
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 审批通过（通过时单据才生效；失败保留抽屉并刷新列表，供稍后重试或驳回） */
async function onApprove(): Promise<void> {
  if (!detail.value || approving.value || rejecting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  approving.value = true
  try {
    const remark = form.value.remark.trim()
    await approveOrder(detail.value.id, remark ? { remark } : {})
    Message.success('已通过，单据已生效')
    emit('decided')
    onClose()
  } catch {
    // 生效失败（如库存不足 40103）：后端错误已统一提示，保留抽屉并刷新列表
    emit('failed')
  } finally {
    approving.value = false
  }
}

/** 审批驳回（意见必填；驳回即单据作废，不做库存回冲） */
async function onReject(): Promise<void> {
  if (!detail.value || approving.value || rejecting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  const remark = form.value.remark.trim()
  if (!remark) {
    Message.warning('驳回时必须填写审批意见')
    return
  }
  rejecting.value = true
  try {
    await rejectApproval(detail.value.id, { remark })
    Message.success('已驳回，单据已作废')
    emit('decided')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理，保留抽屉并刷新列表
    emit('failed')
  } finally {
    rejecting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    title="单据审批"
    :width="640"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <a-spin
      :loading="loading"
      class="drawer-body"
    >
      <template v-if="detail">
        <a-descriptions
          :column="2"
          size="medium"
        >
          <a-descriptions-item label="单据类型">
            <a-tag :color="APPROVAL_ORDER_TYPE_META[detail.orderType].color">
              {{ APPROVAL_ORDER_TYPE_META[detail.orderType].label }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="审批状态">
            <a-tag :color="APPROVAL_STATUS_META[detail.status].color">
              {{ APPROVAL_STATUS_META[detail.status].label }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="单号">
            {{ detail.orderNo }}
          </a-descriptions-item>
          <a-descriptions-item label="往来单位">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="金额">
            <span class="amount">¥ {{ detail.amount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="单据日期">
            {{ formatDateTime(detail.orderDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="仓库">
            {{ detail.warehouseName }}
          </a-descriptions-item>
          <a-descriptions-item label="提交人">
            {{ detail.submittedByName }}
          </a-descriptions-item>
          <a-descriptions-item label="提交时间">
            {{ formatDateTime(detail.submittedAt) }}
          </a-descriptions-item>
          <template v-if="detail.decidedAt">
            <a-descriptions-item label="审批人">
              {{ detail.decidedByName || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="审批时间">
              {{ formatDateTime(detail.decidedAt) }}
            </a-descriptions-item>
            <a-descriptions-item
              label="审批意见"
              :span="2"
            >
              {{ detail.decisionRemark || '-' }}
            </a-descriptions-item>
          </template>
        </a-descriptions>

        <a-divider orientation="left">
          单据明细
        </a-divider>
        <a-table
          row-key="key"
          size="small"
          :columns="itemColumns"
          :data="itemRows"
          :pagination="false"
          :scroll="{ x: itemScrollX }"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #batchNo="{ record }">
            {{ (record as ApprovalItem).batchNo || '-' }}
          </template>
          <template #unitPrice="{ record }">
            ¥ {{ (record as ApprovalItem).unitPrice.toFixed(2) }}
          </template>
          <template #subtotal="{ record }">
            ¥ {{ (record as ApprovalItem).subtotal.toFixed(2) }}
          </template>
        </a-table>

        <a-divider orientation="left">
          审批意见
        </a-divider>
        <a-form
          ref="formRef"
          :model="form"
          :rules="remarkRules"
          layout="vertical"
        >
          <a-form-item
            field="remark"
            :hide-label="true"
          >
            <a-textarea
              v-model="form.remark"
              placeholder="通过选填；驳回必填，≤ 200 字符"
              :max-length="200"
              show-word-limit
              :auto-size="{ minRows: 3, maxRows: 6 }"
              :disabled="readonly"
            />
          </a-form-item>
        </a-form>

        <div class="drawer-footer">
          <a-space>
            <a-button @click="onClose">
              关闭
            </a-button>
            <a-popconfirm
              v-if="!readonly"
              type="warning"
              content="确认通过该单据？通过后立即执行生效（库存 / 流水 / 成本变动），且不可撤销"
              @ok="onApprove"
            >
              <a-button
                type="primary"
                :loading="approving"
              >
                通过
              </a-button>
            </a-popconfirm>
            <a-button
              v-if="!readonly"
              status="danger"
              :loading="rejecting"
              @click="onReject"
            >
              驳回
            </a-button>
          </a-space>
        </div>
      </template>
    </a-spin>
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  display: block;
  width: 100%;
}

.amount {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.drawer-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

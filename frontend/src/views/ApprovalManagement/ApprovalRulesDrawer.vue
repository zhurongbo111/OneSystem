<script setup lang="ts">
import { ref, watch } from 'vue'

import { APPROVAL_ORDER_TYPE_META, getApprovalRules, updateApprovalRules } from '@/api/approval'
import type { ApprovalOrderType } from '@/api/approval'
import { Message } from '@arco-design/web-vue'

// —— types ——
/** 规则编辑行（阈值可空 = 输入框被清空，保存时按 0 处理并拦截） */
interface RuleRow {
  orderType: ApprovalOrderType
  thresholdAmount: number | undefined
  enabled: boolean
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  /** 保存成功（列表 / 父级据此关闭抽屉） */
  (e: 'saved'): void
}>()

// —— constants ——
/** 规则固定展示的四类单据（顺序与后端 GetApprovalRules 一致） */
const ORDER_TYPE_ROWS: ApprovalOrderType[] = [0, 1, 2, 3]

/** 阈值上界（与后端 ProductFieldConstraints.PriceMaxValue 对齐：9999999.99） */
const MAX_THRESHOLD = 9999999.99

// —— helpers ——
/** 生成四行默认规则（阈值 0 = 未配置，未启用） */
function emptyRules(): RuleRow[] {
  return ORDER_TYPE_ROWS.map((orderType) => ({ orderType, thresholdAmount: 0, enabled: false }))
}

// —— reactive state ——
/** 规则行（固定四行，按单据类型顺序） */
const ruleRows = ref<RuleRow[]>(emptyRules())
const loadingDetail = ref(false)
const submitting = ref(false)

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再拉取已配置规则 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    ruleRows.value = emptyRules()
    void fetchRules()
  },
)

// —— methods ——
/** 加载四类单据规则（缺失类型后端返回默认未启用行） */
async function fetchRules(): Promise<void> {
  loadingDetail.value = true
  try {
    const result = await getApprovalRules()
    const byType = new Map(result.map((rule) => [rule.orderType, rule]))
    ruleRows.value = ORDER_TYPE_ROWS.map((orderType) => {
      const rule = byType.get(orderType)
      return {
        orderType,
        thresholdAmount: rule?.thresholdAmount ?? 0,
        enabled: rule?.enabled ?? false,
      }
    })
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loadingDetail.value = false
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/**
 * 保存：逐类型 upsert。
 * 后端要求每项阈值 > 0（未启用的类型同样需填阈值），故提交前统一校验（含上界）。
 */
async function onSave(): Promise<void> {
  if (submitting.value) return
  const invalid = ruleRows.value.some(
    (rule) => !((rule.thresholdAmount ?? 0) > 0) || (rule.thresholdAmount ?? 0) > MAX_THRESHOLD,
  )
  if (invalid) {
    Message.warning('审批阈值必须大于 0 且不超过 9999999.99')
    return
  }
  submitting.value = true
  try {
    await updateApprovalRules(
      ruleRows.value.map((rule) => ({
        orderType: rule.orderType,
        thresholdAmount: rule.thresholdAmount ?? 0,
        enabled: rule.enabled,
      })),
    )
    emit('saved')
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    title="审批规则设置"
    :width="560"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <a-spin
      :loading="loadingDetail"
      class="drawer-body"
    >
      <a-alert
        type="info"
        class="rules-hint"
      >
        阈值以上需审批；未启用则该类单据保存即生效。
      </a-alert>

      <div class="rules-head">
        <span class="rules-head__type">单据类型</span>
        <span class="rules-head__amount">审批阈值（¥）</span>
        <span class="rules-head__enabled">启用</span>
      </div>

      <div
        v-for="rule in ruleRows"
        :key="rule.orderType"
        class="rules-row"
      >
        <span class="rules-row__type">
          {{ APPROVAL_ORDER_TYPE_META[rule.orderType].label }}
        </span>
        <a-input-number
          v-model="rule.thresholdAmount"
          class="rules-row__amount"
          :min="0"
          :max="MAX_THRESHOLD"
          :precision="2"
          :disabled="loadingDetail || submitting"
        />
        <span class="rules-row__enabled">
          <a-switch
            v-model="rule.enabled"
            :disabled="loadingDetail || submitting"
          />
        </span>
      </div>

      <div class="drawer-footer">
        <a-space>
          <a-button @click="onClose">
            取消
          </a-button>
          <a-button
            type="primary"
            :loading="submitting"
            @click="onSave"
          >
            保存
          </a-button>
        </a-space>
      </div>
    </a-spin>
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  display: block;
  width: 100%;
}

.rules-hint {
  margin-bottom: 16px;
}

.rules-head,
.rules-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.rules-head {
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border);
  font-size: 12px;
  color: var(--color-text-3);
}

.rules-row {
  padding: 8px 0;
}

.rules-head__type,
.rules-row__type {
  flex: 1;
}

.rules-head__amount,
.rules-row__amount {
  flex: 0 0 180px;
}

.rules-head__enabled,
.rules-row__enabled {
  flex: 0 0 48px;
  text-align: center;
}

.drawer-footer {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

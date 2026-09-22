<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import {
  AUDIT_ACTION_COLORS,
  AUDIT_RESOURCE_COLORS,
  auditActionLabel,
  auditResourceLabel,
  getAuditLogById,
  operatorText,
} from '@/api/auditLog'
import type { AuditChangeItem, AuditLogDetail } from '@/api/auditLog'
import { formatDateTime } from '@/utils/datetime'
import type { TableColumnData } from '@arco-design/web-vue'

// —— props/emits ——
const props = defineProps<{
  visible: boolean
  /** 当前查看的日志 id；为空时不请求 */
  logId: string | null
}>()

const emit = defineEmits<{ 'update:visible': [value: boolean] }>()

// —— constants ——
/** 字段级差异表格列：字段 / 变更前 / 变更后（只读，无操作列） */
const changeColumns: TableColumnData[] = [
  { title: '字段', dataIndex: 'label', width: 150 },
  { title: '变更前', slotName: 'before', width: 230, ellipsis: true, tooltip: true },
  { title: '变更后', slotName: 'after', width: 230, ellipsis: true, tooltip: true },
]

/** 各列宽度之和，作为差异表格横向滚动最小宽度 */
const changeScrollX = changeColumns.reduce((sum, column) => sum + (column.width ?? 0), 0)

// —— reactive state ——
/** 详情请求 loading（design §4.5：detailLoading） */
const detailLoading = ref(false)
const detail = ref<AuditLogDetail | null>(null)

// —— computed ——
/** 抽屉标题：优先用摘要，未加载完时用固定标题 */
const title = computed(() => detail.value?.summary ?? '操作日志详情')

/** 字段级差异（无差异为空数组） */
const changes = computed<AuditChangeItem[]>(() => detail.value?.changes ?? [])

/** 资源标签颜色 */
const resourceColor = computed(() => AUDIT_RESOURCE_COLORS[detail.value?.resource ?? -1] ?? 'gray')

/** 动作标签颜色 */
const actionColor = computed(() => AUDIT_ACTION_COLORS[detail.value?.action ?? -1] ?? 'gray')

// —— watch ——
/** 打开抽屉（或切换目标日志）时拉取详情 */
watch(
  () => [props.visible, props.logId] as const,
  ([visible, logId]) => {
    if (visible && logId) void fetchDetail(logId)
    if (!visible) detail.value = null
  },
)

// —— methods ——
/** 拉取日志详情（失败提示由请求层统一处理） */
async function fetchDetail(id: string): Promise<void> {
  detailLoading.value = true
  try {
    detail.value = await getAuditLogById(id)
  } catch {
    detail.value = null
  } finally {
    detailLoading.value = false
  }
}

/** 关闭抽屉（同步动作，不置 loading） */
function onClose(): void {
  emit('update:visible', false)
}
</script>

<template>
  <a-drawer
    :visible="visible"
    :width="640"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
  >
    <template #title>
      {{ title }}
    </template>

    <div
      v-loading="detailLoading"
      class="drawer-body"
    >
      <template v-if="detail">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="操作时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item label="操作人">
            {{ operatorText(detail) }}
          </a-descriptions-item>
          <a-descriptions-item label="资源类型">
            <a-tag :color="resourceColor">
              {{ auditResourceLabel(detail.resource) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="动作">
            <a-tag :color="actionColor">
              {{ auditActionLabel(detail.action) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="业务标识">
            {{ detail.resourceNo || '-' }}
          </a-descriptions-item>
          <a-descriptions-item label="摘要">
            {{ detail.summary }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          字段变更
        </a-divider>

        <a-empty
          v-if="changes.length === 0"
          description="本次操作无字段变更"
        />
        <a-table
          v-else
          row-key="field"
          size="small"
          :columns="changeColumns"
          :data="changes"
          :pagination="false"
          :scroll="{ x: changeScrollX }"
        >
          <template #before="{ record }">
            {{ (record as AuditChangeItem).before || '-' }}
          </template>
          <template #after="{ record }">
            <span :class="{ 'change-empty': !(record as AuditChangeItem).after }">
              {{ (record as AuditChangeItem).after || '-' }}
            </span>
          </template>
        </a-table>
      </template>
    </div>
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  min-height: 200px;
}

.detail-desc {
  margin-bottom: 8px;
}

.change-empty {
  color: var(--color-text-3);
}
</style>

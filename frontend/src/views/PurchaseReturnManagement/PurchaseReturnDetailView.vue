<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { APPROVAL_STATUS_META, getApprovals, withdrawApproval } from '@/api/approval'
import type { ApprovalListItem, ApprovalOrderType } from '@/api/approval'
import { getPurchaseReturn, voidPurchaseReturn } from '@/api/purchaseReturn'
import type { PurchaseReturnDetail, PurchaseReturnItem } from '@/api/purchaseReturn'
import { getUser } from '@/api/user'
import SettlementRecords from '@/components/SettlementRecords.vue'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import {
  canStartSettlement,
  canVoidOrder,
  settlementStateColor,
  settlementStateLabel,
  VOID_SETTLED_HINT,
} from '@/utils/settlement'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import { IconPrinter } from '@tabler/icons-vue'

// —— reactive state ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/** 详情数据（null = 尚未加载；notFound = 已加载但不存在 → 404 结果页） */
const detail = ref<PurchaseReturnDetail | null>(null)
const notFound = ref(false)
const loading = ref(false)

/** 创建人姓名（createdBy 为用户 id，解析为可读姓名） */
const creatorName = ref<string | null>(null)

/** 作废 loading（design §4.5：voidingId） */
const voidingId = ref<string | undefined>(undefined)

/** 撤回 loading（042 design §4.5：withdrawing） */
const withdrawing = ref(false)
/** 本单据的审批记录（042：仅待审批时拉取，撤回需要其 id） */
const approvalRecord = ref<ApprovalListItem | null>(null)

// —— constants ——
/** 本域单据类型（042 审批查询用：2 采购退货单） */
const ORDER_TYPE: ApprovalOrderType = 2

const itemColumns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '商品', dataIndex: 'productName', width: 220, ellipsis: true, tooltip: true },
  { title: '单位', dataIndex: 'unit', width: 80, align: 'center' },
  { title: '数量', slotName: 'quantity', width: 110, align: 'right' },
  { title: '单价', slotName: 'unitPrice', width: 130, align: 'right' },
  { title: '小计', slotName: 'subtotal', width: 130, align: 'right' },
]

// —— computed ——
/** 是否已作废（决定底部操作是否显示） */
const isVoided = computed(() => detail.value?.status === 0)
/** 是否待审批（042：隐藏作废、显示提示与撤回） */
const isPending = computed(() => detail.value?.approvalStatus === 1)
/** 是否可撤回（042：待审批 + 审批记录存在 + 当前用户为提交人） */
const canWithdraw = computed(
  () =>
    isPending.value &&
    approvalRecord.value !== null &&
    approvalRecord.value.submittedBy === auth.user?.id,
)
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))

// —— lifecycle ——
onMounted(async () => {
  await fetchDetail()
})

// —— methods ——
async function fetchDetail(): Promise<void> {
  if (!id.value) {
    notFound.value = true
    return
  }
  loading.value = true
  try {
    detail.value = await getPurchaseReturn(id.value)
    notFound.value = false
    creatorName.value = null
    if (detail.value.createdBy) {
      try {
        creatorName.value = (await getUser(detail.value.createdBy)).displayName
      } catch {
        // 创建人已删除等异常：回退显示原始 id（不阻断详情展示）
        creatorName.value = detail.value.createdBy
      }
    }
    approvalRecord.value = null
    // 待审批单据需要审批记录 id 才能撤回（042）：前端按 orderId 匹配，找不到则不显示撤回按钮
    if (detail.value.approvalStatus === 1) {
      await fetchApprovalRecord(detail.value.id)
    }
  } catch {
    notFound.value = true
    detail.value = null
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'purchaseReturns' })
}

/** 打开打印视图（specs/027-erp-export §4.3：详情页头部打印入口） */
function onPrint(): void {
  void router.push({ name: 'purchaseReturnPrint', params: { id: id.value } })
}

/** 作废：回冲库存，仅改状态不删数据 */
async function onVoid(): Promise<void> {
  if (voidingId.value || !detail.value) return
  voidingId.value = detail.value.id
  try {
    await voidPurchaseReturn(detail.value.id)
    Message.success('已作废，库存已回冲')
    detail.value = await getPurchaseReturn(detail.value.id)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voidingId.value = undefined
  }
}

/** 去收付款：采购退货单为收款方向（type=0，供应商退我们钱），预置往来单位 */
function onGoSettlement(): void {
  if (!detail.value) return
  void router.push({ name: 'settlementNew', query: { type: '0', partnerId: detail.value.partnerId } })
}

/**
 * 拉取本单据的审批记录（042）：
 * 后端无「按单据查审批记录」接口，故用「待审批 + 单据类型」一次性取回（本域待审批量小，可接受），
 * 再在前端按 `orderId === 单据 id` 匹配；匹配不到则不显示「撤回」按钮（不影响其他展示）。
 */
async function fetchApprovalRecord(orderId: string): Promise<void> {
  try {
    const result = await getApprovals({ status: 1, orderType: ORDER_TYPE, page: 1, pageSize: 100 })
    approvalRecord.value = result.items.find((item) => item.orderId === orderId) ?? null
  } catch {
    // 错误提示已由请求层统一处理（撤回按钮不显示即可）
    approvalRecord.value = null
  }
}

/** 撤回（仅提交人本人，且单据待审批）；撤回即单据作废，不产生库存回冲 */
async function onWithdraw(): Promise<void> {
  if (withdrawing.value || !approvalRecord.value) return
  withdrawing.value = true
  try {
    await withdrawApproval(approvalRecord.value.id)
    Message.success('已撤回，单据已作废')
    await fetchDetail()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    withdrawing.value = false
  }
}
</script>

<template>
  <div
    v-loading="loading"
    class="detail-page"
  >
    <a-result
      v-if="notFound && !loading"
      status="404"
      title="单据不存在"
      subtitle="该采购退货单可能已被删除，请返回列表查看"
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

    <template v-else-if="detail">
      <!-- 待审批提示（042 §0.2）：单据保存即占位正常，但审批通过前不产生库存变动 -->
      <a-alert
        v-if="isPending"
        type="info"
      >
        该单据待审批，审批通过后才会产生库存变动
      </a-alert>

      <a-page-header
        class="detail-header"
        title="采购退货单详情"
        @back="goBack"
      >
        <template #extra>
          <a-button
            size="small"
            @click="onPrint"
          >
            <template #icon>
              <IconPrinter />
            </template>
            打印
          </a-button>
        </template>
      </a-page-header>

      <a-card :bordered="false">
        <a-descriptions
          :column="2"
          class="detail-desc"
        >
          <a-descriptions-item label="单号">
            {{ detail.returnNo }}
          </a-descriptions-item>
          <a-descriptions-item label="供应商">
            {{ detail.partnerName }}
          </a-descriptions-item>
          <a-descriptions-item label="出库仓">
            {{ detail.warehouseName }}
          </a-descriptions-item>
          <a-descriptions-item label="退货日期">
            {{ formatDateTime(detail.returnDate).slice(0, 10) }}
          </a-descriptions-item>
          <a-descriptions-item label="总金额">
            <span class="amount">¥ {{ detail.totalAmount.toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="结算状态">
            <a-tag :color="settlementStateColor(detail.settlementState)">
              {{ settlementStateLabel(detail.settlementState, detail.unsettledAmount) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="单据状态">
            <a-tag :color="detail.status === 1 ? 'green' : 'red'">
              {{ detail.status === 1 ? '正常' : '已作废' }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="审批状态">
            <a-tag
              v-if="detail.approvalStatus !== 0"
              :color="APPROVAL_STATUS_META[detail.approvalStatus].color"
            >
              {{ APPROVAL_STATUS_META[detail.approvalStatus].label }}
            </a-tag>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item label="创建人">
            {{ creatorName ?? '-' }}
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detail.createdAt) }}
          </a-descriptions-item>
          <a-descriptions-item
            label="备注"
            :span="2"
          >
            {{ detail.remark || '-' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">
          商品明细
        </a-divider>
        <a-table
          row-key="id"
          size="small"
          :columns="itemColumns"
          :data="detail.items"
          :pagination="false"
        >
          <template #seq="{ rowIndex }">
            {{ rowIndex + 1 }}
          </template>
          <template #quantity="{ record }">
            {{ (record as PurchaseReturnItem).quantity }}
          </template>
          <template #unitPrice="{ record }">
            ¥ {{ (record as PurchaseReturnItem).unitPrice.toFixed(2) }}
          </template>
          <template #subtotal="{ record }">
            ¥ {{ (record as PurchaseReturnItem).subtotal.toFixed(2) }}
          </template>
        </a-table>

        <a-divider orientation="left">
          收付款明细
        </a-divider>
        <SettlementRecords
          :order-type="2"
          :order-id="id"
        />
      </a-card>

      <!-- 底部操作：仅正常单显示（作废后操作消失） -->
      <div
        v-if="!isVoided"
        class="detail-actions"
      >
        <a-space>
          <a-button
            v-if="canStartSettlement(detail)"
            type="primary"
            @click="onGoSettlement"
          >
            去收付款
          </a-button>
          <a-popconfirm
            v-if="canWithdraw"
            type="warning"
            content="确认撤回该采购退货单？撤回后单据作废，且不可恢复"
            @ok="onWithdraw"
          >
            <a-button :loading="withdrawing">
              撤回
            </a-button>
          </a-popconfirm>
          <!-- 待审批单据禁止作废（042 §0.2）：隐藏作废按钮 -->
          <template v-if="!isPending">
            <a-tooltip
              v-if="!canVoidOrder(detail)"
              :content="VOID_SETTLED_HINT"
            >
              <span>
                <a-button
                  status="danger"
                  disabled
                >
                  作废
                </a-button>
              </span>
            </a-tooltip>
            <a-popconfirm
              v-else
              type="warning"
              content="确认作废该采购退货单？作废后库存将回冲，且不可恢复"
              @ok="onVoid"
            >
              <a-button
                status="danger"
                :loading="voidingId === detail.id"
              >
                作废
              </a-button>
            </a-popconfirm>
          </template>
        </a-space>
      </div>
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

.detail-header {
  background: var(--color-bg-2);
  border-radius: var(--border-radius-medium);
  padding: 12px 20px;
}

.detail-desc {
  max-width: 960px;
}

.amount {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.detail-actions {
  display: flex;
  justify-content: flex-end;
}
</style>

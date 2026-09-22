<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import {
  getVoucher,
  voidVoucher,
  VOUCHER_SOURCE_TYPE_META,
  VOUCHER_STATUS_META,
  type VoucherDetail,
  type VoucherEntry,
} from '@/api/voucher'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import { IconBan } from '@tabler/icons-vue'

// —— stores/composables ——
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// —— reactive state ——
const loading = ref(false)
const voiding = ref(false)
const notFound = ref(false)
const detail = ref<VoucherDetail | null>(null)

// —— computed ——
/** 凭证不存在（404）时展示结果页，禁止白屏 */
const isEmpty = computed(() => !loading.value && (notFound.value || detail.value === null))

/** 已过账凭证才可作废（作废为终态） */
const canVoid = computed(() => detail.value?.status === 1)

// —— lifecycle ——
onMounted(() => {
  void fetchDetail()
})

// —— methods ——
async function fetchDetail(): Promise<void> {
  const id = String(route.params.id ?? '')
  loading.value = true
  notFound.value = false
  try {
    detail.value = await getVoucher(id)
  } catch {
    // 请求层已提示（含 40400「凭证不存在」）
    detail.value = null
    notFound.value = true
  } finally {
    loading.value = false
  }
}

/** 作废凭证：仅改状态不删数据，余额随之回退 */
async function onVoid(): Promise<void> {
  if (voiding.value || !detail.value) return
  voiding.value = true
  try {
    detail.value = await voidVoucher(detail.value.id)
    Message.success('凭证已作废，余额已回退')
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    voiding.value = false
  }
}

function onBack(): void {
  void router.push({ name: 'vouchers' })
}
</script>

<template>
  <div class="detail-page">
    <a-page-header
      title="凭证详情"
      @back="onBack"
    >
      <template #extra>
        <a-popconfirm
          v-if="auth.hasPermission('vouchers.void') && canVoid"
          type="warning"
          content="确认作废该凭证？作废后余额随之回退，且不可恢复"
          @ok="onVoid"
        >
          <a-button
            status="danger"
            :loading="voiding"
          >
            <template #icon>
              <IconBan />
            </template>
            作废
          </a-button>
        </a-popconfirm>
      </template>
    </a-page-header>

    <a-spin
      :loading="loading"
      style="width: 100%"
    >
      <a-result
        v-if="isEmpty"
        status="404"
        title="凭证不存在或已被删除"
      >
        <template #extra>
          <a-button
            type="primary"
            @click="onBack"
          >
            返回列表
          </a-button>
        </template>
      </a-result>

      <template v-else-if="detail">
        <a-card
          :bordered="false"
          class="detail-card"
        >
          <a-descriptions
            :column="2"
            bordered
          >
            <a-descriptions-item label="凭证号">
              {{ detail.voucherNo }}
            </a-descriptions-item>
            <a-descriptions-item label="记账日期">
              {{ formatDateTime(detail.voucherDate).slice(0, 10) }}
            </a-descriptions-item>
            <a-descriptions-item label="来源">
              <a-tag :color="VOUCHER_SOURCE_TYPE_META[detail.sourceType].color">
                {{ VOUCHER_SOURCE_TYPE_META[detail.sourceType].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="来源单据号">
              {{ detail.sourceNo || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="摘要">
              {{ detail.summary }}
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="VOUCHER_STATUS_META[detail.status].color">
                {{ VOUCHER_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="借方合计">
              <span class="amount">¥ {{ detail.totalDebit.toFixed(2) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="贷方合计">
              <span class="amount">¥ {{ detail.totalCredit.toFixed(2) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="创建时间">
              {{ formatDateTime(detail.createdAt) }}
            </a-descriptions-item>
            <a-descriptions-item label="更新时间">
              {{ formatDateTime(detail.updatedAt) }}
            </a-descriptions-item>
          </a-descriptions>
        </a-card>

        <a-card
          :bordered="false"
          title="分录"
          class="detail-card"
        >
          <a-table
            row-key="id"
            :data="detail.items"
            :pagination="false"
            :bordered="false"
            size="small"
          >
            <template #columns>
              <a-table-column
                title="行号"
                :width="80"
                align="center"
              >
                <template #cell="{ record }">
                  {{ (record as VoucherEntry).lineNo }}
                </template>
              </a-table-column>
              <a-table-column
                title="科目"
                :width="260"
              >
                <template #cell="{ record }">
                  {{ (record as VoucherEntry).accountCode }} {{ (record as VoucherEntry).accountName }}
                </template>
              </a-table-column>
              <a-table-column
                title="摘要"
                :width="220"
                ellipsis
                tooltip
              >
                <template #cell="{ record }">
                  {{ (record as VoucherEntry).summary || '-' }}
                </template>
              </a-table-column>
              <a-table-column
                title="借方"
                :width="150"
                align="right"
              >
                <template #cell="{ record }">
                  <span
                    v-if="(record as VoucherEntry).debit > 0"
                    class="amount"
                  >
                    ¥ {{ (record as VoucherEntry).debit.toFixed(2) }}
                  </span>
                  <span v-else>-</span>
                </template>
              </a-table-column>
              <a-table-column
                title="贷方"
                :width="150"
                align="right"
              >
                <template #cell="{ record }">
                  <span
                    v-if="(record as VoucherEntry).credit > 0"
                    class="amount"
                  >
                    ¥ {{ (record as VoucherEntry).credit.toFixed(2) }}
                  </span>
                  <span v-else>-</span>
                </template>
              </a-table-column>
            </template>
          </a-table>
        </a-card>
      </template>
    </a-spin>
  </div>
</template>

<style scoped>
.detail-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.detail-card {
  border-radius: var(--border-radius-medium);
}

.amount {
  font-variant-numeric: tabular-nums;
}
</style>

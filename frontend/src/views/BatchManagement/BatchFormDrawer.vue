<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createBatch, getBatchById, toUtcMidnight, updateBatch } from '@/api/batch'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface BatchFormState {
  productId: string | undefined
  batchNo: string
  /** 日期选择器绑定值：YYYY-MM-DD（null = 未设置） */
  productionDate: string | undefined
  expiryDate: string | undefined
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑（批次号创建后不可修改，编辑态只读展示） */
  mode: 'create' | 'edit'
  /** 编辑时的批次 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— helpers ——
function emptyForm(): BatchFormState {
  return {
    productId: undefined,
    batchNo: '',
    productionDate: undefined,
    expiryDate: undefined,
    remark: '',
  }
}

/** ISO 时间 → YYYY-MM-DD（本地日历） */
function toDateValue(iso: string | null | undefined): string | undefined {
  if (!iso) return undefined
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return undefined
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<BatchFormState>(emptyForm())

/** 下拉数据源（新增态：仅按批次管理商品） */
const products = ref<ProductPickItem[]>([])

/** 编辑态的批次号 / 商品展示（不可改） */
const lockedBatchNo = ref('')
const lockedProductLabel = ref('')

// —— computed ——
const drawerTitle = computed(() => (props.mode === 'create' ? '新增批次' : '编辑批次'))

const productOptions = computed(() =>
  products.value
    .filter((p) => p.isBatchManaged)
    .map((p) => ({ label: `${p.code} ${p.name}`, value: p.id })),
)

/** 校验规则：与后端 BatchFieldConstraints / CreateBatchRequestValidator 同源 */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  productId: [{ required: true, message: '请选择商品' }],
  batchNo: [
    { required: true, message: '请输入批次号' },
    { match: /^[A-Za-z0-9_-]{1,50}$/, message: '批次号为 1-50 位字母、数字、下划线或连字符' },
  ],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式回填 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    lockedBatchNo.value = ''
    lockedProductLabel.value = ''
    if (props.mode === 'create') {
      void loadProducts()
    } else if (props.editId) {
      void loadDetail(props.editId)
    }
  },
)

// —— methods ——
async function loadProducts(): Promise<void> {
  try {
    products.value = await getProductPickList()
  } catch {
    // 错误提示已由请求层统一处理
  }
}

async function loadDetail(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getBatchById(id)
    form.batchNo = detail.batchNo
    form.productionDate = toDateValue(detail.productionDate)
    form.expiryDate = toDateValue(detail.expiryDate)
    form.remark = detail.remark ?? ''
    lockedBatchNo.value = detail.batchNo
    lockedProductLabel.value = `${detail.productCode} ${detail.productName}`
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

function onClose(): void {
  emit('update:visible', false)
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    if (props.mode === 'edit') {
      await updateBatch(props.editId as string, {
        // 裸日期会被后端按服务器本地时区解析导致入库失败，统一转 UTC 午夜（同其他开单 api 惯例）
        productionDate: form.productionDate ? toUtcMidnight(form.productionDate) : undefined,
        expiryDate: form.expiryDate ? toUtcMidnight(form.expiryDate) : undefined,
        remark: form.remark.trim() || undefined,
      })
      Message.success('批次已更新')
    } else {
      await createBatch({
        productId: form.productId as string,
        batchNo: form.batchNo.trim(),
        productionDate: form.productionDate ? toUtcMidnight(form.productionDate) : undefined,
        expiryDate: form.expiryDate ? toUtcMidnight(form.expiryDate) : undefined,
        remark: form.remark.trim() || undefined,
      })
      Message.success('批次已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（批次号已存在 40129 / 商品未启用批次管理 40000）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :title="drawerTitle"
    :width="560"
    :footer="false"
    unmount-on-close
    @cancel="onClose"
    @close="onClose"
  >
    <a-spin
      :loading="detailLoading"
      class="drawer-body"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
      >
        <a-form-item
          v-if="mode === 'create'"
          label="商品"
          field="productId"
        >
          <a-select
            v-model="form.productId"
            placeholder="选择按批次管理的商品"
            :options="productOptions"
            allow-search
            :filter-option="true"
          />
        </a-form-item>
        <a-form-item
          v-else
          label="商品"
        >
          <a-input
            :model-value="lockedProductLabel"
            disabled
          />
        </a-form-item>

        <!-- 批次号：新增可输入；编辑只读展示（创建后不可修改） -->
        <a-form-item
          v-if="mode === 'create'"
          label="批次号"
          field="batchNo"
        >
          <a-input
            v-model="form.batchNo"
            placeholder="1-50 位字母、数字、下划线或连字符"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          v-else
          label="批次号"
        >
          <a-input
            :model-value="lockedBatchNo"
            disabled
          />
        </a-form-item>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="生产日期"
              field="productionDate"
            >
              <a-date-picker
                v-model="form.productionDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="选填"
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="到期日"
              field="expiryDate"
            >
              <a-date-picker
                v-model="form.expiryDate"
                value-format="YYYY-MM-DD"
                style="width: 100%"
                placeholder="选填（不填 = 永不过期）"
                :disabled="detailLoading"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item
          label="备注"
          field="remark"
        >
          <a-textarea
            v-model="form.remark"
            placeholder="选填，≤ 200 字符"
            :max-length="200"
            show-word-limit
            :auto-size="{ minRows: 2, maxRows: 4 }"
            :disabled="detailLoading"
          />
        </a-form-item>

        <div class="form-footer">
          <a-space>
            <a-button @click="onClose">
              取消
            </a-button>
            <a-button
              type="primary"
              :loading="submitting"
              @click="onSubmit"
            >
              提交
            </a-button>
          </a-space>
        </div>
      </a-form>
    </a-spin>
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  display: block;
  width: 100%;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

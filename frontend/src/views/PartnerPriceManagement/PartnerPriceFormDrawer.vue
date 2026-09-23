<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { getPartners } from '@/api/partner'
import type { Partner } from '@/api/partner'
import { createPartnerPrice, getPartnerPrice, updatePartnerPrice } from '@/api/partnerPrice'
import { getProductPickList } from '@/api/product'
import type { ProductPickItem } from '@/api/product'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'

// —— types ——
interface PartnerPriceFormState {
  partnerId: string | undefined
  productId: string | undefined
  price: number
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 */
  mode: 'create' | 'edit'
  /** 编辑时的协议价 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— constants ——
/** 可配置协议价的往来类型（客户 / 两者） */
const PARTNER_TYPES_FOR_PRICE = [2, 3]

// —— helpers ——
function emptyForm(): PartnerPriceFormState {
  return { partnerId: undefined, productId: undefined, price: 0, remark: '' }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<PartnerPriceFormState>(emptyForm())

/** 下拉数据源（新增态使用） */
const customers = ref<Partner[]>([])
const products = ref<ProductPickItem[]>([])

/** 编辑态的客户 / 商品展示名（不可改）与商品销售价（高于它时提示） */
const lockedPartnerName = ref('')
const lockedProductLabel = ref('')
const salePrice = ref<number | undefined>(undefined)

// —— computed ——
const isEdit = computed(() => props.mode === 'edit')

const drawerTitle = computed(() => (props.mode === 'create' ? '新增协议价' : '编辑协议价'))

const partnerOptions = computed(() => customers.value.map((p) => ({ label: p.name, value: p.id })))

const productOptions = computed(() =>
  products.value.map((p) => ({ label: `${p.code} ${p.name}`, value: p.id })),
)

/** 校验规则：与客户价字段约束同源（specs/036-erp-partner-price/design.md §3.5） */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  price: [
    { required: true, message: '请输入协议单价' },
    {
      validator: (value: unknown, callback: (message?: string) => void) => {
        const price = Number(value)
        if (Number.isNaN(price) || price < 0 || price > 9999999.99) {
          callback('协议单价必须在 0 到 9999999.99 之间')
          return
        }
        callback()
      },
    },
  ],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

/** 已选商品 / 详情返回的商品销售价（新增态随选中商品带出，编辑态取详情） */
const currentSalePrice = computed(() => {
  if (isEdit.value) return salePrice.value
  return products.value.find((p) => p.id === form.productId)?.salePrice
})

/** 协议价高于商品销售价时提示（列表页同样规则） */
const priceWarning = computed(() => {
  const reference = currentSalePrice.value
  if (reference === undefined) return undefined
  return form.price > reference ? '高于默认价（商品销售价 ¥ ' + reference.toFixed(2) + '）' : undefined
})

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式回填（specs/009 §0 防串台约定） */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    lockedPartnerName.value = ''
    lockedProductLabel.value = ''
    salePrice.value = undefined
    void loadOptions()
    if (props.mode === 'edit' && props.editId) {
      void loadDetail(props.editId)
    }
  },
)

// —— methods ——
/** 加载客户 / 商品下拉（客户限制为「客户 / 两者」且启用） */
async function loadOptions(): Promise<void> {
  try {
    const [partners, picks] = await Promise.all([
      getPartners({ status: 1, page: 1, pageSize: 100 }),
      getProductPickList(),
    ])
    customers.value = partners.items.filter((p) => PARTNER_TYPES_FOR_PRICE.includes(p.type))
    products.value = picks
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 加载协议价详情并回填（客户与商品按其名称只读展示，不进表单） */
async function loadDetail(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getPartnerPrice(id)
    form.partnerId = detail.partnerId
    form.productId = detail.productId
    form.price = detail.price
    form.remark = detail.remark ?? ''
    lockedPartnerName.value = detail.partnerName
    lockedProductLabel.value = `${detail.productCode} ${detail.productName}`
    salePrice.value = detail.salePrice
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交：新增含客户与商品，编辑只提交单价与备注（不可改字段不出现在请求体） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const remark = form.remark.trim() || undefined
    if (props.mode === 'edit') {
      await updatePartnerPrice(props.editId as string, { price: form.price, remark })
      Message.success('协议价已更新')
    } else {
      await createPartnerPrice({
        partnerId: form.partnerId as string,
        productId: form.productId as string,
        price: form.price,
        remark,
      })
      Message.success('协议价已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（重复 40131 / 客户与商品校验）
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
        <!-- 客户：新增可选；编辑只读展示（不可改字段，AGENTS.md §4.5） -->
        <a-form-item
          v-if="!isEdit"
          label="客户"
          field="partnerId"
        >
          <a-select
            v-model="form.partnerId"
            :options="partnerOptions"
            placeholder="请选择客户"
            allow-search
            :disabled="detailLoading"
          />
        </a-form-item>
        <a-form-item
          v-else
          label="客户"
        >
          <a-input
            :model-value="lockedPartnerName"
            disabled
          />
        </a-form-item>

        <!-- 商品：同上 -->
        <a-form-item
          v-if="!isEdit"
          label="商品"
          field="productId"
        >
          <a-select
            v-model="form.productId"
            :options="productOptions"
            placeholder="请选择商品"
            allow-search
            :disabled="detailLoading"
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

        <a-form-item
          label="协议单价"
          field="price"
          :extra="priceWarning"
        >
          <a-input-number
            v-model="form.price"
            :min="0"
            :max="9999999.99"
            :precision="2"
            placeholder="同一客户与商品仅一条"
            :disabled="detailLoading"
          />
        </a-form-item>

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

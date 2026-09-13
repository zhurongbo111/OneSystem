<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { createProduct, getCategories, getProduct, updateProduct } from '@/api/product'
import type { Category } from '@/api/product'
import { formatDateTime } from '@/utils/datetime'
import { Message } from '@arco-design/web-vue'
import type { FieldRule, FormInstance } from '@arco-design/web-vue'
import { IconPlus } from '@arco-design/web-vue/es/icon'

import CategoryManagerModal from './CategoryManagerModal.vue'

// —— types ——
interface ProductFormState {
  code: string
  name: string
  categoryId: string | undefined
  unit: string
  purchasePrice: number | undefined
  salePrice: number | undefined
  safetyStock: number
  remark: string
}

// —— props/emits ——
const props = defineProps<{
  /** 抽屉可见性（v-model） */
  visible: boolean
  /** 模式：新增 / 编辑 / 查看（详情，disabled 展示） */
  mode: 'create' | 'edit' | 'view'
  /** 编辑 / 查看时的商品 id */
  editId?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

// —— constants ——
/** 金额上下限（对应后端 PriceMinValue / PriceMaxValue） */
const PRICE_MIN = 0
const PRICE_MAX = 9999999.99
/** 安全库存上下限（对应后端 SafetyStockMinValue / SafetyStockMaxValue） */
const SAFETY_STOCK_MIN = 0
const SAFETY_STOCK_MAX = 999999

// —— helpers ——
function emptyForm(): ProductFormState {
  return {
    code: '',
    name: '',
    categoryId: undefined,
    unit: '',
    purchasePrice: undefined,
    salePrice: undefined,
    safetyStock: 0,
    remark: '',
  }
}

// —— reactive state ——
const formRef = ref<FormInstance>()
const submitting = ref(false)
const detailLoading = ref(false)
const form = reactive<ProductFormState>(emptyForm())

/** 分类下拉数据源 */
const categories = ref<Category[]>([])
const categoriesLoading = ref(false)

/** 分类管理弹窗（就地新建分类） */
const categoryModalVisible = ref(false)

/** 查看态审计信息（不参与表单提交） */
const detailCreatedAt = ref<string | null>(null)
const detailUpdatedAt = ref<string | null>(null)

// —— computed ——
const drawerTitle = computed(() =>
  props.mode === 'create' ? '新增商品' : props.mode === 'edit' ? '编辑商品' : '商品详情',
)

/** 查看态：整表 disabled 且隐藏「新建分类」按钮 */
const isView = computed(() => props.mode === 'view')

const categoryOptions = computed(() =>
  categories.value.map((c) => ({ label: c.name, value: c.id })),
)

/** 校验规则：编码仅新增时校验（编辑 / 查看只读展示） */
const rules = computed<Record<string, FieldRule[]>>(() => ({
  code: [
    { required: true, message: '请输入商品编码' },
    { match: /^[A-Za-z0-9_-]{2,32}$/, message: '编码为 2-32 位字母、数字、下划线或连字符' },
  ],
  name: [
    { required: true, message: '请输入商品名称' },
    { min: 2, max: 50, message: '名称长度必须在 2 到 50 之间' },
  ],
  categoryId: [{ required: true, message: '请选择商品分类' }],
  unit: [
    { required: true, message: '请输入计量单位' },
    { max: 10, message: '单位长度不能超过 10' },
  ],
  purchasePrice: [{ required: true, message: '请输入采购价' }],
  salePrice: [{ required: true, message: '请输入销售价' }],
  remark: [{ max: 200, message: '备注长度不能超过 200' }],
}))

// —— watch ——
/** 打开抽屉时先重置（防数据串台），再按模式拉取分类 / 详情 */
watch(
  () => props.visible,
  (v) => {
    if (!v) return
    Object.assign(form, emptyForm())
    void loadCategories()
    if (props.mode !== 'create' && props.editId) {
      void loadProduct(props.editId)
    }
  },
)

// —— methods ——
/** 加载分类下拉（全量，量小） */
async function loadCategories(): Promise<void> {
  categoriesLoading.value = true
  try {
    categories.value = await getCategories()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    categoriesLoading.value = false
  }
}

/** 加载商品详情并回填（编辑 / 查看） */
async function loadProduct(id: string): Promise<void> {
  detailLoading.value = true
  try {
    const detail = await getProduct(id)
    form.code = detail.code
    form.name = detail.name
    form.categoryId = detail.categoryId
    form.unit = detail.unit
    form.purchasePrice = detail.purchasePrice
    form.salePrice = detail.salePrice
    form.safetyStock = detail.safetyStock
    form.remark = detail.remark ?? ''
    detailCreatedAt.value = detail.createdAt
    detailUpdatedAt.value = detail.updatedAt
  } catch {
    // 错误提示已由请求层统一处理
    onClose()
  } finally {
    detailLoading.value = false
  }
}

/** 新建分类成功后回填选中 */
function onCategoryCreated(category: Category): void {
  if (!categories.value.some((c) => c.id === category.id)) {
    categories.value.push(category)
  }
  form.categoryId = category.id
}

/** 关闭抽屉 */
function onClose(): void {
  emit('update:visible', false)
}

/** 提交（仅新增 / 编辑态渲染提交按钮） */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const errors = await formRef.value?.validate()
  if (errors) return
  submitting.value = true
  try {
    const payload = {
      name: form.name.trim(),
      categoryId: form.categoryId as string,
      unit: form.unit.trim(),
      purchasePrice: form.purchasePrice as number,
      salePrice: form.salePrice as number,
      safetyStock: form.safetyStock,
      remark: form.remark.trim() || undefined,
    }
    if (props.mode === 'edit') {
      await updateProduct(props.editId as string, payload)
      Message.success('商品已更新')
    } else {
      await createProduct({ code: form.code.trim(), ...payload })
      Message.success('商品已创建')
    }
    emit('saved')
    onClose()
  } catch {
    // 错误提示已由请求层统一处理（编码已存在 40101）
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
        <!-- 编码：新增可输入；编辑 / 查看只读展示（创建后不可修改） -->
        <a-form-item
          v-if="mode === 'create'"
          label="商品编码"
          field="code"
        >
          <a-input
            v-model="form.code"
            placeholder="2-32 位字母、数字、下划线或连字符"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          v-else
          label="商品编码"
        >
          <a-input
            :model-value="form.code"
            disabled
          />
        </a-form-item>

        <a-form-item
          label="商品名称"
          field="name"
        >
          <a-input
            v-model="form.name"
            placeholder="2-50 字符"
            :disabled="isView || detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-form-item
          label="商品分类"
          field="categoryId"
        >
          <div class="category-picker">
            <a-select
              v-model="form.categoryId"
              class="category-picker__select"
              :options="categoryOptions"
              :loading="categoriesLoading"
              placeholder="请选择分类"
              :disabled="isView || detailLoading"
              allow-clear
            />
            <a-button
              v-if="!isView"
              @click="categoryModalVisible = true"
            >
              <template #icon>
                <IconPlus />
              </template>
              新建分类
            </a-button>
          </div>
        </a-form-item>

        <a-form-item
          label="计量单位"
          field="unit"
        >
          <a-input
            v-model="form.unit"
            placeholder="如：个 / 箱 / 斤"
            :disabled="isView || detailLoading"
            allow-clear
          />
        </a-form-item>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item
              label="采购价"
              field="purchasePrice"
            >
              <a-input-number
                v-model="form.purchasePrice"
                class="amount-input"
                :min="PRICE_MIN"
                :max="PRICE_MAX"
                :precision="2"
                placeholder="0.00"
                :disabled="isView || detailLoading"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item
              label="销售价"
              field="salePrice"
            >
              <a-input-number
                v-model="form.salePrice"
                class="amount-input"
                :min="PRICE_MIN"
                :max="PRICE_MAX"
                :precision="2"
                placeholder="0.00"
                :disabled="isView || detailLoading"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item
          label="安全库存"
          field="safetyStock"
        >
          <a-input-number
            v-model="form.safetyStock"
            class="amount-input"
            :min="SAFETY_STOCK_MIN"
            :max="SAFETY_STOCK_MAX"
            :precision="0"
            :disabled="isView || detailLoading"
          >
            <template #suffix>
              0 表示不提醒
            </template>
          </a-input-number>
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
            :disabled="isView || detailLoading"
          />
        </a-form-item>

        <!-- 查看态：附加只读审计信息 -->
        <a-descriptions
          v-if="isView && !detailLoading"
          class="view-meta"
          :column="1"
          size="small"
        >
          <a-descriptions-item label="创建时间">
            {{ formatDateTime(detailCreatedAt) }}
          </a-descriptions-item>
          <a-descriptions-item label="更新时间">
            {{ formatDateTime(detailUpdatedAt) }}
          </a-descriptions-item>
        </a-descriptions>

        <div class="form-footer">
          <a-space>
            <a-button @click="onClose">
              {{ isView ? '关闭' : '取消' }}
            </a-button>
            <a-button
              v-if="!isView"
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

    <CategoryManagerModal
      v-model:visible="categoryModalVisible"
      @created="onCategoryCreated"
      @saved="loadCategories"
    />
  </a-drawer>
</template>

<style scoped>
.drawer-body {
  display: block;
  width: 100%;
}

.category-picker {
  display: flex;
  gap: 8px;
  width: 100%;
}

.category-picker__select {
  flex: 1;
}

.amount-input {
  width: 100%;
}

.view-meta {
  margin-bottom: 8px;
}

.form-footer {
  margin-top: 8px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
  text-align: right;
}
</style>

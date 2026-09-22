<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { getAccounts, type AccountTreeNode } from '@/api/account'
import { getAccountMappings, updateAccountMappings, type AccountMapping } from '@/api/voucher'
import { Message } from '@arco-design/web-vue'

// —— types ——
/** 科目树选择项（非末级 / 停用科目不可选，但保留在选项内以便回显） */
interface AccountOption {
  key: string
  title: string
  disabled: boolean
  children: AccountOption[]
}

/** 映射表单行（accountId 为空表示未配置） */
interface MappingFormRow {
  key: string
  label: string
  accountId: string | undefined
}

// —— constants ——
/** 科目树字段映射（与 AccountTreeNode 的 key / title / children / disabled 对应） */
const treeFieldNames = { key: 'key', title: 'title', children: 'children', disabled: 'disabled' }

// —— props/emits ——
const props = defineProps<{ visible: boolean }>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'saved'): void
}>()

// —— reactive state ——
const loading = ref(false)
const submitting = ref(false)
const accountTree = ref<AccountOption[]>([])
const rows = ref<MappingFormRow[]>([])

// —— computed ——
const treeData = computed(() => accountTree.value)

/** 未配置任何映射时提示（缺失映射会让该来源的自动凭证被拒绝） */
const missingCount = computed(() => rows.value.filter((row) => !row.accountId).length)

// —— watch ——
watch(
  () => props.visible,
  (visible) => {
    if (visible) void load()
  },
)

// —— methods ——
/** 加载科目树与现有映射（打开抽屉时执行） */
async function load(): Promise<void> {
  loading.value = true
  try {
    const [accounts, mappings] = await Promise.all([getAccounts(), getAccountMappings()])
    accountTree.value = toOptions(accounts)
    rows.value = mappings.map((mapping: AccountMapping) => ({
      key: mapping.key,
      label: mapping.label,
      accountId: mapping.accountId || undefined,
    }))
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}

/** 保存：全量覆盖提交全部映射键 */
async function onSubmit(): Promise<void> {
  if (submitting.value) return
  const missing = rows.value.filter((row) => !row.accountId)
  if (missing.length > 0) {
    Message.warning(`请为「${missing[0]!.label}」选择科目`)
    return
  }

  submitting.value = true
  try {
    await updateAccountMappings(
      rows.value.map((row) => ({ key: row.key, accountId: row.accountId as string })),
    )
    emit('saved')
    emit('update:visible', false)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    submitting.value = false
  }
}

function onCancel(): void {
  emit('update:visible', false)
}

/** 科目树 → 选择项：末级且启用方可选，其余仅用于展示层级与回显 */
function toOptions(nodes: AccountTreeNode[]): AccountOption[] {
  return nodes.map((node) => {
    const children = toOptions(node.children)
    const isLeaf = children.length === 0
    return {
      key: node.id,
      title: `${node.code} ${node.name}`,
      disabled: !isLeaf || node.status === 0,
      children,
    }
  })
}
</script>

<template>
  <a-drawer
    :visible="props.visible"
    :width="560"
    unmount-on-close
    :footer="false"
    @cancel="onCancel"
  >
    <template #title>
      科目映射
    </template>

    <a-spin
      :loading="loading"
      style="width: 100%"
    >
      <a-alert
        v-if="missingCount > 0"
        type="warning"
        :show-icon="true"
        style="margin-bottom: 12px"
      >
        尚有 {{ missingCount }} 个映射键未配置，缺失映射会自动阻止对应单据生成凭证
      </a-alert>

      <a-form
        :model="{ rows }"
        layout="vertical"
      >
        <a-form-item
          v-for="row in rows"
          :key="row.key"
          :label="row.label"
          required
        >
          <a-tree-select
            v-model="row.accountId"
            :data="treeData"
            :field-names="treeFieldNames"
            placeholder="请选择末级科目"
            allow-search
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-spin>

    <div class="drawer-footer">
      <a-space>
        <a-button @click="onCancel">
          取消
        </a-button>
        <a-button
          type="primary"
          :loading="submitting"
          @click="onSubmit"
        >
          保存
        </a-button>
      </a-space>
    </div>
  </a-drawer>
</template>

<style scoped>
.drawer-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 16px;
  margin-top: 16px;
  border-top: 1px solid var(--color-border);
}
</style>

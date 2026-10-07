<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { getEmployees } from '@/api/employee'
import type { Employee } from '@/api/employee'
import { deletePayroll, generatePayrolls, getPayrolls, updatePayrollStatus } from '@/api/payroll'
import type { Payroll, PayrollStatus } from '@/api/payroll'
import { useAuthStore } from '@/stores/auth'
import { Message } from '@arco-design/web-vue'
import type { TableColumnData } from '@arco-design/web-vue'
import {
  IconCircleCheck,
  IconEdit,
  IconPlus,
  IconRefresh,
  IconRestore,
  IconSearch,
  IconSparkles,
  IconTrash,
} from '@tabler/icons-vue'

import PayrollFormDrawer from './PayrollFormDrawer.vue'

const auth = useAuthStore()

// —— constants ——
/** 状态文案与标签色（specs/044-erp-hcm-payroll/design.md §0.1，唯一来源） */
const statusMeta: Record<number, { label: string; color: string }> = {
  0: { label: '草稿', color: 'blue' },
  1: { label: '已发放', color: 'green' },
}

const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '已发放', value: 1 },
]

/** 年份下拉范围（当年 − 2 ~ 次年，覆盖本期录入） */
const currentYear = new Date().getFullYear()
const yearOptions = [currentYear - 2, currentYear - 1, currentYear, currentYear + 1].map((y) => ({
  label: `${y} 年`,
  value: y,
}))

const monthOptions = Array.from({ length: 12 }, (_, i) => ({ label: `${i + 1} 月`, value: i + 1 }))

/** 列表请求序号：只采纳最后一次发起的请求结果，避免慢响应覆盖新数据 */
let fetchSeq = 0

// —— reactive state ——
const loading = ref(false)
/** 正在发放的工资单 id */
const statusingId = ref<string | undefined>(undefined)
/** 正在删除的工资单 id */
const deletingId = ref<string | undefined>(undefined)
const items = ref<Payroll[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

/** 员工下拉 */
const employeeOptions = ref<Employee[]>([])

/** 筛选输入态与应用态分离（点搜索才生效） */
const yearInput = ref<number | undefined>(undefined)
const monthInput = ref<number | undefined>(undefined)
const employeeInput = ref<string | undefined>(undefined)
const statusInput = ref<PayrollStatus | undefined>(undefined)
const appliedYear = ref<number | undefined>(undefined)
const appliedMonth = ref<number | undefined>(undefined)
const appliedEmployeeId = ref<string | undefined>(undefined)
const appliedStatus = ref<PayrollStatus | undefined>(undefined)

/** 新增 / 编辑抽屉 */
const drawerVisible = ref(false)
const drawerMode = ref<'create' | 'edit'>('create')
const drawerRecord = ref<Payroll | undefined>(undefined)

/** 批量生成弹窗（期间选择） */
const generateVisible = ref(false)
const batchGenerating = ref(false)
const generateYear = ref<number>(currentYear)
const generateMonth = ref<number>(new Date().getMonth() + 1)

// —— computed ——
/** 表格重挂载 key：已应用条件变化时回到第 1 页 */
const tableKey = computed(
  () =>
    `${appliedYear.value ?? ''}|${appliedMonth.value ?? ''}|${appliedEmployeeId.value ?? ''}|${appliedStatus.value ?? ''}`,
)

/** 服务端分页配置 */
const pagination = computed(() => ({
  current: page.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: true,
  showPageSize: true,
  pageSizeOptions: [10, 20, 50],
}))

/** 员工下拉选项（姓名 + 工号） */
const employeeSelectOptions = computed(() =>
  employeeOptions.value.map((e) => ({ label: `${e.name}（${e.employeeNo}）`, value: e.id })),
)

/** 列定义（操作列 3 个操作平铺，宽 220，specs/011-action-column §0） */
const columns: TableColumnData[] = [
  { title: '序号', slotName: 'seq', width: 64, align: 'center' },
  { title: '员工', dataIndex: 'employeeName', width: 120, ellipsis: true, tooltip: true },
  { title: '期间', slotName: 'period', width: 110, align: 'center' },
  { title: '基本工资', slotName: 'baseSalary', width: 110, align: 'right' },
  { title: '津贴', slotName: 'allowance', width: 100, align: 'right' },
  { title: '扣款', slotName: 'deduction', width: 100, align: 'right' },
  { title: '实发', slotName: 'netPay', width: 110, align: 'right' },
  { title: '状态', slotName: 'status', width: 90, align: 'center' },
  { title: '操作', slotName: 'action', width: 220, bodyCellClass: 'action-cell' },
]

/** 各列固定宽度之和，作为表格横向滚动最小宽度 */
const tableScrollX = columns.reduce((sum, c) => sum + (c.width ?? 0), 0)

// —— lifecycle ——
onMounted(() => {
  void fetchList()
  void fetchEmployees()
})

// —— methods ——
/** 拉取员工下拉（含离职员工：历史工资单可能属于已离职员工） */
async function fetchEmployees(): Promise<void> {
  try {
    const result = await getEmployees({ page: 1, pageSize: 100 })
    employeeOptions.value = result.items
  } catch {
    // 错误提示已由请求层统一处理
  }
}

/** 拉取当前条件下的列表（请求序号防止乱序响应覆盖最新结果） */
async function fetchList(): Promise<void> {
  const seq = ++fetchSeq
  loading.value = true
  try {
    const result = await getPayrolls({
      year: appliedYear.value,
      month: appliedMonth.value,
      employeeId: appliedEmployeeId.value,
      status: appliedStatus.value,
      page: page.value,
      pageSize: pageSize.value,
    })
    if (seq !== fetchSeq) return
    items.value = result.items
    total.value = result.total
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

/** 搜索：应用输入条件并回到第 1 页 */
function onSearch(): void {
  appliedYear.value = yearInput.value
  appliedMonth.value = monthInput.value
  appliedEmployeeId.value = employeeInput.value
  appliedStatus.value = statusInput.value
  page.value = 1
  void fetchList()
}

/** 重置：清空条件并回到第 1 页 */
function onReset(): void {
  yearInput.value = undefined
  monthInput.value = undefined
  employeeInput.value = undefined
  statusInput.value = undefined
  appliedYear.value = undefined
  appliedMonth.value = undefined
  appliedEmployeeId.value = undefined
  appliedStatus.value = undefined
  page.value = 1
  void fetchList()
}

/** 刷新当前页 */
function onRefresh(): void {
  void fetchList()
}

function onPageChange(current: number): void {
  page.value = current
  void fetchList()
}

function onPageSizeChange(size: number): void {
  pageSize.value = size
  page.value = 1
  void fetchList()
}

/** 新增（抽屉） */
function onCreate(): void {
  drawerMode.value = 'create'
  drawerRecord.value = undefined
  drawerVisible.value = true
}

/** 编辑（抽屉，直接以列表行回填：列表已含全部字段） */
function onEdit(row: Payroll): void {
  drawerMode.value = 'edit'
  drawerRecord.value = row
  drawerVisible.value = true
}

/** 抽屉保存后刷新列表 */
function onSaved(): void {
  void fetchList()
}

/** 打开批量生成弹窗（默认当前年月） */
function onOpenGenerate(): void {
  const now = new Date()
  generateYear.value = now.getFullYear()
  generateMonth.value = now.getMonth() + 1
  generateVisible.value = true
}

/** 批量生成（弹窗确认，返回 false 保持弹窗不关闭）：为在职员工生成草稿，已存在则跳过 */
async function onBeforeGenerate(): Promise<boolean> {
  if (batchGenerating.value) return false
  batchGenerating.value = true
  try {
    const result = await generatePayrolls(generateYear.value, generateMonth.value)
    Message.success(`批量生成完成：新增 ${result.created} 条、跳过 ${result.skipped} 条`)
    void fetchList()
    return true
  } catch {
    // 错误提示已由请求层统一处理
    return false
  } finally {
    batchGenerating.value = false
  }
}

/** 发放工资单（二次确认后执行；发放后锁定） */
async function onMarkPaid(row: Payroll): Promise<void> {
  if (statusingId.value) return
  statusingId.value = row.id
  try {
    await updatePayrollStatus(row.id, 1)
    Message.success('工资单已发放')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    statusingId.value = undefined
  }
}

/** 删除（二次确认后执行；已发放不可删除） */
async function onDelete(row: Payroll): Promise<void> {
  if (deletingId.value) return
  deletingId.value = row.id
  try {
    await deletePayroll(row.id)
    Message.success('工资单已删除')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    deletingId.value = undefined
  }
}

/** 状态文案（未知值回退占位） */
function statusText(status: number): string {
  return statusMeta[status]?.label ?? '-'
}

/** 状态标签色 */
function statusColor(status: number): string {
  return statusMeta[status]?.color ?? 'gray'
}

/** 期间文本（yyyy-MM） */
function periodText(row: Payroll): string {
  return `${row.year}-${String(row.month).padStart(2, '0')}`
}

/** 金额展示（两位小数） */
function moneyText(value: number): string {
  return value.toFixed(2)
}
</script>

<template>
  <div class="list-page">
    <!-- 页面头：仅标题（操作已并入表格上方工具条） -->
    <div class="page-header">
      <h1 class="page-title">
        薪酬
      </h1>
    </div>

    <a-card
      :bordered="false"
      class="table-card"
    >
      <div class="toolbar">
        <!-- 筛选行 -->
        <a-row
          class="toolbar-filter"
          :gutter="16"
          wrap
        >
          <a-col :span="4">
            <a-select
              v-model="yearInput"
              :options="yearOptions"
              placeholder="全部年份"
              allow-clear
            />
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="monthInput"
              :options="monthOptions"
              placeholder="全部月份"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <a-select
              v-model="employeeInput"
              :options="employeeSelectOptions"
              placeholder="全部员工"
              allow-clear
              allow-search
            />
          </a-col>
          <a-col :span="3">
            <a-select
              v-model="statusInput"
              :options="statusOptions"
              placeholder="全部状态"
              allow-clear
            />
          </a-col>
          <a-col :span="5">
            <div class="toolbar-filter__actions">
              <a-button
                type="primary"
                :loading="loading"
                @click="onSearch"
              >
                <template #icon>
                  <IconSearch />
                </template>
                搜索
              </a-button>
              <a-button
                :loading="loading"
                @click="onReset"
              >
                <template #icon>
                  <IconRestore />
                </template>
                重置
              </a-button>
            </div>
          </a-col>
        </a-row>

        <!-- 操作行：左组主操作靠左，右组数据操作靠右 -->
        <div class="toolbar-actions">
          <div class="toolbar-actions__left">
            <a-button
              v-if="auth.hasPermission('payroll.create')"
              type="primary"
              size="small"
              @click="onOpenGenerate"
            >
              <template #icon>
                <IconSparkles />
              </template>
              批量生成
            </a-button>
            <a-button
              v-if="auth.hasPermission('payroll.create')"
              size="small"
              @click="onCreate"
            >
              <template #icon>
                <IconPlus />
              </template>
              新增工资单
            </a-button>
          </div>
          <div class="toolbar-actions__right">
            <a-button
              size="small"
              :loading="loading"
              @click="onRefresh"
            >
              <template #icon>
                <IconRefresh />
              </template>
              刷新
            </a-button>
          </div>
        </div>
      </div>

      <a-table
        :key="tableKey"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="items"
        :pagination="pagination"
        :scroll="{ x: tableScrollX }"
        @page-change="onPageChange"
        @page-size-change="onPageSizeChange"
      >
        <template #seq="{ rowIndex }">
          {{ (page - 1) * pageSize + rowIndex + 1 }}
        </template>
        <template #period="{ record }">
          {{ periodText(record as Payroll) }}
        </template>
        <template #baseSalary="{ record }">
          {{ moneyText((record as Payroll).baseSalary) }}
        </template>
        <template #allowance="{ record }">
          {{ moneyText((record as Payroll).allowance) }}
        </template>
        <template #deduction="{ record }">
          {{ moneyText((record as Payroll).deduction) }}
        </template>
        <template #netPay="{ record }">
          <span class="net-pay">{{ moneyText((record as Payroll).netPay) }}</span>
        </template>
        <template #status="{ record }">
          <a-tag :color="statusColor((record as Payroll).status)">
            {{ statusText((record as Payroll).status) }}
          </a-tag>
        </template>
        <!-- 操作列（specs/011-action-column）：3 个操作平铺 编辑 / 发放 / 删除；已发放行全部置灰 -->
        <template #action="{ record }">
          <a-space
            class="row-actions"
            :size="4"
          >
            <a-button
              v-if="auth.hasPermission('payroll.update')"
              type="text"
              size="small"
              :disabled="(record as Payroll).status === 1"
              @click="onEdit(record as Payroll)"
            >
              <template #icon>
                <IconEdit />
              </template>
              编辑
            </a-button>
            <a-popconfirm
              v-if="auth.hasPermission('payroll.status')"
              type="warning"
              content="确认发放该工资单？发放后不可修改与删除。"
              @ok="onMarkPaid(record as Payroll)"
            >
              <a-button
                type="text"
                status="success"
                size="small"
                :disabled="(record as Payroll).status === 1"
                :loading="statusingId === (record as Payroll).id"
              >
                <template #icon>
                  <IconCircleCheck />
                </template>
                发放
              </a-button>
            </a-popconfirm>
            <a-popconfirm
              v-if="auth.hasPermission('payroll.delete')"
              type="warning"
              content="确认删除该工资单？"
              @ok="onDelete(record as Payroll)"
            >
              <a-button
                type="text"
                status="danger"
                size="small"
                :disabled="(record as Payroll).status === 1"
                :loading="deletingId === (record as Payroll).id"
              >
                <template #icon>
                  <IconTrash />
                </template>
                删除
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </a-table>
    </a-card>

    <PayrollFormDrawer
      v-model:visible="drawerVisible"
      :mode="drawerMode"
      :record="drawerRecord"
      :employee-options="employeeOptions"
      @saved="onSaved"
    />

    <!-- 批量生成：选择期间后为在职员工生成草稿（已存在则跳过） -->
    <a-modal
      v-model:visible="generateVisible"
      title="批量生成工资单"
      :ok-loading="batchGenerating"
      :on-before-ok="onBeforeGenerate"
      ok-text="生成"
      cancel-text="取消"
    >
      <a-form
        :model="{ year: generateYear, month: generateMonth }"
        layout="vertical"
      >
        <a-form-item label="期间">
          <a-space>
            <a-select
              v-model="generateYear"
              :options="yearOptions"
              placeholder="年份"
              class="generate-period"
            />
            <a-select
              v-model="generateMonth"
              :options="monthOptions"
              placeholder="月份"
              class="generate-period"
            />
          </a-space>
        </a-form-item>
        <div class="generate-tip">
          为在职员工各生成一条草稿（基本工资为 0，需人工填写）；该期间已存在的工资单自动跳过。
        </div>
      </a-form>
    </a-modal>
  </div>
</template>

<style scoped>
.list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

/* 操作列密度（specs/011-action-column §0）：收窄 Arco 文本按钮默认水平 padding */
.row-actions :deep(.arco-btn-text) {
  padding: 0 8px;
}

/* 操作列兜底：按钮组不折行 */
::deep(.action-cell) {
  white-space: nowrap;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.page-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-1);
}

.net-pay {
  font-weight: 600;
}

.toolbar-filter {
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--color-border);
}

.toolbar-filter .arco-col {
  display: flex;
}

.toolbar-filter .arco-col > .arco-select {
  flex: 1;
}

.toolbar-filter__actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.toolbar-actions__left,
.toolbar-actions__right {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.generate-period {
  width: 140px;
}

.generate-tip {
  color: var(--color-text-3);
  font-size: 12px;
  line-height: 1.6;
}

.table-card {
  border-radius: var(--border-radius-medium);
}
</style>

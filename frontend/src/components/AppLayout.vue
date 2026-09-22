<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import {
  IconApps,
  IconArrowsExchange,
  IconBook2,
  IconBriefcase,
  IconBuildingWarehouse,
  IconCalculator,
  IconCash,
  IconChartBar,
  IconClipboardCheck,
  IconClipboardList,
  IconClipboardText,
  IconCoin,
  IconComponents,
  IconDatabase,
  IconFileInvoice,
  IconFileText,
  IconHistory,
  IconHome,
  IconIdBadge2,
  IconInvoice,
  IconLayoutSidebarLeftCollapse,
  IconLayoutSidebarLeftExpand,
  IconList,
  IconPackage,
  IconPackages,
  IconPercentage,
  IconReceipt,
  IconReceiptRefund,
  IconScale,
  IconSettings,
  IconShieldLock,
  IconShoppingBag,
  IconShoppingCart,
  IconSitemap,
  IconStack2,
  IconTags,
  IconTruckDelivery,
  IconTruckReturn,
  IconUser,
  IconUsers,
  IconWallet,
} from '@tabler/icons-vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/** 侧边栏折叠状态（内存态，不持久化） */
const collapsed = ref<boolean>(false)

/** 详情等子路由归属到所属一级菜单，保证侧边栏高亮正确 */
const MENU_ROUTE_MAP: Record<string, string> = {
  userDetail: 'users',
  invoiceDetail: 'invoices',
  purchaseOrderEdit: 'purchaseOrders',
  purchaseOrderDetail: 'purchaseOrders',
  purchaseDetail: 'purchases',
  purchaseReturnDetail: 'purchaseReturns',
  salesOrderEdit: 'salesOrders',
  salesOrderDetail: 'salesOrders',
  salesDetail: 'sales',
  saleReturnDetail: 'salesReturns',
  settlementDetail: 'settlements',
}

/** 菜单选中项：与当前路由名联动（单一数据源） */
const selectedKeys = computed<string[]>(() => {
  const name = typeof route.name === 'string' ? route.name : ''
  return name ? [MENU_ROUTE_MAP[name] ?? name] : []
})

// —— constants ——

/**
 * 顶级分组 key → 该分组内的路由名集合（进入这些路由时自动展开所属分组）。
 * 分组与子项规划的唯一事实源见 specs/025-erp-report/design.md §0.2（未实现的后续规格子项落地时续行）。
 */
const MENU_GROUPS: Record<string, string[]> = {
  showcase: ['components', 'list', 'form'],
  basedata: ['products', 'categories', 'partners'],
  purchase: ['purchaseOrders', 'purchaseOrderNew', 'purchaseOrderEdit', 'purchaseOrderDetail', 'purchases', 'purchaseNew', 'purchaseReturns', 'purchaseReturnNew', 'purchaseReturnDetail'],
  sale: ['salesOrders', 'salesOrderNew', 'salesOrderEdit', 'salesOrderDetail', 'sales', 'salesNew', 'salesReturns', 'saleReturnNew', 'saleReturnDetail'],
  stock: ['inventory', 'stockMovements', 'stockTakes', 'stockTakeNew', 'stockTakeDetail'],
  fund: ['settlements', 'settlementNew', 'settlementDetail', 'reconciliation', 'invoices', 'invoiceNew', 'invoiceDetail'],
  finance: ['accounts', 'taxRates'],
  report: ['inventoryFlowReport', 'stockBalanceReport', 'purchaseSummaryReport', 'salesSummaryReport', 'costProfitReport'],
  system: ['users', 'userDetail', 'loginLogs', 'auditLogs', 'roles', 'departments', 'positions', 'employees'],
}

/**
 * 菜单项 key → 权限点（唯一事实源 `specs/028-erp-rbac/design.md` §0.2）。
 * 未登记的菜单项（首页 / 示例页面）恒可见；分组可见性 = 组内已登记菜单项任一可见。
 */
const MENU_PERMISSIONS: Record<string, string> = {
  products: 'products.view',
  categories: 'categories.view',
  partners: 'partners.view',
  purchaseOrders: 'purchaseOrders.view',
  purchases: 'purchases.view',
  purchaseReturns: 'purchaseReturns.view',
  salesOrders: 'salesOrders.view',
  sales: 'sales.view',
  salesReturns: 'salesReturns.view',
  inventory: 'inventory.view',
  stockMovements: 'stockMovements.view',
  stockTakes: 'stockTakes.view',
  settlements: 'settlements.view',
  reconciliation: 'reconciliation.view',
  invoices: 'invoices.view',
  accounts: 'accounts.view',
  taxRates: 'taxRates.view',
  inventoryFlowReport: 'reports.view',
  stockBalanceReport: 'reports.view',
  purchaseSummaryReport: 'reports.view',
  salesSummaryReport: 'reports.view',
  costProfitReport: 'reports.view',
  users: 'users.view',
  loginLogs: 'loginLogs.view',
  auditLogs: 'auditLogs.view',
  roles: 'roles.view',
  departments: 'departments.view',
  positions: 'positions.view',
  employees: 'employees.view',
}

// —— reactive state ——

/** 展开的子菜单 key（受控，默认全部折叠；进入所属页面时自动展开对应分组，用户手动折叠由 @update:open-keys 同步） */
const openKeys = ref<string[]>([])

// —— computed ——

/** 当前用户名（未加载时显示占位） */
const displayName = computed<string>(() => auth.user?.displayName ?? '用户')

// —— watch ——

// 初始化与路由变化时，自动展开当前路由所属的子菜单（只增不减：不折叠用户手动展开的其他分组）
watch(
  () => route.name,
  (name) => {
    if (typeof name !== 'string') return
    const group = Object.keys(MENU_GROUPS).find((key) => MENU_GROUPS[key].includes(name))
    if (group && !openKeys.value.includes(group)) {
      openKeys.value = [...openKeys.value, group]
    }
  },
  { immediate: true },
)

onMounted(() => {
  // 进入受保护子页面后恢复用户信息（从 HomeView 上移至布局，覆盖所有子页面）
  void auth.fetchCurrentUser()
})

// —— methods ——

/** 菜单项是否可见：未登记权限点（首页 / 示例页面）恒可见，否则需命中权限点 */
function isMenuVisible(key: string): boolean {
  const permission = MENU_PERMISSIONS[key] ?? ''
  return !permission || auth.hasPermission(permission)
}

/** 分组是否可见：组内已登记菜单项任一可见即展示（示例页面分组无登记项，恒可见） */
function isGroupVisible(group: string): boolean {
  const items = (MENU_GROUPS[group] ?? []).filter((key) => key in MENU_PERMISSIONS)
  if (items.length === 0) return true
  return items.some((key) => isMenuVisible(key))
}

function onMenuItemClick(key: string): void {
  void router.push({ name: key })
}

function onLogout(): void {
  auth.logout()
  void router.replace({ name: 'login' })
}
</script>

<template>
  <a-layout class="app-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      class="app-sider"
      :collapsed-width="64"
      :width="208"
      collapsible
      breakpoint="lg"
      :hide-trigger="true"
    >
      <div class="app-logo">
        <span v-if="collapsed">A</span>
        <span
          v-else
          class="app-logo-full"
        >App</span>
      </div>
      <a-menu
        :selected-keys="selectedKeys"
        :open-keys="openKeys"
        :collapsed="collapsed"
        @menu-item-click="onMenuItemClick"
        @update:open-keys="(keys: string[]) => (openKeys = keys)"
      >
        <a-menu-item key="home">
          <template #icon>
            <IconHome />
          </template>
          <span>首页</span>
        </a-menu-item>
        <a-sub-menu key="showcase">
          <template #icon>
            <IconApps />
          </template>
          <template #title>
            <span>示例页面</span>
          </template>
          <a-menu-item key="components">
            <template #icon>
              <IconComponents />
            </template>
            <span>组件示例</span>
          </a-menu-item>
          <a-menu-item key="list">
            <template #icon>
              <IconList />
            </template>
            <span>列表示例</span>
          </a-menu-item>
          <a-menu-item key="form">
            <template #icon>
              <IconFileText />
            </template>
            <span>表单与详情示例</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('basedata')"
          key="basedata"
        >
          <template #icon>
            <IconDatabase />
          </template>
          <template #title>
            <span>基础档案</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('products')"
            key="products"
          >
            <template #icon>
              <IconPackage />
            </template>
            <span>商品管理</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('categories')"
            key="categories"
          >
            <template #icon>
              <IconTags />
            </template>
            <span>分类管理</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('partners')"
            key="partners"
          >
            <template #icon>
              <IconUsers />
            </template>
            <span>往来单位</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('purchase')"
          key="purchase"
        >
          <template #icon>
            <IconShoppingCart />
          </template>
          <template #title>
            <span>采购</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('purchaseOrders')"
            key="purchaseOrders"
          >
            <template #icon>
              <IconClipboardList />
            </template>
            <span>采购订单</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('purchases')"
            key="purchases"
          >
            <template #icon>
              <IconTruckDelivery />
            </template>
            <span>采购入库</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('purchaseReturns')"
            key="purchaseReturns"
          >
            <template #icon>
              <IconReceiptRefund />
            </template>
            <span>采购退货</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('sale')"
          key="sale"
        >
          <template #icon>
            <IconShoppingBag />
          </template>
          <template #title>
            <span>销售</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('salesOrders')"
            key="salesOrders"
          >
            <template #icon>
              <IconFileInvoice />
            </template>
            <span>销售订单</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('sales')"
            key="sales"
          >
            <template #icon>
              <IconReceipt />
            </template>
            <span>销售出库</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('salesReturns')"
            key="salesReturns"
          >
            <template #icon>
              <IconTruckReturn />
            </template>
            <span>销售退货</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('stock')"
          key="stock"
        >
          <template #icon>
            <IconBuildingWarehouse />
          </template>
          <template #title>
            <span>库存</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('inventory')"
            key="inventory"
          >
            <template #icon>
              <IconPackages />
            </template>
            <span>库存查询</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('stockMovements')"
            key="stockMovements"
          >
            <template #icon>
              <IconStack2 />
            </template>
            <span>库存流水</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('stockTakes')"
            key="stockTakes"
          >
            <template #icon>
              <IconClipboardCheck />
            </template>
            <span>库存盘点</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('fund')"
          key="fund"
        >
          <template #icon>
            <IconWallet />
          </template>
          <template #title>
            <span>资金</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('settlements')"
            key="settlements"
          >
            <template #icon>
              <IconCash />
            </template>
            <span>收付款</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('reconciliation')"
            key="reconciliation"
          >
            <template #icon>
              <IconScale />
            </template>
            <span>往来对账</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('invoices')"
            key="invoices"
          >
            <template #icon>
              <IconInvoice />
            </template>
            <span>发票登记</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('finance')"
          key="finance"
        >
          <template #icon>
            <IconCalculator />
          </template>
          <template #title>
            <span>财务</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('accounts')"
            key="accounts"
          >
            <template #icon>
              <IconBook2 />
            </template>
            <span>会计科目</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('taxRates')"
            key="taxRates"
          >
            <template #icon>
              <IconPercentage />
            </template>
            <span>税率</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('report')"
          key="report"
        >
          <template #icon>
            <IconChartBar />
          </template>
          <template #title>
            <span>报表</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('inventoryFlowReport')"
            key="inventoryFlowReport"
          >
            <template #icon>
              <IconArrowsExchange />
            </template>
            <span>进销存报表</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('stockBalanceReport')"
            key="stockBalanceReport"
          >
            <template #icon>
              <IconStack2 />
            </template>
            <span>库存余额表</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('purchaseSummaryReport')"
            key="purchaseSummaryReport"
          >
            <template #icon>
              <IconShoppingCart />
            </template>
            <span>采购汇总</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('salesSummaryReport')"
            key="salesSummaryReport"
          >
            <template #icon>
              <IconShoppingBag />
            </template>
            <span>销售汇总</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('costProfitReport')"
            key="costProfitReport"
          >
            <template #icon>
              <IconCoin />
            </template>
            <span>成本与毛利</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu
          v-if="isGroupVisible('system')"
          key="system"
        >
          <template #icon>
            <IconSettings />
          </template>
          <template #title>
            <span>系统</span>
          </template>
          <a-menu-item
            v-if="isMenuVisible('users')"
            key="users"
          >
            <template #icon>
              <IconUser />
            </template>
            <span>用户管理</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('loginLogs')"
            key="loginLogs"
          >
            <template #icon>
              <IconHistory />
            </template>
            <span>登录日志</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('auditLogs')"
            key="auditLogs"
          >
            <template #icon>
              <IconClipboardText />
            </template>
            <span>操作日志</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('roles')"
            key="roles"
          >
            <template #icon>
              <IconShieldLock />
            </template>
            <span>角色权限</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('departments')"
            key="departments"
          >
            <template #icon>
              <IconSitemap />
            </template>
            <span>部门管理</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('positions')"
            key="positions"
          >
            <template #icon>
              <IconBriefcase />
            </template>
            <span>岗位管理</span>
          </a-menu-item>
          <a-menu-item
            v-if="isMenuVisible('employees')"
            key="employees"
          >
            <template #icon>
              <IconIdBadge2 />
            </template>
            <span>员工档案</span>
          </a-menu-item>
        </a-sub-menu>
      </a-menu>
    </a-layout-sider>
    <a-layout class="app-main">
      <a-layout-header class="app-header">
        <div class="app-header-left">
          <a-button
            class="collapse-trigger"
            type="text"
            :aria-label="collapsed ? '展开侧边栏' : '折叠侧边栏'"
            @click="collapsed = !collapsed"
          >
            <IconLayoutSidebarLeftExpand v-if="collapsed" />
            <IconLayoutSidebarLeftCollapse v-else />
          </a-button>
          <span class="app-title">{{ collapsed ? '' : '应用管理' }}</span>
        </div>
        <div class="app-header-right">
          <a-dropdown
            trigger="click"
            position="br"
          >
            <div
              class="user-trigger"
              role="button"
              aria-label="用户菜单"
            >
              <IconUser class="user-icon" />
              <span class="user-name">{{ displayName }}</span>
            </div>
            <template #content>
              <a-doption @click="onLogout">
                退出登录
              </a-doption>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>
      <a-layout-content class="app-content">
        <RouterView />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<style scoped>
.app-layout {
  height: 100%;
}

.app-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 56px;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-1);
  border-bottom: 1px solid var(--color-border);
  overflow: hidden;
  white-space: nowrap;
}

.app-main {
  height: 100%;
}

.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  padding: 0 24px;
  background: var(--color-bg-2);
  border-bottom: 1px solid var(--color-border-2);
}

.app-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.app-title {
  font-size: 16px;
  font-weight: 600;
}

.app-header-right {
  display: flex;
  align-items: center;
}

.user-trigger {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: var(--border-radius-small);
  cursor: pointer;
  color: var(--color-text-1);
  transition: background var(--action-duration);
}

.user-trigger:hover {
  background: var(--color-fill-2);
}

.user-icon {
  font-size: 20px;
  color: var(--color-text-3);
}

/* Tabler 图标默认 24px，统一收敛到 18px，与 Arco 菜单 / 文本按钮的视觉字号一致；
   线宽 2 在 18px 下偏淡，提到 2.5（仍在 Tabler 的 2~3 视觉区间，不与 Arco 的 4 混淆） */
.app-sider :deep(svg),
.collapse-trigger :deep(svg),
.user-icon {
  width: 18px;
  height: 18px;
  stroke-width: 2.5;
}

.user-name {
  font-size: 14px;
}

.app-content {
  padding: 24px;
  background: var(--color-fill-2);
}
</style>

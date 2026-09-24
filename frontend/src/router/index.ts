import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { setUnauthorizedHandler } from '@/api/request'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true },
  },
  {
    // 全局布局父路由：承载各业务页面（侧边菜单 + 顶部栏 + 内容区）
    path: '/',
    name: 'layout',
    component: () => import('@/components/AppLayout.vue'),
    children: [
      {
        path: '',
        name: 'home',
        component: () => import('@/views/HomeView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'components',
        name: 'components',
        component: () => import('@/views/Showcase/ComponentShowcaseView.vue'),
        meta: { public: true },
      },
      {
        path: 'list',
        name: 'list',
        component: () => import('@/views/Showcase/ListShowcaseView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'form',
        name: 'form',
        component: () => import('@/views/Showcase/FormShowcaseView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'form/new',
        name: 'formNew',
        component: () => import('@/views/Showcase/FormPageFormView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'form/edit/:id',
        name: 'formEdit',
        component: () => import('@/views/Showcase/FormPageFormView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'form/detail/:id',
        name: 'formDetail',
        component: () => import('@/views/Showcase/FormDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('@/views/UserManagement/UsersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'users/detail/:id',
        name: 'userDetail',
        component: () => import('@/views/UserManagement/UserDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'login-logs',
        name: 'loginLogs',
        component: () => import('@/views/LoginLogManagement/LoginLogsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'roles',
        name: 'roles',
        component: () => import('@/views/RoleManagement/RolesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'audit-logs',
        name: 'auditLogs',
        component: () => import('@/views/AuditLogManagement/AuditLogsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'departments',
        name: 'departments',
        component: () => import('@/views/OrgManagement/DepartmentsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'positions',
        name: 'positions',
        component: () => import('@/views/OrgManagement/PositionsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'employees',
        name: 'employees',
        component: () => import('@/views/OrgManagement/EmployeesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'accounts',
        name: 'accounts',
        component: () => import('@/views/FinanceManagement/AccountsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'tax-rates',
        name: 'taxRates',
        component: () => import('@/views/FinanceManagement/TaxRatesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'bank-accounts',
        name: 'bankAccounts',
        component: () => import('@/views/CashManagement/BankAccountsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'cash-journals',
        name: 'cashJournals',
        component: () => import('@/views/CashManagement/CashJournalsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'products',
        name: 'products',
        component: () => import('@/views/ProductManagement/ProductsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'warehouses',
        name: 'warehouses',
        component: () => import('@/views/WarehouseManagement/WarehousesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'categories',
        name: 'categories',
        component: () => import('@/views/CategoryManagement/CategoriesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'partners',
        name: 'partners',
        component: () => import('@/views/PartnerManagement/PartnersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'inventory',
        name: 'inventory',
        component: () => import('@/views/InventoryManagement/InventoryView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'stock-movements',
        name: 'stockMovements',
        component: () => import('@/views/StockMovementManagement/StockMovementsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'stock-takes',
        name: 'stockTakes',
        component: () => import('@/views/StockTakeManagement/StockTakesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'stock-takes/new',
        name: 'stockTakeNew',
        component: () => import('@/views/StockTakeManagement/StockTakeFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'stock-takes/detail/:id',
        name: 'stockTakeDetail',
        component: () => import('@/views/StockTakeManagement/StockTakeDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-orders',
        name: 'purchaseOrders',
        component: () => import('@/views/PurchaseOrderManagement/PurchaseOrdersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-orders/new',
        name: 'purchaseOrderNew',
        component: () => import('@/views/PurchaseOrderManagement/PurchaseOrderFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-orders/edit/:id',
        name: 'purchaseOrderEdit',
        component: () => import('@/views/PurchaseOrderManagement/PurchaseOrderFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-orders/detail/:id',
        name: 'purchaseOrderDetail',
        component: () => import('@/views/PurchaseOrderManagement/PurchaseOrderDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchases',
        name: 'purchases',
        component: () => import('@/views/PurchaseManagement/PurchasesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchases/new',
        name: 'purchaseNew',
        component: () => import('@/views/PurchaseManagement/PurchaseFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchases/detail/:id',
        name: 'purchaseDetail',
        component: () => import('@/views/PurchaseManagement/PurchaseDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-returns',
        name: 'purchaseReturns',
        component: () => import('@/views/PurchaseReturnManagement/PurchaseReturnsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-returns/new',
        name: 'purchaseReturnNew',
        component: () => import('@/views/PurchaseReturnManagement/PurchaseReturnFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'purchase-returns/detail/:id',
        name: 'purchaseReturnDetail',
        component: () => import('@/views/PurchaseReturnManagement/PurchaseReturnDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales',
        name: 'sales',
        component: () => import('@/views/SalesManagement/SalesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales/new',
        name: 'salesNew',
        component: () => import('@/views/SalesManagement/SaleFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales/detail/:id',
        name: 'salesDetail',
        component: () => import('@/views/SalesManagement/SaleDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'quotations',
        name: 'quotations',
        component: () => import('@/views/QuotationManagement/QuotationsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'quotations/new',
        name: 'quotationCreate',
        component: () => import('@/views/QuotationManagement/QuotationFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'quotations/edit/:id',
        name: 'quotationEdit',
        component: () => import('@/views/QuotationManagement/QuotationFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'quotations/detail/:id',
        name: 'quotationDetail',
        component: () => import('@/views/QuotationManagement/QuotationDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-orders',
        name: 'salesOrders',
        component: () => import('@/views/SalesOrderManagement/SalesOrdersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-orders/new',
        name: 'salesOrderNew',
        component: () => import('@/views/SalesOrderManagement/SalesOrderFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-orders/edit/:id',
        name: 'salesOrderEdit',
        component: () => import('@/views/SalesOrderManagement/SalesOrderFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-orders/detail/:id',
        name: 'salesOrderDetail',
        component: () => import('@/views/SalesOrderManagement/SalesOrderDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-returns',
        name: 'salesReturns',
        component: () => import('@/views/SalesReturnManagement/SalesReturnsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-returns/new',
        name: 'saleReturnNew',
        component: () => import('@/views/SalesReturnManagement/SalesReturnFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'sales-returns/detail/:id',
        name: 'saleReturnDetail',
        component: () => import('@/views/SalesReturnManagement/SalesReturnDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'transfers',
        name: 'transfers',
        component: () => import('@/views/TransferManagement/TransfersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'transfers/new',
        name: 'transferNew',
        component: () => import('@/views/TransferManagement/TransferFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'transfers/detail/:id',
        name: 'transferDetail',
        component: () => import('@/views/TransferManagement/TransferDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'settlements',
        name: 'settlements',
        component: () => import('@/views/SettlementManagement/SettlementsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'settlements/new',
        name: 'settlementNew',
        component: () => import('@/views/SettlementManagement/SettlementFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'settlements/detail/:id',
        name: 'settlementDetail',
        component: () => import('@/views/SettlementManagement/SettlementDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reconciliation',
        name: 'reconciliation',
        component: () => import('@/views/SettlementManagement/ReconciliationView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'partner-prices',
        name: 'partnerPrices',
        component: () => import('@/views/PartnerPriceManagement/PartnerPricesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'invoices',
        name: 'invoices',
        component: () => import('@/views/InvoiceManagement/InvoicesView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'invoices/new',
        name: 'invoiceNew',
        component: () => import('@/views/InvoiceManagement/InvoiceFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'invoices/detail/:id',
        name: 'invoiceDetail',
        component: () => import('@/views/InvoiceManagement/InvoiceDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'vouchers',
        name: 'vouchers',
        component: () => import('@/views/GeneralLedgerManagement/VouchersView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'vouchers/new',
        name: 'voucherNew',
        component: () => import('@/views/GeneralLedgerManagement/VoucherFormPage.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'vouchers/detail/:id',
        name: 'voucherDetail',
        component: () => import('@/views/GeneralLedgerManagement/VoucherDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'financial-reports',
        name: 'financialReports',
        component: () => import('@/views/GeneralLedgerManagement/FinancialReportsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reports/inventory-flow',
        name: 'inventoryFlowReport',
        component: () => import('@/views/ReportManagement/InventoryFlowReportView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reports/stock-balance',
        name: 'stockBalanceReport',
        component: () => import('@/views/ReportManagement/StockBalanceReportView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reports/purchase-summary',
        name: 'purchaseSummaryReport',
        component: () => import('@/views/ReportManagement/PurchaseSummaryReportView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reports/sales-summary',
        name: 'salesSummaryReport',
        component: () => import('@/views/ReportManagement/SalesSummaryReportView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'reports/cost-profit',
        name: 'costProfitReport',
        component: () => import('@/views/ReportManagement/CostProfitReportView.vue'),
        meta: { requiresAuth: true },
      },
    ],
  },
  // 单据打印视图（specs/027-erp-export §0.3）：顶层路由，不进 AppLayout（无侧边栏 / 工具条），不进侧边菜单
  {
    path: '/print/purchases/:id',
    name: 'purchasePrint',
    component: () => import('@/views/PurchaseManagement/PurchasePrintView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/print/sales/:id',
    name: 'salePrint',
    component: () => import('@/views/SalesManagement/SalePrintView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/print/purchase-returns/:id',
    name: 'purchaseReturnPrint',
    component: () => import('@/views/PurchaseReturnManagement/PurchaseReturnPrintView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/print/sales-returns/:id',
    name: 'saleReturnPrint',
    component: () => import('@/views/SalesReturnManagement/SalesReturnPrintView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/print/settlements/:id',
    name: 'settlementPrint',
    component: () => import('@/views/SettlementManagement/SettlementPrintView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/print/stock-takes/:id',
    name: 'stockTakePrint',
    component: () => import('@/views/StockTakeManagement/StockTakePrintView.vue'),
    meta: { requiresAuth: true },
  },
  // 无权限页（specs/028-erp-rbac §4.3）：顶层路由，不进 AppLayout（无侧边栏 / 工具条），不进侧边菜单
  {
    path: '/403',
    name: 'forbidden',
    component: () => import('@/views/ForbiddenView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
]

/**
 * 路由名 → 权限点（唯一事实源 `specs/028-erp-rbac/design.md` §0.2）。
 * 集中登记后统一注入 `meta.permission`，避免逐条散写造成漏配；
 * 未登记的路由（首页 / 示例页面）不做权限拦截。
 */
const ROUTE_PERMISSIONS: Record<string, string> = {
  roles: 'roles.view',
  users: 'users.view',
  userDetail: 'users.view',
  loginLogs: 'loginLogs.view',
  auditLogs: 'auditLogs.view',
  departments: 'departments.view',
  positions: 'positions.view',
  employees: 'employees.view',
  accounts: 'accounts.view',
  taxRates: 'taxRates.view',
  bankAccounts: 'bankAccounts.view',
  cashJournals: 'cashJournals.view',
  products: 'products.view',
  warehouses: 'warehouses.view',
  categories: 'categories.view',
  partners: 'partners.view',
  inventory: 'inventory.view',
  stockMovements: 'stockMovements.view',
  stockTakes: 'stockTakes.view',
  stockTakeNew: 'stockTakes.create',
  stockTakeDetail: 'stockTakes.view',
  purchaseOrders: 'purchaseOrders.view',
  purchaseOrderNew: 'purchaseOrders.create',
  purchaseOrderEdit: 'purchaseOrders.update',
  purchaseOrderDetail: 'purchaseOrders.view',
  purchases: 'purchases.view',
  purchaseNew: 'purchases.create',
  purchaseDetail: 'purchases.view',
  purchaseReturns: 'purchaseReturns.view',
  purchaseReturnNew: 'purchaseReturns.create',
  purchaseReturnDetail: 'purchaseReturns.view',
  quotations: 'quotations.view',
  quotationCreate: 'quotations.create',
  quotationEdit: 'quotations.update',
  quotationDetail: 'quotations.view',
  salesOrders: 'salesOrders.view',
  salesOrderNew: 'salesOrders.create',
  salesOrderEdit: 'salesOrders.update',
  salesOrderDetail: 'salesOrders.view',
  sales: 'sales.view',
  salesNew: 'sales.create',
  salesDetail: 'sales.view',
  salesReturns: 'salesReturns.view',
  saleReturnNew: 'salesReturns.create',
  saleReturnDetail: 'salesReturns.view',
  transfers: 'transfers.view',
  transferNew: 'transfers.create',
  transferDetail: 'transfers.view',
  settlements: 'settlements.view',
  settlementNew: 'settlements.create',
  settlementDetail: 'settlements.view',
  reconciliation: 'reconciliation.view',
  partnerPrices: 'partnerPrices.view',
  invoices: 'invoices.view',
  invoiceNew: 'invoices.create',
  invoiceDetail: 'invoices.view',
  vouchers: 'vouchers.view',
  voucherNew: 'vouchers.create',
  voucherDetail: 'vouchers.view',
  financialReports: 'financialReports.view',
  inventoryFlowReport: 'reports.view',
  stockBalanceReport: 'reports.view',
  purchaseSummaryReport: 'reports.view',
  salesSummaryReport: 'reports.view',
  costProfitReport: 'reports.view',
  // 打印为只读展示，复用所属域的 `.view`（`specs/028-erp-rbac` §5 决策）
  purchasePrint: 'purchases.view',
  purchaseReturnPrint: 'purchaseReturns.view',
  salePrint: 'sales.view',
  saleReturnPrint: 'salesReturns.view',
  settlementPrint: 'settlements.view',
  stockTakePrint: 'stockTakes.view',
}

/** 把登记的权限点写入路由 `meta`（含子路由） */
function applyRoutePermissions(records: RouteRecordRaw[]): void {
  records.forEach((record) => {
    const name = typeof record.name === 'string' ? record.name : ''
    const permission = ROUTE_PERMISSIONS[name]
    if (name && permission) {
      record.meta = { ...record.meta, permission }
    }
    if (record.children) applyRoutePermissions(record.children)
  })
}

applyRoutePermissions(routes)

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// 40100 统一处置：清空认证状态后以 SPA 路由跳转登录页（带 redirect 回跳目标，不整页刷新）
setUnauthorizedHandler(() => {
  const auth = useAuthStore()
  const current = router.currentRoute.value
  // 顺序不可颠倒：先清认证状态再跳转，否则守卫会把 /login 重定向回首页
  auth.logout()
  if (current.name === 'login') return
  router.replace({ name: 'login', query: { redirect: current.fullPath } }).catch(() => {
    // 重复导航 / 导航被取消等场景，无需处理
  })
})

// 全局前置守卫：未登录 → 登录页；无该路由权限点 → 403 页
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth && !auth.isLoggedIn) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  // 已登录访问登录页 → 跳首页
  if (to.name === 'login' && auth.isLoggedIn) {
    return { name: 'home' }
  }
  // 刷新后权限集合为空（只有 token 持久化）：先拉取再判定，避免误判为无权限
  if (auth.isLoggedIn && auth.permissions.length === 0) {
    await auth.fetchPermissions()
  }
  const permission = typeof to.meta.permission === 'string' ? to.meta.permission : ''
  if (permission && !auth.hasPermission(permission)) {
    return { name: 'forbidden' }
  }
  return true
})

export default router

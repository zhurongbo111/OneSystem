import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

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
        path: 'products',
        name: 'products',
        component: () => import('@/views/ProductManagement/ProductsView.vue'),
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
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// 全局前置守卫：未登录访问受保护路由 → 跳转登录页
router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth && !auth.isLoggedIn) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  // 已登录访问登录页 → 跳首页
  if (to.name === 'login' && auth.isLoggedIn) {
    return { name: 'home' }
  }
  return true
})

export default router

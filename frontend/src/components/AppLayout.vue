<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import {
  IconApps,
  IconBuildingWarehouse,
  IconComponents,
  IconFileText,
  IconHistory,
  IconHome,
  IconLayoutSidebarLeftCollapse,
  IconLayoutSidebarLeftExpand,
  IconList,
  IconPackage,
  IconPackages,
  IconReceipt,
  IconTags,
  IconTruckDelivery,
  IconUser,
  IconUsers,
} from '@tabler/icons-vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/** 侧边栏折叠状态（内存态，不持久化） */
const collapsed = ref<boolean>(false)

/** 详情等子路由归属到所属一级菜单，保证侧边栏高亮正确 */
const MENU_ROUTE_MAP: Record<string, string> = { userDetail: 'users', purchaseDetail: 'purchases', salesDetail: 'sales' }

/** 菜单选中项：与当前路由名联动（单一数据源） */
const selectedKeys = computed<string[]>(() => {
  const name = typeof route.name === 'string' ? route.name : ''
  return name ? [MENU_ROUTE_MAP[name] ?? name] : []
})

// —— constants ——

/** 「示例页面」子菜单 key */
const SHOWCASE_MENU_KEY = 'showcase'
/** 示例页路由名（进入这些路由时自动展开「示例页面」子菜单） */
const SHOWCASE_ROUTE_NAMES = ['components', 'list', 'form']

/** 「进销存」子菜单 key */
const ERP_MENU_KEY = 'erp'
/** 进销存页路由名（进入这些路由时自动展开「进销存」子菜单） */
const ERP_ROUTE_NAMES = ['products', 'categories', 'partners', 'inventory', 'purchases', 'purchaseNew', 'sales', 'salesNew']

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
    if (SHOWCASE_ROUTE_NAMES.includes(name)) {
      if (!openKeys.value.includes(SHOWCASE_MENU_KEY)) {
        openKeys.value = [...openKeys.value, SHOWCASE_MENU_KEY]
      }
      return
    }
    if (ERP_ROUTE_NAMES.includes(name)) {
      if (!openKeys.value.includes(ERP_MENU_KEY)) {
        openKeys.value = [...openKeys.value, ERP_MENU_KEY]
      }
    }
  },
  { immediate: true }
)

onMounted(() => {
  // 进入受保护子页面后恢复用户信息（从 HomeView 上移至布局，覆盖所有子页面）
  void auth.fetchCurrentUser()
})

// —— methods ——
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
        <a-menu-item key="users">
          <template #icon>
            <IconUser />
          </template>
          <span>用户管理</span>
        </a-menu-item>
        <a-menu-item key="loginLogs">
          <template #icon>
            <IconHistory />
          </template>
          <span>登录日志</span>
        </a-menu-item>
        <a-sub-menu key="erp">
          <template #icon>
            <IconBuildingWarehouse />
          </template>
          <template #title>
            <span>进销存</span>
          </template>
          <a-menu-item key="products">
            <template #icon>
              <IconPackage />
            </template>
            <span>商品管理</span>
          </a-menu-item>
          <a-menu-item key="categories">
            <template #icon>
              <IconTags />
            </template>
            <span>分类管理</span>
          </a-menu-item>
          <a-menu-item key="partners">
            <template #icon>
              <IconUsers />
            </template>
            <span>往来单位</span>
          </a-menu-item>
          <a-menu-item key="inventory">
            <template #icon>
              <IconPackages />
            </template>
            <span>库存查询</span>
          </a-menu-item>
          <a-menu-item key="purchases">
            <template #icon>
              <IconTruckDelivery />
            </template>
            <span>采购入库</span>
          </a-menu-item>
          <a-menu-item key="sales">
            <template #icon>
              <IconReceipt />
            </template>
            <span>销售开单</span>
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

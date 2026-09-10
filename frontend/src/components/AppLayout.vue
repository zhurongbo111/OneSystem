<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import {
  IconMenuFold,
  IconMenuUnfold,
  IconUser,
  IconHome,
  IconApps,
  IconList,
  IconEdit,
  IconHistory,
} from '@arco-design/web-vue/es/icon'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/** 侧边栏折叠状态（内存态，不持久化） */
const collapsed = ref<boolean>(false)

/** 详情等子路由归属到所属一级菜单，保证侧边栏高亮正确 */
const MENU_ROUTE_MAP: Record<string, string> = { userDetail: 'users' }

/** 菜单选中项：与当前路由名联动（单一数据源） */
const selectedKeys = computed<string[]>(() => {
  const name = typeof route.name === 'string' ? route.name : ''
  return name ? [MENU_ROUTE_MAP[name] ?? name] : []
})

/** 当前用户名（未加载时显示占位） */
const displayName = computed<string>(() => auth.user?.displayName ?? '用户')

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
        mode="inline"
        :selected-keys="selectedKeys"
        :collapsed="collapsed"
        @menu-item-click="onMenuItemClick"
      >
        <a-menu-item key="home">
          <template #icon>
            <IconHome />
          </template>
          <span>首页</span>
        </a-menu-item>
        <a-menu-item key="components">
          <template #icon>
            <IconApps />
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
            <IconEdit />
          </template>
          <span>表单与详情示例</span>
        </a-menu-item>
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
            <IconMenuUnfold v-if="collapsed" />
            <IconMenuFold v-else />
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

.user-name {
  font-size: 14px;
}

.app-content {
  padding: 24px;
  background: var(--color-fill-2);
}
</style>

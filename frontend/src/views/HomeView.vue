<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()

onMounted(() => {
  // 刷新后恢复用户信息
  void auth.fetchCurrentUser()
})

function onLogout(): void {
  auth.logout()
  void router.replace({ name: 'login' })
}

function goComponents(): void {
  void router.push({ name: 'components' })
}
</script>

<template>
  <div class="home-page">
    <a-layout class="home-layout">
      <a-layout-header class="home-header">
        <span class="logo">App</span>
        <div class="header-right">
          <a-typography-text v-if="auth.user" class="username">{{ auth.user.displayName }}</a-typography-text>
          <a-button type="text" @click="goComponents">组件示例</a-button>
          <a-button type="text" @click="onLogout">退出登录</a-button>
        </div>
      </a-layout-header>
      <a-layout-content class="home-content">
        <a-card title="欢迎" :bordered="false">
          <a-space direction="vertical" size="large">
            <a-descriptions
              v-if="auth.user"
              title="当前登录用户"
              :column="1"
              layout="horizontal"
              size="large"
            >
              <a-descriptions-item label="显示名称">{{ auth.user.displayName }}</a-descriptions-item>
              <a-descriptions-item label="用户名">{{ auth.user.username }}</a-descriptions-item>
              <a-descriptions-item label="用户 ID">{{ auth.user.id }}</a-descriptions-item>
            </a-descriptions>
            <a-spin v-else />
          </a-space>
        </a-card>
      </a-layout-content>
    </a-layout>
  </div>
</template>

<style scoped>
.home-page {
  height: 100%;
}

.home-layout {
  height: 100%;
}

.home-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  background: var(--color-bg-2);
}

.logo {
  font-size: 18px;
  font-weight: 600;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.home-content {
  padding: 24px;
  background: var(--color-fill-2);
}
</style>

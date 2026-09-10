<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="home-page">
    <a-card
      title="欢迎"
      :bordered="false"
    >
      <a-space
        direction="vertical"
        size="large"
      >
        <a-descriptions
          v-if="auth.user"
          title="当前登录用户"
          :column="1"
          layout="horizontal"
          size="large"
        >
          <a-descriptions-item label="显示名称">
            {{ auth.user.displayName }}
          </a-descriptions-item>
          <a-descriptions-item label="用户名">
            {{ auth.user.username }}
          </a-descriptions-item>
          <a-descriptions-item label="用户 ID">
            {{ auth.user.id }}
          </a-descriptions-item>
        </a-descriptions>
        <a-spin v-else />
      </a-space>
    </a-card>
  </div>
</template>

<style scoped>
.home-page {
  width: 100%;
}
</style>

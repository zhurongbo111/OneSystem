<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { getUser } from '@/api/user'
import type { UserDetail } from '@/api/user'
import { formatDateTime } from '@/utils/datetime'

const route = useRoute()
const router = useRouter()

// —— reactive state ——
const loading = ref(false)
const user = ref<UserDetail | null>(null)
const notFound = ref(false)

// —— lifecycle ——
onMounted(() => {
  void fetchUser()
})

// —— methods ——
/** 按路由 id 查询用户详情；不存在的 id 走 404 空状态兜底 */
async function fetchUser(): Promise<void> {
  const id = String(route.params.id ?? '')
  if (!id) {
    notFound.value = true
    return
  }
  loading.value = true
  try {
    user.value = await getUser(id)
    notFound.value = false
  } catch {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push({ name: 'users' })
}
</script>

<template>
  <div class="detail-page">
    <a-spin
      :loading="loading"
      class="detail-spin"
    >
      <template v-if="!notFound && user">
        <a-page-header
          class="detail-header"
          title="用户详情"
          :subtitle="`用户名：${user.username}`"
          @back="goBack"
        />

        <a-card :bordered="false">
          <a-descriptions
            :column="2"
            bordered
          >
            <a-descriptions-item label="用户名">
              {{ user.username }}
            </a-descriptions-item>
            <a-descriptions-item label="显示名">
              {{ user.displayName }}
            </a-descriptions-item>
            <a-descriptions-item label="邮箱">
              {{ user.email || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="手机号">
              {{ user.phone || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="user.status === 1 ? 'green' : 'red'">
                {{ user.status === 1 ? '启用' : '禁用' }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="最近登录时间">
              {{ formatDateTime(user.lastLoginAt) }}
            </a-descriptions-item>
            <a-descriptions-item label="创建时间">
              {{ formatDateTime(user.createdAt) }}
            </a-descriptions-item>
            <a-descriptions-item label="更新时间">
              {{ formatDateTime(user.updatedAt) }}
            </a-descriptions-item>
          </a-descriptions>
        </a-card>
      </template>

      <a-result
        v-else-if="!loading"
        status="404"
        title="用户不存在"
        subtitle="用户不存在或已被删除"
      >
        <template #extra>
          <a-button
            type="primary"
            @click="goBack"
          >
            返回列表
          </a-button>
        </template>
      </a-result>
    </a-spin>
  </div>
</template>

<style scoped>
.detail-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.detail-spin {
  display: block;
  width: 100%;
}

.detail-header {
  background: var(--color-bg-2);
  border-radius: var(--border-radius-medium);
  padding: 12px 20px;
}
</style>

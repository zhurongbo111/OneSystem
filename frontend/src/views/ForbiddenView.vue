<script setup lang="ts">
import { useRouter } from 'vue-router'

import { IconHome } from '@tabler/icons-vue'

const router = useRouter()

// —— methods ——

/** 返回首页（无权限页只有这一个出口） */
function onBackHome(): void {
  void router.replace({ name: 'home' })
}
</script>

<template>
  <div class="forbidden-view">
    <a-result status="403">
      <template #title>
        无访问权限
      </template>
      <template #subtitle>
        当前账号没有被分配该功能的操作权限，请联系管理员调整角色权限。
      </template>
      <template #extra>
        <a-button
          type="primary"
          @click="onBackHome"
        >
          <template #icon>
            <IconHome />
          </template>
          <span>返回首页</span>
        </a-button>
      </template>
    </a-result>
  </div>
</template>

<style scoped>
.forbidden-view {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 24px;
}
</style>

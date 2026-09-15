<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import { Message } from '@arco-design/web-vue'
import type { FormInstance } from '@arco-design/web-vue'

// —— constants ——
const rules = {
  username: [{ required: true, message: '请输入用户名' }],
  password: [{ required: true, message: '请输入密码' }],
}

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

// —— reactive state ——
const formRef = ref<FormInstance>()
const loading = ref(false)
const form = reactive({
  username: '',
  password: '',
})

async function onSubmit(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  loading.value = true
  try {
    await auth.login(form.username, form.password)
    Message.success('登录成功')
    const redirect = (route.query.redirect as string) || '/'
    await router.replace(redirect)
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <a-card
      class="login-card"
      title="登录"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        layout="vertical"
        @submit-success="onSubmit"
      >
        <a-form-item
          field="username"
          label="用户名"
        >
          <a-input
            v-model="form.username"
            placeholder="请输入用户名"
            allow-clear
          />
        </a-form-item>
        <a-form-item
          field="password"
          label="密码"
        >
          <a-input-password
            v-model="form.password"
            placeholder="请输入密码"
            allow-clear
          />
        </a-form-item>
        <a-form-item>
          <a-button
            type="primary"
            long
            html-type="submit"
            :loading="loading"
          >
            登录
          </a-button>
        </a-form-item>
      </a-form>
      <a-typography-text type="secondary">
        测试账号：admin / admin123
      </a-typography-text>
    </a-card>
  </div>
</template>

<style scoped>
.login-page {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--color-fill-2);
}

.login-card {
  width: 380px;
  border-radius: 8px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
}
</style>

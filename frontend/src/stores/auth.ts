import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { login as loginApi, getCurrentUser, type UserDto } from '@/api/auth'
import { tokenStorage } from '@/api/request'

/**
 * 认证状态：token / user，token 持久化到 localStorage
 */
export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(tokenStorage.get() ?? '')
  const user = ref<UserDto | null>(null)

  const isLoggedIn = computed(() => Boolean(token.value))

  /**
   * 登录：调用接口并持久化凭证
   */
  async function login(username: string, password: string): Promise<void> {
    const result = await loginApi({ username, password })
    token.value = result.token
    user.value = result.user
    tokenStorage.set(result.token)
  }

  /**
   * 拉取当前用户（用于刷新后恢复用户信息）
   */
  async function fetchCurrentUser(): Promise<void> {
    if (!token.value) return
    try {
      user.value = await getCurrentUser()
    } catch {
      // 40100 已由请求层统一处理（清凭证并跳转登录页）
    }
  }

  /**
   * 退出登录：清除凭证
   */
  function logout(): void {
    token.value = ''
    user.value = null
    tokenStorage.clear()
  }

  return { token, user, isLoggedIn, login, fetchCurrentUser, logout }
})

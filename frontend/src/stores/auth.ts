import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { login as loginApi, getCurrentUser, getMyPermissions, type UserDto } from '@/api/auth'
import { tokenStorage } from '@/api/request'

/**
 * 认证状态：token / user / permissions，token 持久化到 localStorage
 */
export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(tokenStorage.get() ?? '')
  const user = ref<UserDto | null>(null)
  /** 当前用户权限点 key 集合（登录响应赋值，`fetchPermissions` 可刷新） */
  const permissions = ref<string[]>([])

  const isLoggedIn = computed(() => Boolean(token.value))

  /**
   * 登录：调用接口、持久化凭证并缓存权限点集合
   */
  async function login(username: string, password: string): Promise<void> {
    const result = await loginApi({ username, password })
    token.value = result.token
    user.value = result.user
    permissions.value = result.permissions
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
   * 刷新权限点集合（角色 / 权限变更后无需重新登录即可生效）
   */
  async function fetchPermissions(): Promise<void> {
    if (!token.value) return
    try {
      permissions.value = await getMyPermissions()
    } catch {
      // 40100 已由请求层统一处理（清凭证并跳转登录页）
    }
  }

  /**
   * 是否具备指定权限点（菜单项 / 按钮级共用同一套 key）
   */
  function hasPermission(key: string): boolean {
    return permissions.value.includes(key)
  }

  /**
   * 是否具备其中任一权限点
   */
  function hasAnyPermission(keys: string[]): boolean {
    return keys.some((key) => permissions.value.includes(key))
  }

  /**
   * 退出登录：清除凭证与权限集合
   */
  function logout(): void {
    token.value = ''
    user.value = null
    permissions.value = []
    tokenStorage.clear()
  }

  return {
    token,
    user,
    permissions,
    isLoggedIn,
    hasPermission,
    hasAnyPermission,
    login,
    fetchCurrentUser,
    fetchPermissions,
    logout,
  }
})

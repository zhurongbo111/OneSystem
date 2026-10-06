import { defineStore } from 'pinia'
import { ref } from 'vue'

import {
  getNotificationSummary,
  markAllNotificationsRead,
  markNotificationRead,
} from '@/api/notification'
import type { NotificationItem } from '@/api/notification'

/**
 * 站内信状态（041）：未读数与最近消息的**唯一事实源**，
 * 供顶栏铃铛与列表页共用——列表页标记已读后顶栏未读数同步（避免两处各自维护导致不一致）。
 * 不做轮询：进入应用与路由切换时各刷新一次（设计 §5）。
 */
export const useNotificationStore = defineStore('notification', () => {
  /** 未读消息数（顶栏角标） */
  const unreadCount = ref(0)

  /** 最近消息（顶栏下拉展示） */
  const recent = ref<NotificationItem[]>([])

  /** 汇总加载态（顶栏下拉内容区） */
  const summaryLoading = ref(false)

  /** 拉取未读汇总（路由切换 / 登录后调用；失败保持上次值，错误提示由请求层统一处理） */
  async function fetchSummary(): Promise<void> {
    summaryLoading.value = true
    try {
      const summary = await getNotificationSummary()
      unreadCount.value = summary.unreadCount
      recent.value = summary.recent
    } catch {
      // 错误提示已由请求层统一处理
    } finally {
      summaryLoading.value = false
    }
  }

  /** 标记单条已读并同步未读数 */
  async function markRead(id: string): Promise<void> {
    await markNotificationRead(id)
    await fetchSummary()
  }

  /** 全部标记已读并同步未读数，返回本次标记条数 */
  async function markAllRead(): Promise<number> {
    const result = await markAllNotificationsRead()
    await fetchSummary()
    return result.affectedCount
  }

  /** 清空本地状态（退出登录时调用，避免下个账号看到上个账号的未读数） */
  function clear(): void {
    unreadCount.value = 0
    recent.value = []
  }

  return { unreadCount, recent, summaryLoading, fetchSummary, markRead, markAllRead, clear }
})

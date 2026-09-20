import { test, expect } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'

/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

async function login(page: import('@playwright/test').Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

test.describe('组件示例页面（集成）', () => {
  test('直接访问 /components 正常渲染默认分类', async ({ page }) => {
    await page.goto('/components')
    await expect(page.getByRole('heading', { name: 'Arco Design 组件示例' })).toBeVisible()
    // 默认「基础」tab 内容可见
    await expect(page.getByText('按钮 Button')).toBeVisible()
  })

  test('切换 tab 到「表单」展示表单组件', async ({ page }) => {
    await page.goto('/components')
    await page.locator('.arco-tabs-tab-title', { hasText: '表单' }).click()
    // 主 tabs 激活项切到「表单」（filter 文本「表单」可排除嵌套 tabs 的激活项），表单分类内容可见
    await expect(page.locator('.arco-tabs-tab-active').filter({ hasText: '表单' })).toBeVisible()
    await expect(page.getByText('选择器 Select')).toBeVisible()
  })

  test('切换 tab 到「图标」展示 Arco / Tabler / Lucide 图标示例', async ({ page }) => {
    await page.goto('/components')
    await page.locator('.arco-tabs-tab-title', { hasText: '图标' }).click()
    // 主 tabs 激活项切到「图标」，图标分类内容可见
    await expect(page.locator('.arco-tabs-tab-active').filter({ hasText: '图标' })).toBeVisible()
    // 图标名在两段网格中重名（如 IconHome），各段改断言卡片标题 + 该段独有图标名
    // Arco 图标示例
    await expect(page.getByText('Arco 常用图标')).toBeVisible()
    await expect(page.getByText('IconNotification')).toBeVisible()
    await expect(page.getByText('旋转 rotate / 动画 spin（Arco 特有）')).toBeVisible()
    // Tabler 图标示例
    await expect(page.getByText('Tabler 常用图标')).toBeVisible()
    await expect(page.getByText('IconBell')).toBeVisible()
    // Lucide 图标示例（断言 Lucide 独有图标名 House）
    await expect(page.getByText('Lucide 常用图标')).toBeVisible()
    await expect(page.getByText('House', { exact: true })).toBeVisible()
  })

  test('已登录首页点击侧边菜单「组件示例」跳转到组件页', async ({ page }) => {
    await login(page)
    await clickMenuItem(page, '组件示例')
    await expect(page).toHaveURL(/\/components/)
    await expect(page.getByRole('heading', { name: 'Arco Design 组件示例' })).toBeVisible()
  })
})

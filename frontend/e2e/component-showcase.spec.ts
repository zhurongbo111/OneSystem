import { test, expect } from '@playwright/test'

/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

async function login(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
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

  test('已登录首页点击侧边菜单「组件示例」跳转到组件页', async ({ page }) => {
    await login(page)
    await page.locator('.arco-menu-item', { hasText: '组件示例' }).click()
    await expect(page).toHaveURL(/\/components/)
    await expect(page.getByRole('heading', { name: 'Arco Design 组件示例' })).toBeVisible()
  })
})

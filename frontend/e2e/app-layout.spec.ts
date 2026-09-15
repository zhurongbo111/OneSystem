import { test, expect, type Page } from '@playwright/test'

import { clickMenuItem, menuGroup, menuItem } from './helpers/menu'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

async function login(page: Page): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

test.describe('全局页面布局（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('登录后进入首页，侧边菜单「首页」选中', async ({ page }) => {
    await login(page)
    // 侧边菜单存在且「首页」为选中态
    const homeItem = page.locator('.arco-menu-item', { hasText: '首页' })
    await expect(homeItem).toBeVisible()
    await expect(homeItem).toHaveClass(/arco-menu-selected/)
  })

  test('首页时子菜单默认折叠，展开分组后点击「组件示例」跳转且菜单选中', async ({ page }) => {
    await login(page)
    // 默认折叠：两个分组的子项均不可见
    await expect(menuItem(page, '组件示例')).toBeHidden()
    await expect(menuItem(page, '商品管理')).toBeHidden()

    // 点击分组标题展开「示例页面」
    await menuGroup(page, '示例页面').click()
    await expect(menuItem(page, '组件示例')).toBeVisible()

    await clickMenuItem(page, '组件示例')
    await expect(page).toHaveURL(/\/components/)
    await expect(page.getByRole('heading', { name: 'Arco Design 组件示例' })).toBeVisible()
    // 「组件示例」菜单项选中
    await expect(menuItem(page, '组件示例')).toHaveClass(/arco-menu-selected/)
  })

  test('直接访问子页面时所属分组自动展开，其余分组仍折叠', async ({ page }) => {
    await login(page)
    await page.goto('/products')
    await expect(page).toHaveURL(/\/products$/)
    // 「进销存」自动展开，子项可见
    await expect(menuItem(page, '商品管理')).toBeVisible()
    // 「示例页面」不属当前路由，保持折叠
    await expect(menuItem(page, '组件示例')).toBeHidden()
  })

  test('折叠按钮可收起 / 展开侧边栏', async ({ page }) => {
    await login(page)
    const sider = page.locator('.arco-layout-sider')
    await expect(sider).not.toHaveClass(/arco-layout-sider-collapsed/)

    // 折叠
    await page.getByRole('button', { name: '折叠侧边栏' }).click()
    await expect(sider).toHaveClass(/arco-layout-sider-collapsed/)

    // 展开
    await page.getByRole('button', { name: '展开侧边栏' }).click()
    await expect(sider).not.toHaveClass(/arco-layout-sider-collapsed/)
  })

  test('用户下拉「退出登录」回到登录页', async ({ page }) => {
    await login(page)
    await page.getByRole('button', { name: '用户菜单' }).click()
    await page.getByText('退出登录').click()
    await expect(page).toHaveURL(/\/login/)
  })
})

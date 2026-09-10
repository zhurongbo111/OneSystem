import { expect, test, type Page } from '@playwright/test'

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

/** 登录并经侧边菜单进入登录日志页 */
async function goLoginLogs(page: Page): Promise<void> {
  await login(page)
  await page.locator('.arco-menu-item', { hasText: '登录日志' }).click()
  await expect(page).toHaveURL(/\/login-logs$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 按登录名搜索 */
async function searchByUsername(page: Page, username: string): Promise<void> {
  await page.getByPlaceholder('搜索登录名').fill(username)
  await page.getByRole('button', { name: '搜索' }).click()
}

/**
 * 延迟目标接口的响应转发：请求仍发往真实 dev 后端、响应内容不变，只改变时序。
 * 本地请求 10~50ms 即返回，loading 态可能在首次轮询前消失，延迟用于稳定捕获。
 */
async function delayApi(page: Page, pattern: RegExp, ms: number): Promise<void> {
  await page.route(pattern, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, ms))
    await route.continue()
  })
}

/** 在日期范围选择器中选中「今天」作为闭区间 */
async function selectTodayRange(page: Page): Promise<void> {
  await page.locator('.filter-bar__range input').first().click()
  const today = page.locator('.arco-picker-cell-today').first()
  await today.click()
  await today.click()
  // 点击空白处关闭面板
  await page.getByRole('heading', { name: '登录日志' }).click()
}

test.describe('登录日志（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入登录日志页并展示表头与分页', async ({ page }) => {
    await goLoginLogs(page)
    await expect(page.getByRole('heading', { name: '登录日志' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '登录名' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '登录时间' })).toBeVisible()
    await expect(page.locator('text=/共 \\d+ 条/')).toBeVisible()
  })

  test('登录成功后可按登录名查询到 admin 的记录', async ({ page }) => {
    await goLoginLogs(page)
    await searchByUsername(page, 'admin')
    await expect(dataRows(page).first()).toContainText('admin')
  })

  test('时间范围筛选「今天」包含刚产生的登录记录', async ({ page }) => {
    await goLoginLogs(page)
    await searchByUsername(page, 'admin')
    await selectTodayRange(page)
    await page.getByRole('button', { name: '搜索' }).click()
    await expect(dataRows(page).first()).toContainText('admin')
  })

  test('登录名无匹配时显示空状态', async ({ page }) => {
    await goLoginLogs(page)
    await searchByUsername(page, 'no_such_login_zzz')
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()
  })

  test('点击搜索时按钮进入 loading，完成后恢复可点', async ({ page }) => {
    await goLoginLogs(page)
    await delayApi(page, /\/api\/login-logs\?/, 600)

    const searchButton = page.getByRole('button', { name: '搜索' })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })
})

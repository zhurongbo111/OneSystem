import { expect, test, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 生成唯一用户名（满足 3-50 位字母 / 数字 / 下划线） */
function uniqueUsername(): string {
  return `e2e_${Date.now().toString(36)}`
}

async function login(page: Page, username = CREDENTIALS.username, password = CREDENTIALS.password): Promise<void> {
  await loginAs(page, username, password)
}

/** 登录并经侧边菜单进入用户管理页 */
async function goUsers(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '用户管理')
  await expect(page).toHaveURL(/\/users$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 分页"共 N 条"文本 */
function totalText(page: Page): ReturnType<typeof page.locator> {
  return page.locator('text=/共 \\d+ 条/')
}

/** 按用户名搜索（点搜索按钮触发服务端查询） */
async function searchByUsername(page: Page, username: string): Promise<void> {
  await page.getByPlaceholder('搜索用户名或显示名').fill(username)
  await page.getByRole('button', { name: '搜索' }).click()
}

/**
 * 延迟目标接口的响应转发：请求仍发往真实 dev 后端、响应内容不变，只改变时序。
 * 本地请求 10~50ms 即返回，loading 态可能在首次轮询前消失，延迟用于稳定捕获。
 * （前端 dev 走 vite proxy，Playwright 在浏览器侧拦截，按 5173 侧 URL 匹配即可）
 */
async function delayApi(page: Page, pattern: RegExp, ms: number): Promise<void> {
  await page.route(pattern, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, ms))
    await route.continue()
  })
}

/** 经抽屉新增用户并等待列表出现 */
async function createUser(page: Page, username: string, displayName: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增用户')).toBeVisible()
  await page.getByPlaceholder('3-50 位字母、数字或下划线').fill(username)
  await page.getByPlaceholder('请输入显示名').fill(displayName)
  await page.getByPlaceholder('6-32 位密码').fill('initPass123')
  // 角色必选（specs/028-erp-rbac）：展开多选下拉勾选第一个角色后关闭，再提交
  // 选项按 :visible 过滤：弹层 DOM 会残留（同域其它下拉的旧选项仍隐藏在 DOM 中）
  const roleSelect = page.locator('.arco-select').filter({ hasText: '请选择角色（至少一个）' })
  await roleSelect.click()
  const firstRole = page.locator('.arco-select-option:visible').first()
  await expect(firstRole).toBeVisible()
  await firstRole.click()
  await page.keyboard.press('Escape')
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '用户已创建')
  await expect(page.getByText('新增用户')).toHaveCount(0)
}

/** 进入指定用户所在行（搜索后取唯一行） */
async function openUserRow(page: Page, username: string): Promise<ReturnType<typeof page.locator>> {
  await searchByUsername(page, username)
  const row = dataRows(page).first()
  await expect(row).toContainText(username)
  return row
}

test.describe('用户管理（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入用户管理页，列表渲染表头与分页', async ({ page }) => {
    await goUsers(page)
    await expect(page.getByRole('heading', { name: '用户管理' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '用户名' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '状态' })).toBeVisible()
    // seed 至少包含 admin 一条
    await expect(totalText(page)).toBeVisible()
    await expect(dataRows(page).first()).toBeVisible()
  })

  test('按用户名搜索命中唯一行，重置恢复列表', async ({ page }) => {
    await goUsers(page)
    // 记录未筛选时的总数，重置后应恢复一致（不依赖具体行内容与库内既有数据）
    const before = await totalText(page).textContent()

    await searchByUsername(page, 'admin')
    await expect(dataRows(page)).toHaveCount(1)
    await expect(page.getByText('admin').first()).toBeVisible()

    // 「重置」为精确匹配，避免命中行内「重置密码」按钮
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await expect(totalText(page)).toHaveText(before ?? '')
  })

  test('搜索不存在的用户名显示空状态', async ({ page }) => {
    await goUsers(page)
    await searchByUsername(page, 'no_such_user_zzz')
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()
  })

  test('新增用户后可搜索到', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 新增')
    await searchByUsername(page, username)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(page.getByText('E2E 新增')).toBeVisible()
  })

  test('编辑用户显示名生效', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 编辑前')
    const row = await openUserRow(page, username)

    await row.getByRole('button', { name: '编辑' }).click()
    await expect(page.getByText('编辑用户')).toBeVisible()
    const displayNameInput = page.getByPlaceholder('请输入显示名')
    // 详情回填完成前字段是禁用的，fill 会自动等到可用，避免输入被回填覆盖
    await displayNameInput.fill('E2E 编辑后')
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '用户已更新')

    await searchByUsername(page, username)
    await expect(dataRows(page).first()).toContainText('E2E 编辑后')
  })

  test('详情接口较慢时表单先重置并禁用，回填后编辑仍生效', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 慢详情')
    const row = await openUserRow(page, username)

    // 延迟详情接口放大"回填未完成"窗口：验证表单不残留上次数据、回填前不可编辑、输入不被覆盖
    await delayApi(page, /\/api\/users\/[^/]+$/, 1500)
    await row.getByRole('button', { name: '编辑' }).click()
    await expect(page.getByText('编辑用户')).toBeVisible()

    const displayNameInput = page.getByPlaceholder('请输入显示名')
    await expect(displayNameInput).toBeDisabled()
    await expect(displayNameInput).toHaveValue('')
    await expect(displayNameInput).toBeEnabled()
    await expect(displayNameInput).toHaveValue('E2E 慢详情')

    await displayNameInput.fill('E2E 慢详情改')
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '用户已更新')

    await searchByUsername(page, username)
    await expect(dataRows(page).first()).toContainText('E2E 慢详情改')
  })

  test('禁用后再启用，状态标签随之变化', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 启停')
    const row = await openUserRow(page, username)
    // 行内有角色 tag（specs/028-erp-rbac 新增角色列），按文本过滤取状态 tag
    await expect(row.locator('.arco-tag', { hasText: '启用' })).toBeVisible()

    // 禁用
    await row.getByRole('button', { name: '禁用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已禁用')).toBeVisible()

    const disabledRow = await openUserRow(page, username)
    await expect(disabledRow.locator('.arco-tag', { hasText: '禁用' })).toBeVisible()

    // 启用
    await disabledRow.getByRole('button', { name: '启用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已启用')).toBeVisible()
    const enabledRow = await openUserRow(page, username)
    await expect(enabledRow.locator('.arco-tag', { hasText: '启用' })).toBeVisible()
  })

  test('重置密码后可用新密码登录', async ({ page }) => {
    const username = uniqueUsername()
    const newPassword = 'resetPass456'
    await goUsers(page)
    await createUser(page, username, 'E2E 重置')
    const row = await openUserRow(page, username)

    // 「重置密码」收纳进行内「更多」下拉（specs/011-action-column：> 3 个操作收纳）
    await row.getByRole('button', { name: '更多操作' }).click()
    await page.locator('.arco-dropdown-option', { hasText: '重置密码' }).click()
    // 以弹窗内的密码输入框确认模态已打开（模态标题与行按钮同文案，避免按文本定位歧义）
    await expect(page.getByPlaceholder('请输入 6-32 位新密码')).toBeVisible()
    await page.getByPlaceholder('请输入 6-32 位新密码').fill(newPassword)
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expectMessage(page, '密码已重置')

    // 退出后以新密码登录成功
    await page.getByRole('button', { name: '用户菜单' }).click()
    await page.getByText('退出登录').click()
    await expect(page).toHaveURL(/\/login/)
    await login(page, username, newPassword)
    await expect(page).toHaveURL(/\/$/)
  })

  test('详情页展示用户信息并返回列表', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 详情')
    const row = await openUserRow(page, username)

    await row.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/users\/detail\//)
    // a-page-header 标题不是 heading 角色，按文本定位
    await expect(page.getByText('用户详情')).toBeVisible()
    await expect(page.getByText(username).first()).toBeVisible()

    // 经侧边菜单返回列表
    await page.locator('.arco-menu-item', { hasText: '用户管理' }).click()
    await expect(page).toHaveURL(/\/users$/)
  })

  test('查询按钮点击后进入 loading，完成后恢复可点', async ({ page }) => {
    await goUsers(page)
    await delayApi(page, /\/api\/users\?/, 600)

    // 搜索与刷新共用查询 loading：点击后按钮进入 loading（不可点），请求完成后恢复
    const searchButton = page.getByRole('button', { name: '搜索' })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })

    const refreshButton = page.getByRole('button', { name: '刷新' })
    await refreshButton.click()
    await expect(refreshButton).toHaveClass(/arco-btn-loading/)
    await expect(refreshButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })

  test('行内禁用按钮确认后进入 loading，仅该行按钮响应', async ({ page }) => {
    const username = uniqueUsername()
    await goUsers(page)
    await createUser(page, username, 'E2E 行内 loading')
    const row = await openUserRow(page, username)

    await delayApi(page, /\/api\/users\/[^/]+\/status/, 1000)
    const toggleButton = row.getByRole('button', { name: '禁用' })
    await toggleButton.click()
    await page.getByRole('button', { name: /确\s*定/ }).click()

    // 请求进行中：该行按钮处于 loading；完成后状态标签变为禁用
    await expect(toggleButton).toHaveClass(/arco-btn-loading/)
    await expect(page.getByText('已禁用')).toBeVisible()
    await expect(row.locator('.arco-tag', { hasText: '禁用' })).toBeVisible()
  })
})

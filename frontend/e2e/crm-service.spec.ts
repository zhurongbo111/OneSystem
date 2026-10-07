import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 生成唯一名称（工单标题 / 客户 / 员工共用前缀） */
function uniqueName(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 登录后经侧边菜单进入指定页面（菜单点击被吞时重试，前端规则 §10.1） */
async function goMenu(page: Page, name: string, url: RegExp): Promise<void> {
  await login(page)
  await expect(async () => {
    await clickMenuItem(page, name)
    await expect(page).toHaveURL(url, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 经侧边菜单进入服务工单页（045：CRM 分组） */
async function goServiceTickets(page: Page): Promise<void> {
  await goMenu(page, '服务工单', /\/service-tickets$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 详情页某字段的值单元格（Arco descriptions 渲染为表格：一行内「标签格 → 值格」相邻）。
 * 直接按行断言会命中同一行的其他字段，故按标签格取相邻值格。
 */
function descValue(page: Page, label: string): Locator {
  return page
    .locator('.arco-descriptions-item-label', { hasText: label })
    .first()
    .locator('xpath=following-sibling::*[1]')
}

/** 取当前登录态 token（接口 fixture / 断言用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 经接口 POST（返回统一响应体，供断言业务码） */
async function apiPost(page: Page, url: string, data: unknown): Promise<{ code: number; data?: { id?: string } }> {
  const token = await apiToken(page)
  const response = await page.request.post(`/api${url}`, {
    data,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { code: number; data?: { id?: string } }
}

/** 客户 fixture（工单必须挂客户；往来单位 UI 行为不在本规格范围），返回客户 id */
async function createCustomerFixture(page: Page, name: string): Promise<string> {
  const body = await apiPost(page, '/partners', { name, type: 2 })
  expect(body.code).toBe(0)
  const id = body.data?.id ?? ''
  expect(id, '客户 fixture 未返回 id').not.toBe('')
  return id
}

/** 员工 fixture（工单负责人下拉数据源） */
async function createEmployeeFixture(page: Page, name: string): Promise<void> {
  const body = await apiPost(page, '/employees', {
    employeeNo: `E${Date.now().toString(36)}`,
    name,
    hireDate: '2026-01-01',
    status: 1,
  })
  expect(body.code).toBe(0)
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 * Arco 选中后旧弹层 DOM 残留且选项会被过滤隐藏，点选项不可靠；Enter 作用于当前聚焦 select。
 */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  await expect(
    selectLocator.page().locator('.arco-select-option:visible', { hasText: keyword }).first(),
  ).toBeVisible()
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 在 Arco select 中按选项文案选中（非搜索型下拉） */
async function selectOption(page: Page, select: Locator, label: string): Promise<void> {
  await select.click()
  await page.locator('.arco-select-option:visible', { hasText: label }).first().click()
  await expect(select).toContainText(label)
}

/** 登记工单（客户 + 可选负责人 / 优先级），提交后停在详情页 */
async function createTicket(
  page: Page,
  title: string,
  customerName: string,
  options: { priorityLabel?: string; ownerName?: string } = {},
): Promise<void> {
  await clickUntil(page, '新建工单', async () => {
    await expect(page).toHaveURL(/\/service-tickets\/new$/)
  })
  await page.getByPlaceholder('1-50 字符').fill(title)
  // 表单内 select 顺序：客户 / 优先级 / 负责人
  await selectBySearch(page.locator('.arco-select').nth(0), customerName)
  if (options.priorityLabel) {
    await selectOption(page, page.locator('.arco-select').nth(1), options.priorityLabel)
  }
  if (options.ownerName) {
    await selectBySearch(page.locator('.arco-select').nth(2), options.ownerName)
  }
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '工单已创建')
  await expect(page).toHaveURL(/\/service-tickets\/detail\//)
}

/** 详情页推进状态（按钮文案即动作名；跳转 / 重渲染期间点击可能被吞，统一重试） */
async function advanceStatus(page: Page, action: string, expectedStatus: string): Promise<void> {
  await clickUntil(page, action, async () => {
    await expect(descValue(page, '状态')).toContainText(expectedStatus)
  })
  await expectMessage(page, `工单已${action}`)
}

/** 详情页关闭工单（终态，二次确认后执行） */
async function closeTicket(page: Page): Promise<void> {
  await clickUntil(page, '关闭', page.getByRole('button', { name: '确认关闭' }))
  await page.getByRole('button', { name: '确认关闭' }).click()
  await expectMessage(page, '工单已关闭')
  await expect(descValue(page, '状态')).toContainText('已关闭')
}

/** 读取工单 id（详情页 URL 末段） */
function detailTicketId(page: Page): string {
  return page.url().split('/detail/')[1] ?? ''
}

test.describe('CRM 服务工单（045）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('登记工单（客户 / 高优先级）→ 指派 → 受理 → 解决（记时间）→ 关闭', async ({ page }) => {
    const customerName = uniqueName('svc_cust')
    const ownerName = uniqueName('svc_owner')
    const title = uniqueName('svc_ticket')

    await goServiceTickets(page)
    await createCustomerFixture(page, customerName)
    await createEmployeeFixture(page, ownerName)

    // 登记：客户 + 高优先级，状态默认「待处理」
    await createTicket(page, title, customerName, { priorityLabel: '高' })
    await expect(descValue(page, '客户')).toContainText(customerName)
    await expect(descValue(page, '优先级')).toContainText('高')
    await expect(descValue(page, '状态')).toContainText('待处理')
    await expect(descValue(page, '解决时间')).toContainText('—')

    // 指派负责人
    await selectBySearch(page.locator('.assign-bar .arco-select'), ownerName)
    await clickUntil(page, '指派', async () => {
      await expect(descValue(page, '负责人')).toContainText(ownerName)
    })
    await expectMessage(page, '工单已指派')

    // 受理 → 解决（记录解决时间）→ 关闭（终态）
    await advanceStatus(page, '受理', '处理中')
    await advanceStatus(page, '解决', '已解决')
    await expect(descValue(page, '解决时间')).not.toContainText('—')

    await closeTicket(page)

    // 终态锁定：不再有编辑 / 状态推进 / 指派入口
    await expect(page.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '受理' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '解决' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '指派' })).toHaveCount(0)
    await expect(page.locator('.assign-bar__hint')).toBeVisible()

    // 列表：状态 / 优先级 / 负责人回显，已关闭行无编辑入口
    await goServiceTickets(page)
    await page.getByPlaceholder('搜索工单号 / 客户 / 标题').fill(title)
    await searchAndWaitHit(page, title)
    const row = dataRows(page).first()
    await expect(row).toContainText('已关闭')
    await expect(row).toContainText('高')
    await expect(row).toContainText(ownerName)
    await expect(row.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(row.getByRole('button', { name: '受理' })).toHaveCount(0)
  })

  test('已解决重开为处理中（清空解决时间）；已关闭改状态 40172', async ({ page }) => {
    const customerName = uniqueName('svc_cust')
    const title = uniqueName('svc_reopen')

    await goServiceTickets(page)
    const customerId = await createCustomerFixture(page, customerName)
    await createTicket(page, title, customerName)

    // 待处理可直接「解决」（design.md §0.1 白名单），记录解决时间
    await advanceStatus(page, '解决', '已解决')
    await expect(descValue(page, '解决时间')).not.toContainText('—')

    // 重开：已解决 → 处理中，解决时间清空
    await advanceStatus(page, '重开', '处理中')
    await expect(descValue(page, '解决时间')).toContainText('—')

    // 关闭后改为终态
    await closeTicket(page)
    const ticketId = detailTicketId(page)
    expect(ticketId, '未取到工单 id').not.toBe('')

    // 接口：已关闭工单再改状态 → 40172
    const token = await apiToken(page)
    const statusResponse = await page.request.put(`/api/service-tickets/${ticketId}/status`, {
      data: { status: 1 },
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await statusResponse.json()) as { code: number }).code).toBe(40172)

    // 接口：已关闭工单再编辑 → 40172（参数本身合法，命中「已关闭为终态」判据）
    const updateResponse = await page.request.put(`/api/service-tickets/${ticketId}`, {
      data: { partnerId: customerId, title: `${title}x`, priority: 1 },
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await updateResponse.json()) as { code: number }).code).toBe(40172)
  })

  test('搜索按钮点击后进入 loading，完成后恢复可点', async ({ page }) => {
    await goServiceTickets(page)
    // 人为拖慢工单分页请求，保证 loading 状态可被断言捕获
    await page.route(/\/api\/service-tickets\?/, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 600))
      await route.continue()
    })

    const searchButton = page.getByRole('button', { name: '搜索', exact: true })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })
})

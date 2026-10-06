import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { expectMessage } from './helpers/message'

/**
 * 库存预警通知（specs/041-erp-stock-alert）阶段七 E2E：
 * - 7.1 制造低库存 → 站内信页「立即扫描」→ 列表出现「低库存」消息且顶栏未读数 +1；
 * - 7.2 点击消息「查看」→ 标记已读（未读数 -1）并跳转库存查询且带筛选；
 * - 7.3 新信号扫描后再「全部已读」→ 未读数归零；
 * - 7.4 同日再次扫描不新增（列表总条数不变、无新告警）；
 * - 7.5 无 `inventory.view` 权限的用户收不到库存告警（登录该用户铃铛无未读）。
 *
 * 低库存 fixture 经 `page.request` 直连后端构造（分类 + 商品：安全库存 100、库存 0），
 * 扫描统一走「立即扫描」按钮（与定时宿主共用同一实现）。
 */

const BACKEND = 'http://localhost:5080'
const BACKEND_HEALTH = `${BACKEND}/health`
const ADMIN = { username: 'admin', password: 'admin123' }

/** 跨用例共享（worker 串行，模块级变量在 test 内顺序传递） */
let lowStockCode = ''
let secondLowStockCode = ''
let thirdLowStockCode = ''

function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.floor(Math.random() * 100)}`
}

function uniqueCode(prefix: string): string {
  return `${prefix}${uniqueSuffix()}`
}

/** 数据行（排除空态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 顶栏未读角标（未读数为 0 时不存在） */
function unreadBadge(page: Page): Locator {
  return page.locator('.notification-trigger .arco-badge-number')
}

/** 读取顶栏未读数（无角标即 0） */
async function unreadCount(page: Page): Promise<number> {
  const badge = unreadBadge(page)
  if ((await badge.count()) === 0) return 0
  const text = (await badge.first().innerText()).trim()
  const parsed = Number.parseInt(text, 10)
  return Number.isNaN(parsed) ? 0 : parsed
}

/** 分页总数（Arco `共 N 条`） */
async function paginationTotal(page: Page): Promise<number> {
  const total = page.locator('.arco-pagination-total')
  if ((await total.count()) === 0) return 0
  const match = /(\d+)/.exec(await total.first().innerText())
  return match ? Number.parseInt(match[1], 10) : 0
}

/** 等待工具条按钮脱离 loading（首屏请求完成） */
async function waitListSettled(page: Page): Promise<void> {
  await expect(page.locator('.toolbar-actions .arco-btn-loading')).toHaveCount(0, { timeout: 15_000 })
}

/** 确认 popconfirm（按提示文案锁定浮层，避免残留 DOM 误点） */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 直连后端登录换取 token（fixture 构造用） */
async function loginToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${BACKEND}/api/auth/login`, { data: ADMIN })
  const body = (await response.json()) as { code: number; data: { token: string } }
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.token
}

/** 直连 API 触发一次扫描（仅用于「消化」现存信号，UI 验证仍走「立即扫描」按钮） */
async function scanViaApi(request: APIRequestContext, token: string): Promise<void> {
  const response = await request.post(`${BACKEND}/api/notifications/scan`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number }
  expect(body.code, JSON.stringify(body)).toBe(0)
}

/**
 * 经接口制造一个「低库存」商品（安全库存 100、初始库存 0 → 触发低库存信号），返回商品编码。
 * 商品创建即创建库存行，无需额外入库。
 */
async function createLowStockProduct(
  request: APIRequestContext,
  token: string
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }
  const code = uniqueCode('E2E_A')

  const categoryResponse = await request.post(`${BACKEND}/api/categories`, {
    headers,
    data: { name: `预警分类${uniqueSuffix()}` },
  })
  const categoryBody = (await categoryResponse.json()) as { code: number; data: { id: string } }
  expect(categoryBody.code, JSON.stringify(categoryBody)).toBe(0)

  const productResponse = await request.post(`${BACKEND}/api/products`, {
    headers,
    data: {
      code,
      name: `预警商品${code}`,
      categoryId: categoryBody.data.id,
      unit: '个',
      purchasePrice: 10,
      salePrice: 20,
      safetyStock: 100,
    },
  })
  const productBody = (await productResponse.json()) as { code: number }
  expect(productBody.code, JSON.stringify(productBody)).toBe(0)

  return code
}

/** 经接口创建角色 */
async function createRoleViaApi(
  request: APIRequestContext,
  token: string,
  name: string,
  permissionKeys: string[]
): Promise<string> {
  const response = await request.post(`${BACKEND}/api/roles`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name, permissionKeys },
  })
  const body = (await response.json()) as { code: number; data: { id: string } }
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.id
}

/** 经接口创建用户并绑定角色 */
async function createUserViaApi(
  request: APIRequestContext,
  token: string,
  username: string,
  roleId: string
): Promise<void> {
  const response = await request.post(`${BACKEND}/api/users`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { username, displayName: 'E2E 无库存权限', password: 'initPass123', roleIds: [roleId] },
  })
  const body = (await response.json()) as { code: number }
  expect(body.code, JSON.stringify(body)).toBe(0)
}

/** 以 admin 身份进入站内消息页（入口为顶栏铃铛，路由直达） */
async function goNotifications(page: Page): Promise<void> {
  await loginAs(page, ADMIN.username, ADMIN.password)
  await page.goto('/notifications')
  await expect(page).toHaveURL(/\/notifications$/)
  await expect(page.getByRole('columnheader', { name: '标题' })).toBeVisible()
  await waitListSettled(page)
}

test.describe('库存预警通知（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const response = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!response.ok()) throw new Error(`status ${response.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('7.1 制造低库存 → 立即扫描 → 站内信出现（低库存）且铃铛未读数 +1', async ({ page, request }) => {
    test.setTimeout(180_000)
    await goNotifications(page)
    const token = await loginToken(request)

    // 消化现存信号并清空未读，保证后续计数只反映本次新信号（同日去重语义）
    await scanViaApi(request, token)
    await page.getByRole('button', { name: '全部已读' }).click()
    await confirmPopconfirm(page, '确认把全部未读消息标记为已读？')
    await expect.poll(() => unreadCount(page)).toBe(0)

    lowStockCode = await createLowStockProduct(request, token)
    await page.getByRole('button', { name: '刷新' }).click()
    await expect.poll(() => unreadCount(page)).toBe(0)

    // 点「立即扫描」直到列表出现该商品消息（Arco 按钮 loading 期间会吞点击）
    await clickUntil(page, '立即扫描', dataRows(page).filter({ hasText: lowStockCode }).first())

    const row = dataRows(page).filter({ hasText: lowStockCode }).first()
    await expect(row).toContainText('低库存')
    await expect.poll(() => unreadCount(page)).toBe(1)
  })

  test('7.2 点击消息：标记已读（未读数 -1）并跳转库存查询且带筛选', async ({ page }) => {
    test.setTimeout(120_000)
    await goNotifications(page)

    const row = dataRows(page).filter({ hasText: lowStockCode }).first()
    await expect(row).toBeVisible()
    const before = await unreadCount(page)
    expect(before).toBeGreaterThan(0)

    await row.getByRole('button', { name: '查看' }).click()

    // 跳转库存查询并预置仓 + 商品编码筛选（specs/041 §4.4）
    await expect(page).toHaveURL(new RegExp(`/inventory\\?[^#]*keyword=${lowStockCode}`))
    await expect(dataRows(page).filter({ hasText: lowStockCode }).first()).toBeVisible()
    await expect.poll(() => unreadCount(page)).toBe(before - 1)
  })

  test('7.3 新信号扫描后再「全部已读」→ 未读数归零', async ({ page, request }) => {
    test.setTimeout(120_000)
    await goNotifications(page)
    const token = await loginToken(request)

    secondLowStockCode = await createLowStockProduct(request, token)
    await clickUntil(page, '立即扫描', dataRows(page).filter({ hasText: secondLowStockCode }).first())
    await expect.poll(() => unreadCount(page)).toBeGreaterThan(0)

    await page.getByRole('button', { name: '全部已读' }).click()
    await confirmPopconfirm(page, '确认把全部未读消息标记为已读？')
    await expectMessage(page, '已标记')
    await expect.poll(() => unreadCount(page)).toBe(0)
  })

  test('7.4 同日再次扫描：不重复生成（列表总条数不变、未读数不增）', async ({ page }) => {
    test.setTimeout(120_000)
    await goNotifications(page)

    const totalBefore = await paginationTotal(page)
    const unreadBefore = await unreadCount(page)

    await page.getByRole('button', { name: '立即扫描' }).click()
    await expectMessage(page, '扫描完成，暂无新告警')

    await expect.poll(() => paginationTotal(page)).toBe(totalBefore)
    await expect.poll(() => unreadCount(page)).toBe(unreadBefore)
  })

  test('7.5 无 inventory.view 权限的用户收不到库存告警', async ({ page, request }) => {
    test.setTimeout(180_000)
    await goNotifications(page)
    const token = await loginToken(request)

    // 新信号扫描：admin（持 inventory.view）收到消息
    thirdLowStockCode = await createLowStockProduct(request, token)
    await clickUntil(page, '立即扫描', dataRows(page).filter({ hasText: thirdLowStockCode }).first())
    await expect.poll(() => unreadCount(page)).toBeGreaterThan(0)

    // 构造「有 notifications.view、无 inventory.view」的角色与用户
    const roleId = await createRoleViaApi(request, token, uniqueCode('e2erole').slice(0, 20), [
      'products.view',
      'notifications.view',
    ])
    const username = uniqueCode('e2enotify').slice(0, 50)
    await createUserViaApi(request, token, username, roleId)

    // 该用户登录后铃铛无未读（不是告警接收人）
    await loginAs(page, username, 'initPass123')
    await expect.poll(() => unreadCount(page)).toBe(0)
  })
})

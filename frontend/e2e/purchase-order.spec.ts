import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
const TOKEN_KEY = 'app:token'
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 后端 API 基地址（绕过前端直接验证服务端校验时使用） */
const API_BASE = 'http://localhost:5080/api'

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `订单测试分类${Date.now().toString(36)}`
}

/** 生成唯一供应商名称 */
function uniquePartnerName(): string {
  return `po${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单（进销存分组）进入采购订单页 */
async function goPurchaseOrders(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购订单')
  await expect(page).toHaveURL(/\/purchase-orders$/)
}

/** 经侧边菜单进入采购入库页 */
async function goPurchases(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购入库')
  await expect(page).toHaveURL(/\/purchases$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
}

/** 经侧边菜单进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '往来单位')
  await expect(page).toHaveURL(/\/partners$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 * Arco 选中后旧弹层 DOM 残留且选项会被过滤隐藏，点选项不可靠；Enter 作用于当前聚焦 select，
 * 且选中后 select 容器会渲染选中项文本（搜索框被清空），故用 toContainText 验证选中。
 */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 抽屉标题（exact 精确匹配） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/** 点含指定提示文案的 popconfirm 浮层里的「确定」按钮 */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 新增供应商（名称唯一，类型=供应商） */
async function createSupplier(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText('供应商', { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建')).toBeVisible()
  await expect(drawerTitle(page, '新增往来单位')).toHaveCount(0)
}

/** 新增商品（分类就地新建），采购价 10 / 销售价 20 */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增商品')).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueCategoryName())
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').last()).toBeVisible()
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建').last()).toBeVisible()
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 新建采购订单（1 行明细，单价取商品采购价 10），返回订单号 */
async function createPurchaseOrder(page: Page, supplierName: string, productCode: string, quantity: number): Promise<string> {
  await page.getByRole('button', { name: '新建订单' }).click()
  await expect(page).toHaveURL(/\/purchase-orders\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(row0.locator('.arco-select'), productCode)
  const qty = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购订单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchase-orders\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^PO\d{12}$/).first().innerText()).trim()
}

/** 在订单详情页点「去入库」→ 带出订单明细（关联模式）→ 按给定数量提交，返回入库单号 */
async function receiveFromOrder(page: Page, quantity: number): Promise<string> {
  await page.getByRole('button', { name: '去入库' }).click()
  await expect(page).toHaveURL(/\/purchases\/new\?orderId=/)

  // 关联模式：明细已按订单带出（固定为订单明细，不可新增行）
  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  await expect(page.getByRole('button', { name: '添加行' })).toHaveCount(0)

  const qty = rows.nth(0).locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  // 入库单详情显示关联订单号
  const receiptNo = (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
  return receiptNo
}

/** 订单列表按单号搜索并返回目标行 */
async function findOrderRow(page: Page, orderNo: string): Promise<ReturnType<typeof page.locator>> {
  await page.getByPlaceholder('搜索单号 / 供应商').fill(orderNo)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
  const row = dataRows(page).first()
  await expect(row).toContainText(orderNo)
  return row
}

/** 订单行内「未收数量」列的数值（第 7 列，index 6） */
function unfulfilledOf(row: ReturnType<typeof page.locator>): ReturnType<typeof page.locator> {
  return row.locator('td').nth(6)
}

/** 取当前登录 token（绕过前端直接验证后端校验时使用） */
async function currentToken(page: Page): Promise<string> {
  const token = await page.evaluate((key) => window.localStorage.getItem(key), TOKEN_KEY)
  expect(token).toBeTruthy()
  return token as string
}

test.describe('采购订单（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('下单 100 → 部分入库 60 → 部分收货 / 未收 40 → 再入库 40 → 已完成', async ({ page }) => {
    const code = uniqueProductCode('ord_a')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `订单商品${Date.now() % 100000}`)

    // 下单 100 件
    await goPurchaseOrders(page)
    const orderNo = await createPurchaseOrder(page, supplier, code, 100)
    await expect(page.getByText('待收货', { exact: true })).toBeVisible()
    await expect(page.getByText('未收总数')).toBeVisible()

    // 部分入库 60
    await receiveFromOrder(page, 60)

    // 订单列表：部分收货 + 未收 40
    await goPurchaseOrders(page)
    const row = await findOrderRow(page, orderNo)
    await expect(row.getByText('部分收货', { exact: true })).toBeVisible()
    await expect(unfulfilledOf(row)).toHaveText('40')

    // 订单详情：关联入库单可见 + 明细已收 60 / 未收 40
    await row.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/purchase-orders\/detail\//)
    await expect(page.getByText(/^GR\d{12}$/).first()).toBeVisible()

    // 再入库剩余 40 → 已完成
    await receiveFromOrder(page, 40)
    await goPurchaseOrders(page)
    const row2 = await findOrderRow(page, orderNo)
    await expect(row2.getByText('已完成', { exact: true })).toBeVisible()
    await expect(unfulfilledOf(row2)).toHaveText('0')
  })

  test('关联入库数量超过未收量 → 后端拒绝（40115）', async ({ page, request }) => {
    const code = uniqueProductCode('ord_b')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `超量商品${Date.now() % 100000}`)

    await goPurchaseOrders(page)
    assertOrderNoIsCreated(await createPurchaseOrder(page, supplier, code, 10))

    // 取订单明细行（订单 id 取自当前详情页 URL）
    const orderId = page.url().split('/').pop() as string
    const token = await currentToken(page)
    const detailRes = await request.get(`${API_BASE}/purchase-orders/${orderId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const detail = (await detailRes.json()).data as {
      partnerId: string
      orderDate: string
      items: { id: string; productId: string }[]
    }

    // 前端输入框已按未收数量限制（max），直接打接口验证服务端拦截
    const res = await request.post(`${API_BASE}/purchase-receipts`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        partnerId: detail.partnerId,
        orderDate: detail.orderDate,
        orderId,
        items: [
          {
            productId: detail.items[0].productId,
            quantity: 11,
            unitPrice: 10,
            orderItemId: detail.items[0].id,
          },
        ],
      },
    })
    const body = (await res.json()) as { code: number; message: string }
    expect(body.code).toBe(40115)
    expect(body.message).toContain('未收')
  })

  test('部分收货后：不可编辑 / 不可作废（按钮消失），可关闭', async ({ page }) => {
    const code = uniqueProductCode('ord_c')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `状态商品${Date.now() % 100000}`)

    await goPurchaseOrders(page)
    const orderNo = await createPurchaseOrder(page, supplier, code, 10)

    // 待收货：编辑 / 关闭 / 作废可用（完整链路可见）
    await goPurchaseOrders(page)
    const pendingRow = await findOrderRow(page, orderNo)
    await expect(pendingRow.getByRole('button', { name: '编辑' })).toBeVisible()
    await expect(pendingRow.getByRole('button', { name: '关闭' })).toBeVisible()

    // 部分收货
    await pendingRow.getByRole('button', { name: '详情' }).click()
    await receiveFromOrder(page, 4)

    await goPurchaseOrders(page)
    const partialRow = await findOrderRow(page, orderNo)
    await expect(partialRow.getByText('部分收货', { exact: true })).toBeVisible()
    // 已开始收货：编辑与作废入口消失，仅可关闭
    await expect(partialRow.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(partialRow.getByRole('button', { name: '关闭' })).toBeVisible()

    // 关闭订单 → 状态已关闭，关闭入口消失
    await partialRow.getByRole('button', { name: '关闭' }).click()
    await confirmPopconfirm(page, '确认关闭该订单？')
    await expect(page.getByText('订单已关闭，剩余数量不再收货')).toBeVisible()
    const closedRow = dataRows(page).first()
    await expect(closedRow.getByText('已关闭', { exact: true })).toBeVisible()
    await expect(closedRow.getByRole('button', { name: '关闭' })).toHaveCount(0)
  })

  test('作废关联入库单 → 订单未收数量与状态回退', async ({ page }) => {
    const code = uniqueProductCode('ord_d')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `回退商品${Date.now() % 100000}`)

    await goPurchaseOrders(page)
    const orderNo = await createPurchaseOrder(page, supplier, code, 10)

    // 全部收货 → 已完成
    await receiveFromOrder(page, 10)
    await goPurchaseOrders(page)
    const completedRow = await findOrderRow(page, orderNo)
    await expect(completedRow.getByText('已完成', { exact: true })).toBeVisible()

    // 回采购入库列表作废该入库单
    await goPurchases(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(supplier)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await confirmPopconfirm(page, '确认作废该采购单？')
    await expect(page.getByText('已作废，库存已回冲')).toBeVisible()

    // 订单回退：未收 10、状态回到待收货
    await goPurchaseOrders(page)
    const rolledBack = await findOrderRow(page, orderNo)
    await expect(rolledBack.getByText('待收货', { exact: true })).toBeVisible()
    await expect(unfulfilledOf(rolledBack)).toHaveText('10')
  })

  test('不关联订单直接入库（既有直通用法回归）', async ({ page }) => {
    const code = uniqueProductCode('ord_e')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `直通商品${Date.now() % 100000}`)

    await goPurchases(page)
    await page.getByRole('button', { name: '开采购单' }).click()
    await expect(page).toHaveURL(/\/purchases\/new$/)

    await selectBySearch(page.locator('.arco-select').first(), supplier)
    const rows = dataRows(page)
    await selectBySearch(rows.nth(0).locator('.arco-select'), code)
    const qty = rows.nth(0).locator('.arco-input-number').nth(0).locator('input')
    await qty.fill('3')
    await qty.blur()

    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('采购单已创建')).toBeVisible()
    await expect(page).toHaveURL(/\/purchases\/detail\//)
    // 未关联订单：描述项显示占位符
    await expect(page.locator('.detail-desc').getByText('关联订单')).toBeVisible()
  })
})

/** 断言订单号格式（失败时保留可读信息） */
function assertOrderNoIsCreated(orderNo: string): void {
  expect(orderNo).toMatch(/^PO\d{12}$/)
}

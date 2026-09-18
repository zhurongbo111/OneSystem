import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
const TOKEN_KEY = 'app:token'
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `销售订单分类${Date.now().toString(36)}`
}

/** 生成唯一往来单位名称 */
function uniquePartnerName(): string {
  return `so${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单进入销售订单页 */
async function goSalesOrders(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '销售订单')
  await expect(page).toHaveURL(/\/sales-orders$/)
}

/** 经侧边菜单进入销售出库页 */
async function goSales(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '销售出库')
  await expect(page).toHaveURL(/\/sales$/)
}

/** 经侧边菜单进入采购入库页（准备库存用） */
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

/** 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项） */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  // 远程搜索下拉异步渲染：先等匹配项出现再 Enter，否则空列表下 Enter 选不中（空库 / 冷启动必现）
  await expect(
    selectLocator.page().locator('.arco-select-option:visible', { hasText: keyword }).first(),
  ).toBeVisible()
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 抽屉标题（exact 精确匹配） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/** 新增往来单位（指定类型文案：供应商 / 客户 / 两者） */
async function createPartner(page: Page, name: string, typeLabel: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建').last()).toBeVisible()
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

/** 通过采购入库单（不关联订单）备货，保证后续销售出库库存充足 */
async function stockIn(page: Page, supplierName: string, productCode: string, quantity: number): Promise<void> {
  await goPurchases(page)
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), supplierName)
  const rows = dataRows(page)
  await selectBySearch(rows.nth(0).locator('.arco-select'), productCode)
  const qty = rows.nth(0).locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
}

/** 新建销售订单（1 行明细，单价取商品销售价 20），返回订单号 */
async function createSalesOrder(page: Page, customerName: string, productCode: string, quantity: number): Promise<string> {
  await page.getByRole('button', { name: '新建订单' }).click()
  await expect(page).toHaveURL(/\/sales-orders\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), customerName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(row0.locator('.arco-select'), productCode)
  const qty = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('销售订单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/sales-orders\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^SO\d{12}$/).first().innerText()).trim()
}

/** 在销售订单详情页点「去出库」→ 带出订单明细（关联模式）→ 按给定数量提交 */
async function shipFromOrder(page: Page, quantity: number): Promise<void> {
  await page.getByRole('button', { name: '去出库' }).click()
  await expect(page).toHaveURL(/\/sales\/new\?orderId=/)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  await expect(page.getByRole('button', { name: '添加行' })).toHaveCount(0)

  const qty = rows.nth(0).locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('销售单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/sales\/detail\//)
}

/** 销售订单列表按单号搜索并返回目标行 */
async function findOrderRow(page: Page, orderNo: string): Promise<ReturnType<typeof page.locator>> {
  await page.getByPlaceholder('搜索单号 / 客户').fill(orderNo)
  await searchAndWaitHit(page, orderNo)
  return dataRows(page).first()
}

/** 订单行内「未发数量」列的数值（第 7 列，index 6） */
function unfulfilledOf(row: ReturnType<typeof page.locator>): ReturnType<typeof page.locator> {
  return row.locator('td').nth(6)
}

test.describe('销售订单（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('下单 10 → 部分出库 4 → 部分发货 / 未发 6 → 出库剩余 6 → 已完成', async ({ page }) => {
    const code = uniqueProductCode('sord_a')
    const supplier = uniquePartnerName()
    const customer = uniquePartnerName()

    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await createPartner(page, customer, '客户')
    await goProducts(page)
    await createProduct(page, code, `销售订单商品${Date.now() % 100000}`)

    // 备货 100（不关联订单的直通入库）
    await stockIn(page, supplier, code, 100)

    // 下单 10
    await goSalesOrders(page)
    const orderNo = await createSalesOrder(page, customer, code, 10)
    await expect(page.getByText('待发货', { exact: true }).first()).toBeVisible()

    // 部分出库 4
    await shipFromOrder(page, 4)

    await goSalesOrders(page)
    const row = await findOrderRow(page, orderNo)
    await expect(row.getByText('部分发货', { exact: true })).toBeVisible()
    await expect(unfulfilledOf(row)).toHaveText('6')

    // 出库剩余 6 → 已完成
    await row.getByRole('button', { name: '详情' }).click()
    await shipFromOrder(page, 6)

    await goSalesOrders(page)
    const row2 = await findOrderRow(page, orderNo)
    await expect(row2.getByText('已完成', { exact: true })).toBeVisible()
    await expect(unfulfilledOf(row2)).toHaveText('0')

    // 出库单列表可见关联订单号
    await goSales(page)
    await page.getByPlaceholder('搜索单号 / 客户').fill(customer)
    await searchAndWaitHit(page, orderNo)
  })
})

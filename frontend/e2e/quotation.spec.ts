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
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 本用例商品统一销售价（协议价以它为对比基准） */
const SALE_PRICE = 20
/** 本用例协议价（客户 × 商品） */
const AGREED_PRICE = 15

/** 生成唯一名称（客户 / 供应商 / 商品名共用前缀） */
function uniqueName(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 登录后经侧边菜单进入指定页面（菜单点击被吞时重试，前端规则 §10.1） */
async function goMenuItem(page: Page, name: string, url: RegExp): Promise<void> {
  await login(page)
  await expect(async () => {
    await clickMenuItem(page, name)
    await expect(page).toHaveURL(url, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 经侧边菜单进入报价单页（037：销售分组） */
async function goQuotations(page: Page): Promise<void> {
  await goMenuItem(page, '报价单', /\/quotations$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await goMenuItem(page, '商品管理', /\/products$/)
}

/** 经侧边菜单进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await goMenuItem(page, '往来单位', /\/partners$/)
}

/** 经侧边菜单进入采购入库页（用于垫库存） */
async function goPurchases(page: Page): Promise<void> {
  await goMenuItem(page, '采购入库', /\/purchases$/)
}

/** 经侧边菜单进入客户价格页（配置协议价） */
async function goPartnerPrices(page: Page): Promise<void> {
  await goMenuItem(page, '客户价格', /\/partner-prices$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
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

/** 抽屉标题（限定在抽屉内：新建按钮文本可能与抽屉标题同名） */
function drawerTitle(page: Page, title: string): Locator {
  return page.locator('.arco-drawer-title', { hasText: title })
}

/**
 * 打开抽屉（带重试点击）。
 * Arco Button 在 loading / 重渲染期间会吞掉 click（前端规则 §10.1），
 * 判据用抽屉标题（限定 .arco-drawer-title）而非页面文本，避免命中同名按钮。
 */
async function openDrawer(page: Page, buttonName: string, title: string): Promise<Locator> {
  await clickUntil(page, buttonName, drawerTitle(page, title))
  return page.locator('.arco-drawer')
}

/** 点击按钮直到页面跳转到目标路由（跳转类按钮同样会被吞点击） */
async function clickUntilUrl(page: Page, buttonName: string, url: RegExp): Promise<void> {
  await clickUntil(page, buttonName, async () => {
    await expect(page).toHaveURL(url)
  })
}

/** 取当前登录态 token（接口断言用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 新增往来单位（名称唯一；账期 / 额度 0 = 现结 / 不限） */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  const drawer = await openDrawer(page, '新增', '新增往来单位')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
  await expect(drawerTitle(page, '新增往来单位')).toHaveCount(0)
}

/** 新增商品（分类就地新建），采购价 10 / 销售价 20 */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  const drawer = await openDrawer(page, '新增', '新增商品')
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`报价测试分类${Date.now().toString(36)}`)
  await drawer.getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = drawer.locator('.arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill(`${SALE_PRICE}.00`)
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 采购单垫库存（数量 qty） */
async function createPurchaseReceipt(
  page: Page,
  supplierName: string,
  productCode: string,
  qty: number,
): Promise<void> {
  await clickUntilUrl(page, '开采购单', /\/purchases\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  await selectBySearch(rows.nth(0).locator('.arco-select'), productCode)
  const qtyInput = rows.nth(0).locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
}

/** 新增协议价（客户 / 商品 / 单价） */
async function createPartnerPrice(page: Page, customerName: string, productCode: string, price: number): Promise<void> {
  const drawer = await openDrawer(page, '新增协议价', '新增协议价')
  await selectBySearch(drawer.locator('.arco-select').nth(0), customerName)
  await selectBySearch(drawer.locator('.arco-select').nth(1), productCode)
  await drawer.locator('.arco-input-number input').fill(String(price))
  await drawer.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '协议价已创建')
  await expect(drawerTitle(page, '新增协议价')).toHaveCount(0)
}

/** 打开报价单新建页并选好客户 */
async function openQuotationForm(page: Page, customerName: string): Promise<void> {
  await clickUntilUrl(page, '新建报价单', /\/quotations\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), customerName)
}

/**
 * 明细行选商品并填数量（单价由批量取价回填，不在此填）。
 * 取价是异步批量请求，故用重试断言等待单价回填。
 */
async function fillQuotationLine(
  page: Page,
  rowIndex: number,
  productCode: string,
  qty: number,
  price: number,
  sourceLabel: '协议价' | '默认价',
): Promise<void> {
  const row = dataRows(page).nth(rowIndex)
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()

  const priceInput = row.locator('.arco-input-number').nth(1).locator('input')
  await expect(async () => {
    expect(Number(await priceInput.inputValue())).toBe(price)
  }).toPass({ timeout: 15_000 })
  await expect(row.locator('.price-source')).toContainText(sourceLabel)
}

/** 详情页描述项（Arco descriptions 渲染为表格行） */
function descRow(page: Page, label: string): Locator {
  return page.locator('tr', { hasText: label })
}

/** 转销售订单（详情页按钮 → 二次确认） */
async function convertToOrder(page: Page): Promise<void> {
  await clickUntil(page, '转销售订单', page.getByRole('button', { name: '确认转单' }))
  await page.getByRole('button', { name: '确认转单' }).click()
  await expectMessage(page, '已转销售订单')
}

/** 读取库存数量（按商品编码查库存查询页数据） */
async function stockQuantity(page: Page, productCode: string): Promise<number> {
  const token = await apiToken(page)
  const response = await page.request.get(`/api/inventory?page=1&pageSize=20&keyword=${productCode}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number; data: { items: { stockQuantity: number }[] } }
  expect(body.code).toBe(0)
  return body.data.items[0]?.stockQuantity ?? 0
}

/** 读取往来对账的应收金额（按客户名查；报价单不产生应收 → 0） */
async function reconciliationReceivable(page: Page, keyword: string): Promise<number> {
  const token = await apiToken(page)
  const response = await page.request.get(`/api/reconciliation?page=1&pageSize=20&keyword=${keyword}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number; data: { items: { receivableAmount: number }[] } }
  expect(body.code).toBe(0)
  return body.data.items[0]?.receivableAmount ?? 0
}

test.describe('报价单（037）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('建报价单带出协议价 → 编辑草稿 → 转销售订单 → 报价单锁定、订单出现', async ({ page }) => {
    const customer = uniqueName('qt_c')
    const supplier = uniqueName('qt_s')
    const productCode = uniqueName('qt_p')

    // 基础数据：客户 / 供应商 / 商品（销售价 20）+ 垫库存 10 + 协议价 15
    await goPartners(page)
    await createPartner(page, customer, '客户')
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, productCode, `报价商品${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 10)
    await goPartnerPrices(page)
    await createPartnerPrice(page, customer, productCode, AGREED_PRICE)

    const stockBefore = await stockQuantity(page, productCode)
    expect(stockBefore).toBe(10)

    // 新建报价单：选客户 + 选商品 → 单价自动回填协议价 15 且标注「协议价」
    await goQuotations(page)
    await openQuotationForm(page, customer)
    await fillQuotationLine(page, 0, productCode, 2, AGREED_PRICE, '协议价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '报价单已创建')
    await expect(page).toHaveURL(/\/quotations\/detail\//)

    // 详情：草稿 + 总额 2 × 15 = 30.00
    await expect(descRow(page, '报价状态')).toContainText('草稿')
    await expect(descRow(page, '总金额')).toContainText('¥ 30.00')

    // 编辑草稿：数量 2 → 3，保存后回到详情（总额 45.00）
    await clickUntilUrl(page, '编辑', /\/quotations\/edit\//)
    const qtyInput = dataRows(page).nth(0).locator('.arco-input-number').nth(0).locator('input')
    // 等明细回填完成（回填前的占位行会被整体替换，过早 fill 会被覆盖）
    await expect(qtyInput).toHaveValue('2')
    await qtyInput.fill('3')
    await qtyInput.blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '报价单已保存')
    await expect(page).toHaveURL(/\/quotations\/detail\//)
    await expect(descRow(page, '总金额')).toContainText('¥ 45.00')

    // 转销售订单：报价单锁定为「已转订单」，转单信息给出可跳转的订单号
    await convertToOrder(page)
    await expect(descRow(page, '报价状态')).toContainText('已转订单')
    const orderNoCell = descRow(page, '转出销售订单')
    await expect(orderNoCell).toContainText('SO')

    // 订单出现：从转单信息点进销售订单详情（计划单据：待发货、金额一致）
    await orderNoCell.locator('a').click()
    await expect(page).toHaveURL(/\/sales-orders\/detail\//)
    await expect(descRow(page, '总金额')).toContainText('¥ 45.00')
    await expect(descRow(page, '订单状态')).toContainText('待发货')

    // 报价单不影响库存与应收：库存仍为 10，客户应收仍为 0
    expect(await stockQuantity(page, productCode)).toBe(stockBefore)
    expect(await reconciliationReceivable(page, customer)).toBe(0)

    // 列表：该报价单显示「已转订单」，且不再有编辑 / 转订单入口
    await goQuotations(page)
    await page.getByPlaceholder('搜索单号 / 客户').fill(customer)
    await searchAndWaitHit(page, customer)
    const row = dataRows(page).first()
    await expect(row).toContainText('已转订单')
    await expect(row.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(row.getByRole('button', { name: '转订单' })).toHaveCount(0)
  })

  test('已转订单的报价单不可再编辑 / 作废 / 转单（40166 / 40167）', async ({ page }) => {
    const customer = uniqueName('qt_d')
    const productCode = uniqueName('qt_q')

    await goPartners(page)
    await createPartner(page, customer, '客户')
    await goProducts(page)
    await createProduct(page, productCode, `锁定商品${Date.now() % 100000}`)

    // 建报价单（未配协议价 → 默认价 = 销售价 20）并转单
    await goQuotations(page)
    await openQuotationForm(page, customer)
    await fillQuotationLine(page, 0, productCode, 1, SALE_PRICE, '默认价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '报价单已创建')
    await expect(page).toHaveURL(/\/quotations\/detail\//)
    const quotationId = page.url().split('/detail/')[1] as string

    await convertToOrder(page)
    await expect(descRow(page, '报价状态')).toContainText('已转订单')

    // UI：锁定态不再显示编辑 / 作废 / 转销售订单
    await expect(page.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '作废' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '转销售订单' })).toHaveCount(0)

    // 接口：作废 → 40166，再转 → 40167
    const token = await apiToken(page)
    const voidResponse = await page.request.put(`/api/quotations/${quotationId}/void`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await voidResponse.json()) as { code: number }).code).toBe(40166)

    const convertResponse = await page.request.post(`/api/quotations/${quotationId}/convert`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await convertResponse.json()) as { code: number }).code).toBe(40167)
  })
})

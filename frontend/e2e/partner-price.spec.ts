import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil, clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage, messageLocator } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 本用例商品统一销售价（协议价以它为对比基准） */
const SALE_PRICE = 20

/** 生成唯一名称（客户 / 供应商 / 商品名共用前缀便于对账页按前缀搜索） */
function uniqueName(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** n 天前的本地日期（YYYY-MM-DD，用于制造逾期单据） */
function daysAgo(n: number): string {
  const d = new Date()
  d.setDate(d.getDate() - n)
  const pad = (v: number) => String(v).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
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

/** 经侧边菜单进入客户价格页（036：资金分组） */
async function goPartnerPrices(page: Page): Promise<void> {
  await goMenuItem(page, '客户价格', /\/partner-prices$/)
}

/** 经侧边菜单进入销售出库页 */
async function goSales(page: Page): Promise<void> {
  await goMenuItem(page, '销售出库', /\/sales$/)
}

/** 经侧边菜单进入采购入库页（用于垫库存） */
async function goPurchases(page: Page): Promise<void> {
  await goMenuItem(page, '采购入库', /\/purchases$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await goMenuItem(page, '商品管理', /\/products$/)
}

/** 经侧边菜单进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await goMenuItem(page, '往来单位', /\/partners$/)
}

/** 经侧边菜单进入往来对账页 */
async function goReconciliation(page: Page): Promise<void> {
  await goMenuItem(page, '往来对账', /\/reconciliation$/)
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

/** 抽屉标题（限定在抽屉内：新建按钮文本可能与抽屉标题同名，如「新增协议价」） */
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

/** 点含指定提示文案的 popconfirm 浮层里的「确定」按钮（每个浮层独立且各含唯一确定按钮） */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 新增往来单位（名称唯一；账期天数 / 信用额度按需设置，0 = 现结 / 不限） */
async function createPartner(
  page: Page,
  name: string,
  typeLabel: '客户' | '供应商',
  terms: { paymentTermDays: number; creditLimit: number },
): Promise<void> {
  const drawer = await openDrawer(page, '新增', '新增往来单位')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  // 抽屉内 input-number 仅两个：账期天数 / 信用额度
  const numberInputs = drawer.locator('.arco-input-number input')
  await numberInputs.nth(0).fill(String(terms.paymentTermDays))
  await numberInputs.nth(1).fill(String(terms.creditLimit))
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
  await catInput.fill(`价格测试分类${Date.now().toString(36)}`)
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

/** 进入销售开单页并选好客户 */
async function openSaleForm(page: Page, customerName: string): Promise<void> {
  await clickUntilUrl(page, '开销售单', /\/sales\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), customerName)
}

/** 改明细行数量（已在开单页时复用，避免重复「开销售单」跳转） */
async function fillSaleQty(page: Page, rowIndex: number, qty: number): Promise<void> {
  const qtyInput = dataRows(page).nth(rowIndex).locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()
}

/** 明细行选商品并填数量（单价由批量取价回填，不在此填） */
async function fillSaleLine(page: Page, rowIndex: number, productCode: string, qty: number): Promise<void> {
  const row = dataRows(page).nth(rowIndex)
  await selectBySearch(row.locator('.arco-select'), productCode)
  await fillSaleQty(page, rowIndex, qty)
}

/**
 * 断言明细行单价与来源标注（协议价 / 默认价）。
 * 取价为异步批量请求，故轮询等待单价回填（不用一次性断言）。
 */
async function expectLinePrice(
  page: Page,
  rowIndex: number,
  price: number,
  sourceLabel: '协议价' | '默认价',
): Promise<void> {
  const row = dataRows(page).nth(rowIndex)
  const priceInput = row.locator('.arco-input-number').nth(1).locator('input')
  await expect(async () => {
    expect(Number(await priceInput.inputValue())).toBe(price)
  }).toPass({ timeout: 15_000 })
  await expect(row.locator('.price-source')).toContainText(sourceLabel)
}

/** 新增协议价（客户 / 商品 / 单价） */
async function createPartnerPrice(
  page: Page,
  customerName: string,
  productCode: string,
  price: number,
): Promise<void> {
  const drawer = await openDrawer(page, '新增协议价', '新增协议价')
  await selectBySearch(drawer.locator('.arco-select').nth(0), customerName)
  await selectBySearch(drawer.locator('.arco-select').nth(1), productCode)
  await drawer.locator('.arco-input-number input').fill(String(price))
  await drawer.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '协议价已创建')
  await expect(drawerTitle(page, '新增协议价')).toHaveCount(0)
}

/** 按关键词搜索客户价格列表并取首行 */
async function searchPartnerPrice(page: Page, keyword: string): Promise<Locator> {
  await page.getByPlaceholder('搜索客户名 / 商品编码 / 商品名称').fill(keyword)
  await searchAndWaitHit(page, keyword)
  return dataRows(page).first()
}

test.describe('客户价格与账期额度（036）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('配置协议价 → 销售开单自动带出协议价且标注「协议价」', async ({ page }) => {
    const customer = uniqueName('pp_c')
    const supplier = uniqueName('pp_s')
    const productCode = uniqueName('pp_a')

    await goPartners(page)
    await createPartner(page, customer, '客户', { paymentTermDays: 0, creditLimit: 0 })
    await createPartner(page, supplier, '供应商', { paymentTermDays: 0, creditLimit: 0 })
    await goProducts(page)
    await createProduct(page, productCode, `价格商品A${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 10)

    // 客户价格页：配置协议价（销售价 20 → 协议价 15）
    await goPartnerPrices(page)
    await createPartnerPrice(page, customer, productCode, 15)
    const row = await searchPartnerPrice(page, productCode)
    await expect(row).toContainText('¥ 15.00')
    await expect(row).toContainText('¥ 20.00')
    await expect(row).toContainText('-5.00') // 价差 = 协议价 − 销售价

    // 销售开单：选客户 + 选商品 → 单价自动回填协议价 15 并标注「协议价」
    await goSales(page)
    await openSaleForm(page, customer)
    await fillSaleLine(page, 0, productCode, 2)
    await expectLinePrice(page, 0, 15, '协议价')

    // 提交：后端重算总额 2 × 15 = 30.00
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')
    await expect(page).toHaveURL(/\/sales\/detail\//)
    // 详情描述项渲染为表格行，按行文本定位（总额与明细小计同值，不能用全局文本断言）
    await expect(page.locator('tr', { hasText: '总金额' })).toContainText('¥ 30.00')
    // 账期 0（现结）→ 到期日 = 单据日期
    await expect(page.locator('tr', { hasText: '到期日' })).toContainText(daysAgo(0))
  })

  test('未配置协议价的商品为「默认价」；删除协议价后回到销售价', async ({ page }) => {
    const customer = uniqueName('pp_d')
    const supplier = uniqueName('pp_s')
    const codeA = uniqueName('pp_b')
    const codeB = uniqueName('pp_c')

    await goPartners(page)
    await createPartner(page, customer, '客户', { paymentTermDays: 0, creditLimit: 0 })
    await createPartner(page, supplier, '供应商', { paymentTermDays: 0, creditLimit: 0 })
    await goProducts(page)
    await createProduct(page, codeA, `价格商品B${Date.now() % 100000}`)
    await createProduct(page, codeB, `价格商品C${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, codeA, 10)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, codeB, 10)

    // 仅商品 A 配协议价（15），商品 B 不配
    await goPartnerPrices(page)
    await createPartnerPrice(page, customer, codeA, 15)

    // 开单：A → 协议价 15；B → 默认价 20
    await goSales(page)
    await openSaleForm(page, customer)
    await fillSaleLine(page, 0, codeA, 1)
    await expectLinePrice(page, 0, 15, '协议价')
    await clickUntilCount(page, '添加行', dataRows(page), 2)
    await fillSaleLine(page, 1, codeB, 1)
    await expectLinePrice(page, 1, 20, '默认价')

    // 删除 A 的协议价 → 该商品恢复默认价
    await goPartnerPrices(page)
    await searchPartnerPrice(page, codeA)
    await clickUntil(page, '删除', page.locator('.arco-trigger-popup', { hasText: '确认删除该协议价' }))
    await confirmPopconfirm(page, '确认删除该协议价')
    await expectMessage(page, '协议价已删除，该商品恢复默认价')

    // 重新开单：A 的单价回到销售价 20 且标注「默认价」
    await goSales(page)
    await openSaleForm(page, customer)
    await fillSaleLine(page, 0, codeA, 1)
    await expectLinePrice(page, 0, 20, '默认价')
  })

  test('额度 1000 / 应收 800 → 开 300 被拒、开 200 通过', async ({ page }) => {
    const customer = uniqueName('pp_e')
    const supplier = uniqueName('pp_s')
    const productCode = uniqueName('pp_f')

    await goPartners(page)
    await createPartner(page, customer, '客户', { paymentTermDays: 0, creditLimit: 1000 })
    await createPartner(page, supplier, '供应商', { paymentTermDays: 0, creditLimit: 0 })
    await goProducts(page)
    await createProduct(page, productCode, `额度商品${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 100)

    // 先开 40 × 20 = 800（应收 800，未超额）
    await goSales(page)
    await openSaleForm(page, customer)
    await fillSaleLine(page, 0, productCode, 40)
    await expectLinePrice(page, 0, 20, '默认价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')
    await expect(page).toHaveURL(/\/sales\/detail\//)

    // 再开 15 × 20 = 300 → 800 + 300 > 1000，整单拒绝（提示可见，不产生单据）
    await goSales(page)
    await openSaleForm(page, customer)
    await fillSaleLine(page, 0, productCode, 15)
    await expectLinePrice(page, 0, 20, '默认价')
    await clickUntil(page, '提交', messageLocator(page, '超出信用额度'))
    await expect(page).toHaveURL(/\/sales\/new$/)

    // 再开 10 × 20 = 200 → 800 + 200 = 1000，刚好通过（仍在开单页，改数量后重新提交）
    await fillSaleQty(page, 0, 10)
    await expectLinePrice(page, 0, 20, '默认价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')
    await expect(page).toHaveURL(/\/sales\/detail\//)
  })

  test('额度 0 不限（大额开单通过）；往来对账逾期列与「仅看逾期」筛选', async ({ page }) => {
    const prefix = uniqueName('pp_g')
    const overdueCustomer = `${prefix}_due`
    const futureCustomer = `${prefix}_future`
    const supplier = uniqueName('pp_s')
    const productCode = uniqueName('pp_h')
    // 逾期客户：现结（账期 0）+ 单据日期 60 天前 → 到期日已过
    const overdueDate = daysAgo(60)

    await goPartners(page)
    await createPartner(page, overdueCustomer, '客户', { paymentTermDays: 0, creditLimit: 0 })
    await createPartner(page, futureCustomer, '客户', { paymentTermDays: 3650, creditLimit: 0 })
    await createPartner(page, supplier, '供应商', { paymentTermDays: 0, creditLimit: 0 })
    await goProducts(page)
    await createProduct(page, productCode, `逾期商品${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 100)

    // 额度 0 = 不限：50 × 20 = 1000（远超常规额度）整单通过
    await goSales(page)
    await openSaleForm(page, overdueCustomer)
    await page.locator('.arco-picker input').first().fill(overdueDate)
    await page.locator('.arco-picker input').first().press('Enter')
    await fillSaleLine(page, 0, productCode, 50)
    await expectLinePrice(page, 0, 20, '默认价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')
    await expect(page).toHaveURL(/\/sales\/detail\//)
    await expect(page.locator('tr', { hasText: '总金额' })).toContainText('¥ 1000.00')
    // 到期日 = 单据日期 + 账期 0
    await expect(page.locator('tr', { hasText: '到期日' })).toContainText(overdueDate)

    // 另一个客户：账期 3650 → 到期日在未来，不逾期
    await goSales(page)
    await openSaleForm(page, futureCustomer)
    await fillSaleLine(page, 0, productCode, 1)
    await expectLinePrice(page, 0, 20, '默认价')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')

    // 往来对账：按共同前缀搜索 → 两客户两行
    await goReconciliation(page)
    await page.getByPlaceholder('搜索往来名称').fill(prefix)
    await searchAndWaitHit(page, prefix)
    const rows = dataRows(page)
    await expect(rows).toHaveCount(2)

    const overdueRow = rows.filter({ hasText: overdueCustomer })
    // 账期 0 / 最大逾期天数 > 0（标红）/ 最早到期日为单据日期当天
    await expect(overdueRow).toContainText('0')
    await expect(overdueRow.locator('.overdue')).toHaveText(/^[1-9]\d*$/)
    await expect(overdueRow).toContainText(overdueDate)

    // 「仅看逾期」筛选：只剩逾期客户一行（未逾期客户被过滤）
    await page.locator('.filter-bar__overdue').click()
    await clickUntilCount(page, '搜索', overdueRow, 1)
    await expect(rows.filter({ hasText: futureCustomer })).toHaveCount(0)
  })
})

import { statSync } from 'node:fs'

import { expect, test, type Download, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端地址（API 直连，不经前端 dev server） */
const BACKEND = 'http://localhost:5080'
/** dev 后端健康检查地址 */
const BACKEND_HEALTH = `${BACKEND}/health`
/** dev 管理员账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/**
 * 跨用例共享的前置数据（本文件串行执行：playwright.config workers = 1）。
 * 由第一个用例创建：往来（客户 / 供应商）、商品、采购垫库存、两张销售出库单。
 */
const fixture = {
  customer: '',
  supplier: '',
  productCode: '',
  invoiceNo: '',
  salesNos: [] as string[],
  purchaseNo: '',
}

/** 唯一名称（往来 / 商品 / 分类 / 发票号） */
function uniqueName(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 当天本地日期 YYYY-MM-DD（日期范围筛选用） */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 详情页内 a-table 的数据行：`a-descriptions` 也是 table（同一 tbody 选择器会把它一并计入），
 * 故详情页断言必须限定在 `.arco-table` 内。
 */
function detailTableRows(page: Page): Locator {
  return page.locator('.arco-table tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉容器（`unmount-on-close`：关闭后整体移除） */
function drawer(page: Page): Locator {
  return page.locator('.arco-drawer')
}

/** 登录并进入首页 */
async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 经侧边菜单进入指定页面 */
async function goPage(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await login(page)
  await clickMenuItem(page, menuName)
  await expect(page).toHaveURL(urlPattern)
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 */
async function selectBySearch(page: Page, selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  // 远程搜索下拉浮层异步渲染：等待匹配选项出现后再点击，避免 Enter 过早选中空项
  await page.locator('.arco-select-option:visible', { hasText: keyword }).first().click()
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 取当前登录态 token（接口 fixture 用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 经接口 POST 发票（返回统一响应体，供断言业务码） */
async function apiPostInvoice(page: Page, payload: Record<string, unknown>): Promise<{ code: number; message: string }> {
  const token = await apiToken(page)
  const response = await page.request.post('/api/invoices', {
    data: payload,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { code: number; message: string }
}

/** 按关键词查列表并取首行 id（接口 fixture 用：销售出库单 / 采购入库单 id） */
async function apiFindOrderId(page: Page, path: string, keyword: string): Promise<string> {
  const token = await apiToken(page)
  const response = await page.request.get(`/api${path}?keyword=${keyword}&page=1&pageSize=20`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { data: { items: { id: string }[] } }
  expect(body.data.items.length, `未找到单据：${keyword}`).toBeGreaterThan(0)
  return body.data.items[0].id
}

// ============================== 前置数据（经界面创建）==============================

/** 新增往来单位（名称唯一；typeLabel = '客户' | '供应商'） */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增往来单位', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer(page).locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
  await expect(page.getByText('新增往来单位', { exact: true })).toHaveCount(0)
}

/** 新增商品（分类就地新建），采购价 10 / 销售价 20 */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueName('发票测试分类'))
  await drawer(page).getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = drawer(page).locator('.arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(page.getByText('新增商品', { exact: true })).toHaveCount(0)
}

/** 采购垫库存（数量 10），返回采购入库单号 */
async function seedStockByPurchase(page: Page, supplierName: string, productCode: string): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)

  await selectBySearch(page, page.locator('.arco-select').first(), supplierName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(page, row0.locator('.arco-select'), productCode)
  const qty0 = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty0.fill('10')
  await qty0.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/** 开一张单行销售单（数量 1、单价 20 → 总额 20.00），返回单号 */
async function createSalesShipment(page: Page, customerName: string, productCode: string): Promise<string> {
  await goPage(page, '销售出库', /\/sales$/)
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)

  await selectBySearch(page, page.locator('.arco-select').first(), customerName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(page, row0.locator('.arco-select'), productCode)
  const qty0 = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty0.fill('1')
  await qty0.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '销售单已创建')
  await expect(page).toHaveURL(/\/sales\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

// ============================== 发票页辅助 ==============================

/** 打开登记发票页并选好往来单位（候选列表就绪） */
async function openInvoiceForm(page: Page, partnerName: string, type: '进项' | '销项' = '销项'): Promise<void> {
  await goPage(page, '发票登记', /\/invoices$/)
  await page.getByRole('button', { name: '登记发票' }).click()
  await expect(page).toHaveURL(/\/invoices\/new$/)
  if (type === '进项') {
    await page.locator('.arco-radio-group').getByText('进项', { exact: true }).click()
  }
  await selectBySearch(page, page.locator('.arco-select').first(), partnerName)
  // 候选单据列表加载完成
  await expect(dataRows(page).first()).toBeVisible()
}

/** 勾选全部候选行并点「全部开票」（金额 = 各行未开票金额） */
async function selectAllAndFill(page: Page): Promise<void> {
  await page.locator('thead .arco-checkbox').first().click()
  await page.getByRole('button', { name: '全部开票' }).click()
}

/** 提交发票表单并等待跳转详情页 */
async function submitInvoice(page: Page): Promise<void> {
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '发票已登记')
  await expect(page).toHaveURL(/\/invoices\/detail\//)
}

/** 列表页按发票号定位并返回该行（关键字筛选：重试点搜索直到命中行出现） */
async function findInvoiceRow(page: Page, invoiceNo: string): Promise<Locator> {
  await goPage(page, '发票登记', /\/invoices$/)
  await page.getByPlaceholder('搜索发票号 / 往来单位 / 单据号').fill(invoiceNo)
  await clickUntil(page, '搜索', dataRows(page).filter({ hasText: invoiceNo }).first())
  return dataRows(page).filter({ hasText: invoiceNo }).first()
}

/** 确认 popconfirm */
async function confirmPopconfirm(page: Page): Promise<void> {
  await page.locator('.arco-popconfirm:visible').getByRole('button', { name: '确定' }).click()
}

/** 点「导出」并等待浏览器下载事件（Arco Button loading 期间会吞 click，故重试） */
async function clickExportAndWaitDownload(page: Page): Promise<Download> {
  let download: Download | null = null
  await expect(async () => {
    const wait = page.waitForEvent('download', { timeout: 5000 })
    await page.getByRole('button', { name: '导出', exact: true }).click()
    download = await wait
  }).toPass({ timeout: 30_000 })
  return download!
}

test.describe('发票登记（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('前置数据与登记：销项发票关联 2 张销售单（部分开票）→ 详情税额与合计正确', async ({ page }) => {
    fixture.customer = uniqueName('发票客户')
    fixture.supplier = uniqueName('发票供应商')
    fixture.productCode = uniqueName('sku_inv')

    // 往来（客户 / 供应商）
    await goPage(page, '往来单位', /\/partners$/)
    await createPartner(page, fixture.customer, '客户')
    await createPartner(page, fixture.supplier, '供应商')

    // 商品 + 采购垫库存（数量 10）
    await goPage(page, '商品管理', /\/products$/)
    await createProduct(page, fixture.productCode, uniqueName('发票测试商品'))
    await goPage(page, '采购入库', /\/purchases$/)
    fixture.purchaseNo = await seedStockByPurchase(page, fixture.supplier, fixture.productCode)

    // 两张销售出库单（各 20.00）
    fixture.salesNos = [
      await createSalesShipment(page, fixture.customer, fixture.productCode),
      await createSalesShipment(page, fixture.customer, fixture.productCode),
    ]

    // 登记销项发票：不含税 40（= 两张单据全额）、税率 13% → 税额 5.20、价税合计 45.20
    fixture.invoiceNo = uniqueName('INV')
    await openInvoiceForm(page, fixture.customer)
    await page.getByPlaceholder('1-50 字符，全局唯一').fill(fixture.invoiceNo)
    await page.getByPlaceholder('不含税金额').fill('40')
    await selectAllAndFill(page)
    await submitInvoice(page)

    // 详情：税额 / 价税合计由后端重算，关联单据 2 行
    await expect(page.locator('.detail-desc').getByText(fixture.invoiceNo)).toBeVisible()
    await expect(page.getByText('¥ 5.20', { exact: true }).first()).toBeVisible()
    await expect(page.getByText('¥ 45.20', { exact: true }).first()).toBeVisible()
    await expect(page.getByText('13%', { exact: true }).first()).toBeVisible()
    const itemRows = detailTableRows(page)
    await expect(itemRows).toHaveCount(2)
    // 明细顺序取请求顺序（候选列表按单据日期倒序），故只断言两张单据都被关联，不约束行序
    await expect(itemRows.filter({ hasText: fixture.salesNos[0] })).toHaveCount(1)
    await expect(itemRows.filter({ hasText: fixture.salesNos[1] })).toHaveCount(1)
  })

  test('金额闸门与方向 / 唯一性：超未开票金额 40133、方向不符 40134、发票号重复 40132', async ({ page }) => {
    await login(page)
    const orderId = await apiFindOrderId(page, '/sales-shipments', fixture.salesNos[0])
    const purchaseId = await apiFindOrderId(page, '/purchase-receipts', fixture.purchaseNo)
    const partnerId = await apiFindOrderId(page, '/partners', fixture.customer)

    // 超未开票金额（两张单已全额开票 → 未开票 0）→ 40133，提示含单号与未开票金额
    const exceeded = await apiPostInvoice(page, {
      invoiceNo: uniqueName('INV'),
      type: 1,
      partnerId,
      invoiceDate: '2026-01-05T00:00:00.000Z',
      amountExcludingTax: 20,
      taxRate: 0.13,
      items: [{ orderType: 1, orderId, amount: 20 }],
    })
    expect(exceeded.code).toBe(40133)
    expect(exceeded.message).toContain(fixture.salesNos[0])
    expect(exceeded.message).toContain('0.00')

    // 销项票关联采购入库单 → 40134
    const direction = await apiPostInvoice(page, {
      invoiceNo: uniqueName('INV'),
      type: 1,
      partnerId,
      invoiceDate: '2026-01-05T00:00:00.000Z',
      amountExcludingTax: 20,
      taxRate: 0.13,
      items: [{ orderType: 0, orderId: purchaseId, amount: 20 }],
    })
    expect(direction.code).toBe(40134)

    // 发票号重复 → 40132
    const duplicated = await apiPostInvoice(page, {
      invoiceNo: fixture.invoiceNo,
      type: 1,
      partnerId,
      invoiceDate: '2026-01-05T00:00:00.000Z',
      amountExcludingTax: 20,
      taxRate: 0.13,
      items: [{ orderType: 1, orderId, amount: 20 }],
    })
    expect(duplicated.code).toBe(40132)
  })

  test('作废释放金额：作废后该往来下单据重新可开票', async ({ page }) => {
    // 作废第一张发票
    const row = await findInvoiceRow(page, fixture.invoiceNo)
    await row.getByRole('button', { name: '作废' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '发票已作废，占用金额已释放')
    await expect(page.locator('tr.row-voided').filter({ hasText: fixture.invoiceNo })).toBeVisible()

    // 作废后未开票金额回到 20 → 可再次开票（取首张销售单全额开票）
    await openInvoiceForm(page, fixture.customer)
    await expect(dataRows(page)).toHaveCount(2)
    await expect(dataRows(page).first()).toContainText('20.00')
    await page.getByPlaceholder('1-50 字符，全局唯一').fill(uniqueName('INV'))
    await page.getByPlaceholder('不含税金额').fill('20')
    await page.locator('tbody .arco-checkbox').first().click()
    await page.getByRole('button', { name: '全部开票' }).click()
    await submitInvoice(page)
    await expect(page.getByText('¥ 22.60', { exact: true }).first()).toBeVisible()
  })

  test('列表筛选（类型 / 往来 / 单据号关键词）与导出发票 Excel', async ({ page }) => {
    await goPage(page, '发票登记', /\/invoices$/)

    // 关键词命中关联单据号（明细 EXISTS）
    await page.getByPlaceholder('搜索发票号 / 往来单位 / 单据号').fill(fixture.salesNos[1])
    await searchAndWaitHit(page, fixture.salesNos[1])
    await expect(dataRows(page).first()).toContainText(fixture.salesNos[1])

    // 日期范围（当天 → 当天）+ 类型（销项）+ 往来筛选
    const today = todayLocal()
    const rangeInputs = page.locator('.toolbar-filter .arco-picker input')
    await rangeInputs.nth(0).fill(today)
    await rangeInputs.nth(0).press('Enter')
    await rangeInputs.nth(1).fill(today)
    await rangeInputs.nth(1).press('Enter')
    await page.locator('.toolbar-filter .arco-select').nth(0).click()
    await page.locator('.arco-select-option', { hasText: '销项' }).first().click()
    await page.locator('.toolbar-filter .arco-select').nth(1).click()
    await page.locator('.arco-select-option', { hasText: fixture.customer }).first().click()
    await page.getByPlaceholder('搜索发票号 / 往来单位 / 单据号').fill('')
    await page.getByRole('button', { name: '搜索' }).click()
    await expect(async () => {
      await expect(dataRows(page).filter({ hasText: fixture.customer }).first()).toBeVisible()
      await expect(dataRows(page).filter({ hasText: fixture.customer })).toHaveCount(2)
    }).toPass({ timeout: 15_000 })

    // 作废行整体置灰（第一张已作废、第二张正常）
    await expect(page.locator('tr.row-voided')).toHaveCount(1)

    // 导出当前筛选全量（发票 + 关联明细两个工作表）
    const download = await clickExportAndWaitDownload(page)
    expect(download.suggestedFilename()).toMatch(/^发票_\d{12}\.xlsx$/)
    const filePath = await download.path()
    expect(filePath).toBeTruthy()
    expect(statSync(filePath!).size).toBeGreaterThan(0)
  })
})

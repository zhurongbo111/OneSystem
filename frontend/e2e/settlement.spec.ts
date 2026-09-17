import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'

/** dev 后端地址（API 直连，不经前端 dev server） */
const BACKEND = 'http://localhost:5080'
/** dev 后端健康检查地址 */
const BACKEND_HEALTH = `${BACKEND}/health`
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

/** 生成唯一往来单位名称 */
function uniquePartnerName(): string {
  return `s${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `收付款测试分类${Date.now().toString(36)}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单进入收付款列表页 */
async function goSettlements(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '收付款')
  await expect(page).toHaveURL(/\/settlements$/)
}

/** 经侧边菜单进入往来对账页 */
async function goReconciliation(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '往来对账')
  await expect(page).toHaveURL(/\/reconciliation$/)
}

/** 经侧边菜单进入销售出库页 */
async function goSales(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '销售出库')
  await expect(page).toHaveURL(/\/sales$/)
}

/** 经侧边菜单进入采购入库页（用于垫库存） */
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

/** 抽屉标题（exact 精确匹配） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 新增往来单位（名称唯一；typeLabel = '客户' | '供应商'） */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
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
  await expect(page.locator('.arco-message-content', { hasText: '分类已创建' }).last()).toBeVisible()
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建')).toBeVisible()
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 采购单垫库存（数量 10） */
async function seedStockByPurchase(page: Page, supplierName: string, productCode: string): Promise<void> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(row0.locator('.arco-select'), productCode)
  const qty0 = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty0.fill('10')
  await qty0.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchases\/detail\//)
}

/**
 * 开一张单行销售单：数量 1、单价默认带出销售价 20 → 总额 20.00；返回单号。
 */
async function createSingleLineSalesShipment(
  page: Page,
  customerName: string,
  productCode: string,
): Promise<string> {
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), customerName)

  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  await selectBySearch(row0.locator('.arco-select'), productCode)
  const qty0 = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty0.fill('1')
  await qty0.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('销售单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/sales\/detail\//)
  await expect(page.getByText('¥ 20.00', { exact: true }).first()).toBeVisible()
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

/** 销售列表按单号定位并返回该行 */
async function findSalesRow(page: Page, orderNo: string): Promise<ReturnType<typeof page.locator>> {
  await goSales(page)
  await page.getByPlaceholder('搜索单号 / 客户').fill(orderNo)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
  const row = dataRows(page).first()
  await expect(row).toContainText(orderNo)
  return row
}

/** 在新建收付款页完成一次收款核销并提交（amount 为本次核销金额） */
async function createReceipt(page: Page, amount: string): Promise<void> {
  // 候选行勾选后默认填入未结金额，再改为目标核销金额
  const row = dataRows(page).first()
  await row.locator('.arco-checkbox').click()
  const amountInput = row.locator('.arco-input-number input')
  await amountInput.fill(amount)
  await amountInput.blur()
  await page.getByRole('button', { name: '提交' }).click()
}

/** 直连后端登录并换取 token */
async function loginToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${BACKEND}/api/auth/login`, { data: CREDENTIALS })
  const body = await res.json()
  return body.data.token as string
}

test.describe('收付款与往来对账（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('部分收款 → 收满 → 作废回退：单据结算状态与往来应收同步变化', async ({ page }) => {
    const code = uniqueProductCode('stl_a')
    const customer = uniquePartnerName()
    const supplier = uniquePartnerName()

    // 准备：客户 + 供应商 + 商品（采购价 10 / 销售价 20）
    await goPartners(page)
    await createPartner(page, customer, '客户')
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, `收付款商品${Date.now() % 100000}`)

    // 垫库存 10 → 开单销售 1 件（总额 20.00）
    await goPurchases(page)
    await seedStockByPurchase(page, supplier, code)
    await goSales(page)
    const orderNo = await createSingleLineSalesShipment(page, customer, code)

    // 销售列表：初始未结算，操作列含「收付款」
    const row = await findSalesRow(page, orderNo)
    await expect(row.getByText('未结算', { exact: true })).toBeVisible()
    await expect(row.getByRole('button', { name: '收付款' })).toBeVisible()

    // 去收付款（收款方向预置）→ 核销 8.00
    await row.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(page.getByRole('radio', { name: '收款' })).toBeChecked()
    await expect(dataRows(page).first()).toContainText(orderNo)
    await createReceipt(page, '8.00')
    await expect(page.getByText('收付款单已创建')).toBeVisible()
    await expect(page).toHaveURL(/\/settlements\/detail\//)
    // 详情：核销明细快照 + 总额 8.00（限定 arco-table 内，避免命中 descriptions 行）
    await expect(page.locator('.arco-table tbody tr').first()).toContainText(orderNo)
    await expect(page.getByText('¥ 8.00', { exact: true }).first()).toBeVisible()

    // 销售列表：部分结算（未结 12.00）
    const partialRow = await findSalesRow(page, orderNo)
    await expect(partialRow.getByText('部分结算（未结 12.00）', { exact: true })).toBeVisible()

    // 往来对账：客户应收 12.00；抽屉「应收未结单据」含该单
    await goReconciliation(page)
    await expect(dataRows(page).first()).toBeVisible()
    const keywordBox = page.getByPlaceholder('搜索往来名称')
    await keywordBox.fill(customer)
    await expect(keywordBox).toHaveValue(customer)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    const reconRow = dataRows(page).filter({ hasText: customer }).first()
    await expect(reconRow).toBeVisible()
    await expect(reconRow).toContainText('12.00')
    await reconRow.getByRole('button', { name: '未结单据' }).click()
    await expect(page.getByText(`未结单据 — ${customer}`, { exact: true })).toBeVisible()
    await expect(page.locator('.arco-drawer').getByText(orderNo, { exact: true })).toBeVisible()
    await page.locator('.arco-drawer-close-btn').click()

    // 再收 12.00 → 已结算；往来应收归零
    const rowForFull = await findSalesRow(page, orderNo)
    await rowForFull.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await createReceipt(page, '12.00')
    await expect(page).toHaveURL(/\/settlements\/detail\//)

    const settledRow = await findSalesRow(page, orderNo)
    await expect(settledRow.getByText('已结算', { exact: true })).toBeVisible()

    // 作废第一张收款单（8.00）→ 单据回到部分结算（未结 12.00）
    await goSettlements(page)
    await page.getByPlaceholder('搜索单号 / 往来单位').fill(customer)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    const receiptRow = dataRows(page).filter({ hasText: '8.00' }).first()
    await receiptRow.getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该收付款单？')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '确认作废该收付款单？' })
      .getByRole('button', { name: /确\s*定/ })
      .click()
    await expect(page.getByText('已作废，单据已结算金额已回退')).toBeVisible()

    // 作废 8.00 后：已结回到 12.00 → 未结 8.00（20 − 12）
    const revertedRow = await findSalesRow(page, orderNo)
    await expect(revertedRow.getByText('部分结算（未结 8.00）', { exact: true })).toBeVisible()
  })

  test('核销校验：方向不匹配与超额核销被拒', async ({ page, request }) => {
    const code = uniqueProductCode('stl_b')
    const customer = uniquePartnerName()
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createPartner(page, customer, '客户')
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, `收付款校验商品${Date.now() % 100000}`)

    await goPurchases(page)
    await seedStockByPurchase(page, supplier, code)
    await goSales(page)
    const orderNo = await createSingleLineSalesShipment(page, customer, code)

    // 前端约束：核销金额输入框 max = 未结金额（超额被 clamp，无法提交超额）
    const salesRow = await findSalesRow(page, orderNo)
    await salesRow.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    const candidateRow = dataRows(page).first()
    await candidateRow.locator('.arco-checkbox').click()
    const amountInput = candidateRow.locator('.arco-input-number input')
    await amountInput.fill('999')
    await amountInput.blur()
    await expect(amountInput).toHaveValue(/^20(\.00)?$/)

    // 后端兜底：方向不匹配（收款单核销采购入库单）与超额核销均被拒（错误信息可见）
    const token = await loginToken(request)
    const headers = { Authorization: `Bearer ${token}` }

    const partnerRes = await request.get(
      `${BACKEND}/api/partners?keyword=${customer}&page=1&pageSize=20`,
      { headers },
    )
    const partnerId = (await partnerRes.json()).data.items[0].id as string

    const orderRes = await request.get(
      `${BACKEND}/api/sales-shipments?keyword=${orderNo}&page=1&pageSize=20`,
      { headers },
    )
    const order = (await orderRes.json()).data.items[0] as { id: string; totalAmount: number }

    const settlementDate = new Date(`${new Date().toISOString().slice(0, 10)}T00:00:00Z`).toISOString()

    const directionRes = await request.post(`${BACKEND}/api/settlements`, {
      headers,
      data: {
        type: 0,
        partnerId,
        settlementDate,
        method: 0,
        items: [{ orderType: 0, orderId: order.id, amount: 1 }],
      },
    })
    const directionBody = await directionRes.json()
    expect(directionBody.code).toBe(40114)
    expect(directionBody.message).toContain('方向')

    const exceededRes = await request.post(`${BACKEND}/api/settlements`, {
      headers,
      data: {
        type: 0,
        partnerId,
        settlementDate,
        method: 0,
        items: [{ orderType: 1, orderId: order.id, amount: order.totalAmount + 1 }],
      },
    })
    const exceededBody = await exceededRes.json()
    expect(exceededBody.code).toBe(40112)
    expect(exceededBody.message).toContain('未结金额')
  })
})

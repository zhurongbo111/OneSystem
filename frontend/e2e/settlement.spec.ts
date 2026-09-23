import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端地址（API 直连，不经前端 dev server） */
const BACKEND = 'http://localhost:5080'
/** dev 后端健康检查地址 */
const BACKEND_HEALTH = `${BACKEND}/health`
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
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
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
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

/** 经侧边菜单进入采购退货页（用于造「收供应商退款」场景） */
async function goPurchaseReturns(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购退货')
  await expect(page).toHaveURL(/\/purchase-returns$/)
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
async function selectBySearch(page: Page, selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  // 远程搜索下拉浮层异步渲染：等待匹配选项出现后再点击，避免 Enter 过早选中空项
  await page.locator('.arco-select-option:visible', { hasText: keyword }).first().click()
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
  await expectMessage(page, '往来单位已创建')
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
  await expectMessage(page, '分类已创建')
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 采购单垫库存（数量 10） */
async function seedStockByPurchase(page: Page, supplierName: string, productCode: string): Promise<void> {
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
  await expect(page.getByText('¥ 20.00', { exact: true }).first()).toBeVisible()
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

/** 销售列表按单号定位并返回该行 */
async function findSalesRow(page: Page, orderNo: string): Promise<ReturnType<typeof page.locator>> {
  await goSales(page)
  await page.getByPlaceholder('搜索单号 / 客户').fill(orderNo)
  await searchAndWaitHit(page, orderNo)
  return dataRows(page).first()
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
    await expectMessage(page, '收付款单已创建')
    await expect(page).toHaveURL(/\/settlements\/detail\//)
    // 详情：核销明细快照 + 总额 8.00（限定 arco-table 内，避免命中 descriptions 行）
    await expect(page.locator('.arco-table tbody tr').first()).toContainText(orderNo)
    await expect(page.getByText('¥ 8.00', { exact: true }).first()).toBeVisible()
    // 核销明细的单号为超链接（跳被核销单据详情；此处只断言链接存在，不打断后续流程）
    await expect(
      page.locator('.arco-table tbody tr').first().getByRole('link', { name: orderNo }),
    ).toBeVisible()

    // 打印视图（specs/027-erp-export）：详情页头部「打印」入口 → 收付款单版式含收付方向与核销明细
    const receiptNo = (await page.locator('.detail-desc').getByText(/^RC\d{12}$/).first().innerText()).trim()
    await page.getByRole('button', { name: '打印', exact: true }).click()
    await expect(page).toHaveURL(/\/print\/settlements\//)
    await expect(page.locator('.arco-layout-sider')).toHaveCount(0)
    await expect(page.getByRole('heading', { name: '收付款单' })).toBeVisible()
    const printPage = page.locator('.print-page')
    await expect(printPage.getByText(receiptNo)).toBeVisible()
    await expect(printPage.getByText(customer)).toBeVisible()
    // 收付方向（收款 / 付款）为必显字段
    await expect(printPage.getByText('类型：')).toBeVisible()
    await expect(printPage.getByText('收款', { exact: true })).toBeVisible()
    await expect(page.locator('.print-table tbody tr')).toHaveCount(1)
    await expect(page.locator('.print-table tbody tr').first()).toContainText(orderNo)
    // 「返回」回到详情页
    await page.getByRole('button', { name: '返回' }).click()
    await expect(page).toHaveURL(/\/settlements\/detail\//)

    // 销售列表：部分结算（未结 12.00）
    const partialRow = await findSalesRow(page, orderNo)
    await expect(partialRow.getByText('部分结算（未结 12.00）', { exact: true })).toBeVisible()

    // 单据详情「收付款明细」反查：可见核销该单的收款单（本次核销金额 8.00）并可跳其详情
    await partialRow.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/sales\/detail\//)
    const settlementSection = page.locator('.settlement-records')
    await expect(settlementSection.locator('tbody tr')).toHaveCount(1)
    await expect(settlementSection.locator('tbody tr').first()).toContainText(receiptNo)
    await expect(settlementSection.locator('tbody tr').first()).toContainText('收款')
    await expect(settlementSection.locator('tbody tr').first()).toContainText('8.00')
    await settlementSection.getByRole('link', { name: receiptNo }).click()
    await expect(page).toHaveURL(/\/settlements\/detail\//)
    await expect(page.locator('.detail-desc').getByText(receiptNo)).toBeVisible()

    // 往来对账：客户应收 12.00；抽屉「应收未结单据」含该单
    await goReconciliation(page)
    await expect(dataRows(page).first()).toBeVisible()
    const keywordBox = page.getByPlaceholder('搜索往来名称')
    await keywordBox.fill(customer)
    await expect(keywordBox).toHaveValue(customer)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: customer }).first())
    const reconRow = dataRows(page).filter({ hasText: customer }).first()
    await expect(reconRow).toBeVisible()
    await expect(reconRow).toContainText('12.00')
    await reconRow.getByRole('button', { name: '未结单据' }).click()
    await expect(page.getByText(`未结单据 — ${customer}`, { exact: true })).toBeVisible()
    await expect(page.locator('.arco-drawer').getByRole('link', { name: orderNo })).toBeVisible()
    await page.locator('.arco-drawer-close-btn').click()

    // 再收 12.00 → 已结算；往来应收归零
    const rowForFull = await findSalesRow(page, orderNo)
    await rowForFull.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await createReceipt(page, '12.00')
    await expect(page).toHaveURL(/\/settlements\/detail\//)

    const settledRow = await findSalesRow(page, orderNo)
    await expect(settledRow.getByText('已结算', { exact: true })).toBeVisible()
    // 已结算 → 无未结金额：列表不再显示「收付款」入口（点入必为空候选）
    await expect(settledRow.getByRole('button', { name: '收付款' })).toHaveCount(0)

    // 作废第一张收款单（8.00）→ 单据回到部分结算（未结 12.00）
    await goSettlements(page)
    // 默认列：「单据类型」显示，「创建时间」不显示（审计信息，列设置可选；收付日期才是列表主时间口径）
    await expect(page.locator('thead')).toContainText('单据类型')
    await expect(page.locator('thead')).not.toContainText('创建时间')
    await page.getByPlaceholder('搜索单号 / 往来单位').fill(customer)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: '8.00' }).first())
    const receiptRow = dataRows(page).filter({ hasText: '8.00' }).first()
    // 列表「单据类型」列：由核销明细派生（该收款单核销的是销售出库单）
    await expect(receiptRow).toContainText('销售出库单')
    await receiptRow.getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该收付款单？')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '确认作废该收付款单？' })
      .getByRole('button', { name: /确\s*定/ })
      .click()
    await expectMessage(page, '已作废，单据已结算金额已回退')

    // 作废 8.00 后：已结回到 12.00 → 未结 8.00（20 − 12）
    const revertedRow = await findSalesRow(page, orderNo)
    await expect(revertedRow.getByText('部分结算（未结 8.00）', { exact: true })).toBeVisible()
    // 回退后重新出现未结金额 → 「收付款」入口恢复显示
    await expect(revertedRow.getByRole('button', { name: '收付款' })).toBeVisible()
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

    const orderRes = await request.get(
      `${BACKEND}/api/sales-shipments?keyword=${orderNo}&page=1&pageSize=20`,
      { headers },
    )
    const orders = (await orderRes.json()).data.items as {
      id: string
      shipmentNo: string
      partnerId: string
      partnerName: string
      totalAmount: number
    }[]
    // 关键字搜索可能命中多条，取首条会拿到别的单据 / 客户（曾导致核销报 40113）
    const order = orders.find((item) => item.shipmentNo === orderNo)
    if (!order) throw new Error(`未按单号找到销售单 ${orderNo}（返回 ${orders.length} 条）`)
    expect(order.partnerName).toBe(customer)
    // 核销的往来单位以单据出参为准，不再按关键字搜伙伴列表取首条
    const partnerId = order.partnerId

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

  test('已核销单据禁止作废：作废被拒 → 作废收付款单回退 → 再作废成功', async ({ page }) => {
    const code = uniqueProductCode('stl_void')
    const customer = uniquePartnerName()
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createPartner(page, customer, '客户')
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, `禁作废商品${Date.now() % 100000}`)

    await goPurchases(page)
    await seedStockByPurchase(page, supplier, code)
    await goSales(page)
    const orderNo = await createSingleLineSalesShipment(page, customer, code)

    // 部分收款 8.00（总额 20.00）→ 单据「部分结算（未结 12.00）」
    const salesRow = await findSalesRow(page, orderNo)
    await salesRow.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(dataRows(page).first()).toContainText(orderNo)
    await createReceipt(page, '8.00')
    await expectMessage(page, '收付款单已创建')
    await expect(page).toHaveURL(/\/settlements\/detail\//)

    // 已核销 → 前端即禁用「作废」（提前拦截，后端 40120 兜底），单据保持正常
    const settledRow = await findSalesRow(page, orderNo)
    await expect(settledRow.getByText('部分结算（未结 12.00）', { exact: true })).toBeVisible()
    await expect(settledRow.getByRole('button', { name: '作废' })).toBeDisabled()

    const stillRow = await findSalesRow(page, orderNo)
    await expect(stillRow.getByText('部分结算（未结 12.00）', { exact: true })).toBeVisible()
    await expect(stillRow.getByRole('button', { name: '作废' })).toBeDisabled()

    // 作废该收款单回退金额 → 单据回到未结算 → 再作废成功
    await goSettlements(page)
    await page.getByPlaceholder('搜索单号 / 往来单位').fill(customer)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: '8.00' }).first())
    const receiptRow = dataRows(page).filter({ hasText: '8.00' }).first()
    await receiptRow.getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该收付款单？')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '确认作废该收付款单？' })
      .getByRole('button', { name: /确\s*定/ })
      .click()
    await expectMessage(page, '已作废，单据已结算金额已回退')

    const revertedRow = await findSalesRow(page, orderNo)
    await expect(revertedRow.getByText('未结算', { exact: true })).toBeVisible()
    await revertedRow.getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该销售单？')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '确认作废该销售单？' })
      .getByRole('button', { name: /确\s*定/ })
      .click()
    await expectMessage(page, '已作废，库存已回冲')
    await expect(dataRows(page).first().getByText('已作废', { exact: true })).toBeVisible()
  })

  test('采购退货收款（供应商退款）：往来可选纯供应商、核销成功且不误入应收', async ({ page }) => {
    const code = uniqueProductCode('stl_refund')
    const supplier = uniquePartnerName()

    // 准备：供应商 + 商品（采购价 10）
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, `退款商品${Date.now() % 100000}`)

    // 垫库存 10 件（采购入库 100.00 未付），再退货 1 件（总额 10.00）
    await goPurchases(page)
    await seedStockByPurchase(page, supplier, code)
    await goPurchaseReturns(page)
    await page.getByRole('button', { name: '开退货单' }).click()
    await expect(page).toHaveURL(/\/purchase-returns\/new$/)
    await selectBySearch(page, page.locator('.arco-select').first(), supplier)
    const formRow = dataRows(page).nth(0)
    await selectBySearch(page, formRow.locator('.arco-select'), code)
    const qtyInput = formRow.locator('.arco-input-number').nth(0).locator('input')
    await qtyInput.fill('1')
    await qtyInput.blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '采购退货单已创建')
    await expect(page).toHaveURL(/\/purchase-returns\/detail\//)
    const returnNo = (await page.locator('.detail-desc').getByText(/^PR\d{12}$/).first().innerText()).trim()

    // 「收付款」预置收款方向 + 该供应商；往来下拉须能显示纯供应商（放宽前只列客户 / 两者，会显示为空）
    await goPurchaseReturns(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(returnNo)
    await searchAndWaitHit(page, returnNo)
    await dataRows(page).first().getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(page.getByRole('radio', { name: '收款' })).toBeChecked()
    await expect(page.locator('.arco-select').first()).toContainText(supplier)

    // 核销该采购退货单（未结 10.00）→ 提交成功
    await expect(dataRows(page).first()).toContainText(returnNo)
    await createReceipt(page, '10.00')
    await expectMessage(page, '收付款单已创建')
    await expect(page).toHaveURL(/\/settlements\/detail\//)

    // 退货单已结算：列表与详情均不再显示「收付款 / 去收付款」入口（无未结金额）
    await goPurchaseReturns(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(returnNo)
    await searchAndWaitHit(page, returnNo)
    const settledReturnRow = dataRows(page).first()
    await expect(settledReturnRow.getByText('已结算', { exact: true })).toBeVisible()
    await expect(settledReturnRow.getByRole('button', { name: '收付款' })).toHaveCount(0)
    await settledReturnRow.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/purchase-returns\/detail\//)
    await expect(page.getByRole('button', { name: '去收付款' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '作废' })).toBeVisible()

    // 往来对账：退供应商的钱不进应收（应收 0.00）；应付为采购入库未付 100.00；未结单据数 1
    await goReconciliation(page)
    const keywordBox = page.getByPlaceholder('搜索往来名称')
    await keywordBox.fill(supplier)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: supplier }).first())
    const reconRow = dataRows(page).filter({ hasText: supplier }).first()
    await expect(reconRow.locator('td').nth(3)).toHaveText('¥ 0.00')
    await expect(reconRow.locator('td').nth(4)).toHaveText('¥ 100.00')
    await expect(reconRow.locator('td').nth(5)).toHaveText('1')
  })
})

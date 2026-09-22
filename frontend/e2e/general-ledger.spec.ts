import { expect, test, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端地址（健康检查 + 接口直连） */
const API_BASE = 'http://localhost:5080'
/** dev 后端健康检查地址 */
const BACKEND_HEALTH = `${API_BASE}/health`
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一往来单位名称 */
function uniquePartnerName(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `总账测试分类${Date.now().toString(36)}`
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉容器 */
function drawer(page: Page): Locator {
  return page.locator('.arco-drawer')
}

/** 本地日期 → YYYY-MM-DD */
function toDateText(date: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** 登录并经侧边菜单进入指定页面（菜单分组展开动画期间点击会被吞，故重试直到 URL 命中） */
async function goPage(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
  await expect(async () => {
    if (!urlPattern.test(page.url())) {
      await clickMenuItem(page, menuName)
    }
    await expect(page).toHaveURL(urlPattern, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 在 Arco search-select 中搜索并选中（Enter 确认高亮项，避免点残留浮层） */
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

/**
 * 在科目树选择中选中末级科目（该行第一个输入框即科目选择）。
 * 直接点树中节点（预置科目默认展开）；下拉在点击过程中会重渲染，故重试直到该行出现选中文本。
 */
async function selectAccount(page: Page, row: Locator, accountCode: string): Promise<void> {
  const input = row.getByRole('textbox').first()
  await input.click()
  await expect(async () => {
    await page
      .locator('.arco-tree-node-title:visible', { hasText: accountCode })
      .first()
      .click({ timeout: 3000 })
    await expect(row).toContainText(accountCode, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 确认 popconfirm 浮层里的「确定」 */
async function confirmPopconfirm(page: Page): Promise<void> {
  await page.locator('.arco-popconfirm:visible').getByRole('button', { name: /确\s*定/ }).click()
}

/** 新增往来单位（名称唯一，类型指定） */
async function createPartner(page: Page, name: string, type: '供应商' | '客户'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增往来单位', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer(page).locator('.arco-radio-group').getByText(type, { exact: true }).click()
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
  await expect(page.getByText('新增往来单位', { exact: true })).toHaveCount(0)
}

/** 新增商品（分类就地新建），返回编码 */
async function createProduct(page: Page, code: string, name: string, purchasePrice: string, salePrice: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await drawer(page).getByPlaceholder('2-50 字符').fill(name)
  await drawer(page).getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueCategoryName())
  await drawer(page).getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill(purchasePrice)
  await numberInputs.nth(1).fill(salePrice)
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(page.getByText('新增商品', { exact: true })).toHaveCount(0)
}

/** 在采购入库开单页开一张单行明细的单据，返回单号 */
async function createPurchaseReceipt(
  page: Page,
  supplierName: string,
  productCode: string,
  quantity: number,
  unitPrice: number,
): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()
  const price = row.locator('.arco-input-number').nth(1).locator('input')
  await price.fill(String(unitPrice))
  await price.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/** 在销售出库开单页开一张单行明细的单据，返回单号 */
async function createSalesShipment(
  page: Page,
  customerName: string,
  productCode: string,
  quantity: number,
  unitPrice: number,
): Promise<string> {
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), customerName)

  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()
  const price = row.locator('.arco-input-number').nth(1).locator('input')
  await price.fill(String(unitPrice))
  await price.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '销售单已创建')
  await expect(page).toHaveURL(/\/sales\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

/** 凭证页按关键词（单号）搜索并等待命中 */
async function searchVoucher(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索凭证号 / 摘要').fill(keyword)
  await searchAndWaitHit(page, keyword)
}

/** 读取当前可见利润表的合计（收入 / 成本费用 / 净利润），用于增量断言（避免与既有凭证数据耦合） */
async function readIncomeTotals(page: Page): Promise<{ revenue: number; cost: number; profit: number }> {
  const text = await page.locator('.report-total--income:visible').first().innerText()
  const numbers = [...text.matchAll(/¥\s*([\d,.]+)/g)].map((m) => Number((m[1] ?? '0').replace(/,/g, '')))
  return { revenue: numbers[0] ?? 0, cost: numbers[1] ?? 0, profit: numbers[2] ?? 0 }
}

test.describe('总账（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('开采购单 → 自动凭证借贷平衡 → 作废单据后凭证作废、余额回退（specs/033 tasks 8.1 / 8.3）', async ({ page }) => {
    const code = uniqueProductCode('gl_a')
    const supplier = uniquePartnerName('s')

    await goPage(page, '往来单位', /\/partners$/)
    await createPartner(page, supplier, '供应商')
    await goPage(page, '商品管理', /\/products$/)
    await createProduct(page, code, `总账商品A${Date.now() % 100000}`, '10.00', '30.00')

    // 开采购入库单：数量 2 × 单价 10 → 总额 20
    await goPage(page, '采购入库', /\/purchases$/)
    const receiptNo = await createPurchaseReceipt(page, supplier, code, 2, 10)

    // 凭证自动生成：借 库存商品 20 / 贷 应付账款 20（来源 = 采购入库单）
    await goPage(page, '凭证', /\/vouchers$/)
    await searchVoucher(page, receiptNo)
    const row = dataRows(page).first()
    await expect(row).toContainText('采购入库单')
    await expect(row.locator('.arco-tag', { hasText: '已过账' })).toBeVisible()

    await row.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/vouchers\/detail\//)
    // 借贷合计相等（金额均为含税总额 20.00）
    const descriptions = page.locator('.arco-descriptions')
    await expect(descriptions.getByText('借方合计')).toBeVisible()
    await expect(descriptions.getByText('¥ 20.00', { exact: true })).toHaveCount(2)
    // 分录：借 库存商品 / 贷 应付账款
    await expect(dataRows(page).filter({ hasText: '库存商品' })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: '应付账款' })).toHaveCount(1)

    // 作废采购入库单 → 其自动凭证同事务作废（余额回退）
    await goPage(page, '采购入库', /\/purchases$/)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(receiptNo)
    await searchAndWaitHit(page, receiptNo)
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '已作废，库存已回冲')

    await goPage(page, '凭证', /\/vouchers$/)
    await searchVoucher(page, receiptNo)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '已作废' })).toBeVisible()
  })

  test('开销售单 → 收入与成本结转凭证 → 利润表毛利与成本报表同源（specs/033 tasks 8.2）', async ({ page }) => {
    const code = uniqueProductCode('gl_b')
    const supplier = uniquePartnerName('s')
    const customer = uniquePartnerName('c')

    await goPage(page, '往来单位', /\/partners$/)
    await createPartner(page, supplier, '供应商')
    await createPartner(page, customer, '客户')
    await goPage(page, '商品管理', /\/products$/)
    await createProduct(page, code, `总账商品B${Date.now() % 100000}`, '10.00', '30.00')

    // 先入库 10 件 × 10 元 → 移动加权均价 10
    await goPage(page, '采购入库', /\/purchases$/)
    await createPurchaseReceipt(page, supplier, code, 10, 10)

    // 读取利润表初始合计（同一期间的既有凭证一并统计，用增量断言）
    await goPage(page, '财务报表', /\/financial-reports$/)
    await page.locator('.arco-tabs-tab-title', { hasText: '利润表' }).click()
    await expect(page.locator('.report-total--income')).toBeVisible()
    await page.waitForTimeout(1000)
    const before = await readIncomeTotals(page)

    // 销售出库 2 件 × 30 元 → 收入 60、成本（均价 10）20
    await goPage(page, '销售出库', /\/sales$/)
    const shipmentNo = await createSalesShipment(page, customer, code, 2, 30)

    // 凭证：收入 + 成本结转同凭证 4 条分录，借方合计 = 60 + 20 = 80
    await goPage(page, '凭证', /\/vouchers$/)
    await searchVoucher(page, shipmentNo)
    await dataRows(page).first().getByRole('button', { name: '详情' }).click()
    await expect(page.locator('.arco-descriptions').getByText('¥ 80.00', { exact: true })).toHaveCount(2)
    await expect(dataRows(page).filter({ hasText: '主营业务收入' })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: '主营业务成本' })).toHaveCount(1)

    // 利润表增量：收入 +60、成本费用 +20、净利润 +40（与 026 成本毛利报表同源口径）
    await goPage(page, '财务报表', /\/financial-reports$/)
    await page.locator('.arco-tabs-tab-title', { hasText: '利润表' }).click()
    await expect(page.locator('.report-total--income')).toBeVisible()
    await page.waitForTimeout(1000)
    const after = await readIncomeTotals(page)

    expect(after.revenue - before.revenue).toBeCloseTo(60, 2)
    expect(after.cost - before.cost).toBeCloseTo(20, 2)
    expect(after.profit - before.profit).toBeCloseTo(40, 2)
  })

  test('手工凭证不平衡阻止提交（后端 40155）；结账后禁止记账（40154）（specs/033 tasks 8.3 / 8.4）', async ({ page }) => {
    await goPage(page, '凭证', /\/vouchers$/)
    await page.getByRole('button', { name: '手工凭证' }).click()
    await expect(page).toHaveURL(/\/vouchers\/new$/)

    // 主表：记账日期（今天）+ 摘要
    const today = toDateText(new Date())
    await page.locator('.arco-picker input').first().fill(today)
    await page.keyboard.press('Enter')
    await page.getByPlaceholder('如：计提折旧 / 期末调整').fill(`总账手工凭证${Date.now().toString(36)}`)

    // 两行分录：借 库存商品 100 / 贷 应付账款 90 → 差额 10（前端阻止提交）
    await page.getByRole('button', { name: '添加分录' }).click()
    const rows = dataRows(page)
    await expect(rows).toHaveCount(2)
    await selectAccount(page, rows.nth(0), '1405')
    await selectAccount(page, rows.nth(1), '2202')

    const debitInput = rows.nth(0).locator('.arco-input-number input').nth(0)
    await debitInput.fill('100')
    await debitInput.blur()
    const creditInput = rows.nth(1).locator('.arco-input-number input').nth(1)
    await creditInput.fill('90')
    await creditInput.blur()

    await expect(page.getByText('差额 ¥ 10.00')).toBeVisible()
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '借贷不平衡，差额 10.00')
    await expect(page).toHaveURL(/\/vouchers\/new$/)

    // 后端同口径：直接调用接口校验 40155（前端已拦截，无法经 UI 触发）
    const token = await page.evaluate(() => localStorage.getItem('app:token'))
    const authHeaders = { Authorization: `Bearer ${token}` }
    const accountsResponse = await page.request.get(`${API_BASE}/api/accounts`, { headers: authHeaders })
    const accounts = ((await accountsResponse.json()).data ?? []) as { id: string; code: string }[]
    const debitAccountId = accounts.find((account) => account.code === '1405')!.id
    const creditAccountId = accounts.find((account) => account.code === '2202')!.id

    const unbalanced = await page.request.post(`${API_BASE}/api/vouchers`, {
      headers: authHeaders,
      data: {
        voucherDate: new Date().toISOString(),
        summary: '接口不平衡校验',
        items: [
          { accountId: debitAccountId, debit: 10, credit: 0 },
          { accountId: creditAccountId, debit: 0, credit: 9 },
        ],
      },
    })
    expect((await unbalanced.json()).code).toBe(40155)

    // 修正贷方 → 平衡后提交成功，跳详情
    await creditInput.fill('100')
    await creditInput.blur()
    await expect(page.getByText('借贷平衡')).toBeVisible()
    await page.getByRole('button', { name: '提交' }).click()
    await expect(page).toHaveURL(/\/vouchers\/detail\//)
    await expect(page.locator('.arco-descriptions').getByText('¥ 100.00', { exact: true })).toHaveCount(2)

    // 期间管理：结账当月 → 该期间禁止记账（40154）
    await goPage(page, '凭证', /\/vouchers$/)
    await page.getByRole('button', { name: '期间管理' }).click()
    const periodLabel = `${today.slice(0, 4)}-${today.slice(5, 7)}`
    const periodRow = drawer(page).locator('tbody tr', { hasText: periodLabel }).first()
    await expect(periodRow).toBeVisible()
    await periodRow.getByRole('button', { name: '结账' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, `期间 ${periodLabel} 已结账`)
    await expect(periodRow.locator('.arco-tag', { hasText: '已结账' })).toBeVisible()

    await drawer(page).locator('.arco-drawer-close-btn').click()
    await expect(drawer(page)).toHaveCount(0)
    await page.getByRole('button', { name: '手工凭证' }).click()
    await expect(page).toHaveURL(/\/vouchers\/new$/)
    await page.locator('.arco-picker input').first().fill(today)
    await page.keyboard.press('Enter')
    await page.getByPlaceholder('如：计提折旧 / 期末调整').fill('结账后记账校验')
    await page.getByRole('button', { name: '添加分录' }).click()
    const lockRows = dataRows(page)
    await selectAccount(page, lockRows.nth(0), '1405')
    await selectAccount(page, lockRows.nth(1), '2202')
    const lockDebit = lockRows.nth(0).locator('.arco-input-number input').nth(0)
    await lockDebit.fill('50')
    await lockDebit.blur()
    const lockCredit = lockRows.nth(1).locator('.arco-input-number input').nth(1)
    await lockCredit.fill('50')
    await lockCredit.blur()
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, `会计期间 ${periodLabel} 已结账，禁止记账`)

    // 反结账：避免影响其他用例（已结账期间会阻断单据作废）
    await goPage(page, '凭证', /\/vouchers$/)
    await page.getByRole('button', { name: '期间管理' }).click()
    const reopenRow = drawer(page).locator('tbody tr', { hasText: periodLabel }).first()
    await reopenRow.getByRole('button', { name: '反结账' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, `期间 ${periodLabel} 已反结账`)
    await expect(reopenRow.locator('.arco-tag', { hasText: '未结账' })).toBeVisible()
  })
})

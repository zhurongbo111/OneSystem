import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'

/**
 * 成本核算与销售毛利（specs/026-erp-cost）：
 * 期初建账（含成本单价）→ 采购入库改单价 → 库存余额表展示均价 / 库存金额；
 * 销售出库后成本与毛利报表的收入 / 成本 / 毛利，作废后归 0；「重算成本」入口可用。
 */

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
const TOKEN_KEY = 'app:token'
/** 商品分类弹窗内分类名输入框 placeholder */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

function uniquePartnerName(): string {
  return `cost_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单进入指定页面 */
async function go(page: Page, name: string, urlPattern: RegExp): Promise<void> {
  await login(page)
  await clickMenuItem(page, name)
  await expect(page).toHaveURL(urlPattern)
}

function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  // 远程搜索下拉异步渲染：先等匹配项出现再 Enter，否则空列表下 Enter 选不中（空库 / 冷启动必现）
  await expect(
    selectLocator.page().locator('.arco-select-option', { hasText: keyword }).first(),
  ).toBeVisible()
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 新增往来单位 */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建')).toBeVisible()
}

/** 新增商品（分类就地新建） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`成本分类${Date.now().toString(36)}`)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').first()).toBeVisible()
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建')).toBeVisible()
}

/** 期初建账：实盘数量 + **成本单价**（本规格新增必填项） */
async function createInitialStock(page: Page, code: string, quantity: number, unitCost: number): Promise<void> {
  await go(page, '库存盘点', /\/stock-takes$/)
  await page.getByRole('button', { name: '新建盘点' }).click()
  await expect(page).toHaveURL(/\/stock-takes\/new$/)

  // 类型切「期初建账」→ 明细出现成本单价列
  await page.getByText('期初建账', { exact: true }).click()
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select').first(), code)

  const inputs = row.locator('.arco-input-number input')
  await inputs.nth(0).fill(String(quantity))
  await inputs.nth(0).blur()
  await inputs.nth(1).fill(String(unitCost))
  await inputs.nth(1).blur()

  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('盘点单已生效')).toBeVisible()
}

/** 开一张采购入库单（指定单价，成本基线），返回单号 */
async function createPurchaseReceipt(
  page: Page,
  supplierName: string,
  code: string,
  qty: number,
  unitPrice: number,
): Promise<string> {
  await go(page, '采购入库', /\/purchases$/)
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), supplierName)
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), code)
  const inputs = row.locator('.arco-input-number input')
  await inputs.nth(0).fill(String(qty))
  await inputs.nth(0).blur()
  await inputs.nth(1).fill(String(unitPrice))
  await inputs.nth(1).blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/** 开一张销售出库单（单价默认带出销售价），返回单号 */
async function createSalesShipment(
  page: Page,
  customerName: string,
  code: string,
  qty: number,
): Promise<string> {
  await go(page, '销售出库', /\/sales$/)
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), customerName)
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), code)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('销售单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/sales\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

test.describe('成本与毛利（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('期初建账（含成本单价）→ 采购入库改单价 → 库存余额表展示库存金额与均价', async ({ page }) => {
    const code = uniqueProductCode('cost')
    const supplier = uniquePartnerName()

    await go(page, '往来单位', /\/partners$/)
    await createPartner(page, supplier, '供应商')
    await go(page, '商品管理', /\/products$/)
    await createProduct(page, code, `成本商品${Date.now() % 100000}`)

    // 期初建账 10 件 × 10 元（成本基线）
    await createInitialStock(page, code, 10, 10)
    // 采购入库 10 件 × 20 元 → 数量 20、金额 300、均价 15
    await createPurchaseReceipt(page, supplier, code, 10, 20)

    await go(page, '库存余额表', /\/reports\/stock-balance$/)
    // 等首屏数据渲染完成：页面挂载未稳时填入的关键词会被重置
    await expect(dataRows(page).first()).toBeVisible()
    // 用回车触发搜索（与成本毛利报表一致）：首屏请求未回时按钮为 loading，点击会被吞掉
    const keyword = page.getByPlaceholder('搜索商品编码或名称')
    await keyword.fill(code)
    await keyword.press('Enter')
    // 库存余额表按分类聚合：先确认按商品编码过滤已生效（仅该商品所属分类一行），再断言聚合值
    const rows = dataRows(page)
    await expect(rows).toHaveCount(1)
    const row = rows.first()
    await expect(row).toContainText('20') // 库存合计
    await expect(row).toContainText('300.00') // 库存金额 = 100 + 200
    await expect(row).toContainText('15.00') // 均价 = 300 / 20
  })

  test('销售出库后成本与毛利报表展示收入 / 成本 / 毛利，该单作废后毛利归 0', async ({ page }) => {
    const code = uniqueProductCode('profit')
    const supplier = uniquePartnerName()
    const customer = uniquePartnerName()

    await go(page, '往来单位', /\/partners$/)
    await createPartner(page, supplier, '供应商')
    await createPartner(page, customer, '客户')
    await go(page, '商品管理', /\/products$/)
    await createProduct(page, code, `毛利商品${Date.now() % 100000}`)

    // 期初 10 件 × 10 元；销售出库 5 件 × 售价 20 → 收入 100、成本 50、毛利 50
    await createInitialStock(page, code, 10, 10)
    const shipmentNo = await createSalesShipment(page, customer, code, 5)

    await go(page, '成本与毛利', /\/reports\/cost-profit$/)
    // 「毛利」与「毛利率」两列都含「毛利」，用精确文本避免 strict mode 冲突
    await expect(page.locator('th', { hasText: /^毛利$/ })).toBeVisible()
    await expect(page.locator('th', { hasText: /^毛利率$/ })).toBeVisible()

    // 按商品筛选后该单据行：收入 100.00、成本 50.00、毛利 50.00
    await page.locator('.filter-bar__product').click()
    await page.locator('.filter-bar__product input').fill(code)
    await page.locator('.filter-bar__product input').press('Enter')
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    const row = dataRows(page).first()
    await expect(row).toContainText(shipmentNo)
    await expect(row).toContainText('100.00') // 销售收入 = 5 × 20
    await expect(row).toContainText('50.00') // 销售成本 = 5 × 10（期初均价）

    // 作废该销售单 → 收入与成本同步回冲，该行毛利归 0
    await go(page, '销售出库', /\/sales$/)
    await page.getByPlaceholder('搜索单号 / 客户').fill(shipmentNo)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该销售单？')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '确认作废该销售单？' })
      .getByRole('button', { name: /确\s*定/ })
      .click()
    await expect(page.getByText('已作废，库存已回冲')).toBeVisible()

    await go(page, '成本与毛利', /\/reports\/cost-profit$/)
    await page.locator('.filter-bar__product').click()
    await page.locator('.filter-bar__product input').fill(code)
    await page.locator('.filter-bar__product input').press('Enter')
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    const afterVoid = dataRows(page).first()
    await expect(afterVoid).toContainText('0.00') // 收入 0、成本 0
  })

  test('重算成本：popconfirm 确认后成功提示含重算流水条数', async ({ page }) => {
    await go(page, '成本与毛利', /\/reports\/cost-profit$/)

    await page.getByRole('button', { name: '重算成本' }).click()
    await expect(page.getByText('重算将按流水顺序重新计算全部成本')).toBeVisible()
    await page
      .locator('.arco-trigger-popup', { hasText: '重算将按流水顺序重新计算全部成本' })
      .getByRole('button', { name: /确\s*定/ })
      .click()

    // 成功提示含「流水 N 条」
    await expect(page.getByText(/重算完成：流水 \d+ 条/)).toBeVisible()
  })
})

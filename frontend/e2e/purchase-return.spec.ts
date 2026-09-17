import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'

/**
 * 采购退货（specs/021-erp-purchase-return）：
 * 开退货单（库存减少 + 流水 -N「采购退货」）→ 结算切换 → 作废回冲（+N「采购退货作废」）；
 * 退货数量 > 库存的行内预警与提交拦截；列表筛选（单号 / 供应商 / 结算状态 / 日期范围）与作废行置灰。
 * 流水文案 / 颜色以 specs/019-erp-stock-movement/design.md §0 表为准（7 采购退货 / 8 采购退货作废）。
 */

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

/** 生成唯一供应商名称 */
function uniquePartnerName(): string {
  return `pr_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 当天本地日期 YYYY-MM-DD */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单（进销存分组）进入采购退货页 */
async function goPurchaseReturns(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购退货')
  await expect(page).toHaveURL(/\/purchase-returns$/)
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

/** 经侧边菜单进入库存查询页 */
async function goInventory(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存查询')
  await expect(page).toHaveURL(/\/inventory$/)
}

/** 经侧边菜单进入库存流水页 */
async function goStockMovements(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存流水')
  await expect(page).toHaveURL(/\/stock-movements$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 * Arco 选中后旧弹层 DOM 残留且选项会被过滤隐藏，点选项不可靠；Enter 作用于当前聚焦 select，
 * 且选中后 select 容器会渲染选中项文本（搜索框被清空），故用 toContainText 验证选中。
 * 下拉数据异步加载（dev 库数据量大时更慢），选项尚未就绪时 Enter 无效果 —— 用 toPass 整体重试。
 */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await expect(async () => {
    await selectLocator.click()
    const input = selectLocator.locator('input')
    await input.fill(keyword)
    await input.press('Enter')
    await expect(selectLocator).toContainText(keyword.slice(0, 12), { timeout: 2000 })
  }).toPass({ timeout: 20000 })
}

/**
 * 点含指定提示文案的 popconfirm 浮层里的「确定」按钮。
 * Arco popconfirm 确认后浮层 DOM 残留不销毁，同一行多个确认按钮会让 getByRole 命中多个；
 * 每个浮层是独立的 .arco-trigger-popup 节点且各含唯一确定按钮，用 hasText 锁定目标浮层即可唯一定位。
 */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 新增供应商（名称唯一，类型=供应商） */
async function createSupplier(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText('供应商', { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建')).toBeVisible()
}

/** 新增商品（分类就地新建；采购价 10、销售价 20） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类（specs/017-erp-category：抽屉内联输入条 + 保存）
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`退货测试分类${Date.now().toString(36)}`)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').last()).toBeVisible()
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建').last()).toBeVisible()
}

/**
 * 库存页按编码搜索（点搜索按钮触发服务端查询）。
 * 先确认关键词已落入输入框再点搜索，并等到过滤后的首行命中该编码，避免并发下读到未过滤的旧列表。
 */
async function searchInventory(page: Page, keyword: string): Promise<void> {
  const input = page.getByPlaceholder('搜索商品编码或名称')
  await input.fill(keyword)
  await expect(input).toHaveValue(keyword)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
  await expect(dataRows(page).first()).toContainText(keyword, { timeout: 15000 })
}

/** 库存页当前行的库存数字 */
function stockOf(page: Page): ReturnType<typeof page.locator> {
  return dataRows(page).first().locator('.stock-quantity')
}

/** 开一张单行明细的采购入库单（商品 × qty），返回单号 */
async function createInbound(page: Page, supplierName: string, code: string, qty: number): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  await selectBySearch(page.locator('.arco-select').first(), supplierName)
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), code)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/** 在退货单详情页取单号 */
async function detailReturnNo(page: Page): Promise<string> {
  return (await page.locator('.detail-desc').getByText(/^PR\d{12}$/).first().innerText()).trim()
}

/**
 * 流水页按来源单号搜索。
 * 断言「没有一行不含该单号」而非「首行含该单号」——最新一条流水本就是本单，
 * 只查首行会在过滤尚未生效时误判为已过滤。
 */
async function searchMovementByOrderNo(page: Page, orderNo: string): Promise<void> {
  const input = page.getByPlaceholder('搜索来源单号')
  await input.fill(orderNo)
  await expect(input).toHaveValue(orderNo)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
  await expect(dataRows(page).filter({ hasNotText: orderNo })).toHaveCount(0, { timeout: 15000 })
}

/** 在退货列表按单号搜索（同样等到所有行都命中该单号，避免误判过滤已生效） */
async function searchReturnByNo(page: Page, returnNo: string): Promise<void> {
  const input = page.getByPlaceholder('搜索单号 / 供应商')
  await input.fill(returnNo)
  await expect(input).toHaveValue(returnNo)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
  await expect(dataRows(page).filter({ hasNotText: returnNo })).toHaveCount(0, { timeout: 15000 })
}

test.describe('采购退货（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('开退货单 → 库存减少 → 流水 -N → 筛选与结算 → 作废回冲 +N → 作废行置灰', async ({ page }) => {
    const code = uniqueProductCode('pr_m')
    const supplier = uniquePartnerName()

    // 准备供应商 + 商品
    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `退货商品${Date.now() % 100000}`)

    // 先采购入库 5，使库存充足
    await goPurchases(page)
    await createInbound(page, supplier, code, 5)
    await goInventory(page)
    await searchInventory(page, code)
    await expect(stockOf(page)).toHaveText('5')

    // 开退货单：退货 2（单价默认带出采购价 10）→ 总额 20.00，跳详情
    await goPurchaseReturns(page)
    await page.getByRole('button', { name: '开退货单' }).click()
    await expect(page).toHaveURL(/\/purchase-returns\/new$/)
    await selectBySearch(page.locator('.arco-select').first(), supplier)
    const formRow = dataRows(page).nth(0)
    await selectBySearch(formRow.locator('.arco-select'), code)
    const qtyInput = formRow.locator('.arco-input-number').nth(0).locator('input')
    await qtyInput.fill('2')
    await qtyInput.blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('采购退货单已创建')).toBeVisible()
    await expect(page).toHaveURL(/\/purchase-returns\/detail\//)
    // 后端重算：2 × 10 = 20.00（表头总额）
    await expect(page.locator('.detail-desc').getByText('¥ 20.00', { exact: true })).toBeVisible()
    const returnNo = await detailReturnNo(page)

    // 库存减少：5 - 2 = 3
    await goInventory(page)
    await searchInventory(page, code)
    await expect(stockOf(page)).toHaveText('3')

    // 流水：-2「采购退货」，来源为退货单号
    await goStockMovements(page)
    await searchMovementByOrderNo(page, returnNo)
    await expect(dataRows(page)).toHaveCount(1)
    const moveRow = dataRows(page).first()
    await expect(moveRow).toContainText('采购退货')
    await expect(moveRow).toContainText(code)
    await expect(moveRow.locator('.qty-minus')).toHaveText('-2')

    // 列表筛选：单号 + 供应商 + 结算状态 + 当天日期范围 → 命中该单
    await goPurchaseReturns(page)
    await searchReturnByNo(page, returnNo)
    const row = dataRows(page).first()
    await expect(row).toContainText(returnNo)
    await expect(row.getByText('未结算', { exact: true })).toBeVisible()
    // 操作列按钮带图标：详情 / 收付款 / 作废（specs/011-action-column §0 顺序；手工结算切换已移除）
    const rowActions = row.locator('td.action-cell button')
    await expect(rowActions).toHaveCount(3)
    await expect(rowActions.nth(0)).toHaveText(/详情/)
    await expect(rowActions.nth(1)).toHaveText(/收付款/)
    await expect(rowActions.nth(2)).toHaveText(/作废/)

    // 供应商筛选（列表筛选下拉不可搜索，直接点选选项）
    await page.getByRole('button', { name: '重置' }).click()
    await page.locator('.toolbar-filter .arco-select').nth(0).click()
    await page.locator('.arco-select-option', { hasText: supplier }).click()
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    await expect(dataRows(page).first()).toContainText(returnNo)

    // 日期范围筛选（当天 → 当天）
    const today = todayLocal()
    const rangeInputs = page.locator('.toolbar-filter .arco-picker input')
    await rangeInputs.nth(0).fill(today)
    await rangeInputs.nth(0).press('Enter')
    await rangeInputs.nth(1).fill(today)
    await rangeInputs.nth(1).press('Enter')
    // 结算状态筛选：未结算
    await page.locator('.toolbar-filter .arco-select').nth(1).click()
    await page.locator('.arco-select-option', { hasText: '未结算' }).click()
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    await expect(dataRows(page).first()).toContainText(returnNo)

    // 重置回全量后按单号重新定位
    await page.getByRole('button', { name: '重置' }).click()
    await searchReturnByNo(page, returnNo)
    await expect(dataRows(page).first()).toContainText(returnNo)

    // 去收付款：采购退货单为收款方向（供应商退我们钱），跳新建收付款页并预置方向
    await dataRows(page).first().getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(page.getByRole('radio', { name: '收款' })).toBeChecked()
    await goPurchaseReturns(page)
    await searchReturnByNo(page, returnNo)

    // 作废：库存回冲 3 → 5
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该采购退货单？')).toBeVisible()
    await confirmPopconfirm(page, '确认作废该采购退货单？')
    await expect(page.getByText('已作废，库存已回冲')).toBeVisible()

    // 已作废行整体置灰，操作（作废 / 收付款）消失
    const voidedRow = dataRows(page).first()
    await expect(voidedRow).toHaveClass(/row-voided/)
    await expect(voidedRow.getByText('已作废', { exact: true })).toBeVisible()
    await expect(voidedRow.getByRole('button', { name: '作废' })).toHaveCount(0)
    await expect(voidedRow.getByRole('button', { name: '收付款' })).toHaveCount(0)

    // 库存回冲
    await goInventory(page)
    await searchInventory(page, code)
    await expect(stockOf(page)).toHaveText('5')

    // 流水：作废回冲 +2「采购退货作废」
    await goStockMovements(page)
    await searchMovementByOrderNo(page, returnNo)
    await expect(dataRows(page)).toHaveCount(2)
    await expect(dataRows(page).first()).toContainText('采购退货作废')
    await expect(dataRows(page).first().locator('.qty-plus')).toHaveText('+2')
    await expect(dataRows(page).nth(1).locator('.qty-minus')).toHaveText('-2')
  })

  test('退货数量 > 当前库存：行内预警可见且提交被拒', async ({ page }) => {
    const code = uniqueProductCode('pr_over')
    const supplier = uniquePartnerName()

    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, code, `超量商品${Date.now() % 100000}`)

    // 仅入库 1
    await goPurchases(page)
    await createInbound(page, supplier, code, 1)

    // 退货 5 > 库存 1 → 行内预警
    await goPurchaseReturns(page)
    await page.getByRole('button', { name: '开退货单' }).click()
    await expect(page).toHaveURL(/\/purchase-returns\/new$/)
    await selectBySearch(page.locator('.arco-select').first(), supplier)
    const row = dataRows(page).nth(0)
    await selectBySearch(row.locator('.arco-select'), code)
    // 商品下拉显示「编码 名称（库存 x）」：选到商品即带出当前库存
    await expect(row.locator('.arco-select').first()).toContainText('库存 1')
    const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
    await qtyInput.fill('5')
    await qtyInput.blur()
    await expect(row.locator('.qty-stock-error')).toHaveText('库存不足，当前库存 1')
    await expect(row.locator('.arco-input-number').nth(0)).toHaveClass(/qty-over-stock/)

    // 提交被拒：停留在开单页并提示库存不足
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('存在退货数量大于当前库存的明细行，请修正后提交')).toBeVisible()
    await expect(page).toHaveURL(/\/purchase-returns\/new$/)

    // 修正为 1 后可提交，且库存归 0
    await qtyInput.fill('1')
    await qtyInput.blur()
    await expect(row.locator('.qty-stock-error')).toHaveCount(0)
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('采购退货单已创建')).toBeVisible()
    await expect(page).toHaveURL(/\/purchase-returns\/detail\//)

    await goInventory(page)
    await searchInventory(page, code)
    await expect(stockOf(page)).toHaveText('0')
  })
})

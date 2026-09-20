import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage, messageLocator } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/**
 * 库存流水（specs/019-erp-stock-movement）：
 * 四类流水全链路（采购入库 +N / 采购作废 -N / 销售出库 -N / 销售作废 +N）、
 * 筛选与空状态、库存页「流水」下钻带 productId 预置。
 * 文案 / 颜色以 specs/019-erp-stock-movement/design.md §0 表为准。
 */

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 跨用例共享：首个用例创建的两张单号（供筛选用例复用，避免重复造数） */
let poNo = ''
let soNo = ''

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一往来单位名称 */
function uniquePartnerName(): string {
  return `mv_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 经侧边菜单（进销存分组）进入库存流水页 */
async function goStockMovements(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存流水')
  await expect(page).toHaveURL(/\/stock-movements$/)
}

/** 经侧边菜单进入采购入库页 */
async function goPurchases(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购入库')
  await expect(page).toHaveURL(/\/purchases$/)
}

/** 经侧边菜单进入销售出库页 */
async function goSales(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '销售出库')
  await expect(page).toHaveURL(/\/sales$/)
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
  // 远程搜索下拉异步渲染：先等匹配项出现再 Enter，否则空列表下 Enter 选不中（空库 / 冷启动必现）
  await expect(
    selectLocator.page().locator('.arco-select-option:visible', { hasText: keyword }).first(),
  ).toBeVisible()
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
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

/** 新增往来单位（名称唯一；typeLabel = '客户' | '供应商'） */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await clickUntil(page, '提交', messageLocator(page, '往来单位已创建'))
}

/** 新增商品（分类就地新建） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类（抽屉内联输入条 + 保存）
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`流水测试分类${Date.now().toString(36)}`)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  // 金额 / 安全库存均为 a-input-number：第 0 / 1 个是采购 / 销售价
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await clickUntil(page, '提交', messageLocator(page, '商品已创建'))
}

/** 开一张单行明细的采购单（商品 × qty），返回单号 */
async function createPurchaseReceiptQty(
  page: Page,
  supplierName: string,
  code: string,
  qty: number,
): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  const supplierSelect = page.locator('.arco-select').first()
  await selectBySearch(supplierSelect, supplierName)
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), code)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()
  await clickUntil(page, '提交', messageLocator(page, '采购单已创建'))
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/** 开一张单行明细的销售单（商品 × qty，单价默认带出销售价），返回单号 */
async function createSalesShipmentQty(
  page: Page,
  customerName: string,
  code: string,
  qty: number,
): Promise<string> {
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)
  const customerSelect = page.locator('.arco-select').first()
  await selectBySearch(customerSelect, customerName)
  const row = dataRows(page).nth(0)
  await selectBySearch(row.locator('.arco-select'), code)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill(String(qty))
  await qtyInput.blur()
  await clickUntil(page, '提交', messageLocator(page, '销售单已创建'))
  await expect(page).toHaveURL(/\/sales\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

/** 在采购 / 销售列表按单号找到该行并作废 */
async function voidOrder(page: Page, kind: 'purchase' | 'sale', orderNo: string): Promise<void> {
  const go = kind === 'purchase' ? goPurchases : goSales
  const placeholder = kind === 'purchase' ? '搜索单号 / 供应商' : '搜索单号 / 客户'
  const confirmText = kind === 'purchase' ? '确认作废该采购单？' : '确认作废该销售单？'
  await go(page)
  await page.getByPlaceholder(placeholder).fill(orderNo)
  await searchAndWaitHit(page, orderNo)
  const row = dataRows(page).first()
  await row.getByRole('button', { name: '作废' }).click()
  await expect(page.getByText(confirmText)).toBeVisible()
  await confirmPopconfirm(page, confirmText)
  await expectMessage(page, '已作废，库存已回冲')
}

/** 流水页按来源单号搜索 */
async function searchByOrderNo(page: Page, orderNo: string): Promise<void> {
  await page.getByPlaceholder('搜索来源单号').fill(orderNo)
  await searchAndWaitHit(page, orderNo)
}

test.describe('库存流水（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('采购入库 +3 → 销售出库 -2 → 销售作废 +2 → 采购作废 -3（四类流水全链路）', async ({ page }) => {
    const code = uniqueProductCode('mv_m')
    const supplier = uniquePartnerName()
    const customer = uniquePartnerName()

    // 准备供应商 + 客户 + 1 个商品
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await createPartner(page, customer, '客户')
    await goProducts(page)
    await createProduct(page, code, `流水商品${Date.now() % 100000}`)

    // 采购入库 3 → 流水页该单号 1 行，「采购入库」+3（绿字）
    await goPurchases(page)
    poNo = await createPurchaseReceiptQty(page, supplier, code, 3)
    await goStockMovements(page)
    await searchByOrderNo(page, poNo)
    await expect(dataRows(page)).toHaveCount(1)
    const rowIn = dataRows(page).first()
    await expect(rowIn).toContainText('采购入库')
    await expect(rowIn).toContainText(code)
    await expect(rowIn.locator('.qty-plus')).toHaveText('+3')

    // 销售出库 2 → 「销售出库」-2（红字）
    await goSales(page)
    soNo = await createSalesShipmentQty(page, customer, code, 2)
    await goStockMovements(page)
    await searchByOrderNo(page, soNo)
    await expect(dataRows(page)).toHaveCount(1)
    const rowOut = dataRows(page).first()
    await expect(rowOut).toContainText('销售出库')
    await expect(rowOut.locator('.qty-minus')).toHaveText('-2')

    // 销售作废 → 回增 +2「销售作废」，倒序在出库行之上
    await voidOrder(page, 'sale', soNo)
    await goStockMovements(page)
    await searchByOrderNo(page, soNo)
    await expect(dataRows(page)).toHaveCount(2)
    await expect(dataRows(page).first()).toContainText('销售作废')
    await expect(dataRows(page).first().locator('.qty-plus')).toHaveText('+2')
    await expect(dataRows(page).nth(1)).toContainText('销售出库')
    await expect(dataRows(page).nth(1).locator('.qty-minus')).toHaveText('-2')

    // 采购作废 → 回冲 -3「采购作废」，倒序在入库行之上
    await voidOrder(page, 'purchase', poNo)
    await goStockMovements(page)
    await searchByOrderNo(page, poNo)
    await expect(dataRows(page)).toHaveCount(2)
    await expect(dataRows(page).first()).toContainText('采购作废')
    await expect(dataRows(page).first().locator('.qty-minus')).toHaveText('-3')
    await expect(dataRows(page).nth(1)).toContainText('采购入库')
    await expect(dataRows(page).nth(1).locator('.qty-plus')).toHaveText('+3')
  })

  test('筛选：单号关键词 / 变动类型 / 未命中显空状态 / 重置', async ({ page }) => {
    await goStockMovements(page)

    // 单号关键词：销售单 2 行（出库 + 作废）
    await searchByOrderNo(page, soNo)
    await expect(dataRows(page)).toHaveCount(2)

    // 类型筛选「销售出库」→ 收窄为 1 行
    const typeSelect = page.locator('.filter-bar__type')
    await typeSelect.click()
    await page.locator('.arco-select-option:visible', { hasText: '销售出库' }).click()
    await searchAndWaitHit(page, soNo)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText('销售出库')

    // 关键词未命中 → 空状态
    await page.getByPlaceholder('搜索来源单号').fill('NO_SUCH_ORDER')
    await clickUntil(page, '搜索', page.locator('tbody .arco-table-tr-empty'))

    // 重置：清空条件（含类型），数据恢复
    await clickUntil(page, '重置', dataRows(page).first())
  })

  test('库存页操作列「流水」下钻 → 流水页带 productId 预置且仅展示该商品流水', async ({ page }) => {
    const code = uniqueProductCode('mv_d')
    const supplier = uniquePartnerName()

    // 造数：1 个商品 + 采购入库 1
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, `下钻商品${Date.now() % 100000}`)
    await goPurchases(page)
    await createPurchaseReceiptQty(page, supplier, code, 1)

    // 库存页找到该商品行 → 操作列「流水」
    await goInventory(page)
    await page.getByPlaceholder('搜索商品编码或名称').fill(code)
    await searchAndWaitHit(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await dataRows(page).first().getByRole('button', { name: '流水' }).click()

    // 流水页带 productId query，预置该商品且仅展示该商品流水
    await expect(page).toHaveURL(/\/stock-movements\?productId=/)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(code)
    await expect(dataRows(page).first()).toContainText('采购入库')
    await expect(dataRows(page).first().locator('.qty-plus')).toHaveText('+1')
    // 其他商品的流水不出现（唯一编码搜索，命中即该商品）
    await expect(dataRows(page)).toHaveCount(1)
  })
})

import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil, clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage, messageLocator } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/**
 * 仓库调拨（specs/039-erp-transfer）：
 * 开调拨单（A 仓 −、B 仓 +、同一事务；两条流水「调拨转出 −N」「调拨转入 +N」）→
 * 列表筛选与详情展示 → 作废回冲（双仓反向 + 两条反向流水「调拨转出作废 +N」「调拨转入作废 −N」）→
 * 同仓调拨被前端禁用（转入仓选项 disabled，后端 40126 兜底）、A 仓库存不足被前端预警 + 提交拦截。
 * 流水文案 / 颜色以 specs/019-erp-stock-movement/design.md §0 表为准
 * （11 调拨转出 / 12 调拨转入 / 13 调拨转出作废 / 14 调拨转入作废）。
 */

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 迁移内置默认仓名称（specs/038-erp-multi-warehouse/design.md §2.5） */
const DEFAULT_WAREHOUSE = '默认仓'

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一仓库编码（2-20 位字母数字，唯一） */
function uniqueWarehouseCode(prefix: string): string {
  return `${prefix}${Date.now().toString(36).toUpperCase()}`
}

/** 生成唯一仓库名称 */
function uniqueWarehouseName(): string {
  return `测试仓${Date.now().toString(36)}`
}

/** 生成唯一往来单位名称 */
function uniquePartnerName(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 当天本地日期 YYYY-MM-DD */
function todayLocal(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/**
 * 每个 page 只登录一次：本文件用例要反复跨页（仓库 / 商品 / 往来单位 / 单据 / 库存 / 流水），
 * 每次导航都重登会显著拉长用例并放大「登录与导航交叉」的偶发失败（helpers/auth.ts 注释同因）。
 */
const loggedInPages = new WeakSet<Page>()

async function ensureLogin(page: Page): Promise<void> {
  if (loggedInPages.has(page)) return
  await login(page)
  loggedInPages.add(page)
}

/**
 * 经侧边菜单进入目标页：菜单点击会被 Arco 动画 / 在途请求吞掉（点了没反应），
 * 故以「URL 到位」为判据重试点击（helpers/action.ts 的 clickUntil 只接受按钮名，菜单项不适用）。
 */
async function goMenu(page: Page, name: string, pattern: RegExp): Promise<void> {
  await ensureLogin(page)
  await expect(async () => {
    await clickMenuItem(page, name)
    await expect(page).toHaveURL(pattern, { timeout: 3000 })
  }).toPass({ timeout: 15000 })
}

/** 经侧边菜单（库存分组）进入调拨单列表页 */
async function goTransfers(page: Page): Promise<void> {
  await goMenu(page, '调拨单', /\/transfers$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await goMenu(page, '商品管理', /\/products$/)
}

/** 经侧边菜单进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await goMenu(page, '往来单位', /\/partners$/)
}

/** 经侧边菜单进入仓库管理页 */
async function goWarehouses(page: Page): Promise<void> {
  await goMenu(page, '仓库管理', /\/warehouses$/)
}

/** 经侧边菜单进入采购入库页 */
async function goPurchases(page: Page): Promise<void> {
  await goMenu(page, '采购入库', /\/purchases$/)
}

/** 经侧边菜单进入库存查询页 */
async function goInventory(page: Page): Promise<void> {
  await goMenu(page, '库存查询', /\/inventory$/)
}

/** 经侧边菜单进入库存流水页 */
async function goStockMovements(page: Page): Promise<void> {
  await goMenu(page, '库存流水', /\/stock-movements$/)
}

/** 当前表格数据行（排除空状态行；限定 arco-table 以避开详情页 arco-descriptions 行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('.arco-table tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉标题（exact 精确匹配） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）：
 * Arco 选中后旧弹层 DOM 残留且选项被过滤隐藏，点选项不可靠，故用 Enter + 选中文本校验。
 */
async function selectBySearch(selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  // 远程搜索下拉异步渲染：先等匹配项出现再 Enter，否则空列表下 Enter 选不中
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
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await clickUntil(page, '提交', async () => {
    await expect(drawerTitle(page, '新增往来单位')).toHaveCount(0)
  })
  await expectMessage(page, '往来单位已创建')
}

/** 新增仓库（编码 / 名称唯一；新增后为启用、非默认仓） */
async function createWarehouse(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增仓库')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('2-20 位字母、数字、下划线或连字符').fill(code)
  await drawer.getByPlaceholder('1-50 字符').fill(name)
  await clickUntil(page, '提交', async () => {
    await expect(drawerTitle(page, '新增仓库')).toHaveCount(0)
  })
  await expectMessage(page, '仓库已创建')
}

/** 新增商品（分类就地新建；采购价 10、销售价 20） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增商品')).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类（specs/017-erp-category：抽屉内联输入条 + 保存）
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`调拨测试分类${Date.now().toString(36)}`)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await clickUntil(page, '提交', async () => {
    await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
  })
  await expectMessage(page, '商品已创建')
}

/**
 * 开一张单行明细的采购入库单（指定入库仓，单行明细），返回单号。
 * warehouseName 为空时用默认仓预选；指定时按名称在选择器内搜索选中。
 */
async function createPurchaseReceipt(
  page: Page,
  supplierName: string,
  productCode: string,
  quantity: number,
  warehouseName?: string,
): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  // 入库仓（038）：默认仓已预选，指定仓时改为搜索选中
  if (warehouseName && warehouseName !== DEFAULT_WAREHOUSE) {
    const inboundSelect = page
      .locator('.arco-form-item', { hasText: '入库仓' })
      .locator('.arco-select')
      .first()
    await selectBySearch(inboundSelect, warehouseName)
  }

  const row = dataRows(page).first()
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

/**
 * 库存页按「商品编码 + 仓」筛选并断言该商品在该仓的库存为 expectedQty。
 * 筛选行下拉顺序固定为「分类 → 仓库」（inventory 页 038 后约定），用 index 1 定位仓库筛选；
 * 单选下拉点新选项即替换旧选择，故连续两次按不同仓断言无需手动重置。
 */
async function expectStockByWarehouse(
  page: Page,
  productCode: string,
  warehouseName: string,
  expectedQty: number,
): Promise<void> {
  // 先按商品编码搜索（点搜索触发服务端查询，等到所有行都命中该编码）
  await page.getByPlaceholder('搜索商品编码或名称').fill(productCode)
  await searchAndWaitHit(page, productCode)
  // 再按仓筛选（替换单选值）
  await page.locator('.toolbar-filter .arco-select').nth(1).click()
  await page.locator('.arco-select-option:visible', { hasText: warehouseName }).first().click()
  // 点搜索，等到「同时包含编码与仓名」的行出现（旧数据不匹配新仓 → 重试）
  const hitRow = dataRows(page).filter({ hasText: productCode }).filter({ hasText: warehouseName })
  await clickUntil(page, '搜索', hitRow.first())
  const row = dataRows(page).first()
  await expect(row).toContainText(warehouseName)
  await expect(row.locator('.stock-quantity')).toHaveText(String(expectedQty))
}

/** 流水页按来源单号搜索 */
async function searchMovementByOrderNo(page: Page, orderNo: string): Promise<void> {
  await page.getByPlaceholder('搜索来源单号').fill(orderNo)
  await searchAndWaitHit(page, orderNo)
}

/** 调拨列表按单号搜索 */
async function searchTransferByNo(page: Page, transferNo: string): Promise<void> {
  await page.getByPlaceholder('搜索单号').fill(transferNo)
  await searchAndWaitHit(page, transferNo)
}

test.describe('仓库调拨（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('开调拨单 → 双仓库存变化 → 流水双条 → 列表筛选与详情展示 → 作废回冲 + 反向流水 → 作废行置灰', async ({ page }) => {
    const productCode = uniqueProductCode('tr_m')
    const productName = `调拨商品${Date.now() % 100000}`
    const warehouseCode = uniqueWarehouseCode('WH')
    const warehouseName = uniqueWarehouseName()
    const supplier = uniquePartnerName('s')

    // 准备：供应商 + B 仓 + 商品
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goWarehouses(page)
    await createWarehouse(page, warehouseCode, warehouseName)
    await goProducts(page)
    await createProduct(page, productCode, productName)

    // 默认仓（A）入库 5
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 5)

    // A 仓库存 5；B 仓库存 0（新建商品时即生成 0 库存行）
    await goInventory(page)
    await expectStockByWarehouse(page, productCode, DEFAULT_WAREHOUSE, 5)
    await expectStockByWarehouse(page, productCode, warehouseName, 0)

    // 开调拨单 A → B，5 件
    await goTransfers(page)
    await page.getByRole('button', { name: '新调拨' }).click()
    await expect(page).toHaveURL(/\/transfers\/new$/)
    // 转出仓默认预选 A（默认仓）；转入仓选 B
    const fromSelect = page.locator('.arco-form-item', { hasText: '转出仓' }).locator('.arco-select').first()
    await expect(fromSelect).toContainText(DEFAULT_WAREHOUSE)
    const toSelect = page.locator('.arco-form-item', { hasText: '转入仓' }).locator('.arco-select').first()
    await toSelect.click()
    await page.locator('.arco-select-option:visible', { hasText: warehouseName }).first().click()
    await expect(toSelect).toContainText(warehouseName)
    // 商品明细行：选商品 + 数量 5
    const row = dataRows(page).nth(0)
    await row.locator('.arco-select').click()
    await page.locator('.arco-select-option:visible', { hasText: productCode }).first().click()
    await expect(row.locator('.arco-select')).toContainText(productCode)
    // 商品下拉显示「编码 名称（库存 x）」：选到商品即带出 A 仓库存 5
    await expect(row.locator('.arco-select')).toContainText('库存 5')
    const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
    await qtyInput.fill('5')
    await qtyInput.blur()
    // 数量合计 = 5
    await expect(page.locator('.form-footer__total-amount')).toHaveText('5')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '调拨单已创建')
    await expect(page).toHaveURL(/\/transfers\/detail\//)
    const transferNo = (await page.locator('.detail-desc').getByText(/^TR\d{12}$/).first().innerText()).trim()

    // 双仓库存：A 0 / B 5
    await goInventory(page)
    await expectStockByWarehouse(page, productCode, DEFAULT_WAREHOUSE, 0)
    await expectStockByWarehouse(page, productCode, warehouseName, 5)

    // 流水：两条，A 仓 -5「调拨转出」、B 仓 +5「调拨转入」
    await goStockMovements(page)
    await searchMovementByOrderNo(page, transferNo)
    await expect(dataRows(page)).toHaveCount(2)
    await expect(dataRows(page).filter({ hasText: '调拨转出' })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: '调拨转入' })).toHaveCount(1)
    const outRow = dataRows(page).filter({ hasText: '调拨转出' }).first()
    await expect(outRow).toContainText(DEFAULT_WAREHOUSE)
    await expect(outRow.locator('.qty-minus')).toHaveText('-5')
    const inRow = dataRows(page).filter({ hasText: '调拨转入' }).first()
    await expect(inRow).toContainText(warehouseName)
    await expect(inRow.locator('.qty-plus')).toHaveText('+5')

    // 列表筛选：单号命中 + 双仓展示 + 状态正常 + 操作列（详情 / 作废）
    await goTransfers(page)
    await searchTransferByNo(page, transferNo)
    const listRow = dataRows(page).first()
    await expect(listRow).toContainText(transferNo)
    await expect(listRow).toContainText(DEFAULT_WAREHOUSE)
    await expect(listRow).toContainText(warehouseName)
    await expect(listRow.getByText('正常', { exact: true })).toBeVisible()
    const rowActions = listRow.locator('td.action-cell button')
    await expect(rowActions).toHaveCount(2)
    await expect(rowActions.nth(0)).toHaveText(/详情/)
    await expect(rowActions.nth(1)).toHaveText(/作废/)

    // 转出仓筛选
    await page.getByRole('button', { name: '重置' }).click()
    await page.locator('.toolbar-filter .arco-select').nth(0).click()
    await page.locator('.arco-select-option:visible', { hasText: DEFAULT_WAREHOUSE }).first().click()
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: transferNo }).first())
    await expect(dataRows(page).first()).toContainText(transferNo)

    // 转入仓筛选
    await page.getByRole('button', { name: '重置' }).click()
    // 重置后 select 内部显示在更新，首次点开下拉可能渲染不稳（选项 resolved 但 click 时 not visible）；
    // 用 toPass 重试「打开下拉 → 选仓」整段，避免单次 click 失败导致 60s 超时（flaky 见前端规则 §10.1）。
    await expect(async () => {
      await page.locator('.toolbar-filter .arco-select').nth(1).click()
      await page
        .locator('.arco-select-option:visible', { hasText: warehouseName })
        .first()
        .click({ timeout: 3000 })
    }).toPass({ timeout: 10000 })
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: transferNo }).first())
    await expect(dataRows(page).first()).toContainText(transferNo)

    // 日期范围筛选（当天 → 当天）
    await page.getByRole('button', { name: '重置' }).click()
    const today = todayLocal()
    const rangeInputs = page.locator('.toolbar-filter .arco-picker input')
    await rangeInputs.nth(0).fill(today)
    await rangeInputs.nth(0).press('Enter')
    await rangeInputs.nth(1).fill(today)
    await rangeInputs.nth(1).press('Enter')
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: transferNo }).first())
    await expect(dataRows(page).first()).toContainText(transferNo)

    // 详情展示：单号 / 双仓 / 行数 / 数量合计 / 状态 / 明细
    await page.getByRole('button', { name: '重置' }).click()
    await searchTransferByNo(page, transferNo)
    await dataRows(page).first().getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/transfers\/detail\//)
    await expect(page.locator('.detail-desc').getByText(transferNo)).toBeVisible()
    await expect(page.locator('.detail-desc')).toContainText(DEFAULT_WAREHOUSE)
    await expect(page.locator('.detail-desc')).toContainText(warehouseName)
    await expect(page.getByText('正常', { exact: true })).toBeVisible()
    // 明细表格：1 行，含编码 + 数量 5
    await expect(dataRows(page).filter({ hasText: productCode })).toHaveCount(1)
    const detailRow = dataRows(page).first()
    await expect(detailRow).toContainText(productCode)
    await expect(detailRow.locator('td').last()).toHaveText('5')

    // 作废：双仓回冲（详情页底部作废按钮）
    await page.getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该调拨单？')).toBeVisible()
    await confirmPopconfirm(page, '确认作废该调拨单？')
    await expectMessage(page, '已作废，双仓库存已回冲')
    // 详情页状态变红
    await expect(page.getByText('已作废', { exact: true })).toBeVisible()
    // 底部作废按钮消失（v-if="!isVoided"）
    await expect(page.locator('.detail-actions')).toHaveCount(0)

    // 库存回冲：A 5 / B 0
    await goInventory(page)
    await expectStockByWarehouse(page, productCode, DEFAULT_WAREHOUSE, 5)
    await expectStockByWarehouse(page, productCode, warehouseName, 0)

    // 流水新增两条反向：A 仓 +5「调拨转出作废」、B 仓 -5「调拨转入作废」
    await goStockMovements(page)
    await searchMovementByOrderNo(page, transferNo)
    await expect(dataRows(page)).toHaveCount(4)
    await expect(dataRows(page).filter({ hasText: '调拨转出作废' })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: '调拨转入作废' })).toHaveCount(1)
    // 原始两条（非作废）仍存在
    const outOnlyRow = dataRows(page).filter({ hasText: '调拨转出', hasNotText: '作废' }).first()
    await expect(outOnlyRow).toContainText(DEFAULT_WAREHOUSE)
    await expect(outOnlyRow.locator('.qty-minus')).toHaveText('-5')
    const inOnlyRow = dataRows(page).filter({ hasText: '调拨转入', hasNotText: '作废' }).first()
    await expect(inOnlyRow).toContainText(warehouseName)
    await expect(inOnlyRow.locator('.qty-plus')).toHaveText('+5')
    // 反向两条
    const outVoidRow = dataRows(page).filter({ hasText: '调拨转出作废' }).first()
    await expect(outVoidRow).toContainText(DEFAULT_WAREHOUSE)
    await expect(outVoidRow.locator('.qty-plus')).toHaveText('+5')
    const inVoidRow = dataRows(page).filter({ hasText: '调拨转入作废' }).first()
    await expect(inVoidRow).toContainText(warehouseName)
    await expect(inVoidRow.locator('.qty-minus')).toHaveText('-5')

    // 列表：作废行置灰，无作废按钮
    await goTransfers(page)
    await searchTransferByNo(page, transferNo)
    const voidedRow = dataRows(page).first()
    await expect(voidedRow).toHaveClass(/row-voided/)
    await expect(voidedRow.getByText('已作废', { exact: true })).toBeVisible()
    await expect(voidedRow.getByRole('button', { name: '作废' })).toHaveCount(0)
    // 详情仍可查看，状态已作废
    await voidedRow.getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/transfers\/detail\//)
    await expect(page.getByText('已作废', { exact: true })).toBeVisible()
    await expect(page.locator('.detail-actions')).toHaveCount(0)
  })

  test('同仓调拨被前端禁用（转入仓选项 disabled，后端 40126 兜底）', async ({ page }) => {
    await goTransfers(page)
    await page.getByRole('button', { name: '新调拨' }).click()
    await expect(page).toHaveURL(/\/transfers\/new$/)
    // 转出仓默认预选默认仓；展开转入仓下拉，默认仓选项 disabled
    const fromSelect = page.locator('.arco-form-item', { hasText: '转出仓' }).locator('.arco-select').first()
    await expect(fromSelect).toContainText(DEFAULT_WAREHOUSE)
    const toSelect = page.locator('.arco-form-item', { hasText: '转入仓' }).locator('.arco-select').first()
    await toSelect.click()
    const defaultOption = page
      .locator('.arco-select-option:visible', { hasText: DEFAULT_WAREHOUSE })
      .first()
    // 选项存在但被禁用（class 含 arco-select-option-disabled）
    await expect(defaultOption).toBeVisible()
    await expect(defaultOption).toHaveClass(/arco-select-option-disabled/)
    // 关闭下拉（disabled 选项不可点击选中）
    await page.keyboard.press('Escape')
  })

  test('A 仓库存不足：行内预警 + 提交被拒；修正后可提交', async ({ page }) => {
    const productCode = uniqueProductCode('tr_over')
    const productName = `超量调拨商品${Date.now() % 100000}`
    const warehouseCode = uniqueWarehouseCode('WH')
    const warehouseName = uniqueWarehouseName()
    const supplier = uniquePartnerName('s')

    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goWarehouses(page)
    await createWarehouse(page, warehouseCode, warehouseName)
    await goProducts(page)
    await createProduct(page, productCode, productName)

    // 默认仓 A 入库 1
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, productCode, 1)

    // 调拨 A → B，5 件 > A 仓库存 1 → 行内预警 + 提交被拒
    await goTransfers(page)
    await page.getByRole('button', { name: '新调拨' }).click()
    await expect(page).toHaveURL(/\/transfers\/new$/)
    const toSelect = page.locator('.arco-form-item', { hasText: '转入仓' }).locator('.arco-select').first()
    await toSelect.click()
    await page.locator('.arco-select-option:visible', { hasText: warehouseName }).first().click()
    const row = dataRows(page).nth(0)
    await row.locator('.arco-select').click()
    await page.locator('.arco-select-option:visible', { hasText: productCode }).first().click()
    // 商品下拉显示「库存 1」
    await expect(row.locator('.arco-select')).toContainText('库存 1')
    const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
    await qtyInput.fill('5')
    await qtyInput.blur()
    // 行内预警
    await expect(row.locator('.qty-stock-error')).toHaveText('库存不足，转出仓可用库存 1')
    await expect(row.locator('.arco-input-number').nth(0)).toHaveClass(/qty-over-stock/)

    // 提交被拒：停留在开单页并提示库存不足
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('存在调拨数量大于转出仓可用库存的明细行，请修正后提交')).toBeVisible()
    await expect(page).toHaveURL(/\/transfers\/new$/)

    // 修正为 1 后可提交
    await qtyInput.fill('1')
    await qtyInput.blur()
    await expect(row.locator('.qty-stock-error')).toHaveCount(0)
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '调拨单已创建')
    await expect(page).toHaveURL(/\/transfers\/detail\//)

    // A 库存 0，B 库存 1
    await goInventory(page)
    await expectStockByWarehouse(page, productCode, DEFAULT_WAREHOUSE, 0)
    await expectStockByWarehouse(page, productCode, warehouseName, 1)
  })
})

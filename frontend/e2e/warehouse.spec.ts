import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil, clickUntilCount } from './helpers/action'
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
/** 迁移内置默认仓名称（specs/038-erp-multi-warehouse/design.md §2.5） */
const DEFAULT_WAREHOUSE = '默认仓'

/** 生成唯一仓库编码（2-20 位字母数字，唯一） */
function uniqueWarehouseCode(prefix: string): string {
  return `${prefix}${Date.now().toString(36).toUpperCase()}`
}

/** 生成唯一仓库名称 */
function uniqueWarehouseName(): string {
  return `测试仓${Date.now().toString(36)}`
}

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `多仓测试分类${Date.now().toString(36)}`
}

/** 生成唯一往来单位名称 */
function uniquePartnerName(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/**
 * 每个 page 只登录一次：本文件用例要反复跨页（仓库 / 商品 / 往来单位 / 单据），
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

/** 经侧边菜单（基础档案分组）进入仓库管理页 */
async function goWarehouses(page: Page): Promise<void> {
  await goMenu(page, '仓库管理', /\/warehouses$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await goMenu(page, '商品管理', /\/products$/)
}

/** 经侧边菜单进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await goMenu(page, '往来单位', /\/partners$/)
}

/** 经侧边菜单进入库存查询页 */
async function goInventory(page: Page): Promise<void> {
  await goMenu(page, '库存查询', /\/inventory$/)
}

/** 经侧边菜单进入库存流水页 */
async function goStockMovements(page: Page): Promise<void> {
  await goMenu(page, '库存流水', /\/stock-movements$/)
}

/** 经侧边菜单进入采购入库页 */
async function goPurchases(page: Page): Promise<void> {
  await goMenu(page, '采购入库', /\/purchases$/)
}

/** 经侧边菜单进入销售出库页 */
async function goSales(page: Page): Promise<void> {
  await goMenu(page, '销售出库', /\/sales$/)
}

/** 经侧边菜单进入库存盘点页 */
async function goStockTakes(page: Page): Promise<void> {
  await goMenu(page, '库存盘点', /\/stock-takes$/)
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
 * 按表单标签定位开单页 / 盘点页表头的仓库下拉（038）：
 * 页面上还有供应商 / 客户等下拉，用 label 锁定避免取错。
 */
function warehouseSelectByLabel(page: Page, label: string): Locator {
  return page.locator('.arco-form-item', { hasText: label }).locator('.arco-select').first()
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）：
 * Arco 选中后旧弹层 DOM 残留且选项被过滤隐藏，点选项不可靠，故用 Enter + 选中文本校验。
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

/** 点含指定提示文案的 popconfirm 浮层里的「确定」按钮 */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 库存页按编码搜索（点搜索按钮触发服务端查询） */
async function searchInventory(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(keyword)
  await searchAndWaitHit(page, keyword)
}

/** 库存页首行的当前库存数字 */
function stockOf(page: Page): Locator {
  return dataRows(page).first().locator('.stock-quantity')
}

/**
 * 仓储类页面按仓库筛选（选项为仅启用仓）：
 * 筛选行下拉顺序固定为「(商品) → 仓库」，故用 index 定位（下拉不可搜索、无 input，不能用 placeholder）。
 */
async function filterByWarehouse(page: Page, warehouseName: string, index: number): Promise<void> {
  await page.locator('.toolbar-filter .arco-select').nth(index).click()
  await page.locator('.arco-select-option:visible', { hasText: warehouseName }).first().click()
}

/** 新增仓库（编码 / 名称唯一；新增后为启用、非默认仓） */
async function createWarehouse(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增仓库')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('2-20 位字母、数字、下划线或连字符').fill(code)
  await drawer.getByPlaceholder('1-50 字符').fill(name)
  // 判据用回调（toHaveCount 返回 Promise，不是 Locator），点击被 loading 吞掉时重试
  await clickUntil(page, '提交', async () => {
    await expect(drawerTitle(page, '新增仓库')).toHaveCount(0)
  })
  await expectMessage(page, '仓库已创建')
}

/** 仓库管理页按编码搜索并返回该行 */
async function searchWarehouseRow(page: Page, code: string): Promise<Locator> {
  await page.getByPlaceholder('搜索仓库编码或名称').fill(code)
  await searchAndWaitHit(page, code)
  const row = dataRows(page).first()
  await expect(row).toContainText(code)
  return row
}

/** 新增往来单位（typeLabel = '客户' | '供应商'） */
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

/** 新增商品（分类就地新建；每个启用仓各建 0 库存行） */
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
  await clickUntil(page, '提交', async () => {
    await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
  })
  await expectMessage(page, '商品已创建')
}

/**
 * 开一张采购入库单（指定入库仓，单行明细），返回单号。
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
    await selectBySearch(warehouseSelectByLabel(page, '入库仓'), warehouseName)
  }

  const row = dataRows(page).first()
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
  const orderNo = (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
  return orderNo
}

/** 开一张销售出库单（指定出库仓，单行明细）并提交 */
async function createSalesShipment(
  page: Page,
  customerName: string,
  productCode: string,
  quantity: number,
  warehouseName?: string,
): Promise<void> {
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), customerName)

  if (warehouseName && warehouseName !== DEFAULT_WAREHOUSE) {
    await selectBySearch(warehouseSelectByLabel(page, '出库仓'), warehouseName)
  }

  const row = dataRows(page).first()
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill(String(quantity))
  await qty.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
}

test.describe('仓库管理（档案）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('新增 → 编辑 → 停用 / 启用 → 设为默认（默认标签随之切换）', async ({ page }) => {
    const code = uniqueWarehouseCode('WH')
    const name = uniqueWarehouseName()

    // 迁移内置默认仓必须存在且带「默认」标签
    await goWarehouses(page)
    await searchWarehouseRow(page, 'DEFAULT')
    await expect(dataRows(page).first().getByText('默认', { exact: true })).toBeVisible()

    // 新增：编码 / 名称唯一，新增后为启用且非默认
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await createWarehouse(page, code, name)
    const row = await searchWarehouseRow(page, code)
    await expect(row.getByText('启用', { exact: true })).toBeVisible()
    await expect(row.getByText('默认', { exact: true })).toHaveCount(0)
    await expect(row.getByRole('button', { name: '设为默认' })).toBeVisible()

    // 编辑（编码不可改，只读展示）
    await row.getByRole('button', { name: '编辑' }).click()
    await expect(drawerTitle(page, '编辑仓库')).toBeVisible()
    const drawer = page.locator('.arco-drawer')
    await expect(drawer.locator('input[disabled]').first()).toHaveValue(code)
    await drawer.getByPlaceholder('选填，≤ 100 字符').fill('上海市浦东新区测试路 1 号')
    await clickUntil(page, '提交', async () => {
      await expect(drawerTitle(page, '编辑仓库')).toHaveCount(0)
    })
    await expectMessage(page, '仓库已更新')

    // 设为默认：本仓带「默认」标签，且原默认仓不再带
    await page.getByRole('button', { name: '重置', exact: true }).click()
    const rowToDefault = await searchWarehouseRow(page, code)
    await rowToDefault.getByRole('button', { name: '设为默认' }).click()
    await expectMessage(page, '已设为默认仓')
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await expect((await searchWarehouseRow(page, code)).getByText('默认', { exact: true })).toBeVisible()
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await expect(
      (await searchWarehouseRow(page, 'DEFAULT')).getByText('默认', { exact: true }),
    ).toHaveCount(0)

    // 默认仓不可停用（后端 40124，页面弹出错误提示）
    await page.getByRole('button', { name: '重置', exact: true }).click()
    const defaultRow = await searchWarehouseRow(page, code)
    await defaultRow.getByRole('button', { name: '停用' }).click()
    await confirmPopconfirm(page, '确认停用该仓库？')
    await expectMessage(page, '默认仓不可停用')

    // 恢复默认仓到内置默认仓，并停用本用例新建的仓库（避免影响其他用例的商品库存行）
    await page.getByRole('button', { name: '重置', exact: true }).click()
    const builtInRow = await searchWarehouseRow(page, 'DEFAULT')
    await builtInRow.getByRole('button', { name: '设为默认' }).click()
    await expectMessage(page, '已设为默认仓')
    await page.getByRole('button', { name: '重置', exact: true }).click()
    const toDisable = await searchWarehouseRow(page, code)
    await toDisable.getByRole('button', { name: '停用' }).click()
    await confirmPopconfirm(page, '确认停用该仓库？')
    await expectMessage(page, '已停用')
  })

  test('停用仓不可被选为新单据的仓库（40123）', async ({ page }) => {
    const code = uniqueWarehouseCode('WH')
    const name = uniqueWarehouseName()

    await goWarehouses(page)
    await createWarehouse(page, code, name)
    const row = await searchWarehouseRow(page, code)
    await row.getByRole('button', { name: '停用' }).click()
    await confirmPopconfirm(page, '确认停用该仓库？')
    await expectMessage(page, '已停用')

    // 开单页仓库下拉只含启用仓：停用仓不出现在选项中
    await goPurchases(page)
    await page.getByRole('button', { name: '开采购单' }).click()
    await expect(page).toHaveURL(/\/purchases\/new$/)
    await warehouseSelectByLabel(page, '入库仓').click()
    await expect(page.locator('.arco-select-option:visible', { hasText: name })).toHaveCount(0)
    await page.keyboard.press('Escape')
  })
})

test.describe('多仓库存（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('两仓各自入库 → 库存按仓显示不同数量，「全部仓库」显示两行', async ({ page }) => {
    const warehouseCode = uniqueWarehouseCode('WH')
    const warehouseName = uniqueWarehouseName()
    const supplier = uniquePartnerName('s')
    const code = uniqueProductCode('wh_in')
    const productName = `多仓商品${Date.now() % 100000}`

    await goWarehouses(page)
    await createWarehouse(page, warehouseCode, warehouseName)
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, productName)

    // 默认仓入库 2、指定仓入库 5
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, code, 2)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, code, 5, warehouseName)

    // 默认仓口径 2
    await goInventory(page)
    await searchInventory(page, code)
    await filterByWarehouse(page, DEFAULT_WAREHOUSE, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(dataRows(page).first()).toContainText(DEFAULT_WAREHOUSE)
    await expect(stockOf(page)).toHaveText('2')

    // 指定仓口径 5
    await searchInventory(page, code)
    await filterByWarehouse(page, warehouseName, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(dataRows(page).first()).toContainText(warehouseName)
    await expect(stockOf(page)).toHaveText('5')

    // 全部仓库：同一商品按仓各一行（行数按当前启用仓数，不断言绝对值）
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await searchInventory(page, code)
    await expect(dataRows(page).filter({ hasText: DEFAULT_WAREHOUSE })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: warehouseName })).toHaveCount(1)
  })

  test('按仓销售出库 → 只扣所选仓；该仓不足时整单拒绝且提示含仓名', async ({ page }) => {
    const warehouseCode = uniqueWarehouseCode('WH')
    const warehouseName = uniqueWarehouseName()
    const supplier = uniquePartnerName('s')
    const customer = uniquePartnerName('c')
    const code = uniqueProductCode('wh_out')
    const productName = `多仓出库商品${Date.now() % 100000}`

    await goWarehouses(page)
    await createWarehouse(page, warehouseCode, warehouseName)
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await createPartner(page, customer, '客户')
    await goProducts(page)
    await createProduct(page, code, productName)

    // 两仓各入库 5
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, code, 5)
    await goPurchases(page)
    await createPurchaseReceipt(page, supplier, code, 5, warehouseName)

    // 从指定仓出库 2：该仓 3，默认仓仍 5
    await goSales(page)
    await createSalesShipment(page, customer, code, 2, warehouseName)
    await expectMessage(page, '销售单已创建')
    await goInventory(page)
    await searchInventory(page, code)
    await filterByWarehouse(page, warehouseName, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(stockOf(page)).toHaveText('3')
    await searchInventory(page, code)
    await filterByWarehouse(page, DEFAULT_WAREHOUSE, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(stockOf(page)).toHaveText('5')

    // 从默认仓出库 10 > 5：整单拒绝，提示含仓名
    await goSales(page)
    await page.getByRole('button', { name: '开销售单' }).click()
    await expect(page).toHaveURL(/\/sales\/new$/)
    await selectBySearch(page.locator('.arco-select').first(), customer)
    const row = dataRows(page).first()
    await selectBySearch(row.locator('.arco-select'), code)
    const qty = row.locator('.arco-input-number').nth(0).locator('input')
    await qty.fill('10')
    await qty.blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, `库存不足：${DEFAULT_WAREHOUSE}`)
    await expect(page).toHaveURL(/\/sales\/new$/)

    // 两仓库存均未变动（整单拒绝）
    await goInventory(page)
    await searchInventory(page, code)
    await filterByWarehouse(page, DEFAULT_WAREHOUSE, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(stockOf(page)).toHaveText('5')
  })

  test('流水页按仓筛选并展示仓库列；盘点按仓带出账面与差异；安全库存 Modal 保存生效', async ({ page }) => {
    const warehouseCode = uniqueWarehouseCode('WH')
    const warehouseName = uniqueWarehouseName()
    const supplier = uniquePartnerName('s')
    const code = uniqueProductCode('wh_mv')
    const productName = `多仓流水商品${Date.now() % 100000}`

    await goWarehouses(page)
    await createWarehouse(page, warehouseCode, warehouseName)
    await goPartners(page)
    await createPartner(page, supplier, '供应商')
    await goProducts(page)
    await createProduct(page, code, productName)

    // 指定仓入库 6（默认仓不入库，便于断言「按仓筛选」确实生效）
    await goPurchases(page)
    const receiptNo = await createPurchaseReceipt(page, supplier, code, 6, warehouseName)

    // 流水页：仓库列存在；按指定仓筛选命中该单，按默认仓筛选不命中（筛选行下拉顺序：商品 → 仓库 → 类型）
    await goStockMovements(page)
    await expect(page.getByRole('columnheader', { name: '仓库' })).toBeVisible()
    await filterByWarehouse(page, warehouseName, 1)
    await page.getByPlaceholder('搜索来源单号').fill(receiptNo)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(dataRows(page).first()).toContainText(warehouseName)

    await page.getByRole('button', { name: '重置', exact: true }).click()
    await filterByWarehouse(page, DEFAULT_WAREHOUSE, 1)
    await page.getByPlaceholder('搜索来源单号').fill(receiptNo)
    await clickUntilCount(page, '搜索', dataRows(page), 0)

    // 盘点：选指定仓，账面取该仓（6），实盘 9 → 差异 +3
    await goStockTakes(page)
    await page.getByRole('button', { name: '新建盘点' }).click()
    await expect(page).toHaveURL(/\/stock-takes\/new$/)
    await selectBySearch(warehouseSelectByLabel(page, '盘点仓'), warehouseName)
    const takeRow = dataRows(page).first()
    await selectBySearch(takeRow.locator('.arco-select'), code)
    await expect(takeRow.locator('.num').first()).toHaveText('6')
    const actual = takeRow.locator('.arco-input-number').nth(0).locator('input')
    await actual.fill('9')
    await actual.blur()
    await expect(takeRow.locator('.diff-pos')).toHaveText('3')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '盘点单已生效')
    await expect(page).toHaveURL(/\/stock-takes\/detail\//)
    await expect(page.getByText(warehouseName).first()).toBeVisible()

    // 盘点后该仓库存为 9
    await goInventory(page)
    await searchInventory(page, code)
    await filterByWarehouse(page, warehouseName, 1)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(stockOf(page)).toHaveText('9')

    // 仓级安全库存：阈值 10 → 触发低库存标记
    const inventoryRow = dataRows(page).first()
    await inventoryRow.getByRole('button', { name: '安全库存' }).click()
    const modal = page.locator('.arco-modal')
    await expect(modal.getByText('安全库存', { exact: true }).first()).toBeVisible()
    await modal.locator('.arco-input-number input').fill('10')
    // Arco Modal 的确定按钮文案含空格，用正则；点击可能被 loading 吞掉，故重试直到提示出现
    await expect(async () => {
      await modal.getByRole('button', { name: /确\s*定/ }).click()
      await expectMessage(page, '安全库存已更新')
    }).toPass({ timeout: 15000 })
    await expect(dataRows(page).first().getByText('低于安全库存')).toBeVisible()
  })
})

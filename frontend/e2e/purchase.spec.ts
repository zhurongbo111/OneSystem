import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickMenuItem } from './helpers/menu'

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

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `采购测试分类${Date.now().toString(36)}`
}

/** 生成唯一供应商名称 */
function uniquePartnerName(): string {
  return `p${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单（进销存分组）进入采购入库页 */
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

/** 经侧边菜单进入库存查询页 */
async function goInventory(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存查询')
  await expect(page).toHaveURL(/\/inventory$/)
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
    selectLocator.page().locator('.arco-select-option', { hasText: keyword }).first(),
  ).toBeVisible()
  await input.press('Enter')
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 抽屉标题（exact 精确匹配） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
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

/** 库存页按编码搜索（点搜索按钮触发服务端查询） */
async function searchInventory(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(keyword)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
}

/** 库存页指定编码行的当前库存数字 */
function stockOf(page: Page): ReturnType<typeof page.locator> {
  return dataRows(page).first().locator('.stock-quantity')
}

/** 新增供应商（名称唯一，类型=供应商） */
async function createSupplier(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText('供应商', { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建')).toBeVisible()
  await expect(drawerTitle(page, '新增往来单位')).toHaveCount(0)
}

/** 新增商品（分类就地新建），返回编码 */
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
  await catInput.fill(uniqueCategoryName())
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  // Arco message DOM 残留（同 popconfirm），本用例创建 2 个商品会累积 2 条同文案提示，取最新一条
  await expect(page.getByText('分类已创建').last()).toBeVisible()
  // 金额 / 安全库存均为 a-input-number：第 0 / 1 个是采购 / 销售价
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  // 同「分类已创建」：Arco message DOM 残留，取最新一条
  await expect(page.getByText('商品已创建').last()).toBeVisible()
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/**
 * 在开单页填一张 2 行明细的采购单并保存，返回单号：
 * 行1 商品A 数量 2、行2 商品B 数量 1 且改单价为 15（默认采购价 10 → 15）。
 * 后端重算：总额 = 2×10 + 1×15 = 35。
 */
async function createPurchaseReceipt(
  page: Page,
  supplierName: string,
  productACode: string,
  productBCode: string,
): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)

  // 供应商（仅供应商 / 两者类型）
  const supplierSelect = page.locator('.arco-select').first()
  await selectBySearch(supplierSelect, supplierName)

  // 明细行 1：商品 A，数量 2
  const rows = dataRows(page)
  await expect(rows).toHaveCount(1)
  const row0 = rows.nth(0)
  const productSelect0 = row0.locator('.arco-select')
  await selectBySearch(productSelect0, productACode)
  // 每行 2 个 input-number：第 0 个数量、第 1 个单价
  const qty0 = row0.locator('.arco-input-number').nth(0).locator('input')
  await qty0.fill('2')
  await qty0.blur()

  // 添加行 2：商品 B，数量 1，改单价 10 → 15
  await page.getByRole('button', { name: '添加行' }).click()
  await expect(rows).toHaveCount(2)
  const row1 = rows.nth(1)
  const productSelect1 = row1.locator('.arco-select')
  await selectBySearch(productSelect1, productBCode)
  const qty1 = row1.locator('.arco-input-number').nth(0).locator('input')
  await qty1.fill('1')
  await qty1.blur()
  const priceInput = row1.locator('.arco-input-number').nth(1).locator('input')
  await priceInput.fill('15')
  await priceInput.blur()

  // 提交
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expect(page.getByText('采购单已创建')).toBeVisible()
  await expect(page).toHaveURL(/\/purchases\/detail\//)

  // 详情页断言：总额 35.00（后端重算 2×10 + 1×15）
  await expect(page.getByText('¥ 35.00', { exact: true })).toBeVisible()
  const orderNo = (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
  return orderNo
}

test.describe('采购入库（列表）', () => {
  test('列设置可隐藏 / 恢复列，序号与操作列固定显示（specs/011-action-column §5）', async ({ page }) => {
    await goPurchases(page)
    await expect(page.getByRole('columnheader', { name: '创建时间' })).toBeVisible()

    await page.getByRole('button', { name: '列设置' }).click()
    // 序号与操作列固定显示，不参与列设置
    await expect(page.locator('.col-settings .arco-checkbox', { hasText: '操作' })).toHaveCount(0)
    await page.locator('.col-settings .arco-checkbox', { hasText: '创建时间' }).click()
    await page.getByRole('heading', { name: '采购入库' }).click()
    await expect(page.getByRole('columnheader', { name: '创建时间' })).toHaveCount(0)

    // 重新勾选后恢复显示
    await page.getByRole('button', { name: '列设置' }).click()
    await page.locator('.col-settings .arco-checkbox', { hasText: '创建时间' }).click()
    await page.getByRole('heading', { name: '采购入库' }).click()
    await expect(page.getByRole('columnheader', { name: '创建时间' })).toBeVisible()
  })
})

test.describe('采购入库（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('开单 → 库存增加 → 去收付款跳转 → 作废回冲（全链路）', async ({ page }) => {
    const codeA = uniqueProductCode('po_a')
    const codeB = uniqueProductCode('po_b')
    const supplier = uniquePartnerName()

    // 准备供应商 + 2 个商品
    await goPartners(page)
    await createSupplier(page, supplier)
    await goProducts(page)
    await createProduct(page, codeA, `采购商品A${Date.now() % 100000}`)
    await createProduct(page, codeB, `采购商品B${Date.now() % 100000}`)

    // 记录开单前库存（均为 0）
    await goInventory(page)
    await searchInventory(page, codeA)
    await expect(stockOf(page)).toHaveText('0')
    await searchInventory(page, codeB)
    await expect(stockOf(page)).toHaveText('0')

    // 开单：A×2（单价 10）+ B×1（改单价 15）→ 总额 35.00，跳详情
    await goPurchases(page)
    const orderNo = await createPurchaseReceipt(page, supplier, codeA, codeB)

    // 库存增加：A → 2，B → 1
    await goInventory(page)
    await searchInventory(page, codeA)
    await expect(stockOf(page)).toHaveText('2')
    await searchInventory(page, codeB)
    await expect(stockOf(page)).toHaveText('1')

    // 回采购列表，按单号找到该行
    await goPurchases(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(orderNo)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    const row = dataRows(page).first()
    await expect(row).toContainText(orderNo)
    // 初始未结算
    await expect(row.getByText('未结算', { exact: true })).toBeVisible()

    // 操作列按钮带图标：详情 / 收付款 / 作废（手工结算切换已移除，结算由收付款单核销驱动）
    await expect(row.getByRole('button', { name: '详情' }).locator('svg')).toHaveCount(1)
    await expect(row.getByRole('button', { name: '作废' }).locator('svg')).toHaveCount(1)
    await expect(row.getByRole('button', { name: '收付款' }).locator('svg')).toHaveCount(1)
    // 按钮顺序：详情 → 收付款 → 作废（specs/011-action-column §0 主操作 → 中性 → 危险）
    const rowActions = row.locator('td.action-cell button')
    await expect(rowActions).toHaveCount(3)
    await expect(rowActions.nth(0)).toHaveText(/详情/)
    await expect(rowActions.nth(1)).toHaveText(/收付款/)
    await expect(rowActions.nth(2)).toHaveText(/作废/)
    // 配色：作废 = danger（红），与「已作废」红标签呼应；收付款为中性
    await expect(row.getByRole('button', { name: '作废' })).toHaveClass(/arco-btn-status-danger/)
    await expect(row.getByRole('button', { name: '详情' })).not.toHaveClass(/arco-btn-status-danger/)

    // 去收付款：跳新建收付款页并按单据类型预置付款方向（核销链路见 settlement.spec.ts）
    await row.getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(page.getByRole('radio', { name: '付款' })).toBeChecked()

    // 作废：库存回冲
    await goPurchases(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(orderNo)
    await page.getByRole('button', { name: '搜索', exact: true }).click()
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await expect(page.getByText('确认作废该采购单？')).toBeVisible()
    await confirmPopconfirm(page, '确认作废该采购单？')
    await expect(page.getByText('已作废，库存已回冲')).toBeVisible()

    // 单据状态「已作废」且操作（作废 / 收付款）消失
    const rowVoided = dataRows(page).first()
    await expect(rowVoided.getByText('已作废', { exact: true })).toBeVisible()
    await expect(rowVoided.getByRole('button', { name: '作废' })).toHaveCount(0)
    await expect(rowVoided.getByRole('button', { name: '收付款' })).toHaveCount(0)

    // 库存回冲：A → 0，B → 0
    await goInventory(page)
    await searchInventory(page, codeA)
    await expect(stockOf(page)).toHaveText('0')
    await searchInventory(page, codeB)
    await expect(stockOf(page)).toHaveText('0')
  })
})

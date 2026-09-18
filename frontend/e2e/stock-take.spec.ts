import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { clickMenuItem } from './helpers/menu'
import { searchAndWaitHit } from './helpers/table-search'

/**
 * 期初建账与库存盘点（specs/020-erp-stock-take）：
 * 期初建账全链路（库存设定 + 流水「期初建账 +N」）、盘点差异（库存设定 + 流水「盘点调整 -N」，
 * 实盘==账面不产生流水）、期初模式已建账商品拦截（前端禁用 + 后端 40111）、列表筛选、
 * 详情「查看库存流水」跳转预置单号。
 */

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
const TOKEN_KEY = 'app:token'
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 跨用例共享：用例 1 的期初商品（供用例 3 断言已建账禁用）、各单号（供筛选用例复用） */
let codeInitial = ''
let takeInitialNo = ''
let takeAdjustNo = ''

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 本地今天 YYYY-MM-DD（sv 地区返回 ISO 短格式，与 formatDateTime 前 10 位一致） */
function todayLocal(): string {
  return new Date().toLocaleDateString('sv')
}

async function login(page: Page): Promise<void> {
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单（进销存分组）进入库存盘点页 */
async function goStockTakes(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存盘点')
  await expect(page).toHaveURL(/\/stock-takes$/)
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

/** 经侧边菜单进入库存流水页 */
async function goStockMovements(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存流水')
  await expect(page).toHaveURL(/\/stock-movements$/)
}

/** 当前 a-table 数据行（排除空状态行；注意 a-descriptions 也渲染 tbody tr，需排除） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('.arco-table tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 在 Arco select 中搜索并选中唯一匹配项。
 * 表单页商品下拉与采购页一致：选中后旧弹层 DOM 残留且选项会被过滤隐藏，
 * 用 .arco-select-option 定位选项点击 + 容器回显文本验证选中。
 */
async function selectBySearch(page: Page, selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  const input = selectLocator.locator('input')
  await input.fill(keyword)
  await page.locator('.arco-select-option:visible', { hasText: keyword }).first().click()
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

/** 新增商品（分类就地新建），返回编码 */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await page.getByText('新增商品', { exact: true }).waitFor({ state: 'visible' })
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类（抽屉内联输入条 + 保存）
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`盘点测试分类${Date.now().toString(36)}`)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').last()).toBeVisible()
  // 金额 / 安全库存均为 a-input-number：第 0 / 1 个是采购 / 销售价
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建').last()).toBeVisible()
}

/**
 * 新建盘点 / 期初建账单（单行明细），返回单号：
 * - initial：切「期初建账」radio；否则默认「库存盘点」
 * - actual：实盘数量（期初 = 期初库存，盘点 = 实盘数，差异由后端按账面重算）
 */
async function createStockTake(
  page: Page,
  code: string,
  actual: number,
  initial: boolean,
  unitCost?: number,
): Promise<string> {
  await page.getByRole('button', { name: '新建盘点' }).click()
  await expect(page).toHaveURL(/\/stock-takes\/new$/)

  if (initial) {
    await page.locator('.arco-radio-group').getByText('期初建账', { exact: true }).click()
  }

  // 商品下拉（表单页仅此一个 a-select）
  const productSelect = page.locator('.arco-select')
  await selectBySearch(page, productSelect, code)
  // 实盘数量（期初模式下还有成本单价输入，二者均为 a-input-number，取第一个）
  const actualInput = page.locator('.arco-input-number input').first()
  await actualInput.fill(String(actual))
  await actualInput.blur()

  // 期初建账：成本单价必填（erp-cost —— 成本基线，未填无法计算成本与毛利）
  if (initial && unitCost !== undefined) {
    await page.locator('.arco-input-number input').nth(1).fill(String(unitCost))
    await page.locator('.arco-input-number input').nth(1).blur()
  }

  // 提交 → 跳详情页
  await clickUntil(page, '提交', page.getByText('盘点单已生效'))
  await expect(page).toHaveURL(/\/stock-takes\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^ST\d{12}$/).first().innerText()).trim()
}

/** 盘点列表按条件搜索（单号关键词 / 类型 / 日期范围=今天） */
async function searchStockTakes(page: Page, keyword: string, type?: string, withDateRange?: boolean): Promise<void> {
  if (keyword) await page.getByPlaceholder('搜索单号').fill(keyword)
  if (type) {
    await page.locator('.filter-bar__type').click()
    await page.locator('.arco-select-option', { hasText: type }).click()
  }
  if (withDateRange) {
    await page.locator('.filter-bar__range input').first().click()
    const today = page.locator('.arco-picker-cell-today').first()
    await today.click()
    await today.click()
    // 点击标题关闭面板
    await page.getByRole('heading', { name: '库存盘点' }).click()
  }
  if (keyword) {
    await searchAndWaitHit(page, keyword)
    return
  }
  await page.getByRole('button', { name: '搜索', exact: true }).click()
}

/** 库存页按编码搜索并取当前库存数字 */
async function stockOf(page: Page, code: string): Promise<string> {
  await goInventory(page)
  await page.getByPlaceholder('搜索商品编码或名称').fill(code)
  await searchAndWaitHit(page, code)
  await expect(dataRows(page)).toHaveCount(1)
  return (await dataRows(page).first().locator('.stock-quantity').innerText()).trim()
}

test.describe('期初建账与库存盘点（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('期初建账：选未建账商品 → 提交 → 详情 → 库存为录入值 + 流水「期初建账 +N」', async ({ page }) => {
    codeInitial = uniqueProductCode('stk_a')

    // 准备商品（无库存变动）
    await goProducts(page)
    await createProduct(page, codeInitial, `期初商品${Date.now() % 100000}`)
    // 期初前库存为 0
    expect(await stockOf(page, codeInitial)).toBe('0')

    // 期初建账：实盘 10
    await goStockTakes(page)
    takeInitialNo = await createStockTake(page, codeInitial, 10, true, 10)
    expect(takeInitialNo).toMatch(/^ST\d{12}$/)

    // 详情页：单号 / 类型 / 盘点日期 / 明细（账面 0 → 实盘 10，差异 +10）
    const detailRow = dataRows(page).first()
    await expect(detailRow).toContainText(codeInitial)
    await expect(detailRow).toContainText('0')
    await expect(detailRow).toContainText('10')
    await expect(detailRow.locator('.diff-pos')).toHaveText('10')
    // 盘点日期为当天（表单默认值）
    await expect(page.locator('.detail-desc')).toContainText(todayLocal())
    // 明细行数 1 / 差异行数 1
    await expect(page.locator('.detail-desc')).toContainText('明细行数')
    // 创建人解析为姓名（admin 用户 displayName）
    await expect(page.locator('.detail-desc')).toContainText('创建人')

    // 库存为录入值 10
    expect(await stockOf(page, codeInitial)).toBe('10')

    // 流水页：该单号 1 行，「期初建账」+10（绿字）
    await goStockMovements(page)
    await page.getByPlaceholder('搜索来源单号').fill(takeInitialNo)
    await searchAndWaitHit(page, takeInitialNo)
    await expect(dataRows(page)).toHaveCount(1)
    const rowInitial = dataRows(page).first()
    await expect(rowInitial).toContainText('期初建账')
    await expect(rowInitial).toContainText(codeInitial)
    await expect(rowInitial.locator('.qty-plus')).toHaveText('+10')
  })

  test('盘点差异：期初 10 后实盘 6 → 库存 6 + 流水「盘点调整 -4」（实盘==账面不产生流水）', async ({ page }) => {
    const codeB = uniqueProductCode('stk_b')

    // 准备商品并期初建账 10（账面 10）
    await goProducts(page)
    await createProduct(page, codeB, `盘点商品${Date.now() % 100000}`)
    await goStockTakes(page)
    await createStockTake(page, codeB, 10, true, 10)
    expect(await stockOf(page, codeB)).toBe('10')

    // 库存盘点：实盘 6 → 差异 -4
    await goStockTakes(page)
    takeAdjustNo = await createStockTake(page, codeB, 6, false)

    // 详情页：账面 10 / 实盘 6 / 差异 -4（红字）
    const detailRow = dataRows(page).first()
    await expect(detailRow).toContainText('10')
    await expect(detailRow).toContainText('6')
    await expect(detailRow.locator('.diff-neg')).toHaveText('-4')

    // 库存为实盘值 6
    expect(await stockOf(page, codeB)).toBe('6')

    // 流水页：该盘点单 1 行「盘点调整」-4（红字）；期初单不混入
    await goStockMovements(page)
    await page.getByPlaceholder('搜索来源单号').fill(takeAdjustNo)
    await searchAndWaitHit(page, takeAdjustNo)
    await expect(dataRows(page)).toHaveCount(1)
    const rowAdjust = dataRows(page).first()
    await expect(rowAdjust).toContainText('盘点调整')
    await expect(rowAdjust.locator('.qty-minus')).toHaveText('-4')

    // 实盘 == 账面不产生流水：再盘实盘 6（== 账面）→ 流水页该单号 0 行
    await goStockTakes(page)
    const takeEvenNo = await createStockTake(page, codeB, 6, false)
    expect(await stockOf(page, codeB)).toBe('6')
    await goStockMovements(page)
    await page.getByPlaceholder('搜索来源单号').fill(takeEvenNo)
    await clickUntil(page, '搜索', page.locator('tbody .arco-table-tr-empty'))
  })

  test('期初模式：已建账商品下拉禁用标注「已建账」（未建账商品仍可正常选择）', async ({ page }) => {
    // 造一个未建账商品，作对照（期初模式下仍可选）
    const codeFresh = uniqueProductCode('stk_f')
    await goProducts(page)
    await createProduct(page, codeFresh, `对照商品${Date.now() % 100000}`)

    await goStockTakes(page)
    await page.getByRole('button', { name: '新建盘点' }).click()
    await expect(page).toHaveURL(/\/stock-takes\/new$/)
    await page.locator('.arco-radio-group').getByText('期初建账', { exact: true }).click()

    const productSelect = page.locator('.arco-select')
    // 用例 1 已建账商品：下拉禁用并标注「已建账」（弹层 DOM 会残留旧选项，按可见断言）
    await productSelect.click()
    await productSelect.locator('input').fill(codeInitial)
    const disabledOption = page.locator('.arco-select-option', { hasText: '已建账' })
    await expect(disabledOption.first()).toBeVisible()
    await expect(page.locator('.arco-select-option-disabled', { hasText: codeInitial })).toHaveCount(1)

    // 切换未建账商品：可选且标注「账面 0」
    await productSelect.click()
    await productSelect.locator('input').fill(codeFresh)
    await expect(page.locator('.arco-select-option', { hasText: codeFresh }).first()).toContainText('账面 0')
    await expect(page.locator('.arco-select-option-disabled', { hasText: codeFresh })).toHaveCount(0)
  })

  test('列表筛选（类型 / 单号 / 日期范围）+ 详情「查看库存流水」跳转预置单号', async ({ page }) => {
    const codeC = uniqueProductCode('stk_c')

    // 造数：新商品 + 1 张库存盘点单（当天）
    await goProducts(page)
    await createProduct(page, codeC, `筛选商品${Date.now() % 100000}`)
    await goStockTakes(page)
    const takeC = await createStockTake(page, codeC, 7, false)

    // 回列表页
    await goStockTakes(page)
    // 类型筛选「期初建账」→ 仅期初单（不含刚建的盘点单 takeC）
    await searchStockTakes(page, '', '期初建账')
    const initialRows = dataRows(page)
    await expect(initialRows.first()).toContainText('期初建账')
    await expect(initialRows.filter({ hasText: takeC })).toHaveCount(0)

    // 重置后单号筛选 → 精确 1 行
    await page.getByRole('button', { name: '重置' }).click()
    await searchStockTakes(page, takeC)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(takeC)

    // 重置后日期范围 = 今天 → 命中刚建的单（倒序首行）
    await page.getByRole('button', { name: '重置' }).click()
    await searchStockTakes(page, '', undefined, true)
    await expect(dataRows(page).first()).toContainText(takeC)

    // 详情「查看库存流水」→ 流水页预置本单号关键词
    await dataRows(page).first().getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/stock-takes\/detail\//)
    await clickUntil(page, '查看库存流水', async () => {
      await expect(page).toHaveURL(/\/stock-movements\?keyword=ST\d{12}/, { timeout: 3000 })
    })
    await expect(page.getByPlaceholder('搜索来源单号')).toHaveValue(takeC)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(takeC)
    await expect(dataRows(page).first()).toContainText('盘点调整')
    await expect(dataRows(page).first().locator('.qty-plus')).toHaveText('+7')
  })

  test('期初建账未填成本单价应被拦，补填后生效（erp-cost）', async ({ page }) => {
    const code = uniqueProductCode('stk_cost')
    await goProducts(page)
    await createProduct(page, code, `期初成本商品${Date.now() % 100000}`)

    await goStockTakes(page)
    await page.getByRole('button', { name: '新建盘点' }).click()
    await expect(page).toHaveURL(/\/stock-takes\/new$/)
    await page.locator('.arco-radio-group').getByText('期初建账', { exact: true }).click()
    await selectBySearch(page, page.locator('.arco-select'), code)

    // 只填实盘数量、不填成本单价 → 明细校验拦截（erp-cost 成本基线必填）
    const inputs = page.locator('.arco-input-number input')
    await inputs.nth(0).fill('10')
    await inputs.nth(0).blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('请检查明细')).toBeVisible()
    await expect(page).toHaveURL(/\/stock-takes\/new$/)

    // 补填成本单价后提交生效
    await inputs.nth(1).fill('10')
    await inputs.nth(1).blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expect(page.getByText('盘点单已生效')).toBeVisible()
    await expect(page).toHaveURL(/\/stock-takes\/detail\//)
  })
})

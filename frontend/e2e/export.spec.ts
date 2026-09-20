import { statSync } from 'node:fs'

import { expect, test, type Download, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 生成唯一商品编码 */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `导出测试分类${Date.now().toString(36)}`
}

/** 生成唯一供应商名称 */
function uniquePartnerName(): string {
  return `s${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 登录并进入指定菜单页 */
async function goPage(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
  await clickMenuItem(page, menuName)
  await expect(page).toHaveURL(urlPattern)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/**
 * 点「导出」并等待浏览器下载事件。
 * Arco Button 在 loading / 重渲染期间会吞掉 click（见前端规则 §10.1），故重试点击直到产生下载。
 */
async function clickExportAndWaitDownload(page: Page): Promise<Download> {
  let download: Download | null = null
  await expect(async () => {
    const wait = page.waitForEvent('download', { timeout: 5000 })
    await page.getByRole('button', { name: '导出', exact: true }).click()
    download = await wait
  }).toPass({ timeout: 30_000 })
  return download!
}

/** 断言导出文件名格式（`<域>_<yyyyMMddHHmm>.xlsx`）且落盘内容非空 */
async function expectDownload(download: Download, domain: string): Promise<void> {
  expect(download.suggestedFilename()).toMatch(new RegExp(`^${domain}_\\d{12}\\.xlsx$`))
  const filePath = await download.path()
  expect(filePath).toBeTruthy()
  expect(statSync(filePath!).size).toBeGreaterThan(0)
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 * 与 purchase.spec.ts 同因：选中后旧弹层 DOM 残留，点选项不可靠。
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

/** 新增供应商（名称唯一，类型=供应商） */
async function createSupplier(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  const drawer = page.locator('.arco-drawer')
  await expect(drawer.getByText('新增往来单位', { exact: true })).toBeVisible()
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText('供应商', { exact: true }).click()
  await drawer.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
}

/** 新增商品（分类就地新建） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder('输入新分类名称（1-20 字符）')
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueCategoryName())
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
}

/**
 * 在开单页填一张 1 行明细的采购单（商品 × 2，单价默认 10 → 总额 20.00）并保存，返回单号。
 * 明细行数保持 1，便于打印视图断言。
 */
async function createPurchaseReceipt(page: Page, supplierName: string, productCode: string): Promise<string> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)

  await selectBySearch(page.locator('.arco-select').first(), supplierName)

  const row = dataRows(page).first()
  await selectBySearch(row.locator('.arco-select'), productCode)
  const qtyInput = row.locator('.arco-input-number').nth(0).locator('input')
  await qtyInput.fill('2')
  await qtyInput.blur()

  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)

  return (await page.locator('.detail-desc').getByText(/^GR\d{12}$/).first().innerText()).trim()
}

test.describe('导出 Excel 与单据打印（erp-export）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('商品列表：按筛选导出，文件名与内容符合约定', async ({ page }) => {
    const code = uniqueProductCode('ex_p')

    // 准备一条可命中的数据，保证导出的是「筛选后仍有数据」的结果
    await goPage(page, '商品管理', /\/products$/)
    await createProduct(page, code, `导出商品${Date.now() % 100000}`)
    await page.getByPlaceholder('搜索商品编码或名称').fill(code)
    await searchAndWaitHit(page, code)

    const download = await clickExportAndWaitDownload(page)
    await expectDownload(download, '商品')
  })

  test('采购入库：列表导出（单据 + 明细）、列表与详情入口进入打印视图', async ({ page }) => {
    const code = uniqueProductCode('ex_g')
    const supplier = uniquePartnerName()

    // 准备供应商 + 商品 + 一张采购入库单（打印视图需要真实单据）
    await goPage(page, '往来单位', /\/partners$/)
    await createSupplier(page, supplier)
    await goPage(page, '商品管理', /\/products$/)
    await createProduct(page, code, `打印商品${Date.now() % 100000}`)
    await goPage(page, '采购入库', /\/purchases$/)
    const orderNo = await createPurchaseReceipt(page, supplier, code)

    // 列表导出：当前筛选（按单号）全量，含「单据 + 明细」两个工作表（内容由后端单测覆盖）
    await goPage(page, '采购入库', /\/purchases$/)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(orderNo)
    await searchAndWaitHit(page, orderNo)
    await expectDownload(await clickExportAndWaitDownload(page), '采购入库')

    // 列表操作列入口：「更多」→ 打印
    const row = dataRows(page).first()
    await row.getByRole('button', { name: '更多操作' }).click()
    await page.locator('.arco-dropdown-option', { hasText: '打印' }).click()
    await expect(page).toHaveURL(/\/print\/purchases\//)

    // 打印视图：脱离布局（无侧边栏）、含单号 / 往来单位 / 明细 / 合计 / 页脚
    await expect(page.locator('.arco-layout-sider')).toHaveCount(0)
    await expect(page.getByRole('heading', { name: '采购入库单' })).toBeVisible()
    await expect(page.locator('.print-page').getByText(orderNo)).toBeVisible()
    await expect(page.locator('.print-page').getByText(supplier)).toBeVisible()
    await expect(page.locator('.print-table tbody tr')).toHaveCount(1)
    await expect(page.locator('.print-summary').getByText('数量合计：2')).toBeVisible()
    await expect(page.locator('.print-summary').getByText('金额合计：¥ 20.00')).toBeVisible()

    // 「打印」为同步动作：桩掉 window.print 后应被调用一次，且打印时间刷新为当刻
    await page.evaluate(() => {
      const w = window as unknown as { __printCalled?: number }
      w.__printCalled = 0
      window.print = () => {
        w.__printCalled = 1
      }
    })
    await page.getByRole('button', { name: '打印', exact: true }).click()
    await expect
      .poll(async () => page.evaluate(() => (window as unknown as { __printCalled?: number }).__printCalled ?? 0))
      .toBe(1)
    await expect(page.locator('.print-footer').getByText(/打印时间：\d{4}-\d{2}-\d{2}/)).toBeVisible()

    // 「返回」回到列表
    await page.getByRole('button', { name: '返回' }).click()
    await expect(page).toHaveURL(/\/purchases$/)

    // 详情页入口：头部操作区「打印」同样进入打印视图
    await page.getByPlaceholder('搜索单号 / 供应商').fill(orderNo)
    await searchAndWaitHit(page, orderNo)
    await dataRows(page).first().getByRole('button', { name: '详情' }).click()
    await expect(page).toHaveURL(/\/purchases\/detail\//)
    await page.getByRole('button', { name: '打印', exact: true }).click()
    await expect(page).toHaveURL(/\/print\/purchases\//)
    await expect(page.getByRole('heading', { name: '采购入库单' })).toBeVisible()
  })

  test('进销存报表：导出含合计行的 xlsx', async ({ page }) => {
    await goPage(page, '进销存报表', /\/reports\/inventory-flow$/)

    // 默认期间为本月（筛选必填已预置），点导出即取当前筛选全量
    await expect(page.getByRole('button', { name: '导出', exact: true })).toBeVisible()
    await expectDownload(await clickExportAndWaitDownload(page), '进销存报表')
  })
})

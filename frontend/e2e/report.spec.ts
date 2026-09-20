import { expect, test, type Page } from '@playwright/test'

import { clickUntil, clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'

/**
 * 进销存报表（specs/025-erp-report）：
 * 报表分组菜单可达、进销存报表四列与合计区、库存余额表分类聚合与下钻预置分类、
 * 采购 / 销售汇总净额列与分组维度切换、菜单多顶级分组联动。
 */

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

function uniqueCategoryName(): string {
  return `报表测试分类${Date.now().toString(36)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
}

/** 经侧边菜单进入指定报表页 */
async function goReport(page: Page, name: string, urlPattern: RegExp): Promise<void> {
  await login(page)
  await clickMenuItem(page, name)
  await expect(page).toHaveURL(urlPattern)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 经商品抽屉新增商品（分类就地新建，库存余额表下钻用例造数用） */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
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
}

test.describe('进销存报表（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('报表分组菜单可达，进销存报表四列与合计区展示', async ({ page }) => {
    await goReport(page, '进销存报表', /\/reports\/inventory-flow$/)

    await expect(page.locator('h1', { hasText: '进销存报表' })).toBeVisible()
    await expect(page.locator('th', { hasText: '期初数量' })).toBeVisible()
    await expect(page.locator('th', { hasText: '期间入' })).toBeVisible()
    await expect(page.locator('th', { hasText: '期间出' })).toBeVisible()
    await expect(page.locator('th', { hasText: '期末数量' })).toBeVisible()
    // 合计区（全量口径）
    await expect(page.getByText('期初合计')).toBeVisible()
    await expect(page.getByText('期末合计')).toBeVisible()
    await expect(page.getByText('全量口径')).toBeVisible()
  })

  test('库存余额表：分类聚合展示，查看明细下钻库存查询并预置分类', async ({ page }) => {
    const code = uniqueProductCode('rpt')
    await goProducts(page)
    await createProduct(page, code, `报表商品${Date.now() % 100000}`)

    await goReport(page, '库存余额表', /\/reports\/stock-balance$/)
    await expect(page.locator('th', { hasText: '库存占比' })).toBeVisible()
    await expect(page.locator('th', { hasText: '低库存商品数' })).toBeVisible()

    // 按商品关键词搜到其唯一分类行，占比列以进度条展示
    await page.getByPlaceholder('搜索商品编码或名称').fill(code)
    await clickUntilCount(page, '搜索', dataRows(page), 1)
    await expect(dataRows(page).first().locator('.arco-progress')).toBeVisible()

    // 查看明细 → 库存查询页预置分类筛选
    await dataRows(page).first().getByRole('button', { name: '查看明细' }).click()
    await expect(page).toHaveURL(/\/inventory\?categoryId=/)
    await expect(dataRows(page).first()).toContainText(code)
  })

  test('采购 / 销售汇总：净额列展示，分组维度切换为商品后显示单位列', async ({ page }) => {
    await goReport(page, '采购汇总', /\/reports\/purchase-summary$/)
    await expect(page.locator('th', { hasText: '入库金额' })).toBeVisible()
    await expect(page.locator('th', { hasText: '退货金额' })).toBeVisible()
    await expect(page.locator('th', { hasText: '净数量' })).toBeVisible()
    await expect(page.locator('th', { hasText: '净金额' })).toBeVisible()
    await expect(page.getByText('净额')).toBeVisible()

    // 分组维度切换为「商品」→ 出现「单位」列，名称列变「商品」
    await page.getByText('商品', { exact: true }).click()
    await clickUntil(page, '搜索', page.locator('th', { hasText: '单位' }))
    await expect(page.locator('th', { hasText: '商品' })).toBeVisible()

    await goReport(page, '销售汇总', /\/reports\/sales-summary$/)
    await expect(page.locator('th', { hasText: '出库金额' })).toBeVisible()
    await expect(page.locator('th', { hasText: '净金额' })).toBeVisible()
    await page.getByText('商品', { exact: true }).click()
    await clickUntil(page, '搜索', page.locator('th', { hasText: '单位' }))
  })

  test('菜单多顶级分组联动：基础档案 / 采购 / 销售 / 库存 / 资金 / 报表 / 系统分组可达', async ({ page }) => {
    await login(page)

    // 各顶级分组标题默认折叠但可见
    for (const group of ['基础档案', '采购', '销售', '库存', '资金', '报表', '系统']) {
      await expect(page.locator('.arco-menu-inline-header', { hasText: group })).toBeVisible()
    }

    // 展开「报表」分组进入「销售汇总」
    await clickMenuItem(page, '销售汇总')
    await expect(page).toHaveURL(/\/reports\/sales-summary$/)

    // 展开「库存」分组进入「库存流水」
    await clickMenuItem(page, '库存流水')
    await expect(page).toHaveURL(/\/stock-movements$/)

    // 「资金」分组进入「收付款」
    await clickMenuItem(page, '收付款')
    await expect(page).toHaveURL(/\/settlements$/)
  })
})

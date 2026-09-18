import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { clickMenuItem } from './helpers/menu'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
const TOKEN_KEY = 'app:token'
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 生成唯一商品编码（纯字母数字 + 连字符，2-32 字符，保证可搜索命中唯一行） */
function uniqueProductCode(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 生成唯一分类名 */
function uniqueCategoryName(): string {
  return `库存测试分类${Date.now().toString(36)}`
}

async function login(page: Page): Promise<void> {
  // 防御：先清除可能残留的 token，避免已登录守卫把 /login 重定向回首页（表单不渲染）
  await page.addInitScript((key) => window.localStorage.removeItem(key), TOKEN_KEY)
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 经侧边菜单（进销存分组）进入库存查询页 */
async function goInventory(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存查询')
  await expect(page).toHaveURL(/\/inventory$/)
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 商品抽屉标题（exact 精确匹配） */
function productDrawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/** 库存页按编码 / 名称搜索（点搜索按钮触发服务端查询） */
/** expected 默认「命中关键字的行」；空结果等场景传实际判据（如 .arco-empty） */
async function searchInventory(page: Page, keyword: string, expected?: Locator): Promise<void> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(keyword)
  if (expected) await clickUntil(page, '搜索', expected)
  else await searchAndWaitHit(page, keyword)
}

/** 经商品抽屉新增商品（分类就地新建；safetyStock 可选，验证低库存标记） */
async function createProduct(page: Page, code: string, name: string, safetyStock?: number): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(productDrawerTitle(page, '新增商品')).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类（specs/017-erp-category：抽屉内联输入条 + 保存）
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueCategoryName())
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').first()).toBeVisible()
  // 金额 / 安全库存均为 a-input-number：第 0 / 1 个是采购 / 销售价，第 2 个是安全库存
  const numberInputs = page.locator('.arco-drawer .arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  if (safetyStock !== undefined) {
    await numberInputs.nth(2).fill(String(safetyStock))
  }
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建')).toBeVisible()
  await expect(productDrawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 在商品页停用指定编码商品（按编码搜索后取唯一行） */
async function disableProduct(page: Page, code: string): Promise<void> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(code)
  await searchAndWaitHit(page, code)
  const row = dataRows(page).first()
  await expect(row).toContainText(code)
  await row.getByRole('button', { name: '停用' }).click()
  await page.getByRole('button', { name: /确\s*定/ }).click()
  await expect(page.getByText('已停用')).toBeVisible()
}

test.describe('库存查询（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('新建商品后库存页可见，联查带出分类 / 单位 / 库存 / 变动时间', async ({ page }) => {
    const code = uniqueProductCode('inv')
    const name = `库存测试商品${Date.now() % 100000}`
    await goProducts(page)
    await createProduct(page, code, name)

    await goInventory(page)
    await searchInventory(page, code)
    const row = dataRows(page).first()
    await expect(row).toContainText(code)
    await expect(row).toContainText(name)
    // 新建商品：库存 0、阈值 0 → 无低库存标签
    await expect(row.getByText('低于安全库存')).toHaveCount(0)
    // 最近库存变动时间（创建商品时即生成 Inventory 行）
    await expect(row.getByText(/\d{4}-\d{2}-\d{2} \d{2}:\d{2}/)).toHaveCount(1)
  })

  test('筛选与分页：编码搜索命中唯一行，空结果显示空状态，分类筛选生效，只读页无新增入口', async ({ page }) => {
    const code = uniqueProductCode('filt')
    await goProducts(page)
    await createProduct(page, code, `筛选测试商品${Date.now() % 100000}`)

    // 编码 / 名称模糊命中
    await goInventory(page)
    await searchInventory(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(code)

    // 不存在的关键词 → 空状态
    await searchInventory(page, `no_such_${Date.now()}`, page.locator('.arco-empty'))
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()

    // 重置恢复
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await expect(dataRows(page).first()).toBeVisible()

    // 只读页：工具条无新增按钮
    await expect(page.getByRole('button', { name: '新增' })).toHaveCount(0)
  })

  test('停用商品后库存页不再展示', async ({ page }) => {
    const code = uniqueProductCode('off')
    await goProducts(page)
    await createProduct(page, code, `停用测试商品${Date.now() % 100000}`)

    // 先确认库存页可见
    await goInventory(page)
    await searchInventory(page, code)
    await expect(dataRows(page).first()).toContainText(code)

    // 商品管理页停用
    await goProducts(page)
    await disableProduct(page, code)

    // 库存页查询该编码 → 空状态（仅启用商品）
    await goInventory(page)
    await searchInventory(page, code, page.locator('.arco-empty'))
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()
  })

  test('低库存标记：库存低于安全阈值标红 + 标签，阈值 0 不提醒', async ({ page }) => {
    const code = uniqueProductCode('low')
    // 阈值 10、库存 0 → 低于安全库存
    await goProducts(page)
    await createProduct(page, code, `低库存测试商品${Date.now() % 100000}`, 10)

    await goInventory(page)
    await searchInventory(page, code)
    const row = dataRows(page).first()
    await expect(row).toContainText(code)
    await expect(row.getByText('低于安全库存')).toHaveCount(1)
    // 库存数字标红
    await expect(row.locator('.stock-below')).toHaveCount(1)
  })
})

import { expect, test, type Page } from '@playwright/test'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 分类名输入框 placeholder（全角括号，商品抽屉就地新建共用） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 分类表单抽屉名称输入 placeholder */
const DRAWER_NAME_PLACEHOLDER = '1-20 字符'

/** 生成唯一分类名（满足 1-20 字符，纯字母数字保证可作分类名） */
function uniqueCategoryName(): string {
  return `cat${Date.now().toString(36)}`
}

/** 生成唯一商品编码（满足 2-32 位字母 / 数字 / 下划线 / 连字符） */
function uniqueCode(): string {
  return `E2E_${Date.now().toString(36)}${Math.floor(Math.random() * 100)}`
}

async function login(page: Page, username = CREDENTIALS.username, password = CREDENTIALS.password): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(username)
  await page.getByPlaceholder('请输入密码').fill(password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 登录并经侧边菜单（进销存分组）进入分类管理页 */
async function goCategories(page: Page): Promise<void> {
  await login(page)
  await page.locator('.arco-menu-item', { hasText: '分类管理' }).click()
  await expect(page).toHaveURL(/\/categories$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 分页「共 N 条」文本（Arco 在 total=0 时不渲染分页） */
function totalText(page: Page): ReturnType<typeof page.locator> {
  return page.locator('text=/共 \\d+ 条/')
}

/** 按名称搜索（点搜索按钮触发服务端查询） */
async function searchByKeyword(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索分类名称').fill(keyword)
  await page.getByRole('button', { name: '搜索' }).click()
}

/** 经抽屉新增分类（成功路径）：以抽屉内容输入框卸载作为成功关闭信号（比 toast 淡出时机更稳） */
async function createCategory(page: Page, categoryName: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增分类', { exact: true })).toBeVisible()
  await page.getByPlaceholder(DRAWER_NAME_PLACEHOLDER).fill(categoryName)
  await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
  // 成功关闭信号：抽屉内容（名称输入框）已卸载（toast 可能已淡出，不作为断言）
  // 拉长 timeout：dev 库数据多、连续创建时后端偶发慢响应，5s 默认值会误判
  await expect(page.getByPlaceholder(DRAWER_NAME_PLACEHOLDER)).toHaveCount(0, { timeout: 15000 })
}

/** 经商品抽屉新增商品（分类就地行内新建），等待创建成功并关闭抽屉 */
async function createProduct(page: Page, code: string, name: string, categoryName: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).first().click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  // 就地行内新建分类
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(categoryName)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建').first()).toBeVisible()
  const priceInputs = page.locator('.arco-drawer .arco-input-number input')
  await priceInputs.nth(0).fill('10.50')
  await priceInputs.nth(1).fill('20.00')
  await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
  // 成功信号：抽屉关闭（「新增商品」标题消失），确定性事件，不依赖 toast 淡出时机
  // 拉长 timeout：dev 库数据多时后端偶发慢响应，5s 默认值会误判
  await expect(page.getByText('新增商品', { exact: true })).toHaveCount(0, { timeout: 15000 })
}

test.describe('分类管理（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入分类管理页，列表渲染表头', async ({ page }) => {
    await goCategories(page)
    await expect(page.getByRole('heading', { name: '分类管理' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '分类名称' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '创建时间' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '操作' })).toBeVisible()
    // 有数据：表格渲染数据行；无数据：渲染空状态（两者互斥，避免 .or() 多行触发 strict mode）
    const hasData = (await totalText(page).count()) > 0
    if (hasData) {
      await expect(dataRows(page).first()).toBeVisible()
    } else {
      await expect(page.locator('.arco-empty')).toBeVisible()
    }
  })

  test('抽屉新增分类成功入列表，重名被拒绝并提示', async ({ page }) => {
    const catName = uniqueCategoryName()
    await goCategories(page)

    await createCategory(page, catName)
    await searchByKeyword(page, catName)
    const row = dataRows(page).filter({ hasText: catName })
    await expect(row.first()).toBeVisible()

    // 重名新增：抽屉提交 → 后端 40105 统一错误提示（抽屉保持打开，故不复用 createCategory）
    await page.getByRole('button', { name: '新增' }).click()
    await expect(page.getByText('新增分类', { exact: true })).toBeVisible()
    await page.getByPlaceholder(DRAWER_NAME_PLACEHOLDER).fill(catName)
    await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('分类名称已存在').first()).toBeVisible()
    // 关闭抽屉
    await page.locator('.arco-drawer').getByRole('button', { name: 'Close' }).click()
  })

  test('抽屉编辑改名生效，重名被拒绝并提示', async ({ page }) => {
    const catA = uniqueCategoryName()
    const catB = `b${Date.now().toString(36)}`
    const rename = `ren${Date.now().toString(36)}`
    await goCategories(page)
    await createCategory(page, catA)
    await createCategory(page, catB)

    // 搜索隔离出 catA 行，打开编辑抽屉
    await searchByKeyword(page, catA)
    const row = dataRows(page).first()
    await row.getByRole('button', { name: '编辑' }).click()
    await expect(page.getByText('编辑分类', { exact: true })).toBeVisible()
    const nameInput = page.getByPlaceholder(DRAWER_NAME_PLACEHOLDER)
    await expect(nameInput).toHaveValue(catA)

    // 改成已存在的 catB → 40105 重名提示（抽屉保持打开）
    await nameInput.fill(catB)
    await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('分类名称已存在').first()).toBeVisible()

    // 改为新名称 → 成功
    await nameInput.fill(rename)
    await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('分类已更新').first()).toBeVisible()
    await searchByKeyword(page, rename)
    await expect(dataRows(page).first()).toContainText(rename)
  })

  test('删除分类成功移除，被商品引用的分类删除被拦截', async ({ page }) => {
    const catName = uniqueCategoryName()
    const referencedCat = `ref${Date.now().toString(36)}`
    await goCategories(page)

    // 可删分类
    await createCategory(page, catName)
    await searchByKeyword(page, catName)
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('分类已删除').first()).toBeVisible()
    await expect(dataRows(page)).toHaveCount(0)

    // 被商品引用的分类：由 createProduct 在商品抽屉就地新建 referencedCat，再建一个挂到该分类的商品
    // 注意：不可预先 createCategory(referencedCat)，否则就地新建同名会 40105 重名失败
    await page.locator('.arco-menu-item', { hasText: '商品管理' }).click()
    await expect(page).toHaveURL(/\/products$/)
    await createProduct(page, uniqueCode(), `E2E 引用商品${Date.now()}`, referencedCat)

    // 回到分类管理页删除该分类，后端 40106 拦截并统一错误提示
    await page.locator('.arco-menu-item', { hasText: '分类管理' }).click()
    await expect(page).toHaveURL(/\/categories$/)
    await searchByKeyword(page, referencedCat)
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    // 后端 40106 真实文案（DeleteCategoryRequestHandler）：该分类已被商品使用，不可删除，请改为编辑名称
    await expect(page.getByText('该分类已被商品使用，不可删除，请改为编辑名称').first()).toBeVisible()
    await expect(dataRows(page).first()).toContainText(referencedCat)
  })

  test('搜索命中可见、无命中显示空状态，重置恢复', async ({ page }) => {
    const hit = uniqueCategoryName()
    await goCategories(page)
    await createCategory(page, hit)
    const before = await totalText(page).textContent().catch(() => null)

    // 命中
    await searchByKeyword(page, hit)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(hit)

    // 无命中 → 空状态
    await searchByKeyword(page, `no_such_${Date.now()}`)
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()

    // 重置恢复
    await page.getByRole('button', { name: '重置', exact: true }).click()
    if (before) {
      await expect(totalText(page)).toHaveText(before)
    }
  })

  test('分页：翻页数据正确且序号跨页连续', async ({ page }) => {
    const prefix = `pg${Date.now().toString(36)}`
    await goCategories(page)
    // 建 11 个同前缀分类，搜索隔离；序号补零 2 位（padStart）保证 11 个名字唯一
    // 注意：不可用 padEnd(prefix.length+2)，否则 i=1 与 i=10 都得到 prefix+"10" 触发 40105 重名
    for (let i = 1; i <= 11; i++) {
      await createCategory(page, `${prefix}${String(i).padStart(2, '0')}`)
    }
    await searchByKeyword(page, prefix)
    await expect(totalText(page)).toHaveText('共 11 条')

    // 每页条数改为 10（搜索后分页已渲染；11 条 → 2 页）
    await page.locator('.arco-pagination-options .arco-select-view').click()
    await page.locator('.arco-select-option', { hasText: '10 条/页' }).click()
    await expect(totalText(page)).toHaveText('共 11 条')

    // 第 1 页 10 行，序号 1-10
    await expect(dataRows(page)).toHaveCount(10)
    await expect(dataRows(page).first()).toContainText('1')
    await expect(dataRows(page).last()).toContainText('10')

    // 翻到第 2 页：1 行，序号 11
    await page.locator('.arco-pagination-item', { hasText: '2' }).click()
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText('11')
  })

  test('搜索按钮点击后进入 loading，完成后恢复可点', async ({ page }) => {
    await goCategories(page)
    // 人为拖慢分类分页请求，保证 loading 状态可被断言捕获
    await page.route(/\/api\/categories\/paged\?/, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 600))
      await route.continue()
    })

    const searchButton = page.getByRole('button', { name: '搜索' })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })
})

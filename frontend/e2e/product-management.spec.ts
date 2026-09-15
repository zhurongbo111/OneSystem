import { expect, test, type Page } from '@playwright/test'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 分类名输入框 placeholder（全角括号） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 生成唯一商品编码（满足 2-32 位字母 / 数字 / 下划线 / 连字符） */
function uniqueCode(): string {
  return `E2E_${Date.now().toString(36)}${Math.floor(Math.random() * 100)}`
}

/** 生成唯一分类名（满足 1-20 字符，纯字母数字保证可作分类名） */
function uniqueCategoryName(): string {
  return `cat${Date.now().toString(36)}`
}

async function login(page: Page, username = CREDENTIALS.username, password = CREDENTIALS.password): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(username)
  await page.getByPlaceholder('请输入密码').fill(password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 登录并经侧边菜单（进销存分组）进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await login(page)
  await page.locator('.arco-menu-item', { hasText: '商品管理' }).click()
  await expect(page).toHaveURL(/\/products$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 分页「共 N 条」文本（Arco 在 total=0 时不渲染分页） */
function totalText(page: Page): ReturnType<typeof page.locator> {
  return page.locator('text=/共 \\d+ 条/')
}

/** 抽屉标题（exact 精确匹配，避免命中列表里含同词的商品名） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/** 按编码 / 名称搜索（点搜索按钮触发服务端查询） */
async function searchByKeyword(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(keyword)
  await page.getByRole('button', { name: '搜索' }).click()
}

/** 在抽屉内就地新建分类（行内输入条），成功后输入条收起且新分类被选中 */
async function createCategoryInDrawer(page: Page, categoryName: string): Promise<void> {
  await page.getByRole('button', { name: '新建分类' }).click()
  const input = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(input).toBeVisible()
  await input.fill(categoryName)
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expect(page.getByText('分类已创建')).toBeVisible()
  await expect(input).toHaveCount(0)
}

/** 经抽屉新增商品（分类就地新建），等待创建成功并关闭抽屉 */
async function createProduct(page: Page, code: string, name: string, categoryName: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增商品')).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await createCategoryInDrawer(page, categoryName)
  const priceInputs = page.locator('.arco-drawer .arco-input-number input')
  await priceInputs.nth(0).fill('10.50')
  await priceInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('商品已创建')).toBeVisible()
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 进入指定商品所在行（按编码搜索后取唯一行） */
async function openProductRow(page: Page, code: string): Promise<ReturnType<typeof page.locator>> {
  await searchByKeyword(page, code)
  const row = dataRows(page).first()
  await expect(row).toContainText(code)
  return row
}

test.describe('商品管理（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入商品管理页，列表渲染表头与分页', async ({ page }) => {
    await goProducts(page)
    await expect(page.getByRole('heading', { name: '商品管理' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '编码' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '库存', exact: true })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '状态' })).toBeVisible()
    // 空表（total=0）时 Arco 隐藏分页，改为断言「暂无数据」或「共 N 条」之一可见
    await expect(page.locator('.arco-empty').or(totalText(page))).toBeVisible()
  })

  test('新增商品（就地新建分类）后可搜索到，初始库存为 0', async ({ page }) => {
    const code = uniqueCode()
    await goProducts(page)
    await createProduct(page, code, 'E2E 商品甲', uniqueCategoryName())
    await searchByKeyword(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(page.getByText('E2E 商品甲')).toBeVisible()
    // 新建商品后端同步初始化库存行 Quantity = 0
    await expect(dataRows(page).first().locator('.stock-cell')).toHaveText('0')
  })

  test('重复编码被拒绝并提示', async ({ page }) => {
    const code = uniqueCode()
    await goProducts(page)
    await createProduct(page, code, 'E2E 商品乙', uniqueCategoryName())

    // 再次新增同一编码（分类同样就地新建，确保前端校验通过、请求真正发出）
    await page.getByRole('button', { name: '新增' }).click()
    await expect(drawerTitle(page, '新增商品')).toBeVisible()
    await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
    await page.getByPlaceholder('2-50 字符').fill('E2E 商品乙二')
    await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
    await createCategoryInDrawer(page, uniqueCategoryName())
    const priceInputs = page.locator('.arco-drawer .arco-input-number input')
    await priceInputs.nth(0).fill('1.00')
    await priceInputs.nth(1).fill('2.00')
    await page.getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('商品编码已存在')).toBeVisible()
    await page.locator('.arco-drawer').getByRole('button', { name: 'Close' }).click()
  })

  test('编辑商品名称生效', async ({ page }) => {
    const code = uniqueCode()
    await goProducts(page)
    await createProduct(page, code, 'E2E 商品丙', uniqueCategoryName())
    const row = await openProductRow(page, code)

    await row.getByRole('button', { name: '编辑' }).click()
    await expect(drawerTitle(page, '编辑商品')).toBeVisible()
    // 详情回填完成前字段禁用，fill 会等待可用
    await page.getByPlaceholder('2-50 字符').fill('E2E 商品丙改')
    await page.getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('商品已更新')).toBeVisible()

    await searchByKeyword(page, code)
    await expect(dataRows(page).first()).toContainText('E2E 商品丙改')
  })

  test('详情抽屉展示字段与审计时间', async ({ page }) => {
    const code = uniqueCode()
    await goProducts(page)
    await createProduct(page, code, 'E2E 商品丁', uniqueCategoryName())
    const row = await openProductRow(page, code)

    await row.getByRole('button', { name: '详情' }).click()
    await expect(drawerTitle(page, '商品详情')).toBeVisible()
    const drawer = page.locator('.arco-drawer')
    // 查看态各字段为禁用输入框，值回填到 input.value（getByText 不匹配 input 值，改用 toHaveValue）
    await expect(drawer.getByPlaceholder('2-50 字符')).toHaveValue('E2E 商品丁')
    // 查看态附加只读审计信息（限定抽屉作用域，避免命中表头「创建时间」）
    await expect(drawer.getByText('创建时间')).toBeVisible()
    await expect(drawer.getByText('更新时间')).toBeVisible()
    await drawer.getByRole('button', { name: 'Close' }).click()
  })

  test('停用后状态标签变化，停用商品可再启用', async ({ page }) => {
    const code = uniqueCode()
    await goProducts(page)
    await createProduct(page, code, 'E2E 商品戊', uniqueCategoryName())
    const row = await openProductRow(page, code)
    // 状态列唯一 tag（新建商品 safetyStock=0 不会低库存，不会渲染低库存 tag）
    await expect(row.locator('.arco-tag')).toHaveText('启用')

    // 停用
    await row.getByRole('button', { name: '停用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已停用')).toBeVisible()
    const stoppedRow = await openProductRow(page, code)
    await expect(stoppedRow.locator('.arco-tag')).toHaveText('停用')

    // 启用
    await stoppedRow.getByRole('button', { name: '启用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已启用')).toBeVisible()
    const enabledRow = await openProductRow(page, code)
    await expect(enabledRow.locator('.arco-tag')).toHaveText('启用')
  })

  test('搜索不存在的编码显示空状态，重置恢复', async ({ page }) => {
    await goProducts(page)
    const before = await totalText(page).textContent().catch(() => null)

    await searchByKeyword(page, `no_such_${Date.now()}`)
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()

    await page.getByRole('button', { name: '重置', exact: true }).click()
    // 空库时重置后分页仍隐藏，仅在有数据时断言总数恢复
    if (before) {
      await expect(totalText(page)).toHaveText(before)
    }
  })

  test('工具条「分类管理」跳转分类管理独立页', async ({ page }) => {
    await goProducts(page)

    // 工具条按钮跳转独立页（specs/erp-category），增删改用例见 category-management.spec.ts
    await page.getByRole('button', { name: '分类管理' }).click()
    await expect(page).toHaveURL(/\/categories$/)
    await expect(page.getByRole('heading', { name: '分类管理' })).toBeVisible()
  })

  test('查询按钮点击后进入 loading，完成后恢复可点', async ({ page }) => {
    await goProducts(page)
    await page.route(/\/api\/products\?/, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 600))
      await route.continue()
    })

    const searchButton = page.getByRole('button', { name: '搜索' })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })
})

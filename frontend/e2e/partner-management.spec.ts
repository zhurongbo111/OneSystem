import { expect, test, type Page } from '@playwright/test'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 生成唯一单位名称（纯字母数字，1-50 字符，保证可搜索命中唯一行） */
function uniquePartnerName(): string {
  return `p${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page, username = CREDENTIALS.username, password = CREDENTIALS.password): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(username)
  await page.getByPlaceholder('请输入密码').fill(password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 登录并经侧边菜单（进销存分组）进入往来单位页 */
async function goPartners(page: Page): Promise<void> {
  await login(page)
  await page.locator('.arco-menu-item', { hasText: '往来单位' }).click()
  await expect(page).toHaveURL(/\/partners$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉标题（exact 精确匹配，避免命中列表里含同词的单位名） */
function drawerTitle(page: Page, title: string): ReturnType<typeof page.locator> {
  return page.getByText(title, { exact: true })
}

/** 按名称 / 联系人搜索（点搜索按钮触发服务端查询） */
async function searchByKeyword(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索单位名称或联系人').fill(keyword)
  await page.getByRole('button', { name: '搜索' }).click()
}

/** 经抽屉新增往来单位，等待创建成功并关闭抽屉 */
async function createPartner(
  page: Page,
  name: string,
  typeLabel: '供应商' | '客户' | '两者',
  opts: { contact?: string; phone?: string } = {},
): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
  const drawer = page.locator('.arco-drawer')
  await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  if (opts.contact) {
    await drawer.getByPlaceholder('选填，≤ 20 字符').fill(opts.contact)
  }
  if (opts.phone) {
    await drawer.getByPlaceholder('选填，11 位手机号').fill(opts.phone)
  }
  await drawer.getByRole('button', { name: '提交' }).click()
  await expect(page.getByText('往来单位已创建')).toBeVisible()
  await expect(drawerTitle(page, '新增往来单位')).toHaveCount(0)
}

/** 进入指定单位所在行（按名称搜索后取唯一行） */
async function openPartnerRow(page: Page, name: string): Promise<ReturnType<typeof page.locator>> {
  await searchByKeyword(page, name)
  const row = dataRows(page).first()
  await expect(row).toContainText(name)
  return row
}

test.describe('往来单位（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入往来单位页，列表渲染表头', async ({ page }) => {
    await goPartners(page)
    await expect(page.getByRole('heading', { name: '往来单位' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '名称' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '类型' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '联系人' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '电话' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '状态' })).toBeVisible()
  })

  test('新增往来单位后可搜索到，类型与状态正确', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '供应商', { contact: '张三', phone: '13800000000' })

    await searchByKeyword(page, name)
    await expect(dataRows(page)).toHaveCount(1)
    const row = dataRows(page).first()
    await expect(row).toContainText(name)
    await expect(row.getByText('供应商', { exact: true })).toBeVisible()
    await expect(row.getByText('张三')).toBeVisible()
    await expect(row.getByText('13800000000')).toBeVisible()
    await expect(row.locator('.arco-tag', { hasText: '启用' })).toBeVisible()
  })

  test('重复名称被拒绝并提示（40102）', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '客户')

    // 再次新增同一名称，应被后端拒绝
    await page.getByRole('button', { name: '新增' }).click()
    await expect(drawerTitle(page, '新增往来单位')).toBeVisible()
    const drawer = page.locator('.arco-drawer')
    await drawer.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
    await drawer.locator('.arco-radio-group').getByText('客户', { exact: true }).click()
    await drawer.getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('往来单位名称已存在')).toBeVisible()
    await drawer.getByRole('button', { name: 'Close' }).click()
  })

  test('编辑往来单位生效且名称不可改', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '客户')
    const row = await openPartnerRow(page, name)

    await row.getByRole('button', { name: '编辑' }).click()
    await expect(drawerTitle(page, '编辑往来单位')).toBeVisible()
    const drawer = page.locator('.arco-drawer')
    // 编辑态名称只读展示
    const nameInput = drawer.locator('.arco-form-item', { hasText: '单位名称' }).getByRole('textbox')
    await expect(nameInput).toBeDisabled()
    await expect(nameInput).toHaveValue(name)
    // 修改类型为「两者」、修改联系人
    await drawer.locator('.arco-radio-group').getByText('两者', { exact: true }).click()
    await drawer.getByPlaceholder('选填，≤ 20 字符').fill('李四')
    await drawer.getByRole('button', { name: '提交' }).click()
    await expect(page.getByText('往来单位已更新')).toBeVisible()

    const updatedRow = await openPartnerRow(page, name)
    await expect(updatedRow.getByText('两者', { exact: true })).toBeVisible()
    await expect(updatedRow.getByText('李四')).toBeVisible()
  })

  test('详情抽屉展示字段与审计时间', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '两者', { contact: '王五' })
    const row = await openPartnerRow(page, name)

    await row.getByRole('button', { name: '详情' }).click()
    await expect(drawerTitle(page, '往来单位详情')).toBeVisible()
    const drawer = page.locator('.arco-drawer')
    const contactInput = drawer.getByPlaceholder('选填，≤ 20 字符')
    await expect(contactInput).toHaveValue('王五')
    await expect(drawer.getByText('创建时间')).toBeVisible()
    await expect(drawer.getByText('更新时间')).toBeVisible()
    await drawer.getByRole('button', { name: 'Close' }).click()
  })

  test('停用后状态标签变化，停用单位可再启用', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '供应商')
    const row = await openPartnerRow(page, name)
    // 状态列 tag（行内还有「类型」tag，按文本限定状态 tag）
    await expect(row.locator('.arco-tag', { hasText: '启用' })).toHaveCount(1)

    // 停用
    await row.getByRole('button', { name: '停用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已停用')).toBeVisible()
    const stoppedRow = await openPartnerRow(page, name)
    await expect(stoppedRow.locator('.arco-tag', { hasText: '停用' })).toHaveCount(1)

    // 启用
    await stoppedRow.getByRole('button', { name: '启用' }).click()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(page.getByText('已启用')).toBeVisible()
    const enabledRow = await openPartnerRow(page, name)
    await expect(enabledRow.locator('.arco-tag', { hasText: '启用' })).toHaveCount(1)
  })

  test('类型筛选生效（供应商筛选排除客户）', async ({ page }) => {
    const name = uniquePartnerName()
    await goPartners(page)
    await createPartner(page, name, '供应商')

    // 切换到「客户」筛选，新建的单位不应出现
    await page.locator('.toolbar-filter .arco-select').first().click()
    await page.locator('.arco-select-option', { hasText: '客户' }).first().click()
    await page.getByRole('button', { name: '搜索' }).click()
    await expect(dataRows(page).filter({ hasText: name })).toHaveCount(0)

    // 切换回「供应商」筛选，应出现
    await page.locator('.toolbar-filter .arco-select').first().click()
    await page.locator('.arco-select-option', { hasText: '供应商' }).first().click()
    await page.getByRole('button', { name: '搜索' }).click()
    await expect(dataRows(page).filter({ hasText: name })).toHaveCount(1)
  })

  test('搜索不存在的名称显示空状态，重置恢复', async ({ page }) => {
    await goPartners(page)

    await searchByKeyword(page, `no_such_partner_${Date.now()}`)
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()

    await page.getByRole('button', { name: '重置', exact: true }).click()
    await expect(page.locator('.arco-empty')).not.toBeVisible()
  })

  test('查询按钮点击后进入 loading，完成后恢复可点', async ({ page }) => {
    await goPartners(page)
    await page.route(/\/api\/partners\?/, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 600))
      await route.continue()
    })

    const searchButton = page.getByRole('button', { name: '搜索' })
    await searchButton.click()
    await expect(searchButton).toHaveClass(/arco-btn-loading/)
    await expect(searchButton).not.toHaveClass(/arco-btn-loading/, { timeout: 10_000 })
  })
})

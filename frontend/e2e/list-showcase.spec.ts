import { test, expect, type Page } from '@playwright/test'

/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 静态数据总条数（见 ListShowcaseView 的 SEED） */
const TOTAL = 25

async function login(page: Page): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('请输入用户名').fill(CREDENTIALS.username)
  await page.getByPlaceholder('请输入密码').fill(CREDENTIALS.password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

/** 进入列表示例页 */
async function goList(page: Page): Promise<void> {
  await login(page)
  await page.locator('.arco-menu-item', { hasText: '列表示例' }).click()
  await expect(page).toHaveURL(/\/list/)
}

/** 当前表格数据行（数据行在原生 tbody 内，排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 分页"共 N 条"文本 */
function totalText(page: Page): ReturnType<typeof page.locator> {
  return page.locator('text=/共 \\d+ 条/')
}

test.describe('列表页样式参照（集成）', () => {
  test('登录后进入列表示例页，默认每页 10 行', async ({ page }) => {
    await goList(page)
    await expect(page.getByRole('heading', { name: '用户列表' })).toBeVisible()
    await expect(dataRows(page)).toHaveCount(10)
    await expect(totalText(page)).toHaveText(`共 ${TOTAL} 条`)
  })

  test('关键词搜索过滤，重置恢复', async ({ page }) => {
    await goList(page)
    await page.getByPlaceholder('搜索名称或邮箱').fill('张伟')
    await page.getByRole('button', { name: '搜索' }).click()
    // 仅命中「张伟」一条
    await expect(dataRows(page)).toHaveCount(1)
    await page.getByRole('button', { name: '重置' }).click()
    await expect(dataRows(page)).toHaveCount(10)
    await expect(totalText(page)).toHaveText(`共 ${TOTAL} 条`)
  })

  test('状态筛选「禁用」减少行数', async ({ page }) => {
    await goList(page)
    await page.locator('.filter-bar__status').click()
    await page.locator('.arco-select-option', { hasText: '禁用' }).click()
    await page.getByRole('button', { name: '搜索' }).click()
    // 禁用用户共 7 条
    await expect(dataRows(page)).toHaveCount(7)
    await expect(totalText(page)).toHaveText('共 7 条')
  })

  test('角色筛选「管理员」减少行数', async ({ page }) => {
    await goList(page)
    await page.locator('.filter-bar__role').click()
    await page.locator('.arco-select-option', { hasText: '管理员' }).click()
    await page.getByRole('button', { name: '搜索' }).click()
    // 管理员共 5 条
    await expect(dataRows(page)).toHaveCount(5)
    await expect(totalText(page)).toHaveText('共 5 条')
  })

  test('勾选首行后批量删除移除该行', async ({ page }) => {
    await goList(page)
    await dataRows(page).first().locator('.arco-checkbox').click()
    await page.getByRole('button', { name: '批量删除' }).click()
    // Popconfirm 确认（两字按钮 Arco 可能插入空格）
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(totalText(page)).toHaveText(`共 ${TOTAL - 1} 条`)
  })

  test('单行删除走 Popconfirm 确认且显示确认文案', async ({ page }) => {
    await goList(page)
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    // 确认气泡需显示文案（Arco popconfirm 用 content 而非 title）
    await expect(page.getByText('确认删除该用户？')).toBeVisible()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(totalText(page)).toHaveText(`共 ${TOTAL - 1} 条`)
  })

  test('切换每页 20 条后行数变化', async ({ page }) => {
    await goList(page)
    await page.locator('.arco-pagination-options .arco-select').click()
    await page.locator('.arco-select-option', { hasText: '20 条/页' }).click()
    await expect(dataRows(page)).toHaveCount(20)
  })

  test('列设置隐藏「邮箱」列后表头消失', async ({ page }) => {
    await goList(page)
    await expect(page.getByRole('columnheader', { name: '邮箱' })).toBeVisible()
    await page.getByRole('button', { name: '列设置' }).click()
    await page.locator('.col-settings .arco-checkbox', { hasText: '邮箱' }).click()
    // 关闭下拉
    await page.getByRole('heading', { name: '用户列表' }).click()
    await expect(page.getByRole('columnheader', { name: '邮箱' })).toHaveCount(0)
  })

  test('筛选无结果时显示空状态', async ({ page }) => {
    await goList(page)
    await page.getByPlaceholder('搜索名称或邮箱').fill('不存在的关键字xyz')
    await page.getByRole('button', { name: '搜索' }).click()
    await expect(dataRows(page)).toHaveCount(0)
    await expect(page.locator('.arco-empty')).toBeVisible()
  })
})

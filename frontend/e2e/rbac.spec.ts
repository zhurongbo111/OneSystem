import { expect, test, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem, ensureMenuItemVisible, menuGroup } from './helpers/menu'
import { expectMessage } from './helpers/message'

const BACKEND_HEALTH = 'http://localhost:5080/health'

const ADMIN = { username: 'admin', password: 'admin123' }

/** 最小权限角色的唯一权限点（仅商品查看） */
const MIN_PERMISSION = 'products.view'

/** 角色下拉选项文本：后端内置角色名为英文，勾选新增的「登录日志」分组避免全量授权 */
const DRAWER_PERMISSION_GROUP = '登录日志'

/** 唯一角色名（与后端 RoleFieldConstraints.NameMaxLength=20 对齐） */
function uniqueRoleName(): string {
  return `e2e${Date.now().toString(36)}`.slice(0, 20)
}

/** 唯一用户名（与后端 UserFieldConstraints.UserNameMaxLength=50 对齐） */
function uniqueUsername(): string {
  return `e2e_${Date.now().toString(36)}`.slice(0, 50)
}

/** 角色列表数据行（排除空态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 以 admin 身份进入角色权限页 */
async function goRoles(page: Page): Promise<void> {
  await loginAs(page, ADMIN.username, ADMIN.password)
  await clickMenuItem(page, '角色权限')
  await expect(page).toHaveURL(/\/roles$/)
  await expect(page.getByRole('columnheader', { name: '角色名称' })).toBeVisible()
}

/** 按角色名搜索 */
async function searchRole(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索角色名称').fill(keyword)
  await page.getByRole('button', { name: '搜索', exact: true }).click()
}

/** 打开新增角色抽屉 */
async function openCreateDrawer(page: Page): Promise<void> {
  await page.getByRole('button', { name: '新增角色' }).click()
  await expect(page.locator('.arco-drawer')).toBeVisible()
}

/** 勾选权限树中指定分组（分组中文名由后端返回，前端不硬编码） */
async function checkPermissionGroup(page: Page, group: string): Promise<void> {
  const node = page.locator('.arco-tree-node', { hasText: group }).first()
  await node.locator('[class*="check"]').first().click()
}

/** 提交抽屉表单 */
async function submitDrawer(page: Page): Promise<void> {
  await page.locator('.arco-drawer').getByRole('button', { name: '提交' }).click()
}

/** 清除本地凭证回到登录页 */
async function logout(page: Page): Promise<void> {
  await page.evaluate(() => window.localStorage.clear())
  await page.goto('/login')
}

/** 取当前登录态 token（接口构造 fixture 用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 经接口创建角色（最小权限角色的 fixture，避免 UI 树节点定位脆弱） */
async function createRoleViaApi(page: Page, name: string, permissionKeys: string[]): Promise<string> {
  const token = await apiToken(page)
  const response = await page.request.post('/api/roles', {
    data: { name, permissionKeys },
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  const body = (await response.json()) as { code: number; data: { id: string } }
  expect(body.code).toBe(0)
  return body.data.id
}

/** 经接口创建用户并绑定角色 */
async function createUserViaApi(page: Page, username: string, roleId: string): Promise<void> {
  const token = await apiToken(page)
  const response = await page.request.post('/api/users', {
    data: { username, displayName: 'E2E 最小权限', password: 'initPass123', roleIds: [roleId] },
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  const body = (await response.json()) as { code: number }
  expect(body.code).toBe(0)
}

test.beforeEach(async ({ request }) => {
  const health = await request.get(BACKEND_HEALTH)
  expect(health.ok(), '后端 dev 环境未就绪：http://localhost:5080').toBeTruthy()
})

test('菜单进入角色权限页：内置角色可见且删除入口禁用', async ({ page }) => {
  await goRoles(page)

  const builtinRow = dataRows(page).filter({ hasText: 'SuperAdmin' }).first()
  await expect(builtinRow).toBeVisible()
  await expect(builtinRow.getByRole('button', { name: '删除' })).toBeDisabled()
})

test('新增角色：勾选权限点后创建成功', async ({ page }) => {
  const name = uniqueRoleName()
  await goRoles(page)

  await openCreateDrawer(page)
  await page.getByPlaceholder('请输入角色名称（最多 20 字）').fill(name)
  await checkPermissionGroup(page, DRAWER_PERMISSION_GROUP)
  await submitDrawer(page)
  await expectMessage(page, '角色已创建')

  await searchRole(page, name)
  await expect(dataRows(page)).toHaveCount(1)
  await expect(dataRows(page).first()).toContainText(name)
})

test('新增角色：名称重复被拒绝（后端 40173）', async ({ page }) => {
  const name = uniqueRoleName()
  await goRoles(page)

  await openCreateDrawer(page)
  await page.getByPlaceholder('请输入角色名称（最多 20 字）').fill(name)
  await checkPermissionGroup(page, DRAWER_PERMISSION_GROUP)
  await submitDrawer(page)
  await expectMessage(page, '角色已创建')

  await openCreateDrawer(page)
  await page.getByPlaceholder('请输入角色名称（最多 20 字）').fill(name)
  await checkPermissionGroup(page, DRAWER_PERMISSION_GROUP)
  await submitDrawer(page)
  await expectMessage(page, '角色名称已存在')
})

test('编辑角色：改名生效（重名排除自身）', async ({ page }) => {
  const name = uniqueRoleName()
  const newName = `${name}b`.slice(0, 20)
  await goRoles(page)

  await openCreateDrawer(page)
  await page.getByPlaceholder('请输入角色名称（最多 20 字）').fill(name)
  await checkPermissionGroup(page, DRAWER_PERMISSION_GROUP)
  await submitDrawer(page)
  await expectMessage(page, '角色已创建')

  await searchRole(page, name)
  await dataRows(page).first().getByRole('button', { name: '编辑' }).click()
  await expect(page.locator('.arco-drawer')).toBeVisible()
  const nameInput = page.getByPlaceholder('请输入角色名称（最多 20 字）')
  // 详情回填后再改名
  await expect(nameInput).toHaveValue(name)
  await nameInput.fill(newName)
  await submitDrawer(page)
  await expectMessage(page, '角色已更新')

  await searchRole(page, newName)
  await expect(dataRows(page)).toHaveCount(1)
  await expect(dataRows(page).first()).toContainText(newName)
})

test('删除角色：自建角色可删除（内置角色入口已禁用）', async ({ page }) => {
  const name = uniqueRoleName()
  await goRoles(page)

  await openCreateDrawer(page)
  await page.getByPlaceholder('请输入角色名称（最多 20 字）').fill(name)
  await checkPermissionGroup(page, DRAWER_PERMISSION_GROUP)
  await submitDrawer(page)
  await expectMessage(page, '角色已创建')

  await searchRole(page, name)
  await dataRows(page).first().getByRole('button', { name: '删除' }).click()
  await page.locator('.arco-popconfirm').getByRole('button', { name: '确定' }).click()
  await expectMessage(page, '角色已删除')

  await searchRole(page, name)
  await expect(dataRows(page)).toHaveCount(0)
})

test('admin（SuperAdmin）不受限：可见全部菜单且可进用户管理', async ({ page }) => {
  await loginAs(page, ADMIN.username, ADMIN.password)

  await expect(menuGroup(page, '系统')).toBeVisible()
  // 分组默认折叠，先展开再断言子项（菜单项不可见 = 无权限被过滤，而非折叠）
  await ensureMenuItemVisible(page, '角色权限')
  await clickMenuItem(page, '用户管理')
  await expect(page).toHaveURL(/\/users$/)
  await expect(page.getByRole('button', { name: '新增', exact: true })).toBeVisible()
})

test('最小权限角色（仅商品查看）：菜单只剩商品管理、无新增按钮、无权限路由跳 403', async ({ page }) => {
  const roleName = uniqueRoleName()
  const username = uniqueUsername()

  await loginAs(page, ADMIN.username, ADMIN.password)
  const roleId = await createRoleViaApi(page, roleName, [MIN_PERMISSION])
  await createUserViaApi(page, username, roleId)

  await logout(page)
  await loginAs(page, username, 'initPass123')

  // 菜单：仅「基础档案 → 商品管理」可见，其余业务分组与系统分组隐藏
  await expect(menuGroup(page, '基础档案')).toBeVisible()
  await ensureMenuItemVisible(page, '商品管理')
  await expect(menuGroup(page, '采购')).toHaveCount(0)
  await expect(menuGroup(page, '系统')).toHaveCount(0)

  // 按钮级：无 products.create → 页面无「新增」类按钮
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
  await expect(page.getByRole('button').filter({ hasText: '新增' })).toHaveCount(0)

  // 无权限路由 → 403 页（导航守卫拦截）
  await page.goto('/purchases')
  await expect(page).toHaveURL(/\/403/)
  await expect(page.getByText('无访问权限')).toBeVisible()

  // 无权限接口 → 统一响应 code 40300
  const token = await apiToken(page)
  const response = await page.request.get('/api/users?page=1&pageSize=20', {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number }
  expect(body.code).toBe(40300)
})

test('403 页可返回首页', async ({ page }) => {
  const roleName = uniqueRoleName()
  const username = uniqueUsername()

  await loginAs(page, ADMIN.username, ADMIN.password)
  const roleId = await createRoleViaApi(page, roleName, [MIN_PERMISSION])
  await createUserViaApi(page, username, roleId)

  await logout(page)
  await loginAs(page, username, 'initPass123')

  await page.goto('/roles')
  await expect(page).toHaveURL(/\/403/)
  await expect(page.getByText('无访问权限')).toBeVisible()

  await page.getByRole('button', { name: '返回首页' }).click()
  // 首页是布局父路由的默认子路由，URL 为根路径（name: 'home' 对应 '/'）
  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByText('当前登录用户')).toBeVisible()
})

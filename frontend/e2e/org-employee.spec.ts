import { statSync } from 'node:fs'

import { expect, test, type Download, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem, menuGroup, menuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 管理员账号（来自项目 seed 数据） */
const ADMIN = { username: 'admin', password: 'admin123' }
/** 无 employees.view 的角色所用权限点（仅商品查看） */
const MIN_PERMISSION = 'products.view'

/** 唯一编码（≤ 20 字符，与后端编码列长对齐） */
function uniqueCode(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`.slice(0, 20)
}

/** 唯一名称（部门 / 岗位 / 姓名用） */
function uniqueName(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉容器（`unmount-on-close`：关闭后整体移除，故可用 toHaveCount(0) 判定「已提交并关闭」） */
function drawer(page: Page): Locator {
  return page.locator('.arco-drawer')
}

/** 登录并经侧边菜单进入指定页面 */
async function goPage(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await loginAs(page, ADMIN.username, ADMIN.password)
  await goMenu(page, menuName, urlPattern)
}

/**
 * 点击侧边菜单并等待进入目标页。
 * Arco 菜单在展开动画 / 重渲染期间会吞掉 click（前端规则 §10.1），故重试点击直到 URL 命中。
 */
async function goMenu(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await expect(async () => {
    if (!urlPattern.test(page.url())) {
      await clickMenuItem(page, menuName)
    }
    await expect(page).toHaveURL(urlPattern, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 取当前登录态 token（接口 fixture 用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 经接口 POST（返回统一响应体，供断言业务码） */
async function apiPost(page: Page, url: string, data: unknown): Promise<{ code: number; data?: { id?: string } }> {
  const token = await apiToken(page)
  const response = await page.request.post(`/api${url}`, {
    data,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { code: number; data?: { id?: string } }
}

/** 经接口创建「最小权限」角色并返回角色 id */
async function createRoleViaApi(page: Page, name: string, permissionKeys: string[]): Promise<string> {
  const body = await apiPost(page, '/roles', { name: name.slice(0, 20), permissionKeys })
  expect(body.code).toBe(0)
  return body.data?.id ?? ''
}

/** 经接口创建用户并绑定角色，返回用户 id（员工绑定账号的 fixture） */
async function createUserViaApi(page: Page, username: string): Promise<string> {
  const roleId = await createRoleViaApi(page, uniqueName('r'), [MIN_PERMISSION])
  const body = await apiPost(page, '/users', {
    username,
    displayName: 'E2E 组织人事',
    password: 'initPass123',
    roleIds: [roleId],
  })
  expect(body.code).toBe(0)

  const token = await apiToken(page)
  const response = await page.request.get(`/api/users?keyword=${username}&page=1&pageSize=20`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const list = (await response.json()) as { data: { items: { id: string; username: string }[] } }
  const matched = list.data.items.find((u) => u.username === username)
  expect(matched, '新建用户未出现在用户列表中').toBeTruthy()
  return matched!.id
}

/** 经接口创建员工（仅用于断言业务码，如账号绑定冲突 40146） */
async function createEmployeeViaApi(page: Page, payload: Record<string, unknown>): Promise<{ code: number }> {
  const token = await apiToken(page)
  const response = await page.request.post('/api/employees', {
    data: payload,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { code: number }
}

/** 清除本地凭证回到登录页 */
async function logout(page: Page): Promise<void> {
  await page.evaluate(() => window.localStorage.clear())
  await page.goto('/login')
}

/**
 * 提交抽屉表单并等待抽屉关闭（= 提交成功）。
 *
 * 不依赖 Message 断言判定完成：同文案 Message 在展示期内会堆叠（前端规则 §10.1），
 * 上一步操作留下的同类提示会让断言提前通过，导致后续步骤跑在旧数据上。
 * 抽屉 `unmount-on-close`，故以「抽屉消失」作为提交完成判据，并重试点击以覆盖 Arco Button 吞 click 的情形。
 */
async function submitDrawerAndWaitClose(page: Page): Promise<void> {
  await expect(async () => {
    if ((await drawer(page).count()) > 0) {
      await drawer(page)
        .getByRole('button', { name: '提交' })
        .click({ timeout: 3000 })
        .catch(() => {
          // 提交成功后抽屉随即卸载，click 可能以 detached 结束：忽略，由下方「抽屉消失」判据裁决
        })
    }
    await expect(drawer(page)).toHaveCount(0, { timeout: 5000 })
  }).toPass({ timeout: 20_000 })
}

/** 确认 popconfirm（行内删除 / 启停二次确认） */
async function confirmPopconfirm(page: Page): Promise<void> {
  await page.locator('.arco-popconfirm:visible').getByRole('button', { name: '确定' }).click()
}

/** 点「导出」并等待浏览器下载事件（Arco Button loading 期间会吞 click，故重试） */
async function clickExportAndWaitDownload(page: Page): Promise<Download> {
  let download: Download | null = null
  await expect(async () => {
    const wait = page.waitForEvent('download', { timeout: 5000 })
    await page.getByRole('button', { name: '导出', exact: true }).click()
    download = await wait
  }).toPass({ timeout: 30_000 })
  return download!
}

// ============================== 部门 ==============================

/** 打开部门抽屉（parentRow 为空表示新增顶级部门） */
async function openDepartmentDrawer(page: Page, parentRow?: Locator): Promise<Locator> {
  if (parentRow) {
    await parentRow.getByRole('button', { name: '新增下级' }).click()
  } else {
    await page.getByRole('button', { name: '新增顶级部门' }).click()
  }
  await expect(drawer(page).getByText('新增部门', { exact: true })).toBeVisible()
  return drawer(page)
}

/** 填写并提交部门表单（不等待结果，供正负用例各自断言） */
async function fillDepartmentForm(target: Locator, code: string, name: string): Promise<void> {
  await target.getByPlaceholder('1-20 字符，全局唯一').fill(code)
  await target.getByPlaceholder('1-50 字符，同一上级下唯一').fill(name)
  await target.getByRole('button', { name: '提交' }).click()
}

/** 新增部门（顶级或指定上级下），等待创建成功（以抽屉关闭为准） */
async function createDepartment(page: Page, code: string, name: string, parentRow?: Locator): Promise<void> {
  const target = await openDepartmentDrawer(page, parentRow)
  await fillDepartmentForm(target, code, name)
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '部门已创建')
}

/** 按部门名称定位所在行（树形表格展开后子行才渲染） */
function departmentRow(page: Page, name: string): Locator {
  return dataRows(page).filter({ hasText: name }).first()
}

// ============================== 岗位 ==============================

/** 新增岗位（等待抽屉关闭） */
async function createPosition(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增', exact: true }).click()
  await expect(drawer(page).getByText('新增岗位', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(code)
  await drawer(page).getByPlaceholder('1-50 字符，全局唯一').fill(name)
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '岗位已创建')
}

// ============================== 员工 ==============================

interface EmployeeDraft {
  employeeNo: string
  name: string
  phone?: string
  hireDate?: string
  departmentName?: string
  positionName?: string
  accountUsername?: string
}

/** 在抽屉内按 placeholder 定位下拉容器（a-select / a-tree-select 的可见控件） */
function selectByPlaceholder(page: Page, placeholder: string): Locator {
  return drawer(page).locator('.arco-select', { has: page.getByPlaceholder(placeholder) })
}

/** 唯一 11 位手机号（避免与历史数据 / 其它用例撞号导致 40147） */
function uniquePhone(): string {
  return `138${String(Date.now()).slice(-8)}`
}

/**
 * 填写员工表单的必填基础字段（工号 / 姓名 / 手机号）。
 * 入职日期单独在最后填（见 createEmployeeViaDrawer：日期面板可能覆盖下方字段）。
 */
async function fillEmployeeRequiredFields(
  page: Page,
  draft: { employeeNo: string; name: string; phone?: string; hireDate?: string },
): Promise<void> {
  await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(draft.employeeNo)
  await drawer(page).getByPlaceholder('1-50 字符').fill(draft.name)
  if (draft.phone) {
    await drawer(page).getByPlaceholder('选填，11 位手机号').fill(draft.phone)
  }
}

/**
 * 填入职日期（放在最后一步）：面板内键入后回车提交，再点抽屉标题收起面板。
 * 不用 Esc——Arco 抽屉 `escToClose` 默认开启且监听 document 级 keydown，Esc 会把整个抽屉关掉。
 */
async function fillHireDate(page: Page, value = '2026-01-01'): Promise<void> {
  const hireInput = drawer(page).getByPlaceholder('请选择入职日期')
  await hireInput.click()
  await hireInput.fill(value)
  await hireInput.press('Enter')
  await expect(hireInput).toHaveValue(value)
  await drawer(page).locator('.arco-drawer-header').click()
}

/** 新增员工（可选：部门 / 岗位 / 关联账号），等待抽屉关闭 */
async function createEmployeeViaDrawer(page: Page, draft: EmployeeDraft): Promise<void> {
  await page.getByRole('button', { name: '新增员工' }).click()
  await expect(drawer(page).getByText('新增员工', { exact: true })).toBeVisible()
  await fillEmployeeRequiredFields(page, draft)

  if (draft.departmentName) {
    // a-tree-select 的可见控件是 SelectView（无 arco-tree-select-trigger 类），故直接用 placeholder 输入框触发弹层
    await drawer(page).getByPlaceholder('不选表示未分配部门').click()
    await page.locator('.arco-tree-node-title-text:visible', { hasText: draft.departmentName }).first().click()
    await expect(page.locator('.arco-tree-node-title-text:visible')).toHaveCount(0)
  }

  if (draft.positionName) {
    const positionSelect = selectByPlaceholder(page, '不选表示未分配岗位')
    await positionSelect.click()
    await page.locator('.arco-select-option:visible', { hasText: draft.positionName }).first().click()
    await expect(positionSelect).toContainText(draft.positionName)
  }

  if (draft.accountUsername) {
    const accountSelect = selectByPlaceholder(page, '不绑定账号')
    await accountSelect.click()
    await page.locator('.arco-select-option:visible', { hasText: draft.accountUsername }).first().click()
    await expect(accountSelect).toContainText(draft.accountUsername)
  }

  // 日期选择放最后：面板可能覆盖其下方字段，避免影响部门 / 岗位 / 账号选择
  await fillHireDate(page, draft.hireDate)

  await drawer(page).getByRole('button', { name: '提交' }).click()
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '员工已创建')
}

test.describe('组织人事（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('部门树：新增三级部门并按同级重名被拒绝（40139）', async ({ page }) => {
    const rootName = uniqueName('总部')
    const childName = uniqueName('研发')
    const grandChildName = uniqueName('后端')
    await goPage(page, '部门管理', /\/departments$/)
    await expect(page.getByRole('columnheader', { name: '部门名称' })).toBeVisible()

    // 顶级 → 二级 → 三级
    await createDepartment(page, uniqueCode('d'), rootName)
    await createDepartment(page, uniqueCode('d'), childName, departmentRow(page, rootName))
    await createDepartment(page, uniqueCode('d'), grandChildName, departmentRow(page, childName))

    // 三级部门已渲染（新增下级后父节点自动展开）
    await expect(departmentRow(page, grandChildName)).toBeVisible()

    // 同一上级下重名：上级为 rootName，名称与二级部门相同 → 40139
    const target = await openDepartmentDrawer(page, departmentRow(page, rootName))
    await fillDepartmentForm(target, uniqueCode('d'), childName)
    await expectMessage(page, '同一上级下部门名称已存在')
    await target.getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)
  })

  test('部门删除保护：有子部门 / 有员工时被拒绝（40140）', async ({ page }) => {
    const parentName = uniqueName('分部')
    const childName = uniqueName('小组')
    const leafName = uniqueName('叶子')
    await goPage(page, '部门管理', /\/departments$/)

    await createDepartment(page, uniqueCode('d'), parentName)
    await createDepartment(page, uniqueCode('d'), childName, departmentRow(page, parentName))
    await createDepartment(page, uniqueCode('d'), leafName)

    // 有子部门 → 拒绝
    await departmentRow(page, parentName).getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '该部门存在子部门，不可删除')

    // 有员工 → 拒绝：经员工抽屉把员工挂到该叶子部门（同时覆盖员工表单的部门树选择）
    await goMenu(page, '员工档案', /\/employees$/)
    await createEmployeeViaDrawer(page, {
      employeeNo: uniqueCode('E'),
      name: uniqueName('员工'),
      departmentName: leafName,
    })

    await goMenu(page, '部门管理', /\/departments$/)
    await departmentRow(page, leafName).getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '该部门存在 1 名员工，不可删除')
  })

  test('岗位：编码 / 名称重复被拒绝（40142 / 40143）', async ({ page }) => {
    const code = uniqueCode('P')
    const name = uniqueName('岗位')
    await goPage(page, '岗位管理', /\/positions$/)

    await createPosition(page, code, name)

    // 编码重复 → 40142
    await page.getByRole('button', { name: '新增', exact: true }).click()
    await expect(drawer(page).getByText('新增岗位', { exact: true })).toBeVisible()
    await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(code)
    await drawer(page).getByPlaceholder('1-50 字符，全局唯一').fill(uniqueName('岗位'))
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '岗位编码已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 名称重复 → 40143
    await page.getByRole('button', { name: '新增', exact: true }).click()
    await expect(drawer(page).getByText('新增岗位', { exact: true })).toBeVisible()
    await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(uniqueCode('P'))
    await drawer(page).getByPlaceholder('1-50 字符，全局唯一').fill(name)
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '岗位名称已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)
  })

  test('岗位删除保护：被员工引用时被拒绝（40144）', async ({ page }) => {
    const code = uniqueCode('P')
    const name = uniqueName('岗位')
    await goPage(page, '岗位管理', /\/positions$/)
    await createPosition(page, code, name)

    // 经员工抽屉把员工挂到该岗位（同时覆盖员工表单的岗位下拉）
    await goMenu(page, '员工档案', /\/employees$/)
    await createEmployeeViaDrawer(page, {
      employeeNo: uniqueCode('E'),
      name: uniqueName('员工'),
      positionName: name,
    })

    await goMenu(page, '岗位管理', /\/positions$/)
    await page.getByPlaceholder('搜索岗位编码或名称').fill(code)
    await searchAndWaitHit(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '该岗位已被 1 名员工引用，不可删除')
  })

  test('员工：新增绑定账号 / 工号重复 / 账号绑定冲突（40145 / 40146）', async ({ page }) => {
    const accountUsername = uniqueName('acc').toLowerCase().slice(0, 20)
    const employeeNo = uniqueCode('E')
    const employeeName = uniqueName('员工')
    await goPage(page, '员工档案', /\/employees$/)
    const boundUserId = await createUserViaApi(page, accountUsername)

    // 新增员工并绑定账号
    await createEmployeeViaDrawer(page, {
      employeeNo,
      name: employeeName,
      phone: uniquePhone(),
      accountUsername,
    })

    // 列表可见并带在职标签
    await page.getByPlaceholder('搜索工号或姓名').fill(employeeNo)
    await searchAndWaitHit(page, employeeNo)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText(employeeName)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '在职' })).toBeVisible()

    // 工号重复 → 40145
    await page.getByRole('button', { name: '新增员工' }).click()
    await expect(drawer(page).getByText('新增员工', { exact: true })).toBeVisible()
    await fillEmployeeRequiredFields(page, { employeeNo, name: uniqueName('员工') })
    await fillHireDate(page)
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '工号已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 账号绑定冲突 → 40146（前端下拉已过滤掉被占用账号，边界在后端，故经接口断言）
    const conflict = await createEmployeeViaApi(page, {
      employeeNo: uniqueCode('E'),
      name: '绑定冲突员工',
      userId: boundUserId,
      hireDate: '2026-01-01',
      status: 1,
    })
    expect(conflict.code).toBe(40146)
  })

  test('员工：筛选 / 编辑 / 离职 / 导出', async ({ page }) => {
    const employeeNo = uniqueCode('E')
    const employeeName = uniqueName('员工')
    const renamed = `${employeeName}x`
    await goPage(page, '员工档案', /\/employees$/)

    await createEmployeeViaDrawer(page, { employeeNo, name: employeeName })

    // 筛选：关键字搜索确实生效（命中行出现且未命中行归零）
    await page.getByPlaceholder('搜索工号或姓名').fill(employeeNo)
    await searchAndWaitHit(page, employeeNo)
    await expect(dataRows(page)).toHaveCount(1)

    // 编辑：工号禁用（创建后不可改），姓名变更后列表反映
    await dataRows(page).first().getByRole('button', { name: '编辑' }).click()
    await expect(drawer(page).getByText('编辑员工', { exact: true })).toBeVisible()
    await expect(drawer(page).getByPlaceholder('1-20 字符，全局唯一')).toBeDisabled()
    await drawer(page).getByPlaceholder('1-50 字符').fill(renamed)
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await submitDrawerAndWaitClose(page)
    await expectMessage(page, '员工已更新')
    await expect(dataRows(page).first()).toContainText(renamed)

    // 离职：状态切换为「离职」（记录保留）
    await dataRows(page).first().getByRole('button', { name: '离职' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '已办理离职')
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '离职' })).toBeVisible()

    // 导出：文件名格式 `<域>_<yyyyMMddHHmm>.xlsx` 且落盘内容非空
    const download = await clickExportAndWaitDownload(page)
    expect(download.suggestedFilename()).toMatch(/^员工档案_\d{12}\.xlsx$/)
    const filePath = await download.path()
    expect(filePath).toBeTruthy()
    expect(statSync(filePath!).size).toBeGreaterThan(0)
  })

  test('无 employees.view 的账号：菜单不含员工档案、直达路由 403、接口 40300', async ({ page }) => {
    const roleName = uniqueName('r').slice(0, 20)
    const username = uniqueName('u').slice(0, 50)

    await loginAs(page, ADMIN.username, ADMIN.password)
    const roleId = await createRoleViaApi(page, roleName, [MIN_PERMISSION])
    await apiPost(page, '/users', {
      username,
      displayName: 'E2E 无员工权限',
      password: 'initPass123',
      roleIds: [roleId],
    })

    await logout(page)
    await loginAs(page, username, 'initPass123')

    // 菜单：无 employees.view → 无「员工档案」项；「系统」分组整体隐藏
    await expect(menuGroup(page, '系统')).toHaveCount(0)
    await expect(menuItem(page, '员工档案')).toHaveCount(0)

    // 直达路由 → 403 页
    await page.goto('/employees')
    await expect(page).toHaveURL(/\/403/)
    await expect(page.getByText('无访问权限')).toBeVisible()

    // 无权限接口 → 统一响应 code 40300
    const token = await apiToken(page)
    const response = await page.request.get('/api/employees?page=1&pageSize=20', {
      headers: { Authorization: `Bearer ${token}` },
    })
    const body = (await response.json()) as { code: number }
    expect(body.code).toBe(40300)
  })
})

import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem, menuGroup, menuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 管理员账号（来自项目 seed 数据） */
const ADMIN = { username: 'admin', password: 'admin123' }
/** 无 payroll.view 的角色所用权限点（仅商品查看） */
const MIN_PERMISSION = 'products.view'

/** 唯一编码（≤ 20 字符，与后端工号列长对齐） */
function uniqueCode(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`.slice(0, 20)
}

/** 唯一名称（姓名 / 角色名用） */
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

/** 弹窗容器（批量生成） */
function modal(page: Page): Locator {
  return page.locator('.arco-modal')
}

/** 经侧边菜单进入指定页面（Arco 菜单展开动画期会吞 click，故重试直到 URL 命中） */
async function openMenu(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await expect(async () => {
    if (!urlPattern.test(page.url())) {
      await clickMenuItem(page, menuName)
    }
    await expect(page).toHaveURL(urlPattern, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/**
 * 登录 → 建员工 fixture → 进页面。
 *
 * 顺序不可颠倒：两个页面的**员工下拉在页面挂载时一次性拉取**（`onMounted`），
 * 先建员工再进页面才能在下拉里选中它；先建页面后建员工会让下拉缺少该员工。
 */
async function goPageWithEmployee(
  page: Page,
  menuName: string,
  urlPattern: RegExp,
): Promise<{ id: string; name: string; employeeNo: string }> {
  await loginAs(page, ADMIN.username, ADMIN.password)
  const employee = await createEmployeeViaApi(page)
  await openMenu(page, menuName, urlPattern)
  return employee
}

/** 取当前登录态 token（接口 fixture 用） */
async function apiToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => window.localStorage.getItem('app:token') ?? '')
  expect(token, '登录态缺失，无法调用接口').not.toBe('')
  return token
}

/** 经接口 GET（按调用方给出的统一响应体形状返回） */
async function apiGet<T>(page: Page, url: string): Promise<T> {
  const token = await apiToken(page)
  const response = await page.request.get(`/api${url}`, { headers: { Authorization: `Bearer ${token}` } })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as T
}

/** 经接口 POST（返回统一响应体） */
async function apiPost(page: Page, url: string, data: unknown): Promise<{ code: number; data?: { id?: string } }> {
  const token = await apiToken(page)
  const response = await page.request.post(`/api${url}`, {
    data,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { code: number; data?: { id?: string } }
}

/**
 * 经接口创建在职员工（考勤 / 薪酬用例的主体），返回员工 id 与姓名。
 * 入职日取去年 1 月：批量生成按「在职 + 入职不晚于期间末」筛人，入职太晚会让本用例的员工不参与生成。
 */
async function createEmployeeViaApi(page: Page): Promise<{ id: string; name: string; employeeNo: string }> {
  const employeeNo = uniqueCode('E')
  const name = uniqueName('员工')
  const hireDate = `${new Date().getFullYear() - 1}-01-01`
  const body = await apiPost(page, '/employees', {
    employeeNo,
    name,
    hireDate,
    status: 1,
  })
  expect(body.code).toBe(0)
  return { id: body.data?.id ?? '', name, employeeNo }
}

/** 清除本地凭证回到登录页 */
async function logout(page: Page): Promise<void> {
  await page.evaluate(() => window.localStorage.clear())
  await page.goto('/login')
}

/** 在作用域内按 placeholder 定位下拉并选中指定文本的可见选项（specs/010 §10.1：限定 :visible 防命中隐藏旧浮层） */
async function selectByPlaceholder(page: Page, scope: Locator, placeholder: string, optionText: string): Promise<void> {
  const select = scope.locator('.arco-select', { has: page.getByPlaceholder(placeholder) })
  await select.click()
  await page.locator('.arco-select-option:visible', { hasText: optionText }).first().click()
}

/**
 * 填日期（放在最后一步）：面板内键入后回车提交，再点抽屉标题收起面板。
 * 不用 Esc——Arco 抽屉 `escToClose` 默认开启且监听 document 级 keydown，Esc 会把整个抽屉关掉。
 */
async function fillDate(page: Page, placeholder: string, value: string): Promise<void> {
  const input = drawer(page).getByPlaceholder(placeholder)
  await input.click()
  await input.fill(value)
  await input.press('Enter')
  await expect(input).toHaveValue(value)
  await drawer(page).locator('.arco-drawer-header').click()
}

/**
 * 提交抽屉表单并等待抽屉关闭（= 提交成功）。
 * 不依赖 Message 断言判定完成：同文案 Message 在展示期内会堆叠，上一步的同类提示会让断言提前通过。
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

/**
 * 填金额输入框。
 *
 * Arco `a-input-number` 在 **blur** 时才把输入值提交到 `v-model`（输入过程中只保留内部文本态），
 * 故必须显式失焦——否则最后一个字段的值进不了表单模型，实发预览少算一项。
 */
async function fillAmount(page: Page, placeholder: string, value: string): Promise<void> {
  const input = drawer(page).getByPlaceholder(placeholder)
  await input.fill(value)
  await input.blur()
}

/** 确认 popconfirm（行内二次确认） */
async function confirmPopconfirm(page: Page): Promise<void> {
  await page.locator('.arco-popconfirm:visible').getByRole('button', { name: '确定' }).click()
}

/** 筛选下拉选中员工并点搜索直到命中 1 行 */
async function filterByEmployee(page: Page, scope: Locator, placeholder: string, name: string): Promise<void> {
  await selectByPlaceholder(page, scope, placeholder, name)
  await clickUntilCount(page, '搜索', dataRows(page), 1)
}

test.describe('人事：考勤与薪酬（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('考勤登记：新增 / 同类型区间重叠被拒 / 编辑 / 删除', async ({ page }) => {
    const employee = await goPageWithEmployee(page, '考勤登记', /\/attendance$/)
    await expect(page.getByRole('columnheader', { name: '员工' })).toBeVisible()

    // 新增：请假 2026-09-21 ~ 2026-09-22（2 天）
    await page.getByRole('button', { name: '新增登记' }).click()
    await expect(drawer(page).getByText('新增考勤登记', { exact: true })).toBeVisible()
    await selectByPlaceholder(page, drawer(page), '请选择员工', employee.name)
    await fillDate(page, '请选择起始日', '2026-09-21')
    await fillDate(page, '请选择结束日', '2026-09-22')
    await submitDrawerAndWaitClose(page)
    await expectMessage(page, '考勤记录已创建')

    // 筛选确实生效：仅该员工 1 行，类型标签为请假，天数为 2
    await filterByEmployee(page, page.locator('.toolbar-filter'), '全部员工（在职）', employee.name)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '请假' })).toBeVisible()
    await expect(dataRows(page).first()).toContainText('2026-09-21')
    await expect(dataRows(page).first()).toContainText('2026-09-22')

    // 同员工同类型区间重叠 → 40000（message 含冲突区间）
    await page.getByRole('button', { name: '新增登记' }).click()
    await expect(drawer(page).getByText('新增考勤登记', { exact: true })).toBeVisible()
    await selectByPlaceholder(page, drawer(page), '请选择员工', employee.name)
    await fillDate(page, '请选择起始日', '2026-09-22')
    await fillDate(page, '请选择结束日', '2026-09-23')
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '日期不可重叠')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 编辑：类型改为加班、日期改为 09-25 ~ 09-26
    await dataRows(page).first().getByRole('button', { name: '编辑' }).click()
    await expect(drawer(page).getByText('编辑考勤登记', { exact: true })).toBeVisible()
    await drawer(page).locator('.arco-radio', { hasText: '加班' }).click()
    await fillDate(page, '请选择起始日', '2026-09-25')
    await fillDate(page, '请选择结束日', '2026-09-26')
    await submitDrawerAndWaitClose(page)
    await expectMessage(page, '考勤记录已更新')
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '加班' })).toBeVisible()

    // 删除：二次确认后行归零（空状态行）
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '考勤记录已删除')
    await expect(dataRows(page)).toHaveCount(0)
  })

  test('薪酬：新增（实发自动计算）/ 期间重复 40170 / 发放后锁定 40171 / 批量生成幂等', async ({ page }) => {
    const employee = await goPageWithEmployee(page, '薪酬', /\/payrolls$/)
    await expect(page.getByRole('columnheader', { name: '实发' })).toBeVisible()

    // 新增：基本工资 1000 + 津贴 200 − 扣款 50 = 实发 1150.00（实时预览）
    await page.getByRole('button', { name: '新增工资单' }).click()
    await expect(drawer(page).getByText('新增工资单', { exact: true })).toBeVisible()
    await selectByPlaceholder(page, drawer(page), '请选择员工', employee.name)
    await fillAmount(page, '请输入基本工资', '1000')
    await fillAmount(page, '请输入津贴', '200')
    await fillAmount(page, '请输入扣款', '50')
    await expect(drawer(page).locator('.net-pay-preview')).toHaveText('1150.00')
    await submitDrawerAndWaitClose(page)
    await expectMessage(page, '工资单已创建')

    // 筛选确实生效：仅该员工 1 行，草稿状态且实发为 1150.00
    await filterByEmployee(page, page.locator('.toolbar-filter'), '全部员工', employee.name)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '草稿' })).toBeVisible()
    await expect(dataRows(page).first()).toContainText('1150.00')

    // 该员工该期间重复新增 → 40170（默认期间与上一条相同：当年当月）
    await page.getByRole('button', { name: '新增工资单' }).click()
    await expect(drawer(page).getByText('新增工资单', { exact: true })).toBeVisible()
    await selectByPlaceholder(page, drawer(page), '请选择员工', employee.name)
    await fillAmount(page, '请输入基本工资', '1')
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '该员工该期间的工资单已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 发放 → 已发放；状态变更后编辑 / 发放 / 删除三个行内操作全部置灰
    await dataRows(page).first().getByRole('button', { name: '发放' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '工资单已发放')
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '已发放' })).toBeVisible()
    await expect(dataRows(page).first().getByRole('button', { name: '编辑' })).toBeDisabled()
    await expect(dataRows(page).first().getByRole('button', { name: '发放' })).toBeDisabled()
    await expect(dataRows(page).first().getByRole('button', { name: '删除' })).toBeDisabled()

    // 后端同样锁定：编辑 / 删除均返回 40171（前端置灰只是第一道防线）
    const list = await apiGet<{ data: { items: { id: string }[] } }>(
      page,
      `/payrolls?employeeId=${employee.id}&page=1&pageSize=20`,
    )
    expect(list.data.items).toHaveLength(1)
    const payrollId = list.data.items[0].id
    const token = await apiToken(page)
    const editResponse = await page.request.put(`/api/payrolls/${payrollId}`, {
      data: { baseSalary: 1, allowance: 0, deduction: 0 },
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await editResponse.json()) as { code: number }).code).toBe(40171)
    const deleteResponse = await page.request.delete(`/api/payrolls/${payrollId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await deleteResponse.json()) as { code: number }).code).toBe(40171)

    // 批量生成：选上一年 12 月（早于当前期间），该期间无工资单 → 为在职员工新增草稿
    const now = new Date()
    const targetYear = now.getFullYear() - 1
    await page.getByRole('button', { name: '批量生成' }).click()
    await expect(modal(page).getByText('批量生成工资单', { exact: true })).toBeVisible()
    await selectByPlaceholder(page, modal(page), '年份', `${targetYear} 年`)
    await selectByPlaceholder(page, modal(page), '月份', '12 月')
    await modal(page).getByRole('button', { name: '生成' }).click()
    await expectMessage(page, '批量生成完成：新增')
    await expect(modal(page)).toBeHidden()

    const generated = await apiGet<{ data: { items: { status: number; baseSalary: number }[] } }>(
      page,
      `/payrolls?year=${targetYear}&month=12&employeeId=${employee.id}&page=1&pageSize=20`,
    )
    expect(generated.data.items).toHaveLength(1)
    expect(generated.data.items[0].status).toBe(0)
    expect(generated.data.items[0].baseSalary).toBe(0)

    // 幂等：同一期间再次生成 → 该员工被跳过（仍只有 1 条）
    await page.getByRole('button', { name: '批量生成' }).click()
    await selectByPlaceholder(page, modal(page), '年份', `${targetYear} 年`)
    await selectByPlaceholder(page, modal(page), '月份', '12 月')
    await modal(page).getByRole('button', { name: '生成' }).click()
    // Message 是单行「批量生成完成：新增 X 条、跳过 Y 条」，故只断言前缀；
    // 「确实跳过（未重复新增）」以后续接口断言的总条数仍为 1 为准
    await expectMessage(page, '批量生成完成')
    await expect(modal(page)).toBeHidden()

    const regenerated = await apiGet<{ data: { total: number } }>(
      page,
      `/payrolls?year=${targetYear}&month=12&employeeId=${employee.id}&page=1&pageSize=20`,
    )
    expect(regenerated.data.total).toBe(1)
  })

  test('无 payroll.view 的账号：菜单不含考勤 / 薪酬、直达路由 403、接口 40300', async ({ page }) => {
    const roleName = uniqueName('r').slice(0, 20)
    const username = uniqueName('u').slice(0, 50)

    await loginAs(page, ADMIN.username, ADMIN.password)
    const role = await apiPost(page, '/roles', { name: roleName, permissionKeys: [MIN_PERMISSION] })
    expect(role.code).toBe(0)
    await apiPost(page, '/users', {
      username,
      displayName: 'E2E 无薪酬权限',
      password: 'initPass123',
      roleIds: [role.data?.id ?? ''],
    })

    await logout(page)
    await loginAs(page, username, 'initPass123')

    // 菜单：「人事」分组整体隐藏（组内已登记菜单项均不可见）
    await expect(menuGroup(page, '人事')).toHaveCount(0)
    await expect(menuItem(page, '考勤登记')).toHaveCount(0)
    await expect(menuItem(page, '薪酬')).toHaveCount(0)

    // 直达路由 → 403 页
    await page.goto('/payrolls')
    await expect(page).toHaveURL(/\/403/)
    await expect(page.getByText('无访问权限')).toBeVisible()

    // 无权限接口 → 统一响应 code 40300
    const token = await apiToken(page)
    const response = await page.request.get('/api/attendances?page=1&pageSize=20', {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await response.json()) as { code: number }).code).toBe(40300)
  })
})

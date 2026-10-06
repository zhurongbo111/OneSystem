import { expect, test, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 生成唯一名称（线索 / 商机 / 客户 / 员工共用前缀） */
function uniqueName(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 登录后经侧边菜单进入指定页面（菜单点击被吞时重试，前端规则 §10.1） */
async function goMenu(page: Page, name: string, url: RegExp): Promise<void> {
  await login(page)
  await expect(async () => {
    await clickMenuItem(page, name)
    await expect(page).toHaveURL(url, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 经侧边菜单进入线索页（043：CRM 分组） */
async function goLeads(page: Page): Promise<void> {
  await goMenu(page, '线索', /\/leads$/)
}

/** 经侧边菜单进入商机页（043：CRM 分组） */
async function goOpportunities(page: Page): Promise<void> {
  await goMenu(page, '商机', /\/opportunities$/)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 抽屉容器 / 抽屉标题（限定在抽屉内，避免命中同名按钮） */
function drawer(page: Page): Locator {
  return page.locator('.arco-drawer')
}

function drawerTitle(page: Page, title: string): Locator {
  return page.locator('.arco-drawer-title', { hasText: title })
}

/** 详情页描述项（Arco descriptions 渲染为表格行） */
function descRow(page: Page, label: string): Locator {
  return page.locator('tr', { hasText: label })
}

/** 取当前登录态 token（接口 fixture / 断言用） */
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

/** 员工 fixture（线索 / 商机负责人下拉数据源；组织人事 UI 行为不在本规格范围） */
async function createEmployeeFixture(page: Page, name: string): Promise<void> {
  const body = await apiPost(page, '/employees', {
    employeeNo: `E${Date.now().toString(36)}`,
    name,
    hireDate: '2026-01-01',
    status: 1,
  })
  expect(body.code).toBe(0)
}

/** 客户 fixture（商机关联客户；应收断言的对象） */
async function createCustomerFixture(page: Page, name: string): Promise<void> {
  const body = await apiPost(page, '/partners', { name, type: 2 })
  expect(body.code).toBe(0)
}

/**
 * 在 Arco search-select 中搜索并选中唯一匹配项（Enter 确认高亮项）。
 * Arco 选中后旧弹层 DOM 残留且选项会被过滤隐藏，点选项不可靠；Enter 作用于当前聚焦 select。
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

/** 在 Arco select 中按选项文案选中（非搜索型下拉） */
async function selectOption(page: Page, select: Locator, label: string): Promise<void> {
  await select.click()
  await page.locator('.arco-select-option:visible', { hasText: label }).first().click()
  await expect(select).toContainText(label)
}

/** 点击按钮直到页面跳转到目标路由（跳转类按钮会被吞点击） */
async function clickUntilUrl(page: Page, buttonName: string, url: RegExp): Promise<void> {
  await clickUntil(page, buttonName, async () => {
    await expect(page).toHaveURL(url)
  })
}

/** 列表内按关键词搜索并返回唯一命中行 */
async function findRow(page: Page, placeholder: string, keyword: string): Promise<Locator> {
  await page.getByPlaceholder(placeholder).fill(keyword)
  await searchAndWaitHit(page, keyword)
  await expect(dataRows(page)).toHaveCount(1)
  return dataRows(page).first()
}

/** 列表抽屉新建线索（可选来源 / 负责人，状态默认「新线索」） */
async function createLead(
  page: Page,
  name: string,
  options: { sourceLabel?: string; ownerName?: string } = {},
): Promise<void> {
  await clickUntil(page, '新建线索', drawerTitle(page, '新增线索'))
  const box = drawer(page)
  await box.getByPlaceholder('1-50 字符').fill(name)
  if (options.sourceLabel) {
    await selectOption(page, box.locator('.arco-select').nth(0), options.sourceLabel)
  }
  if (options.ownerName) {
    await selectBySearch(box.locator('.arco-select').nth(2), options.ownerName)
  }
  await box.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '线索已创建')
  await expect(drawerTitle(page, '新增线索')).toHaveCount(0)
}

/** 详情页新增一条跟进记录（线索 / 商机共用：抽屉标题与字段一致） */
async function addActivity(page: Page, content: string, typeLabel: string): Promise<void> {
  await clickUntil(page, '新增跟进', drawerTitle(page, '新增跟进记录'))
  const box = drawer(page)
  await selectOption(page, box.locator('.arco-select').nth(0), typeLabel)
  await box.getByPlaceholder('1-200 字符').fill(content)
  await box.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '跟进记录已添加')
  await expect(drawerTitle(page, '新增跟进记录')).toHaveCount(0)
  await expect(page.locator('.activity-item__content', { hasText: content })).toBeVisible()
}

/** 读取库存台账总行数（线索 / 商机不应产生任何库存行） */
async function inventoryTotal(page: Page): Promise<number> {
  const token = await apiToken(page)
  const response = await page.request.get('/api/inventory?page=1&pageSize=1', {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number; data: { total: number } }
  expect(body.code).toBe(0)
  return body.data.total
}

/** 读取往来对账的应收金额（按客户名查；线索 / 商机不产生应收 → 0） */
async function reconciliationReceivable(page: Page, keyword: string): Promise<number> {
  const token = await apiToken(page)
  const response = await page.request.get(`/api/reconciliation?page=1&pageSize=20&keyword=${keyword}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as { code: number; data: { items: { receivableAmount: number }[] } }
  expect(body.code).toBe(0)
  return body.data.items[0]?.receivableAmount ?? 0
}

test.describe('CRM 售前（043）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('建线索（来源 / 负责人）→ 加跟进 → 转商机 → 线索已转化、商机出现', async ({ page }) => {
    const ownerName = uniqueName('crm_owner')
    const leadName = uniqueName('crm_lead')
    const activityContent = `首次电话沟通 ${Date.now()}`

    await goLeads(page)
    await createEmployeeFixture(page, ownerName)

    // 新建线索：来源「展会」、负责人选员工，状态默认「新线索」
    await createLead(page, leadName, { sourceLabel: '展会', ownerName })

    const row = await findRow(page, '搜索线索号 / 名称 / 联系人', leadName)
    await expect(row).toContainText('新线索')
    await expect(row).toContainText('展会')
    await expect(row).toContainText(ownerName)

    // 详情：基本信息完整 + 新增一条「电话」跟进
    await clickUntilUrl(page, '查看', /\/leads\/detail\//)
    await expect(descRow(page, '状态')).toContainText('新线索')
    await expect(descRow(page, '来源')).toContainText('展会')
    await expect(descRow(page, '负责人')).toContainText(ownerName)
    await addActivity(page, activityContent, '电话')

    // 转商机：二次确认后线索置「已转化」并回填商机号
    await clickUntil(page, '转商机', page.getByRole('button', { name: '确认转商机' }))
    await page.getByRole('button', { name: '确认转商机' }).click()
    await expectMessage(page, '已转商机')

    await expect(descRow(page, '状态')).toContainText('已转化')
    const opportunityNoCell = descRow(page, '转出商机')
    await expect(opportunityNoCell).toContainText('OP')
    const opportunityNo = (await opportunityNoCell.innerText()).match(/OP\d+/)?.[0] ?? ''
    expect(opportunityNo, '转商机后未回填商机号').not.toBe('')

    // 终态线索锁定：不再有编辑 / 转商机 / 废弃入口
    await expect(page.getByRole('button', { name: '转商机' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: '废弃' })).toHaveCount(0)

    // 商机列表出现该商机（名称取线索名、阶段默认初步接洽）
    await goOpportunities(page)
    const opportunityRow = await findRow(page, '搜索商机号 / 名称', leadName)
    await expect(opportunityRow).toContainText(opportunityNo)
    await expect(opportunityRow).toContainText('初步接洽')
  })

  test('线索废弃后为终态：不可再转商机（40168）', async ({ page }) => {
    const leadName = uniqueName('crm_abandon')

    await goLeads(page)
    await createLead(page, leadName)

    // 操作列「更多」→ 废弃（二次确认）
    const row = await findRow(page, '搜索线索号 / 名称 / 联系人', leadName)
    await row.getByRole('button', { name: '更多操作' }).click()
    await page.locator('.arco-dropdown-option', { hasText: '废弃' }).click()
    await page.getByRole('button', { name: '确认废弃' }).click()
    await expectMessage(page, '线索已废弃')

    await searchAndWaitHit(page, leadName)
    const updated = dataRows(page).first()
    await expect(updated).toContainText('已废弃')
    await expect(updated.getByRole('button', { name: '编辑' })).toHaveCount(0)
    await expect(updated.getByRole('button', { name: '转商机' })).toHaveCount(0)

    // 接口：已废弃线索再转商机 → 40168
    const token = await apiToken(page)
    const listResponse = await page.request.get(`/api/leads?keyword=${leadName}&page=1&pageSize=20`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const listBody = (await listResponse.json()) as { code: number; data: { items: { id: string }[] } }
    expect(listBody.code).toBe(0)
    const leadId = listBody.data.items[0]?.id ?? ''
    expect(leadId, '未找到已废弃线索').not.toBe('')

    const convertResponse = await page.request.post(`/api/leads/${leadId}/convert`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await convertResponse.json()) as { code: number }).code).toBe(40168)
  })

  test('商机新建 / 编辑 / 阶段推进至赢单（终态 40169）且不影响库存与应收', async ({ page }) => {
    const customerName = uniqueName('crm_cust')
    const ownerName = uniqueName('crm_owner')
    const opportunityName = uniqueName('crm_opp')
    const renamedOpportunity = `${opportunityName}x`

    await goOpportunities(page)
    await createCustomerFixture(page, customerName)
    await createEmployeeFixture(page, ownerName)
    const stockBefore = await inventoryTotal(page)

    // 新建商机：客户 / 金额 / 阶段 / 预计成交日期 / 负责人
    await clickUntilUrl(page, '新建商机', /\/opportunities\/new$/)
    await page.getByPlaceholder('1-50 字符').fill(opportunityName)
    await selectBySearch(page.locator('.arco-select').nth(0), customerName)
    await page.locator('.arco-input-number input').first().fill('8888.88')
    await selectOption(page, page.locator('.arco-select').nth(1), '需求确认')
    // 日期选择器 placeholder 恰为「选填」（其余「选填，…」须排除），面板收起后再点负责人下拉
    const expectedCloseInput = page.getByPlaceholder('选填', { exact: true })
    await expectedCloseInput.click()
    await expectedCloseInput.fill('2026-12-31')
    await expectedCloseInput.press('Enter')
    await expect(expectedCloseInput).toHaveValue('2026-12-31')
    await page.keyboard.press('Escape')
    await selectBySearch(page.locator('.arco-select').nth(2), ownerName)
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '商机已创建')
    await expect(page).toHaveURL(/\/opportunities\/detail\//)

    // 详情：客户 / 金额 / 阶段 / 负责人回显正确
    await expect(descRow(page, '名称')).toContainText(opportunityName)
    await expect(descRow(page, '客户')).toContainText(customerName)
    await expect(descRow(page, '预计金额')).toContainText('¥ 8888.88')
    await expect(descRow(page, '阶段')).toContainText('需求确认')
    await expect(descRow(page, '负责人')).toContainText(ownerName)

    // 编辑：名称 / 金额变更后回详情
    await clickUntilUrl(page, '编辑', /\/opportunities\/edit\//)
    // 等详情回填完成再改值：回填是异步请求，过早 fill 会被回填结果覆盖（与单据表单页同约定）
    const nameInput = page.getByPlaceholder('1-50 字符')
    await expect(nameInput).toHaveValue(opportunityName)
    await nameInput.fill(renamedOpportunity)
    const amountInput = page.locator('.arco-input-number input').first()
    await expect(amountInput).toHaveValue('8888.88')
    await amountInput.fill('9999.99')
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '商机已保存')
    await expect(page).toHaveURL(/\/opportunities\/detail\//)
    await expect(descRow(page, '名称')).toContainText(renamedOpportunity)
    await expect(descRow(page, '预计金额')).toContainText('¥ 9999.99')

    // 追加一条跟进记录（商机时间线）
    await addActivity(page, `方案讲解 ${Date.now()}`, '拜访')

    // 阶段推进至赢单
    const opportunityId = page.url().split('/detail/')[1] as string
    await selectOption(page, page.locator('.arco-select').nth(0), '赢单')
    await clickUntil(page, '推进阶段', async () => {
      await expect(descRow(page, '阶段')).toContainText('赢单')
    })
    await expectMessage(page, '阶段已推进至赢单')

    // 终态：阶段下拉禁用（Arco select 以 arco-select-view-disabled 类表达禁用态），接口再改阶段 → 40169
    await expect(page.locator('.arco-select').nth(0)).toHaveClass(/arco-select-view-disabled/)
    const token = await apiToken(page)
    const stageResponse = await page.request.put(`/api/opportunities/${opportunityId}/stage`, {
      data: { stage: 3 },
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(((await stageResponse.json()) as { code: number }).code).toBe(40169)

    // 售前不触碰交易：库存台账行数不变、客户应收为 0
    expect(await inventoryTotal(page)).toBe(stockBefore)
    expect(await reconciliationReceivable(page, customerName)).toBe(0)
  })
})

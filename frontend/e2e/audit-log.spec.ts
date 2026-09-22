import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 后端接口根地址（fixture 经接口构造，避免用例依赖上游表单流程） */
const API_BASE = 'http://localhost:5080'
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 分类名输入框 placeholder（全角括号） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 统一响应信封 */
interface ApiEnvelope<T> {
  code: number
  message: string
  data: T
}

interface CreatedCategory {
  id: string
}

interface CreatedProduct {
  id: string
}

interface CreatedPartner {
  id: string
}

interface CreatedReceipt {
  id: string
  receiptNo: string
}

/** 唯一后缀（字母数字，满足编码 / 名称约束） */
function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

/** 唯一商品编码（2-32 位字母 / 数字 / 下划线 / 连字符） */
function uniqueProductCode(): string {
  return `E2E_AL_${uniqueSuffix()}`
}

/** 唯一分类 / 往来单位名 */
function uniqueName(prefix: string): string {
  return `${prefix}${uniqueSuffix()}`
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 当前表格数据行（排除空状态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 表格行内按列序号取单元格：0 序号 / 1 操作时间 / 2 操作人 / 3 资源类型 / 4 动作 / 5 业务标识 / 6 摘要 / 7 操作 */
function cell(row: Locator, index: number): Locator {
  return row.locator('td').nth(index)
}

/** 抽屉标题（exact 精确匹配，避免命中列表里同名文本） */
function drawerTitle(page: Page, title: string): Locator {
  return page.getByText(title, { exact: true })
}

/** 经侧边菜单进入商品管理页 */
async function goProducts(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
}

/** 经侧边菜单进入采购入库页 */
async function goPurchases(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购入库')
  await expect(page).toHaveURL(/\/purchases$/)
}

/** 经侧边菜单进入操作日志页 */
async function goAuditLogs(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '操作日志')
  await expect(page).toHaveURL(/\/audit-logs$/)
}

/** 按关键词检索操作日志（服务端查询） */
async function searchAuditLogs(page: Page, keyword: string): Promise<void> {
  await page.getByPlaceholder('搜索业务标识 / 操作人').fill(keyword)
  await searchAndWaitHit(page, keyword)
}

/**
 * 在指定筛选下拉中选中选项。
 * 选项按 `:visible` 限定并精确匹配文本（弹层 DOM 残留，且「商品」是「商品分类」的子串）。
 */
async function selectFilterOption(page: Page, selectSelector: string, optionText: string): Promise<void> {
  await page.locator(selectSelector).click()
  await page
    .locator('.arco-select-option:visible')
    .filter({ hasText: new RegExp(`^${optionText}$`) })
    .first()
    .click()
  await expect(page.locator(selectSelector)).toContainText(optionText)
}

/** 经抽屉新增商品（分类就地新建），采购价固定 10.50 */
async function createProductViaUi(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(drawerTitle(page, '新增商品')).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const categoryInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(categoryInput).toBeVisible()
  await categoryInput.fill(uniqueName('cat'))
  await page.locator('.arco-drawer').getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const priceInputs = page.locator('.arco-drawer .arco-input-number input')
  await priceInputs.nth(0).fill('10.50')
  await priceInputs.nth(1).fill('20.00')
  await page.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(drawerTitle(page, '新增商品')).toHaveCount(0)
}

/** 按编码进入指定商品行 */
async function openProductRow(page: Page, code: string): Promise<Locator> {
  await page.getByPlaceholder('搜索商品编码或名称').fill(code)
  await searchAndWaitHit(page, code)
  const row = dataRows(page).first()
  await expect(row).toContainText(code)
  return row
}

/**
 * 点含指定提示文案的 popconfirm 浮层里的「确定」按钮（Arco 浮层 DOM 残留，需按提示文案锁定目标浮层）。
 */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

/** 接口登录并取 token（fixture 构造用） */
async function apiLogin(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${API_BASE}/api/auth/login`, { data: CREDENTIALS })
  const body = (await response.json()) as ApiEnvelope<{ token: string }>
  expect(body.code).toBe(0)
  return body.data.token
}

/** 接口 POST 并返回 data（断言 code = 0） */
async function postData<T>(
  request: APIRequestContext,
  token: string,
  path: string,
  data: unknown,
): Promise<T> {
  const response = await request.post(`${API_BASE}/api${path}`, {
    data,
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as ApiEnvelope<T>
  expect(body.code).toBe(0)
  return body.data
}

/** 接口新增商品（连带新建分类），返回业务码（0 = 成功） */
async function postProduct(
  request: APIRequestContext,
  token: string,
  categoryId: string,
  code: string,
): Promise<number> {
  const response = await request.post(`${API_BASE}/api/products`, {
    data: {
      code,
      name: uniqueName('商品'),
      categoryId,
      unit: '个',
      purchasePrice: 10,
      salePrice: 20,
      safetyStock: 0,
    },
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as ApiEnvelope<CreatedProduct>
  return body.code
}

/** 接口新增商品（自建分类），成功即产生一条「商品 × 新增」日志 */
async function createProductViaApi(request: APIRequestContext, code: string): Promise<void> {
  const token = await apiLogin(request)
  const category = await postData<CreatedCategory>(request, token, '/categories', {
    name: uniqueName('cat'),
  })
  expect(await postProduct(request, token, category.id, code)).toBe(0)
}

/** 接口构造一张可作废的采购入库单，返回单号 */
async function createReceiptViaApi(request: APIRequestContext): Promise<string> {
  const token = await apiLogin(request)
  const category = await postData<CreatedCategory>(request, token, '/categories', {
    name: uniqueName('cat'),
  })
  const product = await postData<CreatedProduct>(request, token, '/products', {
    code: uniqueProductCode(),
    name: uniqueName('商品'),
    categoryId: category.id,
    unit: '个',
    purchasePrice: 10,
    salePrice: 20,
    safetyStock: 0,
  })
  const partner = await postData<CreatedPartner>(request, token, '/partners', {
    name: uniqueName('供应商'),
    type: 1,
  })
  const receipt = await postData<CreatedReceipt>(request, token, '/purchase-receipts', {
    partnerId: partner.id,
    orderDate: new Date().toISOString(),
    items: [{ productId: product.id, quantity: 1, unitPrice: 10 }],
  })
  return receipt.receiptNo
}

test.describe('操作日志（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('菜单进入操作日志页并展示表头与分页', async ({ page }) => {
    await goAuditLogs(page)
    await expect(page.getByRole('heading', { name: '操作日志' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '操作时间' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '资源类型' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: '摘要' })).toBeVisible()
    // 空表（total=0）时 Arco 隐藏分页，改为断言「暂无数据」行或「共 N 条」之一可见
    await expect(page.locator('tbody .arco-table-tr-empty').or(page.locator('text=/共 \\d+ 条/'))).toBeVisible()
  })

  test('编辑商品采购价后在日志中出现记录，详情含字段级差异', async ({ page }) => {
    const code = uniqueProductCode()

    await goProducts(page)
    await createProductViaUi(page, code, uniqueName('审计商品'))
    const row = await openProductRow(page, code)
    await row.getByRole('button', { name: '编辑' }).click()
    await expect(drawerTitle(page, '编辑商品')).toBeVisible()
    // 详情回填完成前字段禁用，fill 会等待可用；第 0 个金额输入为采购价
    await page.locator('.arco-drawer .arco-input-number input').nth(0).fill('8.80')
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '商品已更新')

    await goAuditLogs(page)
    await searchAuditLogs(page, code)

    // 该商品产生两条日志：新增 + 编辑（本次改动）
    const editRow = dataRows(page).filter({ hasText: '编辑' })
    await expect(editRow).toHaveCount(1)
    await expect(cell(editRow.first(), 3)).toContainText('商品')
    await expect(cell(editRow.first(), 4)).toContainText('编辑')
    await expect(cell(editRow.first(), 5)).toHaveText(code)

    // 详情抽屉：字段级差异呈现「采购价 10.50 → 8.80」
    await editRow.first().getByRole('button', { name: '详情' }).click()
    const drawer = page.locator('.arco-drawer').last()
    await expect(drawer).toContainText('编辑商品')
    await expect(drawer).toContainText(code)
    const priceRow = drawer.locator('tbody tr', { hasText: '采购价' })
    await expect(priceRow).toContainText('10.50')
    await expect(priceRow).toContainText('8.80')
  })

  test('作废采购入库单后日志动作为作废，业务标识为单号', async ({ page, request }) => {
    const receiptNo = await createReceiptViaApi(request)

    await goPurchases(page)
    await page.getByPlaceholder('搜索单号 / 供应商').fill(receiptNo)
    await searchAndWaitHit(page, receiptNo)
    await dataRows(page).first().getByRole('button', { name: '作废' }).click()
    await confirmPopconfirm(page, '确认作废该采购单？')
    await expectMessage(page, '已作废，库存已回冲')

    await goAuditLogs(page)
    await searchAuditLogs(page, receiptNo)

    // 该单据产生两条日志：开具 + 作废
    const voidRow = dataRows(page).filter({ hasText: '作废' })
    await expect(voidRow).toHaveCount(1)
    await expect(cell(voidRow.first(), 3)).toContainText('采购入库单')
    await expect(cell(voidRow.first(), 4)).toContainText('作废')
    await expect(cell(voidRow.first(), 5)).toHaveText(receiptNo)

    await voidRow.first().getByRole('button', { name: '详情' }).click()
    const drawer = page.locator('.arco-drawer').last()
    await expect(drawer).toContainText(receiptNo)
    await expect(drawer).toContainText('已作废')
  })

  test('业务失败（重复商品编码）不产生新日志', async ({ page, request }) => {
    const code = uniqueProductCode()
    const token = await apiLogin(request)
    const category = await postData<CreatedCategory>(request, token, '/categories', {
      name: uniqueName('cat'),
    })
    // 首次新增成功；同编码再次新增被业务规则拒绝（40101）
    expect(await postProduct(request, token, category.id, code)).toBe(0)
    expect(await postProduct(request, token, category.id, code)).not.toBe(0)

    await goAuditLogs(page)
    await searchAuditLogs(page, code)

    // 仅首次新增留下日志；被拒绝的第二次新增无记录
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText('新增')
  })

  test('筛选资源类型 / 动作与关键词未命中空状态', async ({ page, request }) => {
    const code = uniqueProductCode()
    await createProductViaApi(request, code)

    await goAuditLogs(page)
    await searchAuditLogs(page, code)
    await expect(dataRows(page)).toHaveCount(1)

    // 资源类型 = 商品：该编码的日志仍命中
    await selectFilterOption(page, '.filter-bar__resource', '商品')
    await searchAndWaitHit(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(cell(dataRows(page).first(), 3)).toContainText('商品')

    // 叠加动作 = 编辑：该编码只有「新增」日志 → 空状态
    await selectFilterOption(page, '.filter-bar__action', '编辑')
    await clickUntilCount(page, '搜索', page.locator('tbody .arco-table-tr-empty'), 1)

    // 关键词未命中 → 空状态
    await page.getByPlaceholder('搜索业务标识 / 操作人').fill('NO_SUCH_AUDIT_LOG_ZZZ')
    await clickUntilCount(page, '搜索', page.locator('tbody .arco-table-tr-empty'), 1)
  })
})

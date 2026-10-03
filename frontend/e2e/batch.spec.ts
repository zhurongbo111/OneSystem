import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/**
 * 批次与保质期管理（specs/040-erp-batch-expiry）阶段七 E2E：
 * - 7.1 商品开启按批次 → 建近效期 / 已过期 / 永久三批次 → 入库各 10 → 库存查询展开批次显示三行；
 * - 7.2 销售出库 FEFO 自动选中批次 1 → 仅批次 1 减少、其余不变；流水含批次号；
 * - 7.3 过期批次出库 40128 / 按批次商品缺批次 40127（后端双保险，UI 已前端拦截，直连 API 断言）；
 * - 7.4 批次列表近效期 / 过期 / 永久标签 +「仅看近效期或过期」筛选 + 就地新建批次（入库时）成功并被选中；
 * - 7.5 非批次商品：库存展开批次列为 `-`、采购开单无批次下拉（链路零变化回归）。
 *
 * 数据布局（同一批次商品，按批次号区分）：
 * - {B}1 近效期（今天 +15 天，FEFO 默认选中）；
 * - {B}2 已过期（昨天；采购页 required 置灰无法 UI 入库，走 API 直连入库；出库 40128）；
 * - {B}3 永久（无到期日，FEFO 排序垫底）；
 * - {B}4 就地新建（采购入库时行内「新建」创建，永不过期）。
 *
 * 直连 API 说明：过期批次入库 / 40127 / 40128 在 UI 层已被前端拦截（拣选下拉置灰过期批次 +
 * 提交前必选校验），故这些后端双保险路径按 cash.spec.ts 先例用 `page.request` 直连后端断言错误码。
 */

const BACKEND = 'http://localhost:5080'
const BACKEND_HEALTH = `${BACKEND}/health`
const CREDENTIALS = { username: 'admin', password: 'admin123' }
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'

/** 跨用例共享（worker 串行，模块级变量在 test 内顺序传递） */
let batchProductCode = ''
let batchNo1 = ''
let batchNo2 = ''
let batchNo3 = ''
let batchNo4 = ''
let supplierName = ''
let customerName = ''

function uniqueBatchNo(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 100)}`
}

function uniqueProductCode(): string {
  return `E2E_B${Date.now().toString(36)}${Math.floor(Math.random() * 100)}`
}

function uniquePartnerName(): string {
  return `b_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

function daysFromToday(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return d.toLocaleDateString('sv')
}

/** 本地日期 YYYY-MM-DD → UTC 午夜 ISO（后端单据日期口径，同前端 toUtcMidnight） */
function toUtcMidnight(date: string): string {
  return new Date(`${date}T00:00:00Z`).toISOString()
}

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

async function goBatches(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '批次管理')
  await expect(page).toHaveURL(/\/batches$/)
}

async function goProducts(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '商品管理')
  await expect(page).toHaveURL(/\/products$/)
}

async function goPartners(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '往来单位')
  await expect(page).toHaveURL(/\/partners$/)
}

async function goPurchases(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '采购入库')
  await expect(page).toHaveURL(/\/purchases$/)
}

async function goSales(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '销售出库')
  await expect(page).toHaveURL(/\/sales$/)
}

async function goInventory(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存查询')
  await expect(page).toHaveURL(/\/inventory$/)
}

async function goStockMovements(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '库存流水')
  await expect(page).toHaveURL(/\/stock-movements$/)
}

function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

function drawer(page: Page): Locator {
  return page.locator('.arco-drawer')
}

/** 在 Arco select 中搜索并选中唯一匹配项（Enter 确认高亮项，避免旧弹层 DOM 残留误点） */
async function selectBySearch(page: Page, select: Locator, keyword: string): Promise<void> {
  await select.click()
  await select.locator('input').fill(keyword)
  await expect(page.locator('.arco-select-option:visible', { hasText: keyword }).first()).toBeVisible()
  await select.locator('input').press('Enter')
  await expect(select).toContainText(keyword.slice(0, 12))
}

/** 新增往来单位（名称唯一） */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增往来单位', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await drawer(page).locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
}

/** 新增商品（分类就地新建），batchManaged 时打开「按批次管理」开关 */
async function createProduct(page: Page, code: string, name: string, batchManaged: boolean): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(`批次测试分类${Date.now().toString(36)}`)
  await drawer(page).getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = drawer(page).locator('.arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  if (batchManaged) {
    await drawer(page).locator('.arco-form-item', { hasText: '按批次管理' }).locator('.arco-switch').click()
  }
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(page.getByText('新增商品', { exact: true })).toHaveCount(0)
}

/**
 * 批次管理页「新增批次」抽屉：选商品（仅按批次商品）+ 批次号 + 可选生产日期 / 到期日。
 * 抽屉到期日无前端上限校验（不拦过去日期），故可 UI 建近效期 / 过期 / 永久批次。
 */
async function createBatchRow(
  page: Page,
  productCode: string,
  batchNo: string,
  productionDays: number | null,
  expiryDays: number | null,
): Promise<void> {
  await page.getByRole('button', { name: '新增', exact: true }).click()
  await expect(page.getByText('新增批次', { exact: true })).toBeVisible()
  const d = drawer(page)
  // 抽屉内商品 a-select 唯一定位（Arco a-select placeholder 在展示 span 上，input 需经 .arco-select 类定位）
  await selectBySearch(page, d.locator('.arco-select').first(), productCode)
  await d.getByPlaceholder('1-50 位字母、数字、下划线或连字符').fill(batchNo)
  const pickers = d.locator('.arco-picker')
  if (productionDays !== null) {
    await pickers.nth(0).locator('input').fill(daysFromToday(productionDays))
    await pickers.nth(0).locator('input').press('Enter')
  }
  if (expiryDays !== null) {
    await pickers.nth(1).locator('input').fill(daysFromToday(expiryDays))
    await pickers.nth(1).locator('input').press('Enter')
  }
  await d.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '批次已创建')
  await expect(d).toHaveCount(0)
}

/**
 * 在开单页行内批次下拉（行内第 2 个 .arco-select）中选中指定批次号。
 * 用于 FEFO 不自动选中的场景（批次 0 库存时 FEFO 不预选，需手动选）。
 */
async function selectBatchOption(page: Page, row: Locator, batchNo: string): Promise<void> {
  const batchSelect = row.locator('.arco-select').nth(1)
  await batchSelect.click()
  const option = page.locator('.arco-select-option:visible', { hasText: batchNo }).first()
  await expect(option).toBeVisible()
  await option.click()
  await expect(batchSelect).toContainText(batchNo)
  // 关闭下拉浮层
  await page.locator('body').click({ position: { x: 0, y: 0 } })
}

/** 库存查询展开批次后取指定批次号行的库存数字 */
async function batchStockOf(page: Page, batchNo: string): Promise<string> {
  const row = dataRows(page).filter({ hasText: batchNo }).first()
  await expect(row).toBeVisible()
  return (await row.locator('.stock-quantity').innerText()).trim()
}

/** 直连后端登录换取 token */
async function loginToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${BACKEND}/api/auth/login`, { data: CREDENTIALS })
  const body = await res.json()
  return body.data.token as string
}

/** 直连 API 解析商品 / 批次 / 往来单位 id（40127 / 40128 / 入库直连需要真实 id） */
async function apiLookups(request: APIRequestContext, token: string): Promise<{
  productId: string
  batchId1: string
  batchId2: string
  batchId3: string
  supplierId: string
  customerId: string
}> {
  const headers = { Authorization: `Bearer ${token}` }
  const find = (items: { id: string }[], match: (it: Record<string, unknown>) => boolean): string => {
    const hit = items.find((it) => match(it))
    if (!hit) throw new Error('apiLookups：未找到目标记录')
    return hit.id
  }
  const [prodRes, b1Res, b2Res, b3Res, supRes, cusRes] = await Promise.all([
    request.get(`${BACKEND}/api/products?keyword=${batchProductCode}&page=1&pageSize=20`, { headers }),
    request.get(`${BACKEND}/api/batches?keyword=${batchNo1}&page=1&pageSize=20`, { headers }),
    request.get(`${BACKEND}/api/batches?keyword=${batchNo2}&page=1&pageSize=20`, { headers }),
    request.get(`${BACKEND}/api/batches?keyword=${batchNo3}&page=1&pageSize=20`, { headers }),
    request.get(`${BACKEND}/api/partners?keyword=${supplierName}&page=1&pageSize=20`, { headers }),
    request.get(`${BACKEND}/api/partners?keyword=${customerName}&page=1&pageSize=20`, { headers }),
  ])
  const [prodBody, b1Body, b2Body, b3Body, supBody, cusBody] = await Promise.all([
    prodRes.json(),
    b1Res.json(),
    b2Res.json(),
    b3Res.json(),
    supRes.json(),
    cusRes.json(),
  ])
  return {
    productId: find(prodBody.data.items, (it) => it.code === batchProductCode),
    batchId1: find(b1Body.data.items, (it) => it.batchNo === batchNo1),
    batchId2: find(b2Body.data.items, (it) => it.batchNo === batchNo2),
    batchId3: find(b3Body.data.items, (it) => it.batchNo === batchNo3),
    supplierId: find(supBody.data.items, (it) => it.name === supplierName),
    customerId: find(cusBody.data.items, (it) => it.name === customerName),
  }
}

/** 直连 API 采购入库（绕过 UI：过期批次 / 任意批次可入；入库类后端不拦过期） */
async function apiInbound(
  request: APIRequestContext,
  token: string,
  partnerId: string,
  productId: string,
  batchId: string,
  quantity: number,
): Promise<Record<string, unknown>> {
  const res = await request.post(`${BACKEND}/api/purchase-receipts`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      partnerId,
      orderDate: toUtcMidnight(daysFromToday(0)),
      items: [{ productId, quantity, unitPrice: 10, batchId }],
    },
  })
  return res.json()
}

test.describe('批次与保质期管理（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('7.1 商品开启按批次 → 建近效期/已过期/永久批次 → 入库各 10 → 库存展开显示三行', async ({ page, request }) => {
    test.setTimeout(180_000)
    batchProductCode = uniqueProductCode()
    batchNo1 = uniqueBatchNo('BN1_')
    batchNo2 = uniqueBatchNo('BN2_')
    batchNo3 = uniqueBatchNo('BN3_')
    supplierName = uniquePartnerName()
    customerName = uniquePartnerName()

    // 准备供应商 + 客户
    await goPartners(page)
    await createPartner(page, supplierName, '供应商')
    await createPartner(page, customerName, '客户')

    // 商品开启「按批次管理」
    await goProducts(page)
    await createProduct(page, batchProductCode, `批次商品${Date.now() % 100000}`, true)

    // 批次管理页：近效期批次（+15 天）/ 已过期批次（-1 天）/ 永久批次
    await goBatches(page)
    await createBatchRow(page, batchProductCode, batchNo1, -10, 15)
    await createBatchRow(page, batchProductCode, batchNo2, -400, -1)
    await createBatchRow(page, batchProductCode, batchNo3, null, null)

    // 批次 1 走 UI 入库：首次入库前批次 0 库存，FEFO 不预选（要求 availableQuantity>0）→ 手动选批次 1
    await goPurchases(page)
    await page.getByRole('button', { name: '开采购单' }).click()
    await expect(page).toHaveURL(/\/purchases\/new$/)
    await selectBySearch(page, page.locator('.arco-select').first(), supplierName)
    const row1 = dataRows(page).first()
    await selectBySearch(page, row1.locator('.arco-select').first(), batchProductCode)
    await selectBatchOption(page, row1, batchNo1)
    await row1.locator('.arco-input-number input').first().fill('10')
    await row1.locator('.arco-input-number input').first().blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '采购单已创建')

    // 批次 2（过期，UI 置灰）/ 批次 3（永久）直连 API 入库各 10（入库类后端不拦过期）
    const token = await loginToken(request)
    const ids = await apiLookups(request, token)
    const inb2 = await apiInbound(request, token, ids.supplierId, ids.productId, ids.batchId2, 10)
    expect(inb2.code, `过期批次入库应成功（入库类不拦过期）：${JSON.stringify(inb2)}`).toBe(0)
    const inb3 = await apiInbound(request, token, ids.supplierId, ids.productId, ids.batchId3, 10)
    expect(inb3.code, `永久批次入库应成功：${JSON.stringify(inb3)}`).toBe(0)

    // 库存查询展开批次：该商品出现三行（批次 1 / 2 / 3 各 10）
    await goInventory(page)
    await page.getByPlaceholder('搜索商品编码或名称').fill(batchProductCode)
    await searchAndWaitHit(page, batchProductCode)
    await page.locator('.toolbar-actions__expand').click()
    await expect(page.getByRole('columnheader', { name: '批次号' })).toBeVisible()
    await expect(dataRows(page).filter({ hasText: batchProductCode })).toHaveCount(3)
    await expect(await batchStockOf(page, batchNo1)).toBe('10')
    await expect(await batchStockOf(page, batchNo2)).toBe('10')
    await expect(await batchStockOf(page, batchNo3)).toBe('10')
    // 批次 2 已过期 → 该行到期日带「已过期」标签
    await expect(dataRows(page).filter({ hasText: batchNo2 }).first().locator('.arco-tag', { hasText: '已过期' })).toBeVisible()
  })

  test('7.2 销售出库 FEFO 选批次 1 → 仅批次 1 减少、其余不变；流水含批次号', async ({ page }) => {
    test.setTimeout(120_000)
    // FEFO：批次 1（+15 天）最早到期且未过期且有库存 → 自动选中；批次 2 过期被禁选
    await goSales(page)
    await page.getByRole('button', { name: '开销售单' }).click()
    await expect(page).toHaveURL(/\/sales\/new$/)
    await selectBySearch(page, page.locator('.arco-select').first(), customerName)
    const row = dataRows(page).first()
    await selectBySearch(page, row.locator('.arco-select').first(), batchProductCode)
    await expect(row.locator('.arco-select').nth(1), 'FEFO 应自动选中批次 1').toContainText(batchNo1)
    await row.locator('.arco-input-number input').first().fill('4')
    await row.locator('.arco-input-number input').first().blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '销售单已创建')
    await expect(page).toHaveURL(/\/sales\/detail\//)
    const orderNo = (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()

    // 库存：批次 1 减为 6、批次 2 / 3 不变
    await goInventory(page)
    await page.getByPlaceholder('搜索商品编码或名称').fill(batchProductCode)
    await searchAndWaitHit(page, batchProductCode)
    await page.locator('.toolbar-actions__expand').click()
    await expect(await batchStockOf(page, batchNo1)).toBe('6')
    await expect(await batchStockOf(page, batchNo2)).toBe('10')
    await expect(await batchStockOf(page, batchNo3)).toBe('10')

    // 流水含批次号：出库流水行「销售出库 -4」且带批次号 1
    await goStockMovements(page)
    await page.getByPlaceholder('搜索来源单号').fill(orderNo)
    await searchAndWaitHit(page, orderNo)
    const mrow = dataRows(page).first()
    await expect(mrow).toContainText('销售出库')
    await expect(mrow).toContainText('-4')
    await expect(mrow).toContainText(batchNo1)
  })

  test('7.3 过期批次出库 40128 / 按批次商品缺批次 40127（后端双保险，直连 API）', async ({ request }) => {
    const token = await loginToken(request)
    const { productId, batchId1, batchId2, customerId } = await apiLookups(request, token)
    const headers = { Authorization: `Bearer ${token}` }
    const orderDate = toUtcMidnight(daysFromToday(0))

    // 过期批次出库 → 40128（message 含批次号 + 「禁止出库」）
    const expiredRes = await request.post(`${BACKEND}/api/sales-shipments`, {
      headers,
      data: { partnerId: customerId, orderDate, items: [{ productId, quantity: 1, unitPrice: 20, batchId: batchId2 }] },
    })
    const expiredBody = await expiredRes.json()
    expect(expiredBody.code, `过期批次出库应 40128：${JSON.stringify(expiredBody)}`).toBe(40128)
    expect(expiredBody.message).toContain('禁止出库')
    expect(expiredBody.message).toContain(batchNo2)

    // 按批次商品缺批次 → 40127（明细不带 batchId）
    const missingRes = await request.post(`${BACKEND}/api/sales-shipments`, {
      headers,
      data: { partnerId: customerId, orderDate, items: [{ productId, quantity: 1, unitPrice: 20 }] },
    })
    const missingBody = await missingRes.json()
    expect(missingBody.code, `缺批次应 40127：${JSON.stringify(missingBody)}`).toBe(40127)
    expect(missingBody.message).toContain('必须指定批次')

    // 对照：正常批次（批次 1）出库可成功（证明 40128 仅针对过期批次）
    const okRes = await request.post(`${BACKEND}/api/sales-shipments`, {
      headers,
      data: { partnerId: customerId, orderDate, items: [{ productId, quantity: 1, unitPrice: 20, batchId: batchId1 }] },
    })
    const okBody = await okRes.json()
    expect(okBody.code, `正常批次出库应成功：${JSON.stringify(okBody)}`).toBe(0)
  })
  test('7.4 批次列表标签 + 仅看近效期/过期筛选 + 就地新建批次（入库时）成功并被选中', async ({ page }) => {
    test.setTimeout(120_000)
    await goBatches(page)
    // 标签：批次 1 近效期（橙）/ 批次 2 已过期（红）/ 批次 3 永久
    await page.getByPlaceholder('搜索批次号 / 商品编码 / 商品名称').fill(batchNo1)
    await searchAndWaitHit(page, batchNo1)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '近效期' })).toBeVisible()
    await page.getByPlaceholder('搜索批次号 / 商品编码 / 商品名称').fill(batchNo2)
    await searchAndWaitHit(page, batchNo2)
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '已过期' })).toBeVisible()
    await page.getByPlaceholder('搜索批次号 / 商品编码 / 商品名称').fill(batchNo3)
    await searchAndWaitHit(page, batchNo3)
    await expect(dataRows(page).first()).toContainText('永久')

    // 「仅看近效期或过期」筛选：批次 3（永久）被滤掉，批次 1 / 2 保留
    await page.getByRole('button', { name: '重置', exact: true }).click()
    await page.getByPlaceholder('搜索批次号 / 商品编码 / 商品名称').fill(batchProductCode)
    await page.locator('.toolbar-filter .arco-checkbox').click()
    await searchAndWaitHit(page, batchProductCode)
    await expect(dataRows(page).filter({ hasText: batchNo1 })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: batchNo2 })).toHaveCount(1)
    await expect(dataRows(page).filter({ hasText: batchNo3 })).toHaveCount(0)

    // 就地新建批次（采购入库时）：行内「新建」展开填批次 4 → 提交成功且选中，库存 8
    batchNo4 = uniqueBatchNo('BN4_')
    await goPurchases(page)
    await page.getByRole('button', { name: '开采购单' }).click()
    await expect(page).toHaveURL(/\/purchases\/new$/)
    await selectBySearch(page, page.locator('.arco-select').first(), supplierName)
    const row = dataRows(page).first()
    await selectBySearch(page, row.locator('.arco-select').first(), batchProductCode)
    await row.getByRole('button', { name: '新建' }).click()
    await row.getByPlaceholder('批次号').fill(batchNo4)
    // 就地新建后批次号展示区应回填
    await expect(row.locator('.arco-input[placeholder="批次号"]')).toHaveValue(batchNo4)
    await row.locator('.arco-input-number input').first().fill('8')
    await row.locator('.arco-input-number input').first().blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '采购单已创建')

    // 批次列表出现批次 4 且库存列 = 8
    await goBatches(page)
    await page.getByPlaceholder('搜索批次号 / 商品编码 / 商品名称').fill(batchNo4)
    await searchAndWaitHit(page, batchNo4)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText('8')
  })

  test('7.5 非批次商品：库存展开批次列为 -、采购开单无批次下拉（链路零变化）', async ({ page }) => {
    test.setTimeout(120_000)
    const plainCode = uniqueProductCode()
    await goProducts(page)
    await createProduct(page, plainCode, `非批次商品${Date.now() % 100000}`, false)

    // 采购开单：选非批次商品后行内只有 1 个 .arco-select（无批次下拉），提交成功
    await goPurchases(page)
    await page.getByRole('button', { name: '开采购单' }).click()
    await expect(page).toHaveURL(/\/purchases\/new$/)
    await selectBySearch(page, page.locator('.arco-select').first(), supplierName)
    const row = dataRows(page).first()
    await selectBySearch(page, row.locator('.arco-select'), plainCode)
    await expect(row.locator('.arco-select'), '非批次商品行内应仅 1 个 select').toHaveCount(1)
    await row.locator('.arco-input-number input').first().fill('5')
    await row.locator('.arco-input-number input').first().blur()
    await page.getByRole('button', { name: '提交', exact: true }).click()
    await expectMessage(page, '采购单已创建')

    // 库存查询展开批次：非批次商品批次号列为 -
    await goInventory(page)
    await page.getByPlaceholder('搜索商品编码或名称').fill(plainCode)
    await searchAndWaitHit(page, plainCode)
    await page.locator('.toolbar-actions__expand').click()
    const invRow = dataRows(page).filter({ hasText: plainCode }).first()
    await expect(invRow).toBeVisible()
    await expect(invRow.locator('.cell-empty', { hasText: '-' }).first()).toBeVisible()
  })
})

import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickUntil, clickUntilCount } from './helpers/action'
import { loginAs } from './helpers/auth'
import { expectMessage } from './helpers/message'

/**
 * 大额单据审批（specs/042-erp-approval）E2E：
 * - 6.1 配置规则（采购入库阈值 1000 并启用）→ 开 1500 采购单 → 单据「待审批」且库存不变；
 * - 6.2 审批通过 → 库存增加、流水出现、单据已通过；审批页「待我审批」不再含该条；
 * - 6.3 开 800 的单据直接生效（无需审批）；驳回 1500 单据 → 单据作废 / 已驳回且库存不变；
 * - 6.4 自审被拒（提示可见）；提交人撤回 → 单据作废且库存不变；
 * - 6.5 站内信出现「待审批」消息并可跳转审批页（复用 041 的站内信通道）。
 *
 * 单据 fixture 经 `page.request` 直连后端构造，且以「采购员」身份提交（与审批人 admin 分离，
 * 才能覆盖「不可自审」）；规则配置与审批 / 撤回动作一律走 UI（本规格新增的可感知行为）。
 */

const BACKEND = 'http://localhost:5080'
const BACKEND_HEALTH = `${BACKEND}/health`
const ADMIN = { username: 'admin', password: 'admin123' }
const BUYER_PASSWORD = 'initPass123'

/** 「其余三类单据」的阈值哨兵：须 > 0（后端要求）且大到不会触发 */
const NEVER_THRESHOLD = 9999999

/**
 * 「采购入库」的审批阈值：**远高于其他 spec 的采购金额**。
 * e2e 全量并行共享同一个临时库，规则一旦启用即对所有用例生效，
 * 而多数 spec 靠「开采购单入库」造库存——阈值取得足够高才能避免改造既有用例（见 tasks 6.6）。
 */
const PURCHASE_THRESHOLD = 1000000

/** 「需审批」单：数量 10000 × 单价 120 = 1,200,000 ≥ 阈值 */
const PENDING_QTY = 10000
const PENDING_PRICE = 120

/** 「无需审批」单：100 × 100 = 10,000 < 阈值 */
const SMALL_QTY = 100
const SMALL_PRICE = 100

/** 规则配置抽屉里采购入库所在行号（顺序与后端 GetApprovalRules 一致） */
const PURCHASE_INBOUND_ROW = 0

/** 跨用例共享（同文件串行执行，模块级变量顺序传递） */
let buyerUsername = ''
let productCode = ''
let productId = ''
let partnerId = ''
/** 6.2 待审批单（通过用例） */
let pendingReceiptNo = ''
let pendingReceiptId = ''
/** 6.4a 自审用例的单据 */
let selfReviewReceiptId = ''

function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`
}

function uniqueCode(prefix: string): string {
  return `${prefix}${uniqueSuffix()}`
}

/** 数据行（排除空态行） */
function dataRows(page: Page): Locator {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 等待列表工具条按钮脱离 loading（首屏请求完成） */
async function waitListSettled(page: Page): Promise<void> {
  await expect(page.locator('.toolbar-actions .arco-btn-loading')).toHaveCount(0, { timeout: 15_000 })
}

/** 确认 popconfirm（按提示文案锁定浮层，避免残留 DOM 误点） */
async function confirmPopconfirm(page: Page, contentText: string): Promise<void> {
  await page
    .locator('.arco-trigger-popup', { hasText: contentText })
    .getByRole('button', { name: /确\s*定/ })
    .click()
}

interface ApiEnvelope<T> {
  code: number
  message: string
  data: T
}

/** 直连后端登录换取 token */
async function loginToken(
  request: APIRequestContext,
  username: string,
  password: string,
): Promise<string> {
  const response = await request.post(`${BACKEND}/api/auth/login`, {
    data: { username, password },
  })
  const body = (await response.json()) as ApiEnvelope<{ token: string }>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.token
}

/** 经接口创建角色 */
async function createRoleViaApi(
  request: APIRequestContext,
  token: string,
  name: string,
  permissionKeys: string[],
): Promise<string> {
  const response = await request.post(`${BACKEND}/api/roles`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name, permissionKeys },
  })
  const body = (await response.json()) as ApiEnvelope<{ id: string }>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.id
}

/** 经接口创建用户并绑定角色 */
async function createUserViaApi(
  request: APIRequestContext,
  token: string,
  username: string,
  roleId: string,
): Promise<void> {
  const response = await request.post(`${BACKEND}/api/users`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      username,
      displayName: 'E2E 采购员',
      password: BUYER_PASSWORD,
      roleIds: [roleId],
    },
  })
  const body = (await response.json()) as ApiEnvelope<unknown>
  expect(body.code, JSON.stringify(body)).toBe(0)
}

/** 经接口创建往来单位（供应商） */
async function createPartnerViaApi(request: APIRequestContext, token: string): Promise<string> {
  const response = await request.post(`${BACKEND}/api/partners`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name: `审批供应商${uniqueSuffix()}`, type: 1 },
  })
  const body = (await response.json()) as ApiEnvelope<{ id: string }>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.id
}

/** 经接口创建商品（安全库存 0，避免低库存告警干扰） */
async function createProductViaApi(
  request: APIRequestContext,
  token: string,
): Promise<{ id: string; code: string }> {
  const headers = { Authorization: `Bearer ${token}` }
  const categoryResponse = await request.post(`${BACKEND}/api/categories`, {
    headers,
    data: { name: `审批分类${uniqueSuffix()}` },
  })
  const categoryBody = (await categoryResponse.json()) as ApiEnvelope<{ id: string }>
  expect(categoryBody.code, JSON.stringify(categoryBody)).toBe(0)

  const code = uniqueCode('E2E_AP')
  const productResponse = await request.post(`${BACKEND}/api/products`, {
    headers,
    data: {
      code,
      name: `审批商品${code}`,
      categoryId: categoryBody.data.id,
      unit: '个',
      purchasePrice: 10,
      salePrice: 20,
      safetyStock: 0,
    },
  })
  const productBody = (await productResponse.json()) as ApiEnvelope<{ id: string }>
  expect(productBody.code, JSON.stringify(productBody)).toBe(0)
  return { id: productBody.data.id, code }
}

interface ReceiptResult {
  id: string
  receiptNo: string
  approvalStatus: number
  status: number
}

/** 以采购员身份经接口开一张采购入库单 */
async function createReceiptViaApi(
  request: APIRequestContext,
  token: string,
  quantity: number,
  unitPrice: number,
): Promise<ReceiptResult> {
  const response = await request.post(`${BACKEND}/api/purchase-receipts`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      partnerId,
      orderDate: new Date().toISOString(),
      items: [{ productId, quantity, unitPrice }],
    },
  })
  const body = (await response.json()) as ApiEnvelope<ReceiptResult>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data
}

/** 查询采购入库单（校验审批状态与单据状态） */
async function getReceipt(
  request: APIRequestContext,
  token: string,
  id: string,
): Promise<ReceiptResult> {
  const response = await request.get(`${BACKEND}/api/purchase-receipts/${id}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  const body = (await response.json()) as ApiEnvelope<ReceiptResult>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data
}

/** 查询该商品当前库存数量（库存查询接口按商品编码检索，跨仓求和） */
async function getStockQuantity(request: APIRequestContext, token: string): Promise<number> {
  const response = await request.get(`${BACKEND}/api/inventory`, {
    headers: { Authorization: `Bearer ${token}` },
    params: { keyword: productCode, page: 1, pageSize: 20 },
  })
  const body = (await response.json()) as ApiEnvelope<{ items: { stockQuantity: number }[] }>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.items.reduce((sum, item) => sum + item.stockQuantity, 0)
}

/** 查询某单据号对应的库存流水条数（keyword 匹配单号） */
async function getMovementCount(
  request: APIRequestContext,
  token: string,
  receiptNo: string,
): Promise<number> {
  const response = await request.get(`${BACKEND}/api/stock-movements`, {
    headers: { Authorization: `Bearer ${token}` },
    params: { keyword: receiptNo, page: 1, pageSize: 20 },
  })
  const body = (await response.json()) as ApiEnvelope<{ total: number }>
  expect(body.code, JSON.stringify(body)).toBe(0)
  return body.data.total
}

/** 以 admin 身份进入单据审批页 */
async function goApprovals(page: Page): Promise<void> {
  await loginAs(page, ADMIN.username, ADMIN.password)
  await page.goto('/approvals')
  await expect(page).toHaveURL(/\/approvals$/)
  await expect(page.getByRole('columnheader', { name: '单据号' })).toBeVisible()
  await waitListSettled(page)
}

/** 在审批页按单据号定位审批行 */
async function approvalRow(page: Page, receiptNo: string): Promise<Locator> {
  const row = dataRows(page).filter({ hasText: receiptNo }).first()
  await expect(row).toBeVisible({ timeout: 15_000 })
  return row
}

/** 打开审批抽屉并等待详情加载完成 */
async function openDecideDrawer(page: Page, receiptNo: string): Promise<void> {
  const row = await approvalRow(page, receiptNo)
  await row.getByRole('button', { name: '审批' }).click()
  await expect(page.getByText('单据明细')).toBeVisible({ timeout: 15_000 })
}

test.describe('大额单据审批（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const response = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!response.ok()) throw new Error(`status ${response.status()}`)
    } catch {
      throw new Error(
        `后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`,
      )
    }
  })

  test.afterAll(async ({ request }) => {
    // 审批规则是全局配置（不按用户隔离）：用例结束后复位为「全部不启用」，
    // 避免并行执行的其他 spec（靠开采购单造库存）受本文件影响（tasks 6.6 硬口径）。
    try {
      const token = await loginToken(request, ADMIN.username, ADMIN.password)
      await request.put(`${BACKEND}/api/approval-rules`, {
        headers: { Authorization: `Bearer ${token}` },
        data: {
          rules: [0, 1, 2, 3].map((orderType) => ({
            orderType,
            thresholdAmount: 1,
            enabled: false,
          })),
        },
      })
    } catch {
      // 清理失败不影响用例结论
    }
  })

  test('6.0 准备采购员账号与商品 / 供应商档案', async ({ request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)

    // 采购员：可开采购入库单，同时具备审批权限（用于验证「不可自审」）
    const roleId = await createRoleViaApi(request, adminToken, uniqueCode('e2eappr').slice(0, 20), [
      'purchases.view',
      'purchases.create',
      'products.view',
      'partners.view',
      'approvals.view',
      'approvals.approve',
    ])
    buyerUsername = uniqueCode('e2ebuyer').slice(0, 50)
    await createUserViaApi(request, adminToken, buyerUsername, roleId)

    const product = await createProductViaApi(request, adminToken)
    productId = product.id
    productCode = product.code
    partnerId = await createPartnerViaApi(request, adminToken)
  })

  test('6.1 配置审批规则：采购入库阈值 1000 并启用', async ({ page }) => {
    test.setTimeout(120_000)
    await goApprovals(page)

    await clickUntil(page, '审批规则', page.locator('.rules-row').first())
    const rows = page.locator('.rules-row')
    await expect(rows).toHaveCount(4)

    // 采购入库：阈值 1000 + 启用；其余三类：填「永不触发」阈值且保持不启用
    const targetAmount = rows.nth(PURCHASE_INBOUND_ROW).locator('.rules-row__amount input')
    await targetAmount.fill(String(PURCHASE_THRESHOLD))
    await targetAmount.blur()
    await rows.nth(PURCHASE_INBOUND_ROW).locator('.arco-switch').click()
    for (let index = 0; index < 4; index += 1) {
      if (index === PURCHASE_INBOUND_ROW) continue
      const amount = rows.nth(index).locator('.rules-row__amount input')
      await amount.fill(String(NEVER_THRESHOLD))
      await amount.blur()
    }

    await clickUntil(page, '保存', page.locator('.rules-row').first())
    await expectMessage(page, '审批规则已保存')
  })

  test('6.1 开 1500 采购单 → 单据「待审批」且库存不变', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)

    const created = await createReceiptViaApi(request, buyerToken, PENDING_QTY, PENDING_PRICE)
    expect(created.approvalStatus).toBe(1) // 待审批
    pendingReceiptNo = created.receiptNo
    pendingReceiptId = created.id

    // 未生效：库存仍为 0、无流水
    expect(await getStockQuantity(request, adminToken)).toBe(0)
    expect(await getMovementCount(request, adminToken, pendingReceiptNo)).toBe(0)

    // 列表可见并带「待审批」标签（不凭空消失）；详情提示未生效
    await loginAs(page, ADMIN.username, ADMIN.password)
    await page.goto('/purchases')
    await expect(page.getByRole('columnheader', { name: '审批状态' })).toBeVisible()
    await waitListSettled(page)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: pendingReceiptNo }).first())

    const row = dataRows(page).filter({ hasText: pendingReceiptNo }).first()
    await expect(row).toContainText('待审批')

    await row.getByRole('button', { name: '详情' }).click()
    await expect(page.locator('.arco-alert').filter({ hasText: '待审批' })).toBeVisible({
      timeout: 15_000,
    })
  })

  test('6.2 审批通过 → 库存增加、流水出现、单据已通过', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)

    await goApprovals(page)
    await openDecideDrawer(page, pendingReceiptNo)
    await clickUntil(
      page,
      '通过',
      page.locator('.arco-trigger-popup', { hasText: '确认通过该单据？' }),
    )
    await confirmPopconfirm(page, '确认通过该单据？')
    await expectMessage(page, '已通过')

    // 通过后「待我审批」不再含该单
    await expect(dataRows(page).filter({ hasText: pendingReceiptNo })).toHaveCount(0, {
      timeout: 15_000,
    })

    const receipt = await getReceipt(request, adminToken, pendingReceiptId)
    expect(receipt.approvalStatus).toBe(2) // 已通过
    expect(receipt.status).toBe(1) // 正常
    expect(await getStockQuantity(request, adminToken)).toBe(PENDING_QTY)
    expect(await getMovementCount(request, adminToken, pendingReceiptNo)).toBe(1)
  })

  test('6.3 开 800 采购单 → 直接生效（无需审批）', async ({ request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)

    const created = await createReceiptViaApi(request, buyerToken, SMALL_QTY, SMALL_PRICE)
    expect(created.approvalStatus).toBe(0) // 无需审批
    expect(await getStockQuantity(request, adminToken)).toBe(PENDING_QTY + SMALL_QTY) // 保存即生效
  })

  test('6.3 驳回 1500 单据 → 单据作废 / 已驳回且库存不变', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)

    const created = await createReceiptViaApi(request, buyerToken, PENDING_QTY, PENDING_PRICE)
    expect(created.approvalStatus).toBe(1)
    const stockBefore = await getStockQuantity(request, adminToken)

    await goApprovals(page)
    await openDecideDrawer(page, created.receiptNo)
    await page.getByPlaceholder('通过选填；驳回必填，≤ 200 字符').fill('金额有误，请重开')
    await clickUntilCount(page, '驳回', dataRows(page).filter({ hasText: created.receiptNo }), 0)
    await expectMessage(page, '已驳回')

    const receipt = await getReceipt(request, adminToken, created.id)
    expect(receipt.approvalStatus).toBe(3) // 已驳回
    expect(receipt.status).toBe(0) // 单据一并作废
    expect(await getStockQuantity(request, adminToken)).toBe(stockBefore) // 从未生效，无回冲
  })

  test('6.4 自审被拒：提交人自己审批 → 提示不可自审', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)
    const created = await createReceiptViaApi(request, buyerToken, PENDING_QTY, PENDING_PRICE)
    expect(created.approvalStatus).toBe(1)
    selfReviewReceiptId = created.id

    await loginAs(page, buyerUsername, BUYER_PASSWORD)
    await page.goto('/approvals')
    await expect(page.getByRole('columnheader', { name: '单据号' })).toBeVisible()
    await waitListSettled(page)

    await openDecideDrawer(page, created.receiptNo)
    await clickUntil(
      page,
      '通过',
      page.locator('.arco-trigger-popup', { hasText: '确认通过该单据？' }),
    )
    await confirmPopconfirm(page, '确认通过该单据？')
    await expectMessage(page, '不能审批自己提交的单据')

    // 失败保留抽屉，单据仍为待审批（无半截数据）
    await expect(page.getByText('单据明细')).toBeVisible()
    expect((await getReceipt(request, adminToken, selfReviewReceiptId)).approvalStatus).toBe(1)
  })

  test('6.4 提交人撤回 → 单据作废且库存不变', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)
    const created = await createReceiptViaApi(request, buyerToken, PENDING_QTY, PENDING_PRICE)
    expect(created.approvalStatus).toBe(1)
    const stockBefore = await getStockQuantity(request, adminToken)

    await loginAs(page, buyerUsername, BUYER_PASSWORD)
    await page.goto('/purchases')
    await expect(page.getByRole('columnheader', { name: '审批状态' })).toBeVisible()
    await waitListSettled(page)
    await clickUntil(page, '搜索', dataRows(page).filter({ hasText: created.receiptNo }).first())
    await dataRows(page)
      .filter({ hasText: created.receiptNo })
      .first()
      .getByRole('button', { name: '详情' })
      .click()


    // 待审批单据详情：显示未生效提示 + 「撤回」入口（隐藏「作废」）
    await expect(page.locator('.arco-alert').filter({ hasText: '待审批' })).toBeVisible({
      timeout: 15_000,
    })
    await expect(page.getByRole('button', { name: '作废' })).toHaveCount(0)
    await clickUntil(
      page,
      '撤回',
      page.locator('.arco-trigger-popup', { hasText: '撤回' }),
    )
    await confirmPopconfirm(page, '撤回')
    await expectMessage(page, '已撤回')

    const receipt = await getReceipt(request, adminToken, created.id)
    expect(receipt.approvalStatus).toBe(4) // 已撤回
    expect(receipt.status).toBe(0) // 一并作废
    expect(await getStockQuantity(request, adminToken)).toBe(stockBefore)
  })

  test('6.5 站内信出现「待审批」消息并可跳转审批页', async ({ page, request }) => {
    test.setTimeout(120_000)
    const adminToken = await loginToken(request, ADMIN.username, ADMIN.password)
    const buyerToken = await loginToken(request, buyerUsername, BUYER_PASSWORD)
    const created = await createReceiptViaApi(request, buyerToken, PENDING_QTY, PENDING_PRICE)
    expect(created.approvalStatus).toBe(1)

    // 提交审批时已给具备 approvals.approve 权限的用户发信（admin 为超级管理员）
    const listResponse = await request.get(`${BACKEND}/api/notifications`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      params: { page: 1, pageSize: 20 },
    })
    const listBody = (await listResponse.json()) as ApiEnvelope<{
      items: { title: string; content: string }[]
    }>
    expect(listBody.code, JSON.stringify(listBody)).toBe(0)
    expect(
      listBody.data.items.some((item) => item.title === '待审批单据'),
      JSON.stringify(listBody.data.items.map((item) => item.title)),
    ).toBe(true)

    await loginAs(page, ADMIN.username, ADMIN.password)
    await page.goto('/notifications')
    await expect(page.getByRole('columnheader', { name: '标题' })).toBeVisible()
    await waitListSettled(page)

    // 消息标题为固定模板「待审批单据」（单号在消息内容里，列表关键词只匹配标题，故按标题断言）
    const row = dataRows(page).filter({ hasText: '待审批单据' }).first()
    await page.getByRole('button', { name: '刷新' }).click()
    await expect(row).toBeVisible({ timeout: 20_000 })

    await row.getByRole('button', { name: '查看' }).click()
    await expect(page).toHaveURL(/\/approvals/)
    await expect(page.getByRole('columnheader', { name: '单据号' })).toBeVisible()
  })
})

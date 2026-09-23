import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'

import { clickUntil } from './helpers/action'
import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端地址（API 直连，用于构造「类型不匹配 40162」场景） */
const BACKEND = 'http://localhost:5080'
/** dev 后端健康检查地址 */
const BACKEND_HEALTH = `${BACKEND}/health`
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }
/** 商品分类弹窗内分类名输入框 placeholder（与 ProductFormDrawer 分类弹窗一致） */
const CATEGORY_PLACEHOLDER = '输入新分类名称（1-20 字符）'
/** 占位单据 id（非空即可：账户校验早于单据校验，不要求单据真实存在） */
const PLACEHOLDER_ORDER_ID = '00000000-0000-0000-0000-000000000001'

/** 唯一编码（≤ 20 字符，与 BankAccounts.Code 列长对齐） */
function uniqueCode(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`.slice(0, 20)
}

/** 唯一名称 */
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

/**
 * 点击侧边菜单并等待进入目标页（Arco 菜单在展开动画期间会吞掉 click，故重试点击）。
 */
async function goMenu(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await expect(async () => {
    if (!urlPattern.test(page.url())) {
      await clickMenuItem(page, menuName)
    }
    await expect(page).toHaveURL(urlPattern, { timeout: 3000 })
  }).toPass({ timeout: 20_000 })
}

/** 登录并经侧边菜单进入指定页面 */
async function goPage(page: Page, menuName: string, urlPattern: RegExp): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
  await goMenu(page, menuName, urlPattern)
}

/** 提交抽屉表单并等待抽屉关闭（= 提交成功） */
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

/** 在 Arco select 中点击并选中指定文案的选项 */
async function pickOption(page: Page, select: Locator, text: string): Promise<void> {
  await select.click()
  await page.locator('.arco-select-option:visible', { hasText: text }).first().click()
  await expect(select).toContainText(text)
}

/** 在 Arco search-select 中搜索并选中唯一匹配项 */
async function selectBySearch(page: Page, selectLocator: Locator, keyword: string): Promise<void> {
  await selectLocator.click()
  await selectLocator.locator('input').fill(keyword)
  // 远程搜索下拉浮层异步渲染：等待匹配选项出现后再点击，避免 Enter 过早选中空项
  await page.locator('.arco-select-option:visible', { hasText: keyword }).first().click()
  await expect(selectLocator).toContainText(keyword.slice(0, 12))
}

// ============================== 业务数据准备（复用收付款域的开单链路） ==============================

/** 新增往来单位 */
async function createPartner(page: Page, name: string, typeLabel: '客户' | '供应商'): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增往来单位', { exact: true })).toBeVisible()
  const target = drawer(page)
  await target.getByPlaceholder('1-50 字符，创建后不可修改').fill(name)
  await target.locator('.arco-radio-group').getByText(typeLabel, { exact: true }).click()
  await target.getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '往来单位已创建')
  await expect(page.getByText('新增往来单位', { exact: true })).toHaveCount(0)
}

/** 新增商品（分类就地新建），采购价 10 / 销售价 20 */
async function createProduct(page: Page, code: string, name: string): Promise<void> {
  await page.getByRole('button', { name: '新增' }).click()
  await expect(page.getByText('新增商品', { exact: true })).toBeVisible()
  await page.getByPlaceholder('2-32 位字母、数字、下划线或连字符').fill(code)
  await page.getByPlaceholder('2-50 字符').fill(name)
  await page.getByPlaceholder('如：个 / 箱 / 斤').fill('个')
  await page.getByRole('button', { name: '新建分类' }).click()
  const catInput = page.getByPlaceholder(CATEGORY_PLACEHOLDER)
  await expect(catInput).toBeVisible()
  await catInput.fill(uniqueName('资金测试分类'))
  await drawer(page).getByRole('button', { name: '保存' }).click()
  await expectMessage(page, '分类已创建')
  const numberInputs = drawer(page).locator('.arco-input-number input')
  await numberInputs.nth(0).fill('10.00')
  await numberInputs.nth(1).fill('20.00')
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await expectMessage(page, '商品已创建')
  await expect(page.getByText('新增商品', { exact: true })).toHaveCount(0)
}

/** 采购单垫库存（数量 10） */
async function seedStockByPurchase(page: Page, supplierName: string, productCode: string): Promise<void> {
  await page.getByRole('button', { name: '开采购单' }).click()
  await expect(page).toHaveURL(/\/purchases\/new$/)
  await selectBySearch(page, page.locator('.arco-select').first(), supplierName)
  const row = dataRows(page).first()
  await selectBySearch(page, row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill('10')
  await qty.blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '采购单已创建')
  await expect(page).toHaveURL(/\/purchases\/detail\//)
}

/** 开一张单行销售单（数量 1、销售价 20 → 总额 20.00），返回单号 */
async function createSingleLineSalesShipment(
  page: Page,
  customerName: string,
  productCode: string,
): Promise<string> {
  await page.getByRole('button', { name: '开销售单' }).click()
  await expect(page).toHaveURL(/\/sales\/new$/)
  await selectBySearch(page, page.locator('.arco-select').first(), customerName)
  const row = dataRows(page).first()
  await selectBySearch(page, row.locator('.arco-select'), productCode)
  const qty = row.locator('.arco-input-number').nth(0).locator('input')
  await qty.fill('1')
  await qty.blur()
  await page.getByRole('button', { name: '提交', exact: true }).click()
  await expectMessage(page, '销售单已创建')
  await expect(page).toHaveURL(/\/sales\/detail\//)
  return (await page.locator('.detail-desc').getByText(/^GI\d{12}$/).first().innerText()).trim()
}

// ============================== 资金账户 ==============================

/** 新增资金账户：typeLabel = '现金' | '银行'，initialBalance 为初始余额 */
async function createBankAccount(
  page: Page,
  code: string,
  name: string,
  typeLabel: '现金' | '银行',
  initialBalance: string,
  bankName?: string,
): Promise<void> {
  await page.getByRole('button', { name: '新增', exact: true }).click()
  await expect(drawer(page).getByText('新增资金账户', { exact: true })).toBeVisible()
  await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(code)
  await drawer(page).getByPlaceholder('1-50 字符').fill(name)
  await drawer(page).locator('.arco-radio-group').first().getByText(typeLabel, { exact: true }).click()
  if (typeLabel === '银行') {
    await drawer(page).getByPlaceholder('≤ 100 字符，银行账户必填').fill(bankName ?? '测试银行')
  }
  await drawer(page).locator('.arco-input-number input').fill(initialBalance)
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '资金账户已创建')
}

/** 按名称定位资金账户所在行 */
function bankAccountRow(page: Page, name: string): Locator {
  return dataRows(page).filter({ hasText: name }).first()
}

// ============================== 用例 ==============================

test.describe('资金出纳（集成）', () => {
  test.beforeAll(async ({ request }) => {
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('资金账户：新增银行账户 / 编码重复 40160 / 停用 / 余额总览', async ({ page }) => {
    const code = uniqueCode('BA')
    const name = uniqueName('银行账户')
    await goPage(page, '资金账户', /\/bank-accounts$/)
    await expect(page.getByRole('columnheader', { name: '账户编码' })).toBeVisible()

    // 余额总览（账户数 / 总余额）与列表并存
    await expect(page.getByText('启用账户数')).toBeVisible()
    await expect(page.getByText('资金总余额（元）')).toBeVisible()

    await createBankAccount(page, code, name, '银行', '1000', '招商银行深圳分行')

    // 筛选：关键字搜索确实生效（命中行出现且未命中行归零）
    await page.getByPlaceholder('搜索账户编码或名称').fill(code)
    await searchAndWaitHit(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    const row = dataRows(page).first()
    await expect(row.locator('.arco-tag', { hasText: '银行' })).toBeVisible()
    await expect(row).toContainText('1,000.00')

    // 编码重复 → 40160
    await page.getByRole('button', { name: '新增', exact: true }).click()
    await expect(drawer(page).getByText('新增资金账户', { exact: true })).toBeVisible()
    await drawer(page).getByPlaceholder('1-20 字符，全局唯一').fill(code)
    await drawer(page).getByPlaceholder('1-50 字符').fill(uniqueName('重复账户'))
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '资金账户编码已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 停用：状态标签切换（历史数据保留）
    await bankAccountRow(page, name).getByRole('button', { name: '停用' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '已停用')
    await expect(bankAccountRow(page, name).locator('.arco-tag', { hasText: '停用' })).toBeVisible()
  })

  test('资金日记账：收款关联银行账户 → 日记账出现该笔且余额正确 / 被引用账户删除保护 40161', async ({
    page,
  }) => {
    // 本用例需铺开单链路（商品 / 往来 / 采购垫库存 / 销售单 / 收款 / 日记账），放宽超时
    test.setTimeout(120_000)
    const code = uniqueCode('BJ')
    const accountName = uniqueName('日记账账户')
    const productCode = uniqueCode('cash')
    const customer = uniqueName('c')
    const supplier = uniqueName('s')

    // 准备资金账户（初始余额 1000）
    await goPage(page, '资金账户', /\/bank-accounts$/)
    await createBankAccount(page, code, accountName, '银行', '1000', '中国银行深圳分行')

    // 准备客户 / 供应商 / 商品，并垫库存后开一张销售单（总额 20.00）
    await clickMenuItem(page, '往来单位')
    await expect(page).toHaveURL(/\/partners$/)
    await createPartner(page, customer, '客户')
    await createPartner(page, supplier, '供应商')

    await clickMenuItem(page, '商品管理')
    await expect(page).toHaveURL(/\/products$/)
    await createProduct(page, productCode, `资金商品${Date.now() % 100000}`)

    await clickMenuItem(page, '采购入库')
    await expect(page).toHaveURL(/\/purchases$/)
    await seedStockByPurchase(page, supplier, productCode)

    await clickMenuItem(page, '销售出库')
    await expect(page).toHaveURL(/\/sales$/)
    const orderNo = await createSingleLineSalesShipment(page, customer, productCode)

    // 销售列表 → 「收付款」→ 方式选银行转账 + 资金账户选新建账户 → 核销 20.00
    await clickMenuItem(page, '销售出库')
    await expect(page).toHaveURL(/\/sales$/)
    await page.getByPlaceholder('搜索单号 / 客户').fill(orderNo)
    await searchAndWaitHit(page, orderNo)
    await dataRows(page).first().getByRole('button', { name: '收付款' }).click()
    await expect(page).toHaveURL(/\/settlements\/new/)
    await expect(dataRows(page).first()).toContainText(orderNo)

    // 方式切「银行转账」后，资金账户下拉按类型过滤（只列银行账户）
    const methodSelect = page.locator('.arco-form-item', { hasText: '方式' }).locator('.arco-select').first()
    await pickOption(page, methodSelect, '银行转账')
    const accountSelect = page.locator('.arco-form-item', { hasText: '资金账户' }).locator('.arco-select').first()
    await pickOption(page, accountSelect, code)

    const candidateRow = dataRows(page).first()
    await candidateRow.locator('.arco-checkbox').click()
    const amountInput = candidateRow.locator('.arco-input-number input')
    await amountInput.fill('20.00')
    await amountInput.blur()
    await page.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '收付款单已创建')
    await expect(page).toHaveURL(/\/settlements\/detail\//)
    const receiptNo = (await page.locator('.detail-desc').getByText(/^RC\d{12}$/).first().innerText()).trim()

    // 资金日记账：期初 1000 + 收 20 → 期末 1020
    await clickMenuItem(page, '资金日记账')
    await expect(page).toHaveURL(/\/cash-journals$/)
    await pickOption(page, page.locator('.arco-select').first(), code)
    await clickUntil(page, '查询', dataRows(page).filter({ hasText: receiptNo }).first())
    await expect(dataRows(page).filter({ hasText: receiptNo })).toHaveCount(1)
    const balancePanel = page.locator('.balance-panel')
    await expect(balancePanel).toContainText('1,000.00')
    await expect(balancePanel).toContainText('1,020.00')

    // 被收付款单引用的账户禁止删除 → 40161
    await clickMenuItem(page, '资金账户')
    await expect(page).toHaveURL(/\/bank-accounts$/)
    await page.getByPlaceholder('搜索账户编码或名称').fill(code)
    await searchAndWaitHit(page, code)
    await bankAccountRow(page, accountName).getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '资金账户已被收付款单引用，禁止删除')
  })

  test('结算方式与账户类型不匹配 → 40162（现金账户挂银行转账）', async ({ page, request }) => {
    // 预置现金账户（种子 CASH）
    const token = await loginToken(request)
    const listRes = await request.get(`${BACKEND}/api/bank-accounts?keyword=CASH&page=1&pageSize=20`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const listBody = await listRes.json()
    expect(listBody.code, `查询资金账户失败：${JSON.stringify(listBody)}`).toBe(0)
    const cashAccount = listBody.data.items.find((item: { code: string }) => item.code === 'CASH')
    expect(cashAccount).toBeTruthy()

    // 先建一个往来单位（账户校验在往来单位存在性之后、被核销单据校验之前，故只需真实往来 + 占位明细）
    const partnerRes = await request.post(`${BACKEND}/api/partners`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: uniqueName('api客户'), type: 2 },
    })
    const partnerBody = await partnerRes.json()
    expect(partnerBody.code, `新增往来单位失败：${JSON.stringify(partnerBody)}`).toBe(0)

    // 银行转账方式关联现金账户 → 40162
    const res = await request.post(`${BACKEND}/api/settlements`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        type: 0,
        partnerId: partnerBody.data.id,
        settlementDate: new Date().toISOString(),
        method: 1,
        bankAccountId: cashAccount.id,
        // 占位明细：只求通过格式校验（账户校验早于被核销单据校验，单据可不存在）
        items: [{ orderType: 1, orderId: PLACEHOLDER_ORDER_ID, amount: 1 }],
      },
    })
    const body = await res.json()
    expect(body.code).toBe(40162)
    expect(body.message).toContain('结算方式与资金账户类型不匹配')

    // 前端资金账户页可见该预置现金账户（种子开箱可用）
    await goPage(page, '资金账户', /\/bank-accounts$/)
    await page.getByPlaceholder('搜索账户编码或名称').fill('CASH')
    await searchAndWaitHit(page, 'CASH')
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '现金' })).toBeVisible()
  })
})

/** 直连后端登录并换取 token（用于构造接口层的异常用例） */
async function loginToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${BACKEND}/api/auth/login`, { data: CREDENTIALS })
  const body = await res.json()
  return body.data.token as string
}

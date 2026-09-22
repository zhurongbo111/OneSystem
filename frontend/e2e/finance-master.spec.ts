import { expect, test, type Locator, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'
import { searchAndWaitHit } from './helpers/table-search'

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = 'http://localhost:5080/health'
/** dev 管理员账号（来自项目 seed 数据） */
const ADMIN = { username: 'admin', password: 'admin123' }

/** 唯一编码（≤ 20 字符，与后端编码列长对齐） */
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

// ============================== 会计科目 ==============================

/** 打开科目抽屉（parentRow 为空表示新增一级科目） */
async function openAccountDrawer(page: Page, parentRow?: Locator): Promise<Locator> {
  if (parentRow) {
    await parentRow.getByRole('button', { name: '新增下级' }).click()
  } else {
    await page.getByRole('button', { name: '新增一级科目' }).click()
  }
  await expect(drawer(page).getByText('新增科目', { exact: true })).toBeVisible()
  return drawer(page)
}

/** 填写科目表单（编码 / 名称；不提交，供正负用例各自断言） */
async function fillAccountForm(target: Locator, code: string, name: string): Promise<void> {
  await target.getByPlaceholder('1-20 字符，全局唯一').fill(code)
  await target.getByPlaceholder('1-50 字符').fill(name)
}

/** 新增科目（一级或指定上级下），等待创建成功（以抽屉关闭为准） */
async function createAccount(page: Page, code: string, name: string, parentRow?: Locator): Promise<void> {
  const target = await openAccountDrawer(page, parentRow)
  await fillAccountForm(target, code, name)
  await target.getByRole('button', { name: '提交' }).click()
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '科目已创建')
}

/** 按科目名称定位所在行（树形表格展开后子行才渲染） */
function accountRow(page: Page, name: string): Locator {
  return dataRows(page).filter({ hasText: name }).first()
}

// ============================== 税率 ==============================

/** 新增税率（等待抽屉关闭） */
async function createTaxRate(page: Page, code: string, name: string, rate = 13): Promise<void> {
  await page.getByRole('button', { name: '新增', exact: true }).click()
  await expect(drawer(page).getByText('新增税率', { exact: true })).toBeVisible()
  await fillTaxRateForm(drawer(page), code, name, rate)
  await drawer(page).getByRole('button', { name: '提交' }).click()
  await submitDrawerAndWaitClose(page)
  await expectMessage(page, '税率已创建')
}

/** 填写税率表单（编码 / 名称 / 税率） */
async function fillTaxRateForm(target: Locator, code: string, name: string, rate = 13): Promise<void> {
  await target.getByPlaceholder('1-20 字符，全局唯一').fill(code)
  await target.getByPlaceholder('1-50 字符，全局唯一').fill(name)
  // 抽屉内仅一个数字输入（税率），无需额外定位锚点
  await target.locator('.arco-input-number input').fill(String(rate))
}

test.describe('财务主数据（集成）', () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 })
      if (!res.ok()) throw new Error(`status ${res.status()}`)
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`)
    }
  })

  test('会计科目：预置科目可见 / 新增下级 / 编码重复 40149 / 删除保护 40150', async ({ page }) => {
    await goPage(page, '会计科目', /\/accounts$/)
    await expect(page.getByRole('columnheader', { name: '科目名称' })).toBeVisible()

    // 预置科目：种子科目可见并带「预置」标记（F3）
    const presetRow = accountRow(page, '库存现金')
    await expect(presetRow).toBeVisible()
    await expect(presetRow.locator('.arco-tag', { hasText: '预置' })).toBeVisible()

    // 预置科目不可删除 → 40150
    await presetRow.getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '预置科目不可删除')

    // 新增一级科目 → 其下新增下级科目（形成树）
    const rootCode = uniqueCode('A')
    const rootName = uniqueName('科目')
    const childName = uniqueName('子科目')
    await createAccount(page, rootCode, rootName)
    await createAccount(page, uniqueCode('A'), childName, accountRow(page, rootName))
    await expect(accountRow(page, childName)).toBeVisible()

    // 编码重复 → 40149（复用已存在的一级科目编码）
    const duplicate = await openAccountDrawer(page)
    await fillAccountForm(duplicate, rootCode, uniqueName('重复编码'))
    await duplicate.getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '科目编码已存在')
    await duplicate.getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 有子科目 → 40150
    await accountRow(page, rootName).getByRole('button', { name: '删除' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '该科目存在子科目，不可删除')
  })

  test('税率：新增 / 编码与名称重复 40151 / 40152 / 筛选 / 编辑 / 停用', async ({ page }) => {
    const code = uniqueCode('T')
    const name = uniqueName('税率')
    const renamed = `${name}改`
    await goPage(page, '税率', /\/tax-rates$/)
    await expect(page.getByRole('columnheader', { name: '税率编码' })).toBeVisible()

    // 新增（税率 13.5%，覆盖 4 位小数口径的常规取值）
    await createTaxRate(page, code, name, 13.5)

    // 筛选：关键字搜索确实生效（命中行出现且未命中行归零）
    await page.getByPlaceholder('搜索税率编码或名称').fill(code)
    await searchAndWaitHit(page, code)
    await expect(dataRows(page)).toHaveCount(1)
    await expect(dataRows(page).first()).toContainText('13.5%')

    // 编码重复 → 40151
    await page.getByRole('button', { name: '新增', exact: true }).click()
    await expect(drawer(page).getByText('新增税率', { exact: true })).toBeVisible()
    await fillTaxRateForm(drawer(page), code, uniqueName('税率'))
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '税率编码已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 名称重复 → 40152
    await page.getByRole('button', { name: '新增', exact: true }).click()
    await expect(drawer(page).getByText('新增税率', { exact: true })).toBeVisible()
    await fillTaxRateForm(drawer(page), uniqueCode('T'), name)
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await expectMessage(page, '税率名称已存在')
    await drawer(page).getByRole('button', { name: '取消' }).click()
    await expect(drawer(page)).toHaveCount(0)

    // 编辑：改名后列表反映
    await dataRows(page).first().getByRole('button', { name: '编辑' }).click()
    await expect(drawer(page).getByText('编辑税率', { exact: true })).toBeVisible()
    await drawer(page).getByPlaceholder('1-50 字符，全局唯一').fill(renamed)
    await drawer(page).getByRole('button', { name: '提交' }).click()
    await submitDrawerAndWaitClose(page)
    await expectMessage(page, '税率已更新')
    await expect(dataRows(page).first()).toContainText(renamed)

    // 停用：状态标签切换（记录保留）
    await dataRows(page).first().getByRole('button', { name: '停用' }).click()
    await confirmPopconfirm(page)
    await expectMessage(page, '已停用')
    await expect(dataRows(page).first().locator('.arco-tag', { hasText: '停用' })).toBeVisible()
  })
})

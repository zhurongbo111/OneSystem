import { test, expect, type Page } from '@playwright/test'

import { loginAs } from './helpers/auth'
import { clickMenuItem } from './helpers/menu'
import { expectMessage } from './helpers/message'

/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: 'admin', password: 'admin123' }

/** 静态数据总条数（见 useOrderStore 的 SEED） */
const TOTAL = 12

async function login(page: Page): Promise<void> {
  await loginAs(page, CREDENTIALS.username, CREDENTIALS.password)
}

/** 进入统一列表页（/form） */
async function goList(page: Page): Promise<void> {
  await login(page)
  await clickMenuItem(page, '表单与详情示例')
  await expect(page).toHaveURL(/\/form$/)
}

/** 当前表格数据行（数据行在原生 tbody 内，排除空状态行） */
function dataRows(page: Page): ReturnType<typeof page.locator> {
  return page.locator('tbody tr:not(.arco-table-tr-empty)')
}

/** 分页"共 N 条"文本 */
function totalText(page: Page): ReturnType<typeof page.locator> {
  return page.locator('text=/共 \\d+ 条/')
}

test.describe('表单与详情示例：统一列表 + 抽屉/页面双形态（集成）', () => {
  test('登录后进入列表页，默认每页 10 行、共 12 条', async ({ page }) => {
    await goList(page)
    await expect(page.getByRole('heading', { name: '订单列表（表单与详情示例）' })).toBeVisible()
    await expect(dataRows(page)).toHaveCount(10)
    await expect(totalText(page)).toHaveText(`共 ${TOTAL} 条`)
  })

  test('新增·抽屉：空表单提交被校验拦截，抽屉不关闭', async ({ page }) => {
    await goList(page)
    await page.getByRole('button', { name: '新增·抽屉' }).click()
    const drawer = page.locator('.arco-drawer')
    await expect(drawer).toBeVisible()
    await expect(drawer.locator('.arco-drawer-title')).toHaveText('新增订单')
    // 空表单提交 → 校验失败，抽屉保持打开
    await drawer.getByRole('button', { name: '提交' }).click()
    await expect(drawer).toBeVisible()
    await expect(drawer.getByText('请输入客户')).toBeVisible()
  })

  test('新增·抽屉：填完必填项提交后列表出现新行', async ({ page }) => {
    await goList(page)
    await page.getByRole('button', { name: '新增·抽屉' }).click()
    const drawer = page.locator('.arco-drawer')
    await expect(drawer).toBeVisible()
    await drawer.getByPlaceholder('请输入客户').fill('测试客户E2E')
    await drawer.getByPlaceholder('请输入商品').fill('测试商品E2E')
    await drawer.getByRole('spinbutton').fill('100')
    // 创建时间：点日期选择器后点面板中"当前日"单元格（默认展示当月）
    await drawer.getByPlaceholder('请选择日期').click()
    await page.locator('.arco-picker-cell-today').click()
    await drawer.getByRole('button', { name: '提交' }).click()
    await expect(drawer).toBeHidden()
    await expectMessage(page, '订单已创建')
    // 新行插入首行（upsert unshift），客户名可见，总数 +1
    await expect(dataRows(page).first().getByText('测试客户E2E')).toBeVisible()
    await expect(totalText(page)).toHaveText(`共 ${TOTAL + 1} 条`)
  })

  test('编辑（用抽屉）：形态菜单选择后抽屉回填，提交后列表更新', async ({ page }) => {
    await goList(page)
    const firstRow = dataRows(page).first()
    // 列顺序：序号(0) 订单号(1) 客户(2)
    const customerBefore = (await firstRow.locator('td').nth(2).textContent())?.trim()
    await firstRow.getByRole('button', { name: '编辑' }).click()
    // Arco 下拉选项渲染为 li.arco-dropdown-option，非 menuitem 角色
    await page.locator('.arco-dropdown-option', { hasText: '用抽屉' }).click()
    const drawer = page.locator('.arco-drawer')
    await expect(drawer).toBeVisible()
    await expect(drawer.locator('.arco-drawer-title')).toHaveText('编辑订单')
    // 客户字段已回填（输入框值为该行客户）
    const customerInput = drawer.getByPlaceholder('请输入客户')
    await expect(customerInput).toHaveValue(customerBefore)
    // 改金额后提交
    await drawer.getByRole('spinbutton').fill('666')
    await drawer.getByRole('button', { name: '提交' }).click()
    await expect(drawer).toBeHidden()
    await expectMessage(page, '订单已更新')
    // 列表首行金额列（索引 4）更新为 ¥ 666.00
    await expect(dataRows(page).first().locator('td').nth(4)).toHaveText('¥ 666.00')
  })

  test('新增·页面：跳转 /form/new，明细子表格默认 1 行，可添加行', async ({ page }) => {
    await goList(page)
    await page.getByRole('button', { name: '新增·页面' }).click()
    await expect(page).toHaveURL(/\/form\/new$/)
    await expect(page.locator('.arco-page-header-title')).toHaveText('新增订单')
    // 明细子表格默认 1 空行
    await expect(page.locator('.arco-table tbody tr')).toHaveCount(1)
    // 添加行 → 2 行
    await page.getByRole('button', { name: '添加行' }).click()
    await expect(page.locator('.arco-table tbody tr')).toHaveCount(2)
  })

  test('新增·页面：空表单提交被拦截，URL 不变且错误可见', async ({ page }) => {
    await goList(page)
    await page.getByRole('button', { name: '新增·页面' }).click()
    await expect(page).toHaveURL(/\/form\/new$/)
    await page.getByRole('button', { name: '提交' }).click()
    // URL 不变（未跳转详情）
    await expect(page).toHaveURL(/\/form\/new$/)
    await expect(page.getByText('请输入客户')).toBeVisible()
    // 明细行错误提示（默认 1 空行未填商品名）
    await expect(page.getByText('请至少添加一条商品明细并填写商品名称')).toBeVisible()
  })

  test('新增·页面：填完必填项与明细后提交，跳转详情页并展示所填数据', async ({ page }) => {
    await goList(page)
    await page.getByRole('button', { name: '新增·页面' }).click()
    await expect(page).toHaveURL(/\/form\/new$/)
    await page.getByPlaceholder('请输入客户').fill('页面示例客户')
    await page.getByPlaceholder('请输入商品').fill('页面示例商品')
    // 金额（表单区第一个 spinbutton，明细数量在其后）
    await page.getByRole('spinbutton').first().fill('888')
    // 创建时间：点日期选择器后点面板中"当前日"单元格
    await page.getByPlaceholder('请选择日期').click()
    await page.locator('.arco-picker-cell-today').click()
    // 明细行商品名称
    await page.getByPlaceholder('请输入明细商品名称').fill('明细商品A')
    await page.getByRole('button', { name: '提交' }).click()
    await expect(page).toHaveURL(/\/form\/detail\//)
    await expect(page.locator('.arco-page-header-title')).toHaveText('订单详情')
    await expect(page.getByText('页面示例客户').first()).toBeVisible()
    await expect(page.getByText('明细商品A').first()).toBeVisible()
  })

  test('编辑（用页面）：形态菜单选择后跳转 /form/edit/:id 并回填，提交后回详情', async ({ page }) => {
    await goList(page)
    const firstRow = dataRows(page).first()
    // 列顺序：序号(0) 订单号(1) 客户(2)
    const customerBefore = (await firstRow.locator('td').nth(2).textContent())?.trim()
    await firstRow.getByRole('button', { name: '编辑' }).click()
    // Arco 下拉选项渲染为 li.arco-dropdown-option，非 menuitem 角色
    await page.locator('.arco-dropdown-option', { hasText: '用页面' }).click()
    await expect(page).toHaveURL(/\/form\/edit\//)
    await expect(page.locator('.arco-page-header-title')).toHaveText('编辑订单')
    // 客户字段已回填（动态数据，非硬编码）
    await expect(page.getByPlaceholder('请输入客户')).toHaveValue(customerBefore)
    // 明细行至少 1 行且已回填商品名
    await expect(page.locator('.arco-table tbody tr')).not.toHaveCount(0)
    await expect(page.locator('.arco-table tbody input').first()).not.toHaveValue('')
    // 改客户后提交 → 回详情页
    await page.getByPlaceholder('请输入客户').fill('页面编辑客户')
    await page.getByRole('button', { name: '提交' }).click()
    await expect(page).toHaveURL(/\/form\/detail\//)
    await expect(page.getByText('页面编辑客户').first()).toBeVisible()
  })

  test('查看：进入统一详情页，展示全部字段与明细子表格，可打开编辑抽屉', async ({ page }) => {
    await goList(page)
    const firstRow = dataRows(page).first()
    const orderNo = (await firstRow.locator('td').nth(1).textContent())?.trim()
    await firstRow.getByRole('button', { name: '查看' }).click()
    await expect(page).toHaveURL(/\/form\/detail\//)
    await expect(page.locator('.arco-page-header-title')).toHaveText('订单详情')
    await expect(page.getByText(orderNo as string).first()).toBeVisible()
    // 详情页含创建人/更新时间只读字段与商品明细子表格
    await expect(page.getByText('创建人').first()).toBeVisible()
    await expect(page.getByText('更新时间').first()).toBeVisible()
    await expect(page.getByText('商品明细').first()).toBeVisible()
    // 详情页可打开编辑抽屉且回填
    await page.getByRole('button', { name: '编辑' }).click()
    const drawer = page.locator('.arco-drawer')
    await expect(drawer).toBeVisible()
    await expect(drawer.locator('.arco-drawer-title')).toHaveText('编辑订单')
    await expect(drawer.getByPlaceholder('请输入客户')).not.toHaveValue('')
  })

  test('详情页未知 id 显示 404 空态与返回按钮', async ({ page }) => {
    await goList(page)
    await page.goto('/form/detail/not-exist')
    await expect(page.getByText('订单不存在或已被删除')).toBeVisible()
    await expect(page.getByRole('button', { name: '返回列表' })).toBeVisible()
  })

  test('删除：Popconfirm 确认后列表移除该行', async ({ page }) => {
    await goList(page)
    await dataRows(page).first().getByRole('button', { name: '删除' }).click()
    await expect(page.getByText('确认删除该订单？')).toBeVisible()
    await page.getByRole('button', { name: /确\s*定/ }).click()
    await expect(totalText(page)).toHaveText(`共 ${TOTAL - 1} 条`)
  })
})

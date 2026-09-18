import { expect, type Locator, type Page } from '@playwright/test'

/** 判据：元素可见性（Locator），或自定义断言块（行数 / URL 等无法用可见性表达的场景） */
type Expected = Locator | (() => Promise<void>)

/**
 * 点击按钮直到预期结果出现。
 *
 * Arco Button 在 loading / 重渲染期间会吞掉 click（不报错、无副作用），
 * 单次点击可能完全没生效（表现为表单不提交、列表不过滤、页面不跳转），
 * 故重试点击直到判据成立。判据见前端规则 §10.1。
 */
export async function clickUntil(
  page: Page,
  buttonName: string,
  expected: Expected,
  timeout = 15000,
): Promise<void> {
  await expect(async () => {
    await page.getByRole('button', { name: buttonName, exact: true }).click()
    if (typeof expected === 'function') await expected()
    else await expect(expected).toBeVisible({ timeout: 3000 })
  }).toPass({ timeout })
}

/** 判据是「某 locator 的行数」时用（精确行数 / 空结果等） */
export async function clickUntilCount(
  page: Page,
  buttonName: string,
  locator: Locator,
  count: number,
  timeout = 15000,
): Promise<void> {
  await clickUntil(
    page,
    buttonName,
    async () => {
      await expect(locator).toHaveCount(count, { timeout: 3000 })
    },
    timeout,
  )
}

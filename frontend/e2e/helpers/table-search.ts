import { expect, type Page } from '@playwright/test'

/**
 * 列表页「输入关键字 → 点搜索」，并等到过滤确实生效。
 *
 * 两个坑（判据见前端规则 §10.1）：
 * 1. Arco Button 在 loading 期间会吞掉 click（不报错、过滤不生效）：
 *    首屏请求未完成时点「搜索」等于没点，故重试点击直到命中行出现；
 * 2. 「没有一行不含关键字」在表格尚未渲染（0 行）时会提前通过，
 *    故先等命中行出现，再断言无未命中行。
 */
export async function searchAndWaitHit(page: Page, keyword: string): Promise<void> {
  const rows = page.locator('tbody tr:not(.arco-table-tr-empty)')
  const searchButton = page.getByRole('button', { name: '搜索', exact: true })
  await expect(async () => {
    await searchButton.click()
    // 顺序不可颠倒：先等数据到达并命中（表格 0 行时「无未命中行」会恒真、提前通过），
    // 再等未命中行归零——只有后者才是过滤生效的判据，放在重试体内才能在点击被吞时重试。
    await expect(rows.filter({ hasText: keyword })).not.toHaveCount(0, { timeout: 3000 })
    await expect(rows.filter({ hasNotText: keyword })).toHaveCount(0, { timeout: 3000 })
  }).toPass({ timeout: 20000 })
}

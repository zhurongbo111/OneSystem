import { expect, type Page } from '@playwright/test'

/**
 * 打开页面并等到应用挂载完成（`#app` 渲染出内容）。
 *
 * 背景：dev server 每轮冷启动，偶发在首次导航时因依赖重新预打包让懒加载 chunk 504，
 * 表现为**纯白页且 URL 停在原地址**，且不会自愈（把 URL 断言超时调到 30s 也只是慢一点失败）。
 * 因此这类环境问题只能靠「重新导航」兜住，与功能断言分开处理——
 * 同 `helpers/auth.ts` 等登录表单渲染再操作的重试思路。
 */
export async function gotoApp(page: Page, url: string): Promise<void> {
  await expect(async () => {
    await page.goto(url)
    await expect(page.locator('#app > *')).not.toHaveCount(0, { timeout: 10_000 })
  }).toPass({ timeout: 30_000 })
}

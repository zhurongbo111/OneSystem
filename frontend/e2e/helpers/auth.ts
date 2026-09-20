import { expect, type Page } from '@playwright/test'

/** 凭证 localStorage key（与 src/api/request.ts 保持一致） */
export const TOKEN_KEY = 'app:token'

/** 登录页路径 */
const LOGIN_PATH = '/login'

/**
 * 登录并等待进入首页。
 *
 * 与各 spec 内联写法的差异（均为消除偶发失败）：
 * 1. 清凭证只在「导航目标是 /login」时执行。init script 对该 page 的每次完整导航都会跑，
 *    若无条件清 token，登录后用例用 `page.goto('/xxx')` 直达页面时 token 会被再次清掉，
 *    路由守卫随即把页面重定向回登录页（表现为菜单 / 页面内容缺失）。
 * 2. 先导航到 about:blank 再清凭证：init script 会立即在当前页面执行一次，
 *    若当前是业务页且仍有在途请求，清 token 会让这些请求返回 40100，
 *    触发统一处置跳转登录页（见前端规则 §8），与紧随其后的 goto('/login') 相互干扰、
 *    导航交叉（表现为登录表单不渲染、fill 等到用例超时）。
 * 3. 等登录表单出现再操作；未渲染（dev server 偶发慢 / 导航被中断）则重新导航重试，
 *    避免在空白页上直接 fill 而耗尽整个用例的超时预算。
 */
export async function loginAs(page: Page, username: string, password: string): Promise<void> {
  await page.goto('about:blank')
  await page.addInitScript(
    ({ key, loginPath }) => {
      try {
        if (window.location.pathname === loginPath) {
          window.localStorage.removeItem(key)
        }
      } catch {
        // about:blank 等不可访问 localStorage 的上下文，忽略
      }
    },
    { key: TOKEN_KEY, loginPath: LOGIN_PATH },
  )

  const usernameInput = page.getByPlaceholder('请输入用户名')
  await expect(async () => {
    await page.goto(LOGIN_PATH)
    await expect(usernameInput).toBeVisible({ timeout: 10_000 })
  }).toPass({ timeout: 30_000 })

  await usernameInput.fill(username)
  await page.getByPlaceholder('请输入密码').fill(password)
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/$/)
}

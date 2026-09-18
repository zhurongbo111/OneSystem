import { chromium } from "@playwright/test";

/**
 * 冷启动预热。
 *
 * 场景：后端刚启动 / 数据库刚重建时，首个真实请求要完成 JIT、EF 模型构建与查询编译，
 * 首屏与首个下拉远程搜索会明显变慢，靠前的用例容易超时（表现为偶发失败，重跑即过）。
 * 这里用真实浏览器先登录一次，把登录与首屏链路预热，再交给正式用例。
 *
 * 注意：不在这里断言业务结果，预热失败不影响用例自身的校验。
 */
async function globalSetup(): Promise<void> {
  const browser = await chromium.launch({ channel: "chromium" });
  const page = await browser.newPage();
  try {
    await page.goto("http://localhost:5173/login", { timeout: 60_000 });
    await page.getByPlaceholder("请输入用户名").fill("admin", { timeout: 60_000 });
    await page.getByPlaceholder("请输入密码").fill("admin123", { timeout: 60_000 });
    await page.getByRole("button", { name: "登录" }).click({ timeout: 60_000 });
    await page.waitForURL(/\/$/, { timeout: 60_000 });
  } finally {
    await browser.close();
  }
}

export default globalSetup;

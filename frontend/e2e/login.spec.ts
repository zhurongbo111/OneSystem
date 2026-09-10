import { test, expect } from "@playwright/test";

/** dev 后端健康检查地址 */
const BACKEND_HEALTH = "http://localhost:5080/health";
/** dev 测试账号（来自项目 seed 数据） */
const CREDENTIALS = { username: "admin", password: "admin123" };

test.describe("登录与路由守卫（集成）", () => {
  test.beforeAll(async ({ request }) => {
    // 前置：确认 dev 后端已启动，避免产生误导性失败
    try {
      const res = await request.get(BACKEND_HEALTH, { timeout: 5000 });
      if (!res.ok()) throw new Error(`status ${res.status()}`);
    } catch {
      throw new Error(`后端服务未启动（${BACKEND_HEALTH}），请先运行：cd backend && dotnet run --project src/App.Api`);
    }
  });

  test("未登录直接访问首页被重定向到登录页", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByText("测试账号：admin / admin123")).toBeVisible();
  });

  test("登录成功进入首页并展示当前用户", async ({ page }) => {
    await page.goto("/login");
    await page.getByPlaceholder("请输入用户名").fill(CREDENTIALS.username);
    await page.getByPlaceholder("请输入密码").fill(CREDENTIALS.password);
    await page.getByRole("button", { name: "登录" }).click();

    // 登录成功跳转首页
    await expect(page).toHaveURL(/\/$/);
    // 首页展示当前用户信息（「管理员」在头部与描述列表各出现一次，取首个）
    await expect(page.getByText("管理员").first()).toBeVisible();
    await expect(page.getByText("admin")).toBeVisible();
  });

  test("退出登录回到登录页", async ({ page }) => {
    // 先登录
    await page.goto("/login");
    await page.getByPlaceholder("请输入用户名").fill(CREDENTIALS.username);
    await page.getByPlaceholder("请输入密码").fill(CREDENTIALS.password);
    await page.getByRole("button", { name: "登录" }).click();
    await expect(page).toHaveURL(/\/$/);

    // 退出：经布局顶部栏用户下拉
    await page.getByRole("button", { name: "用户菜单" }).click();
    await page.getByText("退出登录").click();
    await expect(page).toHaveURL(/\/login/);
  });
});

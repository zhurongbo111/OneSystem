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
    // 首页展示当前用户信息：头部用户名用类名精确定位（getByText 按子串匹配，
    // 侧边菜单「岗位管理 + 员工档案」相邻文本会命中隐藏的菜单容器，不可用于断言）
    await expect(page.locator(".user-name")).toHaveText("管理员");
    await expect(page.getByText("admin", { exact: true })).toBeVisible();
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

  test("凭证失效（40100）经路由跳转回登录页，重新登录回到原页面", async ({ page }) => {
    // 先登录并进入受保护页面
    await page.goto("/login");
    await page.getByPlaceholder("请输入用户名").fill(CREDENTIALS.username);
    await page.getByPlaceholder("请输入密码").fill(CREDENTIALS.password);
    await page.getByRole("button", { name: "登录" }).click();
    await expect(page).toHaveURL(/\/$/);
    await page.goto("/products");
    await expect(page).toHaveURL(/\/products$/);

    // 打「未整页刷新」标记，并把凭证改成无效值（请求拦截器从 localStorage 读取）
    await page.evaluate(() => {
      document.body.dataset.e2eMarker = "kept";
      window.localStorage.setItem("app:token", "invalid-token");
    });

    // 触发一次受保护请求（点「搜索」）。页面上的在途请求也可能先返回 40100：
    // 此时按钮已随页面卸载，点击会失败，忽略即可——两种触发路径都由下面的跳转断言兜住。
    await page
      .getByRole("button", { name: "搜索", exact: true })
      .click({ timeout: 3000 })
      .catch(() => undefined);

    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });

    // 回跳目标 = 失效前所在页面
    expect(decodeURIComponent(page.url())).toContain("redirect=/products");
    // 标记仍在 → 未发生整页刷新（整页刷新会重建 body）
    await expect(page.locator('body[data-e2e-marker="kept"]')).toHaveCount(1);

    // 重新登录后回到原页面（redirect 生效）
    await page.getByPlaceholder("请输入用户名").fill(CREDENTIALS.username);
    await page.getByPlaceholder("请输入密码").fill(CREDENTIALS.password);
    await page.getByRole("button", { name: "登录" }).click();
    await expect(page).toHaveURL(/\/products$/);
  });
});

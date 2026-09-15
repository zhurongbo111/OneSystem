import { defineConfig, devices } from "@playwright/test";

/**
 * 前端集成测试（e2e）配置。
 *
 * 运行前置（需人工启动，测试不自动拉起服务）：
 * 1. 后端 dev：cd backend && dotnet run --project src/App.Api   （http://localhost:5080）
 * 2. 前端 dev：cd frontend && npm run dev                        （http://localhost:5173）
 *
 * 运行：npm run test:e2e
 */
export default defineConfig({
  testDir: "./e2e",
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL: "http://localhost:5173",
    // 默认无头运行；本地想看界面用 `npm run test:e2e -- --headed` 覆盖
    // 注意：Playwright 配置只认 `headless`（默认 true=无头），`use.headed` 不是有效字段
    headless: true,
    // 使用全量 chromium，避免依赖 chromium_headless_shell（国内网络下载失败）
    channel: "chromium",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});

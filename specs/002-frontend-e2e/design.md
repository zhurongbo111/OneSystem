---
created: 2026-09-09
updated: 2026-09-09
---

# 设计规格：frontend-e2e（前端集成测试自动化）

## 1. 技术选型

Playwright（`@playwright/test`，已在 `frontend` devDependencies）。

## 2. 目录与配置

- 测试目录：`frontend/e2e/`，用例文件 `*.spec.ts`。
- `frontend/playwright.config.ts` 重写：
  - `testDir: './e2e'`
  - `baseURL: http://localhost:5173`（前端 dev server）
  - **不配置 webServer 自动拉起**：dev 后端（dotnet）需人工启动，前端 dev 服务由测试者自行启动；避免 CI 外误拉起进程。
  - 仅保留 chromium 项目，trace 仅首试失败保留。
- `package.json` 脚本：`"test:e2e": "playwright test"`。

## 3. 环境前置

运行前需人工启动：

- 后端：`cd backend; dotnet run --project src/App.Api`（监听 `http://localhost:5080`）
- 前端：`cd frontend; npm run dev`（监听 `http://localhost:5173`）

用例 `beforeAll` 请求 `http://localhost:5080/health`，失败则抛出明确错误「后端服务未启动（http://localhost:5080），请先启动 backend」。

## 4. 用例设计（`e2e/login.spec.ts`）

| # | 用例 | 步骤 | 断言 |
|---|---|---|---|
| 1 | 登录全流程 | 打开 `/login` → 输入 `admin` / `admin123` → 点击「登录」 | URL 变为 `/`；页面出现「管理员」与「admin」 |
| 2 | 退出登录 | 点击「退出登录」 | URL 回到 `/login` |
| 3 | 路由守卫 | 未登录状态 `goto('/')` | 被重定向到 `/login` |

元素定位统一使用 role / placeholder 语义定位，不依赖 class。

## 5. 与既有约定的关系

- 与前端规则「不单写单元测试」无冲突：本方案是**集成测试的自动化**，直连真实后端，不打 mock。
- 同步更新：`AGENTS.md` 第 6 节（测试策略总述）、`.codebuddy/rules/frontend/RULE.mdc` 第 8 节。

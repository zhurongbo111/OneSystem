# 任务清单：frontend-e2e（前端集成测试自动化）

> 执行顺序：T1 → T2 → T3 → T4。完成后勾选；执行每个任务前重读规格相关章节。

## T1 Playwright 配置

- [x] 重写 `frontend/playwright.config.ts`（testDir `e2e/`、baseURL `http://localhost:5173`、仅 chromium 项目；`channel: chromium` 使用全量 chromium 以规避 headless-shell 下载失败）
- [x] `frontend/package.json` 增加 `test:e2e` 脚本（并安装 `@playwright/test`）

## T2 测试用例

- [x] 新建 `frontend/e2e/login.spec.ts`：beforeAll 健康检查后端 → 登录全流程 → 退出登录 → 未登录守卫拦截

## T3 规格与文档同步

- [x] 更新 `AGENTS.md` 第 6 节测试策略（前端集成测试自动化：Playwright e2e）
- [x] 更新 `.codebuddy/rules/frontend/RULE.mdc` 第 8 节（e2e 位置与运行方式）

## T4 验证

- [x] 前后端 dev 服务运行中，`npm run test:e2e` 全部用例通过（3 passed）
- [x] 文档说明前置服务启动方式（`playwright.config.ts` 头部注释 + 前端规则第 8 节）

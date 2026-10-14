---
created: 2026-09-09
updated: 2026-10-14
---

# 任务清单：frontend-e2e（前端集成测试自动化）

> 执行顺序：T1 → T2 → T3 → T4；T5 为跨平台 / CI 接入（2026-10-13 追加）。完成后勾选；执行每个任务前重读规格相关章节。

## T1 配置与运行编排

- [x] 重写 `frontend/playwright.config.ts`（testDir `e2e/`、baseURL `http://localhost:5173`、仅 chromium 项目；`channel: chromium` 使用全量 chromium 以规避 headless-shell 下载失败）
- [x] `frontend/package.json` 增加 `test:e2e` 脚本（并安装 `@playwright/test`）
- [x] 新增 `frontend/scripts/e2e-run.ps1`：每轮 `app_e2e_<时间戳>` 库、历史残留清理、后端生命周期与跑完删库（流程 / 失败处理见 design §6）
- [x] `frontend/package.json` 暴露 `e2e:run`；`.gitignore` 忽略库名历史清单

## T2 测试用例

- [x] 新建 `frontend/e2e/login.spec.ts`：beforeAll 健康检查后端 → 登录全流程 → 退出登录 → 未登录守卫拦截

## T3 规格与文档同步

- [x] 更新 `AGENTS.md` 第 6 节测试策略（前端集成测试自动化：Playwright e2e）
- [x] 更新 `.codebuddy/rules/frontend/RULE.mdc` 第 8 节（e2e 位置与运行方式）
- [x] 同步 `.codebuddy/CONTEXT.md` §4 常用命令 / §5 环境说明与前端规则 §10（推荐 `npm run e2e:run`，手工流程仍可用）

## T4 验证

- [x] 前后端 dev 服务运行中，`npm run test:e2e` 全部用例通过
- [x] 文档说明前置服务启动方式（`playwright.config.ts` 头部注释 + 前端规则第 8 节）
- [x] `npm run e2e:run` 一键跑通全流程：每轮独立库、结束自动删除、无残留

## T5 跨平台与 CI 接入（2026-10-13）

- [x] 新增 `frontend/scripts/e2e-run.sh`：Linux / macOS 等价编排（参数与 `e2e-run.ps1` 同名、行为对齐 design §6.3）
- [x] 新增 `frontend/scripts/e2e-run.mjs`：按平台分发到上述两个脚本，透传参数与退出码；`frontend/package.json` 的 `e2e:run` 改为调用它（`e2e-run.ps1` 补 `-Help`）
- [x] `.github/workflows/ci.yml`：`e2e` job（PostgreSQL 服务容器 + `npm run e2e:run`，PR 触发）；镜像构建留在 `.github/workflows/docker-image.yml`（`main` push / 手动触发，编排见 design §6.4）
- [x] 同步规格与文档：design §6.3 / §6.4、`.codebuddy/CONTEXT.md` §4 / §5、`AGENTS.md` §8.1
- [ ] CI `e2e` job 在 `main` push 上跑通全量用例（脚本退出码 0，临时库已删除）
- [ ] 在 Linux / macOS 本地跑通 `npm run e2e:run`（非 Windows 平台回归）

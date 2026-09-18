# OneSystem

进销存 ERP 系统（学习 / 演示项目）。单仓库 monorepo：Vue 3 前端 + .NET 8 后端 + PostgreSQL。

## 技术栈

| 端 | 选型 |
|---|---|
| 前端 | Vue 3 + TypeScript + Vite + Arco Design Vue + Pinia |
| 后端 | .NET 8 + ASP.NET Core + EF Core（PostgreSQL） |
| 测试 | 后端 xUnit（Handler 业务逻辑）；前端 Playwright e2e（直连 dev 后端，不打 mock） |

## 快速开始

前置：.NET 8 SDK、Node.js、PostgreSQL（本机 5432）。先建库 `app`——迁移只建表不建库；dev 连接串在 `backend/src/App.Api/appsettings.Development.json`（仅本地开发库，按本机账号密码修改）。

```powershell
# 1. 后端（端口 5080；首次启动自动执行迁移，并在用户表为空时建内置管理员 admin / admin123）
cd backend
dotnet run --project src/App.Api

# 2. 前端（端口 5173；/api 由 Vite 代理转发到 5080）
cd frontend
npm install
npm run dev
```

浏览器打开 <http://localhost:5173>，用 `admin` / `admin123` 登录。

其余命令（单测、e2e、构建、lint、类型检查）与端口约定见 `.codebuddy/CONTEXT.md` §4（**该表为唯一事实源**）。

## 目录结构

```
├── AGENTS.md            工程约定总则（分支 / 提交 / API 契约 / 测试策略）
├── .codebuddy/          CONTEXT.md（项目结构摘要）+ rules/（前后端专项规则）
├── specs/               功能规格：每个功能一个 <序号>-<功能名>/（requirement / design / tasks）
├── backend/             src/App.Api、src/App.Core、src/App.Infrastructure、tests/App.Tests
└── frontend/            src/（api / components / views 等）+ e2e/
```

后端分层与各层命名规律见 `.codebuddy/CONTEXT.md` §2，前端功能域与目录归属见 §3。

## 开发流程

规格驱动（SDD）：先写 `specs/<序号>-<功能名>/` 三件套（需求 → 设计 → 任务），再按 `tasks.md` 顺序实现并逐项勾选；需求变更先改规格再改代码。分支从 `main` 切出 `feature/<功能名>`，提交信息用 Conventional Commits。

完整约定见 `AGENTS.md`，ERP 功能的批次与依赖路线见 `specs/ROADMAP.md`。

## 文档索引

| 想了解 | 看这里 |
|---|---|
| 工程约定（分层 / API 契约 / 错误码 / 分支与提交 / 测试策略） | `AGENTS.md` |
| 项目结构、关键文件位置、命令与端口 | `.codebuddy/CONTEXT.md` |
| 后端实现细则 | `.codebuddy/rules/backend/RULE.mdc` |
| 前端实现细则 | `.codebuddy/rules/frontend/RULE.mdc` |
| 单个功能的需求与设计 | `specs/<序号>-<功能名>/` |
| ERP 功能路线与跨功能决策 | `specs/ROADMAP.md` |

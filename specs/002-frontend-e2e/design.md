---
created: 2026-09-09
updated: 2026-09-20
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
- 同步更新：`AGENTS.md` 第 6 节（测试策略总述）、`.codebuddy/rules/frontend/RULE.mdc` §10（e2e 用例命名与运行细则）。

## 6. 运行编排与数据隔离（`npm run e2e:run`）

> 目标：每轮 e2e 使用全新数据库，跑完自动清理。用例本身自带数据且唯一命名，数据累积不影响正确性，但会让后续运行明显变慢、失败现场被历史数据污染。
> 实现：`frontend/scripts/e2e-run.ps1`（Windows PowerShell），由 `frontend/package.json` 的 `e2e:run` 调用。

### 6.1 依赖的后端既有能力（无需改后端代码）

- **切库**：ASP.NET Core 配置优先级为 `appsettings.json → appsettings.{Env}.json → 环境变量 → 命令行`，故
  `dotnet run --project src/App.Api -- --ConnectionStrings:Default="Host=...;Database=<库名>"` 可覆盖 `appsettings.Development.json` 的开发连接串。
- **建库**：dev 下 `DatabaseInitializer` 执行 `MigrateAsync`，库不存在时 EF 自动 `CREATE DATABASE`，随后完成迁移与内置管理员种子（幂等）。
- **删库**：`dotnet ef database drop --force`（需后端已停止，否则构建输出被占用）。

### 6.2 库命名与残留清理

- 库名 `app_e2e_<yyyyMMddHHmmss>`：每轮独立，互不干扰、可并行运行。
- 历史清单 `frontend/scripts/.e2e-db-history`（每行一个库名，已加入 `.gitignore`）：每次运行开始时逐条删除清单中的库并清空清单；正常结束时删除本轮库且**不**入清单；`-KeepDatabase` 保留本轮库供排查，此时记入清单，由下次运行开始时清理。

### 6.3 脚本流程与失败处理

| # | 行为 | 失败处理 |
|---|---|---|
| 1 | 校验 5080 空闲（`Get-NetTCPConnection` 与 `netstat` 双通道检测） | 已占用则报出占用进程 PID 并以退出码 2 结束——占用者连的是开发库，静默复用会让用例打在错误的数据集上 |
| 2 | 清理历史残留库 | 删除失败的库保留在清单中，下次运行继续尝试 |
| 3 | 以本轮库启动后端，轮询 `/health` 就绪 | 同时监视启动进程是否提前退出：端口被占时 `dotnet run` 会立刻退出，只看健康检查会命中"别人的后端"；超时或提前退出均以退出码 2 结束 |
| 4 | 检查 5173：未监听则以脚本启动前端 dev | 脚本启动的前端在结束时一并停止；已在运行的沿用 |
| 5 | 运行 `npx playwright test --output=test-results/e2e-run` | 原样返回退出码 |
| 6 | `taskkill /T` 停止后端进程树并等待端口释放 → 停脚本启动的前端 → 删除本轮库（`--no-build`，避免与其它仍在运行的后端抢构建输出） | 清理放在 `finally`，异常路径同样执行；删库失败则把库名写入历史清单，避免无声残留 |

**退出码约定**：`0` 用例全通过、`1` 有失败用例、`2` 脚本级错误（端口被占用 / 后端未就绪 / 启动进程提前退出）。

- **连接串来源**：解析 `backend/src/App.Api/appsettings.Development.json` 的 `ConnectionStrings:Default`，仅替换 `Database` 段——不在脚本内重复维护账号密码（`AGENTS.md` §7）。
- **产物目录**：固定 `test-results/e2e-run`（已被 `.gitignore` 忽略），运行前清空，避免与手工运行互相覆盖。
- **待办**：脚本为 Windows PowerShell 实现；若后续接入 Linux CI 需提供等价脚本（届时再评估）。

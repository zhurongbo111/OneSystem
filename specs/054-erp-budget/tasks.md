---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：预算管理（erp-budget）

> 依据 `specs/054-erp-budget/design.md` 拆分。新增预算表 + 编制 / 执行对比用例 + 权限 / 菜单续行 + 前端 + e2e。
> 前置：`031`（科目）、`033`（凭证 / 期间 / 取数口径）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `Budget` + `BudgetFieldConstraints`；`AppDbContext` 追加 `Budgets`
- [ ] 1.2 EF 配置（`(AccountId, Year, Month)` 唯一索引、FK、列类型）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpBudget -p src/App.Infrastructure -s src/App.Api`

## 二、后端：仓储与用例

- [ ] 2.1 `IBudgetRepository`（`GetByYearAsync` / `ReplaceAsync`）+ 实现 + 注册；读模型 `BudgetItem`
- [ ] 2.2 `IBudgetExecutionQueryRepository.GetActualAsync`（复用 `033` §0.4 口径）+ 实现 + 注册；读模型 `BudgetExecutionItem`
- [ ] 2.3 用例 `GetBudgets` / `SaveBudgets`（`40191` / `40154`）/ `GetBudgetExecution`（差异 / 执行率）
- [ ] 2.4 DTO + `BudgetsDtoMapper`；`BudgetsController`（3 端点）+ DI；写用例接入操作日志（`029` §0.1 续行，资源 `Budget`）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40191`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40192`

## 四、单元测试（后端）

- [ ] 4.1 编制：保存 / 读取往返、`40191`、已结账月份 `40154`、全量替换
- [ ] 4.2 执行：实际与 `033` §0.4 一致（作废剔除）、差异 / 执行率、预算 0 边界
- [ ] 4.3 校验：`year` / `month` / `amount` 与去重
- [ ] 4.4 字段约束一致性单测
- [ ] 4.5 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/budget.ts`
- [ ] 5.2 `views/BudgetManagement/BudgetsView.vue`（年度选择 + 12 月预算表 + 执行对比抽屉）
- [ ] 5.3 `router/index.ts` 新增路由；`AppLayout.vue`「财务」分组续行「预算管理」+ `MENU_ROUTE_MAP`
- [ ] 5.4 按钮接入 `auth.hasPermission` 与 loading（`010`）；已结账月份禁用
- [ ] 5.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/budget.spec.ts`：编预算 → 发生费用 → 执行对比显示实际 / 差异 / 执行率
- [ ] 6.2 同文件：已结账月份改预算 `40154`；非损益科目 `40191`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含总账 / 费用域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `budgets.*`；刷新 `updated`
- [ ] 7.2 `specs/025-erp-report/design.md` §0.2「财务」分组续行；刷新 `updated`
- [ ] 7.3 `.codebuddy/CONTEXT.md` §2（实体 / 读模型 / 仓储）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.4 `specs/ROADMAP.md` §4.2 状态更新（`054` → 已实现）；§3 / §7 同步；§6 错误码续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 预算编制 → 执行对比闭环可用；执行口径与 `033` §0.4 一致；不产生任何写入拦截。

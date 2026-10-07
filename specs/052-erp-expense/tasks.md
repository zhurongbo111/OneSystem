---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：费用报销（erp-expense）

> 依据 `specs/052-erp-expense/design.md` 拆分。新增报销单（三态）+ 审批生成凭证 + 权限 / 菜单续行 + 前端 + e2e。
> 前置：`033`（凭证通道）、`031`（科目）、`034`（资金账户）、`030`（员工）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `Expense` / `ExpenseItem` + 枚举 `ExpenseStatus`；`AppDbContext` 追加两 DbSet
- [ ] 1.2 EF 配置（`ExpenseNo` 唯一、FK、索引、快照列）；`VoucherSourceType` 续行 `Expense`
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpExpense -p src/App.Infrastructure -s src/App.Api`

## 二、后端：仓储与用例

- [ ] 2.1 读模型 `ExpenseListItem` / `ExpenseDetail` + `IExpenseRepository`（分页 / 详情 / 增改（明细全量替换）/ 单号 / `UpdateStatusAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 DTO + `ExpensesDtoMapper`
- [ ] 2.3 用例 `CreateExpense` / `GetExpenses` / `GetExpenseById` / `UpdateExpense`（`40185`）/ `VoidExpense`（`40185`）
- [ ] 2.4 用例 `ApproveExpense`（科目校验 `40187` + 同事务生成凭证，复用 `033` `VoucherFactory` / `VoucherWriter`）
- [ ] 2.5 `ExpensesController`（6 端点）+ DI；写用例接入操作日志（`029` §0.1 续行，资源 `Expense`）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40185`–`40187`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40188`

## 四、单元测试（后端）

- [ ] 4.1 创建：合计计算、报销人 / 账户存在性
- [ ] 4.2 编辑 / 作废：草稿可改、非草稿 `40185`
- [ ] 4.3 审批：`40186` / `40187`；凭证分录与平衡；`40154` 回滚；`40158`
- [ ] 4.4 列表：筛选与明细行数
- [ ] 4.5 字段约束一致性单测
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/expense.ts`（+ `EXPENSE_STATUS_META`）
- [ ] 5.2 `views/ExpenseManagement/ExpensesView.vue`
- [ ] 5.3 `ExpenseFormPage.vue`（科目树仅损益类末级启用 + 明细合计）
- [ ] 5.4 `ExpenseDetailView.vue`（审批 / 作废）
- [ ] 5.5 `router/index.ts` 新增 3 条路由；`AppLayout.vue`「财务」分组续行「费用报销」+ `MENU_ROUTE_MAP`
- [ ] 5.6 按钮接入 `auth.hasPermission` 与 loading（`010`）
- [ ] 5.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/expense.spec.ts`：建报销单 → 审批 → 总账凭证 / 科目余额体现
- [ ] 6.2 同文件：费用科目非法 `40187`；重复审批 `40186`；已审批编辑 `40185`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含总账 / 资金域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/033-erp-general-ledger/design.md` §0.1 `VoucherSourceType` 续行 `Expense`；刷新 `updated`
- [ ] 7.2 `specs/028-erp-rbac/design.md` §0.2 续行 `expenses.*`；刷新 `updated`
- [ ] 7.3 `specs/025-erp-report/design.md` §0.2「财务」分组续行；刷新 `updated`
- [ ] 7.4 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 读模型 / 仓储）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.5 `specs/ROADMAP.md` §4.2 状态更新（`052` → 已实现）；§3 覆盖矩阵「L4 财务 · 报销 / 固资 / 预算」同步；§7「预算 / 固定资产 / 费用报销」条目改列待评估；§6 错误码续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 报销 → 审批 → 凭证闭环可用；期间闸门与映射缺失整体回滚。

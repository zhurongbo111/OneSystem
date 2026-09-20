---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：资金出纳（erp-cash）

> 依据 `specs/034-erp-cash/design.md` 拆分。含资金账户 + 资金日记账 + 余额总览 + `023` 收付款关联账户改造 + 权限 / 菜单续行 + e2e。
> 前置：`023`（收付款）已实现；`033`（总账）弱依赖。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `BankAccount` + 枚举 `BankAccountType` / `BankAccountStatus`；`AppDbContext` 追加 `BankAccounts`
- [ ] 1.2 字段约束常量类 `BankAccountFieldConstraints`
- [ ] 1.3 EF 配置 `BankAccountConfiguration`（唯一 `Code`）；`SettlementConfiguration` 追加 `BankAccountId`（可空 FK + 索引）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpCash -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.5 `DatabaseInitializer` 幂等预置现金账户（`Code = CASH`）

## 二、后端：资金账户用例与接口

- [ ] 2.1 读模型 `BankAccountListItem` / `BankAccountBalanceItem` + `IBankAccountRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `GetAllEnabledAsync` / `GetBalancesAsync` / `IsReferencedAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `BankAccountListItemDto` / `BankAccountDetailDto` / `BankAccountBalanceItemDto` + `BankAccountDtoMapper`
- [ ] 2.3 用例 `BankAccounts/GetBankAccounts` / `CreateBankAccount` / `GetBankAccountById` / `UpdateBankAccount` / `DeleteBankAccount`（`40161`）/ `UpdateBankAccountStatus` / `GetBankAccountSummary`
- [ ] 2.4 `BankAccountsController`（7 端点）+ DI 注册

## 三、后端：资金日记账

- [ ] 3.1 读模型 `CashJournalEntryItem` / `CashJournalResult` + `ICashJournalQueryRepository`（`GetJournalAsync`）+ 实现 + 注册
- [ ] 3.2 用例 `CashJournals/GetCashJournal`（期初 + 流水 + 逐笔结余 + 期末）
- [ ] 3.3 `CashJournalsController`（1 端点）+ DI 注册

## 四、后端：`023` 收付款关联账户改造

- [ ] 4.1 `Settlement` 增 `BankAccountId`；`ISettlementRepository` 列表 / 详情出参增 `BankAccountName`
- [ ] 4.2 `CreateSettlement` 校验：账户存在（`40400`）、启用、类型与 `method` 匹配（`40162`）；落库 `BankAccountId`
- [ ] 4.3 `GetSettlements` / `GetSettlementById` 出参增账户名

## 五、后端：错误码

- [ ] 5.1 `ErrorCode.cs` 追加 `40160`–`40162`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40163`

## 六、单元测试（后端）

- [ ] 6.1 账户用例：编码唯一、银行账户缺开户行 / 账号 `40000`、被引用禁删 `40161`、筛选分页
- [ ] 6.2 余额 / 日记账：口径正确、排除作废、期初期末与逐笔结余、空区间
- [ ] 6.3 类型匹配：现金单挂银行账户 `40162` / 银行单挂现金账户 `40162` / `Other` 通过
- [ ] 6.4 种子幂等单测
- [ ] 6.5 字段约束一致性单测
- [ ] 6.6 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归，含 `023` 收付款链路）

## 七、前端

- [ ] 7.1 `api/bankAccount.ts`（账户 CRUD / summary / 日记账）
- [ ] 7.2 `views/CashManagement/BankAccountsView.vue`（余额总览 + 列表）+ `BankAccountFormDrawer.vue`
- [ ] 7.3 `views/CashManagement/CashJournalsView.vue`
- [ ] 7.4 `views/SettlementManagement/SettlementFormPage.vue`（`023`）追加资金账户选择（按结算方式过滤类型）
- [ ] 7.5 `router/index.ts` 新增 `bank-accounts` / `cash-journals`；`AppLayout.vue`「财务」分组续行两项 + `MENU_ROUTE_MAP`
- [ ] 7.6 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 7.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 八、E2E（Playwright）

- [ ] 8.1 新增 `e2e/cash.spec.ts`：建银行账户 → 收款关联账户 → 资金日记账出现该笔、余额正确
- [ ] 8.2 同文件：现金账户关联银行转账 `40162`；删除被引用账户 `40161`
- [ ] 8.3 `cd frontend && npm run test:e2e` 全量通过（含 `settlement.spec.ts` 回归）

## 九、规格与上下文联动

- [ ] 9.1 `specs/028-erp-rbac/design.md` §0.2 续行 `bankAccounts.*` / `cashJournals.view`；刷新其 `updated`
- [ ] 9.2 `specs/025-erp-report/design.md` §0.2「财务」分组续行「银行账户」「资金日记账」
- [ ] 9.3 `specs/023-erp-settlement/design.md` 加「演进（erp-cash）」注记（`Settlement.BankAccountId`）
- [ ] 9.4 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 9.5 `specs/ROADMAP.md` §4.2 状态更新（`034` → 已实现）；§3 覆盖矩阵「资金出纳」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 清单守卫通过：8 个新增端点均标注合法权限点；账户类型与结算方式匹配校验生效。

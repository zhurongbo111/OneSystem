---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：总账（erp-general-ledger）

> 依据 `specs/033-erp-general-ledger/design.md` 拆分。含会计期间 + 凭证（手工 / 自动）+ 科目映射 + 科目余额表 + 资产负债表 / 利润表 + **既有单据写入路径改造** + e2e。
> 前置：`031`（会计科目 / 税率）、`023`（收付款）、`026`（成本）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> **重要**：§五 改造既有单据写入路径，实现期间请分单据类型推进并保持 `dotnet test` 全绿（自动凭证与单据同事务）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `AccountingPeriod` / `Voucher` / `VoucherEntry` / `AccountMapping` + 枚举 `PeriodStatus` / `VoucherStatus` / `VoucherSourceType`；`AppDbContext` 追加 4 个 `DbSet`
- [ ] 1.2 字段约束常量类 `VoucherFieldConstraints` / `AccountMappingFieldConstraints`
- [ ] 1.3 EF 配置（唯一索引、FK、`numeric(18,2)`、快照列）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpGeneralLedger -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.5 `DatabaseInitializer` 幂等种子：预置 `AccountMappings` 8 键 → `031` 预置科目；预置当年 12 个期间

## 二、后端：期间与凭证用例

- [ ] 2.1 仓储 `IAccountingPeriodRepository` / `IVoucherRepository` / `IAccountMappingRepository` + 实现 + 注册
- [ ] 2.2 共享服务 `VoucherFactory`（§0.2 模板）+ 借贷平衡校验
- [ ] 2.3 共享出参 `PeriodDto` / `VoucherListItemDto` / `VoucherDetailDto` / `VoucherEntryDto` / `AccountMappingDto` + Mapper
- [ ] 2.4 用例 `AccountingPeriods/GetPeriods` / `ClosePeriod` / `ReversePeriod`
- [ ] 2.5 用例 `Vouchers/GetVouchers` / `CreateVoucher`（手动）/ `GetVoucherById` / `VoidVoucher`
- [ ] 2.6 用例 `AccountMappings/GetAccountMappings` / `UpdateAccountMappings`
- [ ] 2.7 `AccountingPeriodsController` / `VouchersController` / `AccountMappingsController` + DI 注册

## 三、后端：报表

- [ ] 3.1 `IFinancialReportQueryRepository`（`GetAccountBalancesAsync` / `GetBalanceSheetAsync` / `GetIncomeStatementAsync`）+ 实现 + 注册
- [ ] 3.2 用例 `FinancialReports/GetAccountBalance` / `GetBalanceSheet` / `GetIncomeStatement`
- [ ] 3.3 `FinancialReportsController`（3 端点）+ DI 注册

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40154`–`40159`（§3.3）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40160`

## 五、后端：既有单据写入路径改造（自动凭证，按单据类型推进）

> 每类完成后跑 `dotnet test`，保持全绿。

- [ ] 5.1 `015` 采购入库：`CreatePurchaseReceipt` 追加凭证（借存货 / 贷应付）；`VoidPurchaseReceipt` 作废凭证
- [ ] 5.2 `016` 销售出库：`CreateSalesShipment` 追加收入 + 成本结转分录；`VoidSalesShipment` 作废
- [ ] 5.3 `021` 采购退货：`CreatePurchaseReturn` / `VoidPurchaseReturn`
- [ ] 5.4 `022` 销售退货：`CreateSalesReturn`（收入冲减 + 成本转回）/ `VoidSalesReturn`
- [ ] 5.5 `023` 收付款：`CreateSettlement`（按 `SettlementMethod` 选现金 / 银行科目）/ `VoidSettlement`

## 六、单元测试（后端）

- [ ] 6.1 `VoucherFactory`：6 类来源 + 成本结转分录正确；收付款选科目；映射缺失 `40158`
- [ ] 6.2 手工凭证：平衡 / 不平衡 `40155` / 无分录 `40156` / 科目非法 `40157` / 期间已结账 `40154` / 期间不存在 `40159`
- [ ] 6.3 期间：结账 / 反结账 / 结账后禁记账
- [ ] 6.4 自动凭证改造：创建后有凭证且平衡；作废后凭证 `Voided`；映射缺失使单据失败回滚
- [ ] 6.5 报表：科目余额表口径；资产负债表恒等式；利润表与 `026` 毛利一致
- [ ] 6.6 字段约束一致性单测
- [ ] 6.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归，含自动凭证改造后的单据链路）

## 七、前端

- [ ] 7.1 `api/voucher.ts`（凭证 / 期间 / 映射）；`api/financialReport.ts`
- [ ] 7.2 `views/GeneralLedgerManagement/VouchersView.vue`（筛选 / 列表 / 作废）+ `VoucherFormPage.vue`（分录子表 + 借贷差额）+ `VoucherDetailView.vue`
- [ ] 7.3 `views/GeneralLedgerManagement/AccountMappingsView.vue`
- [ ] 7.4 `views/GeneralLedgerManagement/FinancialReportsView.vue`（三 tab）
- [ ] 7.5 `router/index.ts` 新增 4 条路由（`meta.permission`）；`AppLayout.vue`「财务」分组续行「凭证」「财务报表」+ `MENU_ROUTE_MAP`
- [ ] 7.6 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 7.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 八、E2E（Playwright）

- [ ] 8.1 新增 `e2e/general-ledger.spec.ts`：开采购单 → 凭证自动生成（借存货 / 贷应付）→ 借方合计 = 贷方合计
- [ ] 8.2 同文件：开销售单（含成本）→ 收入 + 成本结转凭证 → 利润表毛利与成本报表一致
- [ ] 8.3 同文件：手工凭证不平衡 `40155`；作废单据 → 凭证作废、余额回退
- [ ] 8.4 同文件：结账后新增凭证 `40154`
- [ ] 8.5 `cd frontend && npm run test:e2e` 全量通过（含既有单据用例回归——自动凭证不改变既有可见行为）

## 九、规格与上下文联动

- [ ] 9.1 `specs/028-erp-rbac/design.md` §0.2 续行 `vouchers.*` / `financialReports.view`；刷新其 `updated`
- [ ] 9.2 `specs/025-erp-report/design.md` §0.2「财务」分组续行「凭证」「财务报表」
- [ ] 9.3 `specs/015` / `016` / `021` / `022` / `023` / `026` 各 `design.md` 加「演进（erp-general-ledger）」注记
- [ ] 9.4 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 9.5 `specs/ROADMAP.md` §4.2 状态更新（`033` → 已实现）；§3 覆盖矩阵「总账」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 自动凭证与单据同事务：不存在"单据生效但无凭证"；映射缺失经种子保证开箱可用。
- 清单守卫通过：12 个新增端点均标注合法权限点。

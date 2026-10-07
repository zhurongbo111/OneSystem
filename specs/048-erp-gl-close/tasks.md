---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：期初建账与期末结转（erp-gl-close）

> 依据 `specs/048-erp-gl-close/design.md` 拆分。扩展 `033`：期初余额表 + 损益结转 + 结账闸门 + 年末结转 + 报表取数修订 + 前端 + e2e。
> 前置：`033`（期间 / 凭证 / 报表）、`031`（科目 / 预置科目 / 种子）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与种子

- [ ] 1.1 新增实体 `OpeningBalance` + `OpeningBalanceFieldConstraints`；`AppDbContext` 追加 `OpeningBalances`
- [ ] 1.2 EF 配置（`AccountId` 唯一、FK、列类型）
- [ ] 1.3 `VoucherSourceType` 续行 `ProfitCarry` / `YearCarry`
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpGlClose -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.5 `DatabaseInitializer` 幂等追加预置科目 `4104 利润分配` 与映射键 `RetainedEarnings`

## 二、后端：仓储与只读查询

- [ ] 2.1 `IOpeningBalanceRepository`（`GetAllAsync` / `ReplaceAllAsync`）+ 实现 + 注册；读模型 `OpeningBalanceItem`
- [ ] 2.2 `IVoucherRepository` 追加 `ExistsPostedAsync` / `ExistsBySourceAsync`
- [ ] 2.3 `IProfitCarryQueryRepository`（`GetPeriodProfitNetAsync` / `GetProfitBalanceAsync`）+ 实现 + 注册；读模型 `ProfitCarryEntryItem` / `ProfitBalanceItem`

## 三、后端：用例与接口

- [ ] 3.1 `OpeningBalances/GetOpeningBalances` / `SaveOpeningBalances`（平衡校验 `40000` / 窗口 `40176`）+ `OpeningBalancesController` + DI
- [ ] 3.2 `AccountingPeriods/CarryProfit`（模板组装 + 幂等 `40177` / 空 `40178`）+ 端点
- [ ] 3.3 `AccountingPeriods/CarryYear`（前置 `40180` / `40159` + 幂等）+ 端点
- [ ] 3.4 `CloseAccountingPeriod` 改造：增结账闸门 `40179`
- [ ] 3.5 报表取数改造（`GetAccountBalance` / `GetBalanceSheet` 期初两段相加）
- [ ] 3.6 复用 `033` 的 `VoucherFactory` / `VoucherWriter` 写入结转凭证（不另立路径）

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40176`–`40180`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40181`

## 五、单元测试（后端）

- [ ] 5.1 期初：平衡 / 不平 `40000`、窗口 `40176`、末级 / 停用 `40157`、全量替换
- [ ] 5.2 损益结转：分录正确、借贷平衡、`40177` / `40178` / `40158`
- [ ] 5.3 结账闸门 `40179` + `033` 结账回归
- [ ] 5.4 年末结转：`40180`（前置 / 零余额 / 重复）、`40159`；结转后余额正确
- [ ] 5.5 报表期初两段相加 + 资产负债表恒等式
- [ ] 5.6 字段约束一致性单测
- [ ] 5.7 `cd backend && dotnet build` / `dotnet test` 通过（含 `033` 回归）

## 六、前端

- [ ] 6.1 `api/voucher.ts`：期初余额 / 结转 / 年结接口与类型
- [ ] 6.2 `views/GeneralLedgerManagement/OpeningBalanceDrawer.vue`（新增，借贷合计与差额提示）
- [ ] 6.3 `PeriodManagementDrawer.vue`：增「结转损益 / 年末结转」（状态置灰与原因）
- [ ] 6.4 `FinancialReportsView.vue`：科目余额表工具条「期初建账」入口
- [ ] 6.5 按钮接入 loading（`010`）与权限（`auth.hasPermission`）
- [ ] 6.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/gl-close.spec.ts`：期初建账（平衡成功 / 不平 `40000`）→ 记账 → 结转损益 → 结账
- [ ] 7.2 同文件：未结转结账 `40179`；年末结转后本年利润清零
- [ ] 7.3 `cd frontend && npm run test:e2e` 全量通过（含总账 / 报表域回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/033-erp-general-ledger/design.md` §0.1（`VoucherSourceType` 续行、结账闸门）/ §0.3（`RetainedEarnings`）/ §0.4（期初口径修订）改写为最终态 + 演进指针；刷新 `updated`
- [ ] 8.2 `specs/031-erp-finance-master/design.md` §2.4 预置科目续行 `4104`；刷新 `updated`
- [ ] 8.3 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 读模型 / 仓储）/ §3（总账域）同步
- [ ] 8.4 `specs/ROADMAP.md` §4.2 状态更新（`048` → 已实现）；§3 覆盖矩阵「L4 财务 · 总账 GL」缺口同步；§6 错误码占用续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 建账 → 记账 → 结转 → 结账 → 年结闭环可用；报表期初口径与 §0.1 一致。

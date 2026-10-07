---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：多币种与合并报表（erp-multi-currency）

> 依据 `specs/058-erp-multi-currency/design.md` 拆分。新增币种 / 汇率 + 公司本位币 + 单据 / 凭证多币种 + 合并报表 + 前端 + e2e。
> 前置：`057`（多公司隔离）、`033`（凭证 / 报表）、`031`（科目）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `Currency` / `ExchangeRate` + 枚举 `CurrencyStatus` + 两常量类；`AppDbContext` 追加两 DbSet
- [ ] 1.2 EF 配置（`Code` 唯一、`(From, To, EffectiveDate)` 唯一、FK）
- [ ] 1.3 `Company` 追加 `BaseCurrencyId`；`VoucherEntry` 追加 `CurrencyId` / `ExchangeRate`
- [ ] 1.4 §0.3 单据追加 `CurrencyId` / `ExchangeRate`（默认 = 公司本位币 / 1 并回填）
- [ ] 1.5 增量迁移 `dotnet ef migrations add AddErpMultiCurrency -p src/App.Infrastructure -s src/App.Api`（含预置 `CNY`）
- [ ] 1.6 `DatabaseInitializer` 幂等保证 `CNY` 存在并为各公司本位币默认

## 二、后端：组件与用例

- [ ] 2.1 `ICurrencyRepository` / `IExchangeRateRepository` + 实现 + 注册；读模型
- [ ] 2.2 `IExchangeRateResolver`（同币种 1 / 取最新 / `40198` / `40199`）+ 注册
- [ ] 2.3 用例 `Currencies/*`（列表 / 新增 / 编辑 / 状态）+ `CurrenciesController`
- [ ] 2.4 用例 `ExchangeRates/*`（列表 / 新增 / 删除）+ `ExchangeRatesController`
- [ ] 2.5 `057` 扩展：`Companies/UpdateBaseCurrency`（`40198` / `40200`）
- [ ] 2.6 单据 `Create*` 改造：按业务日期取汇率、金额存本位币、落快照；自动凭证透传至分录
- [ ] 2.7 用例 `ConsolidatedReports/GetConsolidatedReport`（折算合并；`40199`）+ Controller
- [ ] 2.8 写用例接入操作日志（`029` §0.1 续行，资源 `Currency` / `ExchangeRate`）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40198`–`40200`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40201`

## 四、单元测试（后端）

- [ ] 4.1 汇率：同日唯一、取最新、同币种 1、`40198` / `40199`
- [ ] 4.2 折算：本位币金额 = 原币 × 快照汇率；汇率变动不改历史
- [ ] 4.3 本位币：有数据改 `40200`；停用本位币币种 `40198`
- [ ] 4.4 凭证：自动透传、手工默认本位币
- [ ] 4.5 合并报表：单公司单币种与 `033` 一致；两公司折算合计；`40199`
- [ ] 4.6 回归：`057` 隔离用例；仅 `CNY` 时既有用例全绿
- [ ] 4.7 字段约束一致性单测
- [ ] 4.8 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/currency.ts` / `api/consolidatedReport.ts`；既有单据 api 类型增 `currencyId` / `exchangeRate`
- [ ] 5.2 `views/CurrencyManagement/CurrenciesView.vue`（币种 + 汇率两 tab）
- [ ] 5.3 `views/ReportManagement/ConsolidatedReportView.vue`（年度 + 报告币种）
- [ ] 5.4 单据表单增币种下拉 + 折算本位币展示
- [ ] 5.5 `router/index.ts` 新增路由；`AppLayout.vue`「系统」续行「币种与汇率」、「报表」续行「合并报表」+ `MENU_ROUTE_MAP`
- [ ] 5.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/multi-currency.spec.ts`：维汇率 → 外币单据 → 本位币金额正确；合并报表折算合计
- [ ] 6.2 同文件：缺汇率 `40199`；本位币已用后修改 `40200`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含 `057` 回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/057-erp-multi-org/design.md` §0.1 / §2.1 增本位币；刷新 `updated`
- [ ] 7.2 `specs/033-erp-general-ledger/design.md` §0.4 / §2.3 增分录币种口径；刷新 `updated`
- [ ] 7.3 `specs/028-erp-rbac/design.md` §0.2 续行 `currencies.*` / `exchangeRates.*` / `consolidatedReports.view`；刷新 `updated`
- [ ] 7.4 `specs/025-erp-report/design.md` §0.2 续行两项；刷新 `updated`
- [ ] 7.5 `.codebuddy/CONTEXT.md` §2（实体 / 组件 / 读模型）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.6 `specs/ROADMAP.md` §4.2 状态更新（`058` → 已实现）；§7 条目改列待评估；§6 续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 多币种单据 / 凭证闭环可用；合并报表折算正确；仅 `CNY` 时行为与升级前一致。

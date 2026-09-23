---
created: 2026-09-17
updated: 2026-09-23
---

# 任务清单：进销存报表（erp-report）

> 依据 `specs/025-erp-report/design.md` 拆分。纯只读：仅新增只读查询仓储 + 4 个用例 + 前端 4 页 + 菜单分组重构。按顺序实现，完成后勾选。
> 前置：`019`（流水）已实现；`012` / `013` / `015` / `016` / `021` / `022` / `023` 已实现（金额口径消费其单据表）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：只读查询仓储

- [x] 1.1 新增读模型 `App.Core/Abstractions/InventoryFlowItem.cs` / `StockBalanceItem.cs` / `PurchaseSummaryItem.cs` / `SalesSummaryItem.cs`（可选含合计模型），`sealed record` + `required` + `init`（合计模型另建 `InventoryFlowTotal` / `StockBalanceTotal` / `PurchaseSummaryTotal` / `SalesSummaryTotal`）
- [x] 1.2 新增 `App.Core/Abstractions/IReportQueryRepository.cs`（4 个方法，签名见 design §3.1）
- [x] 1.3 `App.Infrastructure/Repositories/ReportQueryRepository.cs`：进销存报表（期初段 + 区间段按 `ProductId` 合并，`StockTakeAdjust` 按符号拆分，`onlyChanged` 过滤，全量合计）
- [x] 1.4 同仓储：库存余额表（按分类聚合 + 低库存 / 零库存计数）、采购汇总、销售汇总（往来 / 商品两维度，作废过滤，退货合并）
- [x] 1.5 `App.Infrastructure/DependencyInjection.cs` 注册 `IReportQueryRepository`

## 二、后端：用例与接口

- [x] 2.1 `Features/Reports/ReportFieldConstraints.cs`（`MaxRangeDays = 366`）+ `ReportPageDto<T>`（`items` / `total` / `page` / `pageSize` / `summary`；实现为两个泛型参数 `ReportPageDto<TItem, TSummary>`，为避免污染全局分页契约）
- [x] 2.2 共享出参 `Features/Reports/` 下 4 个 `*Dto`（进销存报表行 / 余额行 / 采购汇总行 / 销售汇总行 + 各合计模型）+ `ReportsDtoMapper.cs`（正向 `To` + DTO 名）
- [x] 2.3 新增用例 `Reports/GetInventoryFlow`（Request + Validator + Handler + Response）
- [x] 2.4 新增用例 `Reports/GetStockBalance`
- [x] 2.5 新增用例 `Reports/GetPurchaseSummary`（`groupBy` 枚举 `SummaryGroupBy`）
- [x] 2.6 新增用例 `Reports/GetSalesSummary`
- [x] 2.7 新增 `App.Api/Controllers/ReportsController.cs`（4 个 GET，`FromQuery` 传参）
- [x] 2.8 `App.Core/DependencyInjection.cs` 注册 4 个 Handler 与对应 Validator

## 三、单元测试

- [x] 3.1 `GetInventoryFlow`：筛选传参组合 / `期末 = 期初 + 入 − 出` / `summary` 为全量口径 / 空结果
- [x] 3.2 `GetStockBalance`：分类聚合映射 + 低库存口径与 `014` 同源 + 全库合计
- [x] 3.3 `GetPurchaseSummary` / `GetSalesSummary`：两个分组维度 / 净额 = 入 − 退 / 无退货分组为 0 / 作废过滤透传
- [x] 3.4 Validator 边界：`start < end`、366 天上限、`pageSize` 100/101、`keyword` 50/51
- [x] 3.5 **对账一致性**：期初 + 采购 + 销售 + 盘点链路的 `期末 == Inventory.Quantity`（以真实 `ReportQueryRepository` + InMemory 验证口径，另附盘点双向拆分 / 半开区间 / 期初不漂移用例）
- [x] 3.6 扩展 `FieldValidationConsistencyTests`（`MaxRangeDays` 同源；`keyword` 对齐 `ProductFieldConstraints.KeywordMaxLength`）
- [x] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [x] 4.1 `src/api/report.ts`（`ReportPage<T>` + 4 个查询函数 + 类型）
- [x] 4.2 `InventoryFlowReportView.vue`（筛选 + 表格 + 合计区 + 列设置 + 服务端分页）
- [x] 4.3 `StockBalanceReportView.vue`（分类聚合 + 占比 + 「查看明细」跳库存查询并预置 `categoryId`）；占比传给 `a-progress` 的是 0–1 比例（该组件 `percent` 本身即 0–1），显示文本用 `#text` 插槽固定 `xx.xx%`，与导出侧同口径
- [x] 4.4 `PurchaseSummaryReportView.vue`（期间 + 供应商 + 分组维度切换 + 净额列 + 合计行）
- [x] 4.5 `SalesSummaryReportView.vue`（同采购，客户维度）
- [x] 4.6 `src/router/index.ts` 新增 4 条路由（`reports/*`，懒加载 + `requiresAuth`）
- [x] 4.7 `src/components/AppLayout.vue` 按 design §0.2 总表重构菜单为多顶级分组（含迁移既有子项、`MENU_ROUTE_MAP` 同步）
- [x] 4.8 `frontend/e2e/helpers/menu.ts` 的「菜单项 → 分组」映射同步为新分组名
- [x] 4.9 `.codebuddy/rules/frontend/RULE.mdc` §4.8 后新增「比例类 props 语义」判据（`a-progress.percent` 为 0–1，不再二次 ×100）
- [x] 4.10 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 新增 `e2e/report.spec.ts`：进销存报表（期间筛选 → 期初 / 入 / 出 / 期末四列展示、合计区可见）
- [x] 5.2 同文件：库存余额表分类聚合行 + 「查看明细」跳库存查询并预置分类；先期初建账造非零库存，再断言占比文本恰为 `100.00%`（占比为 0 时该缺陷不可见）
- [x] 5.3 同文件：采购 / 销售汇总（分组维度切换 → 净额列可见）
- [x] 5.4 同文件：菜单分组调整后各分组可达（「报表」「库存」「资金」等分组展开与选中联动）
- [x] 5.5 `cd frontend && npm run test:e2e` 全量通过（含既有各域 spec 在菜单分组调整后的回归）

## 六、规格与上下文联动

- [x] 6.1 `specs/005-app-layout/design.md` 加「演进（erp-report）」注记：菜单由两组改为多顶级分组（§0.2 总表）
- [x] 6.2 `specs/009-user-management/design.md` 加注记：「用户管理」「登录日志」归入「系统」分组
- [x] 6.3 `specs/014-erp-inventory-query/design.md` 加注记：库存余额表（汇总视图）由 `025` 提供，明细视图仍以 `014` 为准
- [x] 6.4 `specs/019-erp-stock-movement/design.md` §0 加注记：报表按变动类型归类入 / 出（口径在 `025` §0.1）
- [x] 6.5 `.codebuddy/CONTEXT.md` §2（Reports Feature / 只读仓储 / 读模型）、§3（ReportManagement 域、api 文件）、§6（规格清单分类）同步
- [x] 6.6 `specs/ROADMAP.md` 状态列更新（`025` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。

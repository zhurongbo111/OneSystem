---
created: 2026-09-17
updated: 2026-09-23
---

# 任务清单：成本核算与销售毛利（erp-cost）

> 依据 `specs/026-erp-cost/design.md` 拆分。含既有表加成本列 + 9 处写入路径成本接入 + 重算用例 + 成本毛利报表 + 期初成本录入。**按阶段顺序实现，每阶段结束跑全量测试**。
> 前置：`019` / `020` / `021` / `022` 已实现（本规格改造其写入路径）；`025` 已实现（报表域、`ReportPageDto`、`MaxRangeDays`、菜单「报表」分组）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [x] 1.1 `Inventory` 追加 `CostAmount` / `AverageCost`；`StockMovement` 追加 `UnitCost` / `TotalCost`；`StockTakeItem` 追加 `UnitCost`（实体 + EF 配置，`numeric(18,4)`，默认 0）
- [x] 1.2 增量迁移 `dotnet ef migrations add AddErpCost -p src/App.Infrastructure -s src/App.Api`（迁移内不回填，回填走重算用例）
- [x] 1.3 读模型：新增 `StockMovementCostRow`；`StockBalanceItem`（`025`）追加 `TotalCostAmount` / `AverageCost` / `HasCostAnomaly`；`StockMovementItem`（`019`）追加 `UnitCost` / `TotalCost`；新增 `CostProfitItem`（对应合计模型 `CostProfitTotal`）
- [x] 1.4 `ErrorCode.cs` 追加 `40118 CostRecalculationRunning`

## 二、后端：成本数据能力

- [x] 2.1 `IInventoryRepository` 追加 `GetAverageCostAsync` / `ApplyInboundCostAsync` / `ApplyOutboundCostAsync`（`ExecuteUpdateAsync` 原子表达，先加数量再加金额；结存 0 归零）+ 实现（另加 `SetCostAsync`，仅重算写回成本两列用）
- [x] 2.2 `IStockMovementRepository` 追加 `GetMovementUnitCostAsync`（冲销还原成本）与 `GetAllForCostAsync`（重算取数，按 `CreatedAt, Id` 升序）+ 实现（另加 `UpdateCostAsync`，仅重算写回流水成本列用）
- [x] 2.3 `FieldValidationConsistencyTests` 扩展：`unitCost` 边界同源 `ProductFieldConstraints.PriceMaxValue`；新增 `numeric(18,4)` 精度断言辅助

## 三、后端：写入路径成本接入（9 处）

- [x] 3.1 `Purchases/CreatePurchaseOrder`：入库按单据明细单价加权 + 流水成本列
- [x] 3.2 `Purchases/VoidPurchaseOrder`：按原入库流水单价回冲
- [x] 3.3 `Sales/CreateSalesOrder`：按变动前均价结转出库成本
- [x] 3.4 `Sales/VoidSalesOrder`：按原出库流水单价回正
- [x] 3.5 `StockTakes/CreateStockTake`：期初按录入单价、盘点按当前均价（盘盈入 / 盘亏出）
- [x] 3.6 `PurchaseReturns/CreatePurchaseReturn` + `VoidPurchaseReturn`（均价出 / 原单价回）
- [x] 3.7 `SalesReturns/CreateSalesReturn` + `VoidSalesReturn`（原销售成本入 / 原单价回，兜底均价）
- [x] 3.8 所有路径断言：成本与数量同事务、失败路径不写成本、`Commit` 异常回滚（既有用例的事务调用序列已同步新增成本调用）

## 四、后端：用例与接口

- [x] 4.1 新增用例 `Costs/RecalculateCosts`（锁 + 时序推演 + 批量写回 + 统计返回；幂等；并发 `40118`；冲销单价优先取本次推演结果，与历史成本列无关）
- [x] 4.2 新增用例 `Reports/GetCostProfitReport`（`groupBy` 三维度 + 毛利 / 毛利率 + 成本缺失标注）
- [x] 4.3 `ReportsController` 追加 `GET /api/reports/cost-profit`；新增 `CostsController`（`POST /api/costs/recalculate`）
- [x] 4.4 `App.Core/DependencyInjection.cs` 注册 2 个 Handler + Validator + `CostRecalculationLock`（Singleton）；`AddInfrastructure` 无需新增注册
- [x] 4.5 `CreateStockTakeRequestValidator` 扩展：期初 `unitCost` 必填与区间、盘点模式禁止传入

## 五、单元测试

- [x] 5.1 加权平均链路（期初 → 入库 → 出库 → 作废回冲）逐条断言单价 / 金额 / 均价
- [x] 5.2 冲销还原成本（采购 / 销售作废）+ 缺价兜底（原流水缺失按 0 计入且不阻断）
- [x] 5.3 盘点成本（盘盈 / 盘亏 / 无差异不动成本）+ 期初按录入单价
- [x] 5.4 `RecalculateCosts`：结果与逐条写入一致、二次执行幂等、并发 `40118`、统计正确、不改 `Quantity`、期间过滤只限写回范围
- [x] 5.5 `GetCostProfitReport`：毛利 / 毛利率（收入 0 → `null`）/ 退货冲减 / 成本缺失标注 / 传参透传
- [x] 5.6 对账一致性：`Σ 流水 TotalCost == Inventory.CostAmount`（重算用例推演结果与库存成本列一致，含作废回冲链路）
- [x] 5.7 Validator 边界：期初建账成本必填与区间、盘点模式禁止传成本、重算区间 366 天
- [x] 5.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）—— 506 通过 / 0 失败

## 六、前端

- [x] 6.1 `src/api/cost.ts`（`recalculateCosts` + 结果类型）；`src/api/report.ts` 追加 `getCostProfitReport`
- [x] 6.2 `views/ReportManagement/CostProfitReportView.vue`（筛选 + 分组维度 + 毛利列 + 合计 + 成本完整性标签 + 「重算成本」`a-popconfirm` + `recalculating`）
- [x] 6.3 `StockTakeFormPage.vue` 期初模式成本单价列 + 期初金额 computed + 模式切换清空
- [x] 6.4 `StockBalanceReportView.vue`（`025`）追加库存金额 / 均价 / 成本异常列与合计
- [x] 6.5 `StockMovementsView.vue`（`019`）追加成本单价 / 成本金额列
- [x] 6.6 `src/router/index.ts` 新增 `reports/cost-profit`；`AppLayout.vue`「报表」分组追加「成本与毛利」
- [x] 6.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [x] 7.1 新增 `e2e/cost.spec.ts`：期初建账（含成本单价）→ 采购入库（改单价）→ 成本与毛利报表展示均价 / 金额
- [x] 7.2 同文件：销售出库后毛利行金额正确（收入 / 成本 / 毛利）；该单作废后毛利归 0
- [x] 7.3 同文件：重算成本按钮（popconfirm → loading → 成功提示含流水条数）
- [x] 7.4 改造既有 `stock-take.spec.ts`：期初建账补成本单价必填（未填被拦 + 填写后通过）
- [x] 7.5 `cd frontend && npm run test:e2e` 全量通过（含 `report` / `stock-movement` / `purchase` / `sale` / 退货既有用例回归）

## 八、规格与上下文联动

- [x] 8.1 `specs/019-erp-stock-movement/design.md` §0 加「演进（erp-cost）」注记：变动类型表续行成本单价来源；流水追加成本列
- [x] 8.2 `specs/020-erp-stock-take/design.md` 加注记：期初建账新增成本单价（必填）、明细追加 `UnitCost`、页面成本列
- [x] 8.3 `specs/021` / `022` `design.md` 加注记：退货 / 作废回冲的成本口径与流水成本列
- [x] 8.4 `specs/025-erp-report/design.md` §0.1 加注记：库存余额表追加库存金额 / 均价列；成本毛利口径以 `026` §0.3 为准
- [x] 8.5 `.codebuddy/CONTEXT.md` §2（成本入口与指针，明细见本规格 `design.md`）、§3（api 文件与报表域页面）、§6 同步
- [x] 8.6 `specs/ROADMAP.md` 状态列更新（`026` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 对账口径双恒等式成立：`Σ 流水 Quantity == Inventory.Quantity` 与 `Σ 流水 TotalCost == Inventory.CostAmount`。

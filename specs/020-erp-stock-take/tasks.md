---
created: 2026-09-16
updated: 2026-09-16
---

# 任务清单：期初建账与库存盘点（erp-stock-take）

> 依据 `specs/020-erp-stock-take/design.md` 拆分。含后端新单据域 + 库存设定 / 流水写入 + 三个查询用例 + 前端列表 / 新建 / 详情。按顺序实现，完成后勾选。
> 前置：`019-erp-stock-movement` 已实现（本规格消费其流水表与 `IStockMovementRepository`）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [ ] 1.1 新增 `App.Core/Entities/StockTake.cs`、`StockTakeItem.cs`、`StockTakeType.cs`、`StockTakeFieldConstraints.cs`（仅实盘数量区间，其余引用既有常量）
- [ ] 1.2 `App.Infrastructure/Persistence/Configurations/StockTakeConfiguration.cs`、`StockTakeItemConfiguration.cs` + `AppDbContext` 新增 2 个 `DbSet`
- [ ] 1.3 `StockMovementType` 追加 `InitialStock = 5`、`StockTakeAdjust = 6`
- [ ] 1.4 `IInventoryRepository` 追加 `SetQuantityAsync` / `GetQuantitiesAsync`；`IStockMovementRepository` 追加 `GetProductIdsWithMovementsAsync`（均含实现）
- [ ] 1.5 新增 `IStockTakeRepository`（`AddAsync` / `GetPagedAsync` / `GetDetailAsync` / `GenerateTakeNoAsync`）+ `App.Infrastructure/Repositories/StockTakeRepository.cs` + `DependencyInjection.cs` 注册
- [ ] 1.6 `App.Core/Errors/ErrorCode.cs` 追加 `40111 StockInitialNotAllowed`
- [ ] 1.7 增量迁移 `dotnet ef migrations add AddErpStockTake -p src/App.Infrastructure -s src/App.Api`

## 二、后端：用例与接口

- [ ] 2.1 共享出参 `Features/StockTakes/StockTakeListItemDto.cs` / `StockTakeDetailDto.cs` / `StockTakeProductPickDto.cs` + `StockTakesDtoMapper.cs`
- [ ] 2.2 新增用例 `StockTakes/GetStockTakes`（Request + Validator + Handler + Response）
- [ ] 2.3 新增用例 `StockTakes/CreateStockTake`（含差异重算、期初限制、库存设定 + 流水写入事务）
- [ ] 2.4 新增用例 `StockTakes/GetStockTakeById`
- [ ] 2.5 新增用例 `StockTakes/GetStockTakePickProducts`（启用商品 + 当前库存 + `hasMovements` 标记）
- [ ] 2.6 新增 `App.Api/Controllers/StockTakesController.cs`（4 端点；`pick-products` 注册在 `{id:guid}` 之前）
- [ ] 2.7 `App.Core/DependencyInjection.cs` 注册 4 个 Handler 与对应 Validator

## 三、单元测试

- [ ] 3.1 `CreateStockTake` 成功用例：盘点（差异重算 / 快照 / 统计 / 差异行设定 + 写流水 / 无差异行不动）/ 期初（流水类型 `InitialStock`）
- [ ] 3.2 `CreateStockTake` 异常用例：`40110` / `40400` / `40107` / `40111`（并断言不写库存、不写流水、不落单）；`Commit` 抛异常 → `RollbackAsync`
- [ ] 3.3 `GetStockTakes`（筛选传参 / 分页映射）、`GetStockTakeById`（含明细快照 / `40400`）、`GetStockTakePickProducts`（`hasMovements` 标记）
- [ ] 3.4 `CreateStockTakeRequestValidator` / `GetStockTakesRequestValidator` 边界 + `FieldValidationConsistencyTests` 扩展（列长 / 实盘区间 / items 上限 / keyword）
- [ ] 3.5 对账一致性用例：期初 + 盘点调整后 `Σ 流水变动量 == Inventory.Quantity`
- [ ] 3.6 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [ ] 4.1 `src/api/stockTake.ts`（类型 + `getStockTakes` / `getStockTakeById` / `createStockTake` / `getStockTakePickProducts`）
- [ ] 4.2 `src/views/StockTakeManagement/StockTakesView.vue`（筛选行 + 操作行 + 表格 + 服务端分页）
- [ ] 4.3 `StockTakeFormPage.vue`（类型切换 + 明细子表格 + 差异实时展示 + 期初模式禁用已建账商品 + `submitting`）
- [ ] 4.4 `StockTakeDetailView.vue`（表头 + 明细只读 + 「查看库存流水」跳转 + 404 空态）
- [ ] 4.5 `src/router/index.ts` 新增 `stock-takes` / `stock-takes/new` / `stock-takes/detail/:id` 三条路由
- [ ] 4.6 `src/components/AppLayout.vue`「进销存」分组追加子项「库存盘点」+ `MENU_ROUTE_MAP` 增加 `stockTakeDetail`
- [ ] 4.7 `StockMovementsView.vue` 变动类型下拉与展示支持新增两种类型（依据 `specs/019-erp-stock-movement/design.md` §0 表）
- [ ] 4.8 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/stock-take.spec.ts`：期初建账（选未建账商品 → 提交 → 详情展示 → 库存为录入值 + 流水出现 `+N`「期初建账」）
- [ ] 5.2 同文件：盘点差异（账面 x、实盘 x−4 → 库存变化 + 流水 `-4`「盘点调整」）；实盘等于账面**不产生**流水
- [ ] 5.3 同文件：期初模式下已建账商品被拦截（下拉禁用标注，或后端 `40111` 统一提示）
- [ ] 5.4 同文件：列表筛选（类型 / 单号 / 日期范围）+ 详情「查看库存流水」跳转预置单号筛选
- [ ] 5.5 `cd frontend && npm run test:e2e` 全量通过（含采购 / 销售 / 库存 / 流水既有用例回归）

## 六、规格与上下文联动

- [x] 6.1 `specs/019-erp-stock-movement/design.md` §0 表续行：`InitialStock = 5`「期初建账」`purple`、`StockTakeAdjust = 6`「盘点调整」`gold`（随本规格起草同步）
- [ ] 6.2 `.codebuddy/CONTEXT.md` §2（新 Feature / 仓储 / 读模型 / 错误码）、§3（新前端域目录与 api 文件）、§6（规格清单分类）同步
- [ ] 6.3 `specs/ROADMAP.md` 状态列更新（`020` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。

---
created: 2026-09-16
updated: 2026-09-17
---

# 任务清单：库存流水（erp-stock-movement）

> 依据 `specs/019-erp-stock-movement/design.md` 拆分。含后端新表 + 四个既有用例写入接入 + 查询接口 + 前端列表页。按顺序实现，完成后勾选。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：流水表与写入接入

- [x] 1.1 新增 `App.Core/Entities/StockMovement.cs` 与 `StockMovementType.cs`（本期 `PurchaseInbound` / `PurchaseVoid` / `SalesOutbound` / `SalesVoid` 四个取值）
- [x] 1.2 `App.Infrastructure/Persistence/Configurations/StockMovementConfiguration.cs`（列长引用 `OrderFieldConstraints`；索引 `(ProductId, CreatedAt)` / `CreatedAt` / `SourceNo`）+ `AppDbContext` 新增 `DbSet`
- [x] 1.3 新增 `App.Core/Abstractions/IStockMovementRepository.cs` 与读模型 `StockMovementItem.cs`（联查 `Products` / `Users` 字段）
- [x] 1.4 `App.Infrastructure/Repositories/StockMovementRepository.cs`（`AppendAsync` / `GetPagedAsync` / `SumQuantityAsync`）+ `App.Infrastructure/DependencyInjection.cs` 注册
- [x] 1.5 增量迁移 `dotnet ef migrations add AddErpStockMovement -p src/App.Infrastructure -s src/App.Api`
- [x] 1.6 改造 `Purchases/CreatePurchaseOrder`：事务内每行 `IncrementAsync(+q)` 后追加 `PurchaseInbound` 流水
- [x] 1.7 改造 `Purchases/VoidPurchaseOrder`：事务内每行 `IncrementAsync(-q)` 后追加 `PurchaseVoid` 流水
- [x] 1.8 改造 `Sales/CreateSalesOrder`：事务内每行 `TryDecrementAsync(q)` 成功后追加 `SalesOutbound` 流水
- [x] 1.9 改造 `Sales/VoidSalesOrder`：事务内每行 `IncrementAsync(+q)` 后追加 `SalesVoid` 流水

## 二、后端：查询接口

- [x] 2.1 新增共享出参 `Features/StockMovements/StockMovementListItemDto.cs` + `StockMovementsDtoMapper.cs`（正向映射，方法名 `To` + DTO 名）
- [x] 2.2 新增用例 `Features/StockMovements/GetStockMovements`（Request + Validator + Handler + Response）
- [x] 2.3 新增 `App.Api/Controllers/StockMovementsController.cs`（`GET /api/stock-movements`，`FromQuery` page / pageSize / keyword / productId / type / start / end）
- [x] 2.4 `App.Core/DependencyInjection.cs` 注册 `GetStockMovementsRequestHandler` 与 `IValidator<GetStockMovementsRequest>`

## 三、单元测试

- [x] 3.1 `GetStockMovements` Handler 用例：筛选传参组合 / DTO 映射（符号、空值）/ 分页 `total` 透传
- [x] 3.2 四个既有 Handler 测试扩展：每行明细 `AppendAsync` 参数断言（类型 / 方向 / `SourceId` / `SourceNo` / 操作人 / 时间）
- [x] 3.3 四个既有 Handler 失败路径断言不写流水（`40103` / `40104` / `40107` / `40108`）与事务回滚断言
- [x] 3.4 对账一致性用例：四条链路（入库 / 作废、出库 / 作废）后 `Σ 变动量 == Inventory.Quantity`
- [x] 3.5 `GetStockMovementsRequestValidator` 边界 + `FieldValidationConsistencyTests` 扩展（`SourceNo` 列长 == 常量；`keyword` 20 通过 / 21 拒绝）
- [x] 3.6 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [x] 4.1 `src/api/stockMovement.ts`（`StockMovementListItem` / `StockMovementQuery` 类型 + `getStockMovements`）
- [x] 4.2 `src/views/StockMovementManagement/StockMovementsView.vue`（筛选行 + 表格 + 服务端分页 + 路由 query `productId` 预置）
- [x] 4.3 `src/router/index.ts` 新增路由 `stock-movements`（name `stockMovements`，懒加载）
- [x] 4.4 `src/components/AppLayout.vue`「进销存」分组追加子项「库存流水」
- [x] 4.5 `src/views/InventoryManagement/InventoryView.vue` 新增操作列「流水」（Tabler `IconListDetails`）跳转并带 `productId`
- [x] 4.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 新增 `e2e/stock-movement.spec.ts`：菜单进入流水页渲染；采购入库后出现 `+N`（类型「采购入库」+ 来源单号）；该单作废后出现 `-N`（「采购作废」）；销售出库 `-N` / 销售作废 `+N`
- [x] 5.2 同文件：筛选（商品 / 变动类型 / 单号关键词 / 重置）与空状态
- [x] 5.3 同文件：库存查询页操作列「流水」下钻 → 流水页带 `productId` 预置筛选且仅展示该商品流水
- [x] 5.4 `cd frontend && npm run test:e2e` 全量通过（含采购 / 销售 / 库存既有用例回归）

## 六、规格与上下文联动

- [x] 6.1 `specs/014-erp-inventory-query/design.md` 加「演进（erp-stock-movement）」注记（§4.4 / §5：操作列从无 → 只读「流水」入口）
- [x] 6.2 `.codebuddy/CONTEXT.md` §2（新 Feature / 仓储 / 读模型）、§3（新前端域目录与 api 文件）、§6（规格清单分类）同步
- [x] 6.3 `specs/ROADMAP.md` 状态列更新（`019` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。

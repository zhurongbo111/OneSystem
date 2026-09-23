---
created: 2026-09-17
updated: 2026-09-23
---

# 任务清单：多仓库（erp-multi-warehouse）

> 依据 `specs/038-erp-multi-warehouse/design.md` 拆分。含**库存维度升级（破坏性）** + 仓库档案 + 五类单据带仓 + 流水带仓 + 报表 / 页面按仓改造。**按阶段顺序实现，每阶段结束跑全量测试**。
> 前置：`019` / `023` 已实现（流水与结算）；`024` 若已落地，表名为 `PurchaseReceipts` / `SalesShipments`（本规格一律按**落地时实际表名**处理）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> **风险提示**：阶段 A 的迁移会改写既有数据（回填默认仓、唯一键切换）——执行前备份 dev 库，并在迁移后立即跑一次库存对账（§A 验证项）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、阶段 A：数据模型与迁移（破坏性）

- [x] 1.1 新增实体 `Warehouse` + `WarehouseFieldConstraints` + EF 配置 + `AppDbContext` `DbSet`
- [x] 1.2 `Inventory` 实体 / 配置改造：追加 `WarehouseId` / `SafetyStock`；唯一键改 `(ProductId, WarehouseId)`
- [x] 1.3 `StockMovement` 追加 `WarehouseId`（+ 配置：NOT NULL、FK、索引 `(WarehouseId, ProductId, CreatedAt)`）；`StockMovementItem` 追加 `WarehouseId` / `WarehouseName`
- [x] 1.4 五类单据实体 / 配置追加 `WarehouseId` / `WarehouseName`（采购入库 / 销售出库 / 采购退货 / 销售退货 / 库存盘点）
- [x] 1.5 增量迁移 `AddErpMultiWarehouse`：建仓表 → 插入默认仓（固定 GUID）→ 加列 + 回填 → 唯一键切换 → NOT NULL；Down 完整
- [x] 1.6 `ErrorCode.cs` 追加 `40122` / `40123` / `40124` / `40125`
- [ ] 1.7 迁移验证（dev 库）：默认仓存在；`Inventory` 行均指向默认仓且数量 / 阈值与迁移前一致；流水与单据回填默认仓；唯一索引与索引清单正确
  - 待办：需对 dev 库执行一次迁移并跑按仓对账（属人工验证项，见「完成定义」）

## 二、阶段 A：仓储与用例（维度贯通）

- [x] 2.1 `IWarehouseRepository` + 实现（含 `GetDefaultAsync` / `ClearDefaultAsync` / `GetEnabledAsync`）+ 注册
- [x] 2.2 `IInventoryRepository` 全部方法追加 `warehouseId`；`IncrementAsync` 改为「行不存在先建后加」；新增 `EnsureRowAsync` / `UpdateSafetyStockAsync` / `GetTotalQuantityAsync`；实现同步（`ExecuteUpdateAsync` 按 `(ProductId, WarehouseId)` 定位）
- [x] 2.3 `IStockMovementRepository`：`AppendAsync` 带仓；`GetPagedAsync` 加 `warehouseId` 筛选与仓名联查；`SumQuantityAsync` / `GetAllForCostAsync` / `GetProductIdsWithMovementsAsync` 加可选 `warehouseId`
- [x] 2.4 五类单据仓储：`GetPagedAsync` 加 `warehouseId` 筛选；`AddAsync` 写仓与仓名快照；`GetDetailAsync` 返回仓字段
- [x] 2.5 仓库用例 7 个（`GetWarehouses` / `CreateWarehouse` / `GetWarehouseById` / `UpdateWarehouse` / `UpdateWarehouseStatus` / `SetDefaultWarehouse` / `GetWarehousePickList`）+ 共享出参（`WarehouseDto` / `WarehousePickDto`）+ `WarehouseDtoMapper` + Validator
- [x] 2.6 `WarehousesController`（7 端点，`pick` 在 `{id:guid}` 之前）+ DI 注册
- [x] 2.7 新增用例 `Inventory/UpdateInventorySafetyStock` + `PUT /api/inventory/safety-stock`

## 三、阶段 B：既有用例改造（按仓）

- [x] 3.1 `Products/CreateProduct`：为每个启用仓建 0 库存行（`SafetyStock` 取商品阈值）；`UpdateProduct` / `UpdateProductStatus` 改用组织级合计 `GetTotalQuantityAsync`
- [x] 3.2 `PurchaseReceipts/CreatePurchaseReceipt` + `VoidPurchaseReceipt`：仓解析（空 → 默认仓）+ 库存 / 流水按仓
- [x] 3.3 `SalesShipments/CreateSalesShipment` + `VoidSalesShipment`：同上；库存不足 message 含仓名
- [x] 3.4 `PurchaseReturns/CreatePurchaseReturn` + `VoidPurchaseReturn`：同上
- [x] 3.5 `SalesReturns/CreateSalesReturn` + `VoidSalesReturn`：同上
- [x] 3.6 `StockTakes/CreateStockTake` + `GetStockTakePickProducts`：账面按仓、`hasMovements` 按仓、按仓设定库存；期初建账按仓
- [x] 3.7 `Inventory/GetInventory` / `StockMovements/GetStockMovements`：追加 `warehouseId` 筛选与仓名列
- [x] 3.8 各单据列表 / 详情 DTO 追加 `warehouseId` / `warehouseName`；列表请求追加 `warehouseId`
- [x] 3.9 `029` 审计摘要追加仓库名（单据类 / 盘点），并新增 `Inventory` 资源（仓级安全库存维护）

## 四、阶段 B：报表与导出

- [x] 4.1 `Reports/GetInventoryFlow` / `GetStockBalance` 追加 `warehouseId` 筛选（不传 = 全部仓合并；余额表按商品聚合改为 Σ 各仓）
- [x] 4.2 `027` 导出：单据（五类）/ 库存 / 流水导出列追加「仓库」列；进销存报表与库存余额表导出追加 `warehouseId` 筛选（报表行为分类聚合行，不加仓列）
- [x] 4.3 `025` §0.1 / §0.2 与 `026` §0.1 续行：仓库维度说明（**成本按仓维护、组织级 = 各仓合计**，见 design.md §0 / §5）
- [x] 4.4 成本域随库存维度改造：`Inventory` 成本方法追加 `warehouseId`、`Costs/RecalculateCosts` 按「商品 × 仓」分账推演（`StockMovementCostRow` 追加 `WarehouseId`）

## 五、单元测试（后端）

- [x] 5.1 仓库用例：CRUD / 编码名称重复 / 默认仓 `40124` / 停用仓设默认 `40123` / 排序（默认置顶）/ pick 仅启用（`WarehouseRequestHandlerTests`）
- [x] 5.2 库存按仓：`IncrementAsync` upsert、`TryDecrementAsync` 按仓隔离、`SetQuantityAsync` / `GetQuantitiesAsync` 按仓、`GetPagedAsync` 筛选与仓级低库存判定、安全库存边界（`InventoryRepositoryTests` / `MultiWarehouseTests` / `WarehouseFieldConsistencyTests`）
- [x] 5.3 五类单据带仓：默认仓兜底 / 停用仓 `40123` / 不存在 `40400` / 库存与流水带仓 / 出参仓名（`MultiWarehouseTests`）
- [x] 5.4 盘点按仓：账面取仓、期初按仓判断、差异只影响所选仓（`MultiWarehouseTests`）
- [x] 5.5 对账一致性（按仓）：任一 (商品, 仓) 的 `Σ 流水 Quantity == Inventory.Quantity`；组织级汇总不变（`MultiWarehouseTests.按仓对账…`）
- [x] 5.6 `CreateProduct` 每启用仓建行 + 阈值继承（`MultiWarehouseTests`）
- [x] 5.7 字段约束一致性：仓库列长 / `keyword` / `safetyStock` 边界（`WarehouseFieldConsistencyTests`）
- [x] 5.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归；1055 通过）

## 六、前端

- [x] 6.1 `src/api/warehouse.ts`（7 个接口 + 类型）；`api/inventory.ts` 追加 `updateInventorySafetyStock`
- [x] 6.2 `views/WarehouseManagement/WarehousesView.vue`（列表 + 默认仓标签 + 启停 / 设为默认 + 分页）
- [x] 6.3 `views/WarehouseManagement/WarehouseFormDrawer.vue`（新增 / 编辑，编码不可改，打开先重置）
- [x] 6.4 开单页改造（采购入库 / 销售出库 / 采购退货 / 销售退货）：仓库下拉（默认仓预选，入库仓 / 出库仓文案）；商品下拉库存随所选仓刷新（`GET /api/products/pick?warehouseId=`）
- [x] 6.5 `StockTakeFormPage.vue`：盘点仓下拉（必选）+ 账面按仓带出 + 期初按仓
- [x] 6.6 `InventoryView.vue`：仓库筛选 / 仓库列 / 仓级安全库存列 / 「安全库存」单字段 Modal（`saveSafetyStockSubmitting`）
- [x] 6.7 `StockMovementsView.vue`：仓库筛选 + 仓库列
- [x] 6.8 单据列表 / 详情 / 打印页展示仓库；列表加仓库筛选
- [x] 6.9 报表页（进销存 / 余额表）加仓库筛选；成本报表加「全组织口径」提示
- [x] 6.10 `router/index.ts` 新增 `warehouses`；`AppLayout.vue`「基础档案」分组追加「仓库管理」
- [x] 6.11 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [x] 7.1 新增 `e2e/warehouse.spec.ts`：仓库新增 / 编辑 / 启停 / 设为默认；默认仓标签切换
- [x] 7.2 同文件：两仓各自采购入库 → 库存查询按仓显示不同数量；全部仓下同一商品按仓各一行
- [x] 7.3 同文件：从指定仓销售出库 → 该仓扣减、另一仓不变；该仓不足时提交被拒（提示含仓名）
- [x] 7.4 同文件：流水页按仓筛选；盘点选仓后账面与差异正确；安全库存 Modal 保存成功
- [x] 7.5 既有各域 spec 适配：`stock-take.spec.ts` 商品下拉限定到明细行（表头新增盘点仓下拉）、`purchase-return` / `sale-return` 列表筛选下拉下标右移（新增出库仓 / 入库仓下拉）
- [x] 7.6 `cd frontend && npm run test:e2e` 全量通过
  - 结果：`npm run e2e:run` 全量 **166 passed / 0 failed**（退出码 0，独立临时库跑完自动删库）；`e2e/warehouse.spec.ts` 单独连跑 3 次均 5 passed（验证竞态修复有效）

## 八、规格与上下文联动

- [x] 8.1 `specs/ROADMAP.md` §6.2 加「已落地（`038`）」注记（原判断与既定动作的落地结果 + 两处必要修订）
- [x] 8.2 `specs/012-erp-product/design.md` 加注记：`Inventory` 唯一键升级 + 仓级安全库存；`Products.SafetyStock` 语义收敛为组织级提醒线 / 初始值
- [x] 8.3 `specs/014` / `015` / `016` / `019` / `020` / `021` / `022` / `025` / `026` / `027` `design.md` 加注记：按仓维度（筛选 / 列 / 口径）
- [x] 8.4 `specs/028-erp-rbac/design.md` §0.2 续行：`warehouses.*`（已登记）与 `inventory.update`
- [x] 8.5 `specs/029-erp-audit-log/design.md` §0.1 续行：仓库创建 / 更新 / 启停 / 设默认 + `Inventory` 资源；摘要模板加仓库名（§0.1 / §3.6）
- [ ] 8.6 `specs/039-erp-transfer/`（本规格之后起草）已在 §0 引用本规格维度约定
  - 待办：`039` 规格尚未起草，起草时按其 §0 引用本规格的仓维度与「调拨两条流水」约定（**留给该规格的已知坑**见 §九 变更记录第 3、4 条）
- [x] 8.7 `.codebuddy/CONTEXT.md` §2（Warehouse / WarehouseResolver / 库存维度 / 错误码）、§3（WarehouseManagement 域、`api/warehouse.ts`）、§6（`038` 已实现）同步
- [x] 8.8 `specs/ROADMAP.md` 状态列更新（`038` → 已实现）

## 九、e2e 阶段发现并修复的缺陷（变更记录）

> 全量 e2e 首轮 13 failed，逐一定位后修复；教训已各自上升为规则判据，不在规格内重复。

1. **库存余额表整页空（`50000`）**：`GetStockBalanceAsync` 把「商品级分组子查询」又套了一层「按分类分组聚合」，EF Core 无法翻译（`could not be translated`）→ 改为「SQL 取商品级投影 + 内存按分类聚合」（上层维度基数远小于明细行）。**规则**：后端规则 §5.4「查询可翻译性」新增。
2. **商品下拉库存显示上一个仓（前端竞态）**：5 个表单（采购入库 / 销售出库 / 采购退货 / 销售退货 / 库存盘点）在「默认仓预选 + 用户改仓」时各发一次取数请求，慢的旧响应覆盖新仓数据（盘点页表现为账面显示默认仓的 0）。修法：各自加 `productFetchSeq` 序号仲裁。**规则**：前端规则 §5.1 新增「联动重取同样要序号仲裁」。
3. **e2e 侧适配（非产品缺陷）**：`MENU_GROUP_MAP` 补「仓库管理 → 基础档案」（否则菜单不展开、5 个新用例全失败）；`stock-take.spec.ts` / `report.spec.ts` 的商品下拉限定到明细行（表头新增盘点仓下拉后 `.arco-select` 从 1 个变 2 个，strict mode 冲突）；`purchase-return` / `sale-return` 列表筛选下拉下标右移。
4. **`039` 调拨需注意**：调拨是「同一单据两个仓」的唯一场景，其两条流水必须各带自己的 `WarehouseId`，且成本结转按「转出仓均价 → 转入仓入库」双向处理（不能只改数量）；起草 `039` 时按本规格 §3.1 的成本按仓约定展开。

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 迁移后对账双口径成立：按仓 `Σ 流水 == 该仓库存`、组织级 `Σ 流水 == 全组织库存`。

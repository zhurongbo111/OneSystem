---
created: 2026-09-17
updated: 2026-09-17
---

# 任务清单：批次与保质期管理（erp-batch-expiry）

> 依据 `specs/040-erp-batch-expiry/design.md` 拆分。含商品开关 + 批次档案 + **库存 / 单据 / 流水加批次维度（改造面广）** + 6 个用例 + 前端批次页与开单页批次列。**按阶段顺序实现，每阶段跑全量测试**。
> 前置：`038`（多仓）与 `039`（调拨）已实现；`019`（流水）/ `026`（成本）已实现。
> **待用户确认**：序列号管理列为范围外（路线图原文含序列号），见 `requirement.md` §5；确认后开工。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 零、前置门禁

- [ ] 0.1 **用户确认**「只做批次 + 保质期，序列号不做」（范围裁剪）；确认后进入阶段一

## 一、阶段 A：数据模型与迁移

- [ ] 1.1 `Products` 追加 `IsBatchManaged`（实体 + EF 配置 + 商品抽屉校验口径）
- [ ] 1.2 新增实体 `Batch` + `BatchFieldConstraints` + EF 配置（唯一索引 `(ProductId, BatchNo)`、`ExpiryDate` 索引）+ `AppDbContext` `DbSet`
- [ ] 1.3 `Inventory` 追加 `BatchId`（可空 + FK + 索引）；唯一键 `(ProductId, WarehouseId, BatchId)` + **部分唯一索引** `WHERE "BatchId" IS NULL`（`HasFilter`）
- [ ] 1.4 `StockMovement` 追加 `BatchId` + 索引；`StockMovementItem` 追加 `BatchId` / `BatchNo`
- [ ] 1.5 六张明细表追加 `BatchId` / `BatchNo` 快照（采购入库 / 销售出库 / 采购退货 / 销售退货 / 盘点 / 调拨）
- [ ] 1.6 `InventoryItem`（`014` 读模型）追加 `BatchId` / `BatchNo` / `ExpiryDate`
- [ ] 1.7 增量迁移 `dotnet ef migrations add AddErpBatchExpiry -p src/App.Infrastructure -s src/App.Api`（历史数据不回填批次）
- [ ] 1.8 `ErrorCode.cs` 追加 `40127 BatchRequired` / `40128 BatchExpired` / `40129 BatchNoExists`

## 二、阶段 A：仓储与批次档案用例

- [ ] 2.1 读模型 `BatchPickItem` / `BatchListItem`（含库存合计 / 最早到期日）
- [ ] 2.2 `IBatchRepository` + 实现（`GetByIdAsync` / `ExistsByBatchNoAsync` / `GetPagedAsync`（含 `onlyExpiring`）/ `GetPickListAsync`（到期日升序）/ `AddAsync` / `UpdateAsync`）+ 注册
- [ ] 2.3 `IInventoryRepository` 全部按数量方法追加 `batchId`；新增 `GetBatchQuantitiesAsync`；`GetPagedAsync` 追加 `batchId` / `expandBatch`（展开行 + 汇总行的 `SUM` / `MAX` 判定）
- [ ] 2.4 批次用例 6 个（`GetBatches` / `CreateBatch` / `GetBatchById` / `UpdateBatch` / `UpdateBatchStatus` / `GetBatchPickList`）+ 出参 + Mapper + Validator
- [ ] 2.5 `BatchesController`（6 端点，`pick` 在 `{id:guid}` 之前）+ DI 注册

## 三、阶段 B：单据与流水按批次（五类 + 调拨）

- [ ] 3.1 通用批次校验组件（Handler 内共用私有方法或 Domain 辅助：按商品开关校验必填 / 批次归属 / 停用 / 过期 / 就地新建）
- [ ] 3.2 `Purchases/CreatePurchaseOrder` + `VoidPurchaseOrder`：批次必填、就地新建、库存 / 流水带批次
- [ ] 3.3 `Sales/CreateSalesOrder` + `VoidSalesOrder`：批次必填、**过期拦截**、库存 / 流水带批次
- [ ] 3.4 `PurchaseReturns/CreatePurchaseReturn` + `VoidPurchaseReturn`：批次必填、过期拦截、按批次回增
- [ ] 3.5 `SalesReturns/CreateSalesReturn` + `VoidSalesReturn`：批次必填（支持就地新建）、按批次操作
- [ ] 3.6 `StockTakes/CreateStockTake` + `GetStockTakePickProducts`：按批次读账面 / 设定 / 写流水；期初按批次判断 `hasMovements`
- [ ] 3.7 `Transfers/CreateTransfer` + `VoidTransfer`（`039`）：启用 `BatchId` 与 `BatchNo` 快照、批次必填与过期校验
- [ ] 3.8 五类 + 调拨的列表 / 详情 / 创建 DTO 追加 `batchId` / `batchNo`；库存查询 / 流水出参追加批次字段
- [ ] 3.9 `029` 审计摘要：按批次出库 / 入库的摘要含批次号（如「销售出库单 GI…（商品 A / 批次 B1）」）

## 四、阶段 B：报表与导出

- [ ] 4.1 `025` §0.1 续行：报表按批次的处理（进销存报表支持 `expandBatch` / 批次筛选，或明确「报表到商品维度、批次看库存与流水」——按设计取其一并写清）
- [ ] 4.2 `027` 导出范围表续行：库存查询（展开批次时含批次列）、库存流水（批次列）、批次列表导出
- [ ] 4.3 `039` 设计规格加注记：`TransferItem.BatchId` 启用与必填规则

## 五、单元测试（后端）

- [ ] 5.1 批次用例：CRUD / 商品未开启 `40000` / 批次号重复 `40129` / 日期校验 / `onlyExpiring` 筛选 / pick 排序与过期标记
- [ ] 5.2 库存按批次：三类写方法行定位与批次隔离；`GetBatchQuantitiesAsync`；非批次路径与 `038` 一致
- [ ] 5.3 五类单据 + 调拨：缺批次 `40127` / 过期 `40128` / 批次不属于商品 `40400` / 停用批次 `40000` / 非批次商品传批次 `40000` / 快照与流水带批次
- [ ] 5.4 就地新建：成功 / 与 `batchId` 互斥 `40000` / 重复 `40129` 且单据不落库
- [ ] 5.5 盘点按批次：账面 / 设定 / 流水 / 无差异不动
- [ ] 5.6 对账一致性：按「商品 × 仓 × 批次」`Σ 流水 == Inventory.Quantity`；按仓 / 组织级汇总仍成立
- [ ] 5.7 字段约束一致性：`BatchNo` 50 / 明细快照列长 / `keyword` 50-51 / `NearExpiryDays` 单点
- [ ] 5.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 六、前端

- [ ] 6.1 `src/api/batch.ts`（6 个接口 + 类型 + 过期 / 近效期标签映射常量）
- [ ] 6.2 `views/BatchManagement/BatchesView.vue`（筛选含「仅看近效期 / 过期」+ 到期日标签 + 启停 + 分页）
- [ ] 6.3 `views/BatchManagement/BatchFormDrawer.vue`（新增 / 编辑，批次号不可改，打开先重置）
- [ ] 6.4 `views/BatchManagement/BatchPickSelect.vue`（开单页明细行批次选择 + 就地新建 Modal + 过期禁用 + 默认最早未过期）
- [ ] 6.5 `ProductFormDrawer.vue` 追加「按批次管理」开关
- [ ] 6.6 五类开单页明细追加「批次」列（按批次商品必填校验提示）
- [ ] 6.7 `InventoryView.vue`「展开批次」开关 + 批次 / 到期日列 + 批次号筛选（展开时隐藏安全库存列）
- [ ] 6.8 `StockMovementsView.vue` 批次列；`StockTakeFormPage.vue` 按批次录入；`TransferFormPage.vue` 批次列
- [ ] 6.9 `router/index.ts` 新增 `batches`；`AppLayout.vue`「库存」分组追加「批次管理」
- [ ] 6.10 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/batch.spec.ts`：商品开启按批次 → 建两个批次 → 入库各 10 → 库存查询展开批次显示两行
- [ ] 7.2 同文件：销售出库选批次 1 → 仅批次 1 减少；流水含批次号
- [ ] 7.3 同文件：选过期批次出库被拒（`40128` 提示可见）；按批次商品未选批次被拒（`40127`）
- [ ] 7.4 同文件：批次列表近效期 / 过期标签与「仅看近效期 / 过期」筛选；就地新建批次（入库时）成功并被选中
- [ ] 7.5 既有各域 spec 回归（非批次商品下批次列为 `-`，链路不变）；`inventory-management.spec.ts` / `stock-take.spec.ts` / `transfer.spec.ts` 适配
- [ ] 7.6 `cd frontend && npm run test:e2e` 全量通过

## 八、规格与上下文联动

- [ ] 8.1 `specs/012-erp-product/design.md` 加注记：`IsBatchManaged` 开关
- [ ] 8.2 `specs/038-erp-multi-warehouse/design.md` §0 加注记：`Inventory` 唯一键在批次落地后为 `(ProductId, WarehouseId, BatchId)` + 部分唯一索引
- [ ] 8.3 `specs/019` / `020` / `021` / `022` / `025` / `026` `design.md` 加注记：批次维度（流水批次列、盘点按批次、报表口径）
- [ ] 8.4 `specs/027-erp-export/design.md` §0.2 续行导出项
- [ ] 8.5 `specs/ROADMAP.md` §5 注记：序列号管理未纳入本规格（建议独立规格）
- [ ] 8.6 `.codebuddy/CONTEXT.md` §2（Batches 实体 / 仓储 / 错误码 / 库存维度）、§3（BatchManagement 域、api 文件）、§6 同步
- [ ] 8.7 `specs/ROADMAP.md` 状态列更新（`040` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 不分批次商品行为零变化（既有 e2e 全绿）；按批次对账口径成立。

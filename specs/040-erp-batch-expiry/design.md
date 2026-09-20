---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：批次与保质期管理（erp-batch-expiry）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-partner`（档案域）与 `erp-purchase`（单据域）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 维度不重复定义：仓库维度见 `specs/038-erp-multi-warehouse/design.md` §0；成本见 `specs/026-erp-cost/design.md` §0.1；流水类型文案见 `specs/019-erp-stock-movement/design.md` §0（本规格**不新增流水类型**，批次只是流水的附加维度）。

## 0. 批次与保质期约定（唯一事实源）

| 概念 | 规则 |
|---|---|
| 批次粒度 | 批次属于**商品**（`Batches.ProductId` + `BatchNo` 唯一），**跨仓共享**同一批次；某商品在某仓的某批次数量按 `(商品, 仓, 批次)` 定位 |
| 是否按批次 | 由 `Products.IsBatchManaged` 决定（商品档案开关）；**关闭时全链路 `BatchId` 为空**，行为与不分批次完全一致（零影响） |
| 批次号 | 用户录入（`varchar(50)`，可含字母数字与 `-` / `_`），同商品内唯一（大小写不敏感）；**创建后不可改**（同编码原则） |
| 生产 / 到期日 | 均可空；到期日与生产日期同日或早于生产日期 → `40000`（Validator）；「业务日期 + 保质期天数」的自动推算不做（用户直接填到期日） |
| 过期判定 | `ExpiryDate < 今天`（**日期粒度**：到期日当天仍可用，次日起过期）；无到期日的批次永不过期 |
| 近效期判定 | `ExpiryDate <= 今天 + NearExpiryDays(30)` 且未过期 → 近效期；常量 `BatchFieldConstraints.NearExpiryDays = 30`（唯一来源） |
| 过期拦截范围 | **出库类**（销售出库 `SalesOutbound`、采购退货 `PurchaseReturnOut`）拒绝已过期批次 → `40128`；**入库类**（采购入库、销售退货、盘点盘盈、期初）不限制 |
| 明细必填 | 按批次商品的单据明细行**必须指定批次**（`BatchRequired 40127`）；非按批次商品传批次 → `40000`（防串数据） |
| 库存行唯一性 | `Inventory` 唯一键 `(ProductId, WarehouseId, BatchId)`；非批次商品用 `BatchId IS NULL` **部分唯一索引**（`HasFilter`）保证与 `038` 的 `(ProductId, WarehouseId)` 语义一致 |
| 安全库存判定 | 仍按 `(商品, 仓)` **汇总**：数量 = `SUM(Quantity)`，阈值 = 该组合下 `MAX(SafetyStock)`（批次行不各自设阈值）——保持 `038` §0 的仓级语义不被批次打散 |
| 批次停用 | `Status`（复用 `PartnerStatus`）：停用批次不可用于**新的**出入库单；历史单据与流水保留；停用不删除 |
| 成本口径 | 组织级移动加权平均不变（`026` §0.1）；**不做批次成本**；调拨（`039`）按行批次数量移动，成本单价仍取商品均价 |
| 对账口径 | 任一「商品 × 仓 × 批次」满足 `Σ 流水 Quantity == Inventory.Quantity`（`038` §0 口径的加批次版本） |

## 1. 总体设计

```
批次档案（前端 /batches，归「库存」分组）
  → BatchesController
    → App.Core/Features/Batches/<Action>/*RequestHandler
      → IBatchRepository（含 GetPickListAsync：按商品 + 仓返回可用批次，到期日升序）
      → PostgreSQL（Batches）

库存与单据按批次
  采购入库 / 销售出库 / 采购退货 / 销售退货 / 盘点 / 调拨 Handler
    → IInventoryRepository.<方法>(productId, warehouseId, batchId, ...)（追加 batchId）
    → IStockMovementRepository.AppendAsync（流水带 BatchId）
    → PostgreSQL（Inventory / StockMovements / 单据明细含 BatchId + BatchNo 快照）
```

核心原则：

- **维度可空、增量兼容**：`BatchId` 可空；不分批次商品路径与 `038` 完全一致（既有数据、既有 e2e 零改造）。
- **批次不是新单据**：不新增单据类型、不新增流水类型，批次只是库存 / 单据 / 流水的附加维度（避免概念爆炸）。
- **过期只在出库侧拦**：入库过期货是现实（收到临期货），拦入库会把业务堵死；出库拦才是合规要点。
- **判定集中**：过期 / 近效期判定逻辑收在 `IBatchRepository` 的查询与 Handler 的一处校验（不在前端单独判断——前端只做展示标注，后端兜底）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；状态枚举复用 `PartnerStatus`。

### 2.1 `Products` 追加开关

| 列 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `IsBatchManaged` | `boolean` | NOT NULL，默认 `false` | 是否按批次管理（`012` 商品档案留演进注记） |

### 2.2 实体 `App.Core/Entities/Batch.cs` 与表 `Batches`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | 批次所属商品 |
| `BatchNo` | `string` | `varchar(50)` | NOT NULL | 批次号，同商品内唯一（大小写不敏感），创建后不可改 |
| `ProductionDate` | `DateTimeOffset?` | `timestamptz` | NULL | 生产日期（UTC 午夜） |
| `ExpiryDate` | `DateTimeOffset?` | `timestamptz` | NULL | 到期日（UTC 午夜） |
| `Status` | `PartnerStatus` | `smallint` | NOT NULL，默认 `1` | 启用 / 停用 |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 唯一索引：`(ProductId, BatchNo)`——数据库唯一索引大小写敏感，应用层 `ExistsByBatchNoAsync` 做大小写不敏感判定（同 `009` / `013` 惯例）。
- 索引：`(ExpiryDate)`（近效期 / 过期筛选）；外键不级联删除（批次不删除）。

### 2.3 `Inventory` 批次维度

| 变化 | 内容 |
|---|---|
| 新增列 | `BatchId`（`uuid`，NULL，FK → `Batches(Id)`，索引） |
| 唯一约束 | 保留 `(ProductId, WarehouseId, BatchId)` 唯一索引；另建**部分唯一索引** `(ProductId, WarehouseId) WHERE "BatchId" IS NULL`（EF `HasFilter`）与非批次路径的语义一致 |
| 实体 | `Inventory` 追加 `BatchId`；`InventoryItem`（`014` 读模型）追加 `BatchId` / `BatchNo` / `ExpiryDate`（联查 `Batches`） |

### 2.4 `StockMovements` 批次维度

| 变化 | 内容 |
|---|---|
| 新增列 | `BatchId`（`uuid`，NULL，FK → `Batches(Id)`） |
| 索引 | 新增 `(BatchId)`；`StockMovementItem` 追加 `BatchId` / `BatchNo`（联查 `Batches`） |

### 2.5 单据明细批次列（五类单据一致）

| 表 | 新增列 | 必填规则 |
|---|---|---|
| `PurchaseOrderItems`（`024` 后 `PurchaseReceiptItems`） | `BatchId`（`uuid?`）、`BatchNo`（`varchar(50)?` 快照） | 按批次商品必填；支持**就地新建批次**（创建单据时同时建批次） |
| `SalesOrderItems`（`024` 后 `SalesShipmentItems`） | 同上 | 按批次商品必填；**禁止过期批次** |
| `PurchaseReturnItems` | 同上 | 按批次商品必填；**禁止过期批次** |
| `SalesReturnItems` | 同上 | 按批次商品必填；支持就地新建（退回的批次可能未建档） |
| `StockTakeItems` | 同上 | 按批次商品必填（按批次盘点） |
| `TransferItems`（`039`） | `BatchId`（`uuid?`）+ `BatchNo` 快照 | `039` 已预留 `BatchId` 列，本规格启用并补 `BatchNo` 快照与必填规则 |

- 明细存 `BatchNo` 快照（同 `ProductName` / `Unit` 快照原则）：列表 / 详情 / 打印 / 导出免联查。

### 2.6 迁移与字段约束

- 迁移：`dotnet ef migrations add AddErpBatchExpiry -p src/App.Infrastructure -s src/App.Api`（增量迁移：加 `Products.IsBatchManaged`、建 `Batches`、`Inventory` / `StockMovements` 加列与索引、六张明细表加列）；**历史数据不回填批次**（既有商品 `IsBatchManaged = false`，既有库存行 `BatchId = NULL`，与旧语义一致）。
- 新增 `App.Core/Entities/BatchFieldConstraints.cs`：`BatchNoMinLength = 1` / `BatchNoMaxLength = 50` / `BatchNoPattern = ^[A-Za-z0-9_-]{1,50}$` / `NearExpiryDays = 30` / `KeywordMaxLength = 50`（对齐 `BatchNo` 列长）；`RemarkMaxLength` 引用 `OrderFieldConstraints.RemarkMaxLength`；生产 / 到期日无独立上限（Validator 校验「到期日 ≥ 生产日期」）。
- 一致性由 `FieldValidationConsistencyTests` 扩展守护（列长 == 常量、`keyword` 50/51、`NearExpiryDays` 单点定义）。

## 3. 后端设计

### 3.1 仓储接口

`IBatchRepository`（新增，`App.Core/Abstractions/`）：

| 方法 | 说明 |
|---|---|
| `Task<Batch?> GetByIdAsync(Guid id, ...)` | 按 id |
| `Task<bool> ExistsByBatchNoAsync(Guid productId, string batchNo, Guid? excludeId, ...)` | 同商品内批次号唯一（大小写不敏感） |
| `Task<(IReadOnlyList<Batch> Items, int Total)> GetPagedAsync(string? keyword, Guid? productId, PartnerStatus? status, bool? onlyExpiring, int page, int pageSize, ...)` | 列表（`keyword` 匹配 `BatchNo`；`onlyExpiring` = 近效期或已过期；`AsNoTracking`） |
| `Task<IReadOnlyList<BatchPickItem>> GetPickListAsync(Guid productId, Guid warehouseId, ...)` | 批次下拉：该商品在该仓**有库存或未过期**的批次 + 该仓可用库存 + 到期日；**按 `ExpiryDate` 升序（无到期日最后）**；读模型含 `IsExpired` / `IsNearExpiry` |
| `Task AddAsync(Batch, ...)` / `Task UpdateAsync(Batch, ...)` | 新增 / 更新（批次号不可改） |

- 新增读模型：`BatchPickItem`（`BatchId` / `BatchNo` / `ExpiryDate` / `AvailableQuantity` / `IsExpired` / `IsNearExpiry`）；`BatchListItem`（列表行含商品编码 / 名称 / 库存合计 / 最早到期日 / 状态）。

`IInventoryRepository`（`038` 定义，本规格**全部按数量方法追加 `Guid? batchId`**）：

| 方法（改造后） | 说明 |
|---|---|
| `EnsureRowAsync(productId, warehouseId, batchId, safetyStock, ...)` | 建行（非批次传 `null`） |
| `IncrementAsync(productId, warehouseId, batchId, delta, ...)` | 按批次累加（行不存在先建） |
| `TryDecrementAsync(productId, warehouseId, batchId, amount, ...)` | 按批次条件扣减 |
| `SetQuantityAsync(productId, warehouseId, batchId, quantity, ...)` | 盘点 / 期初设定 |
| `GetQuantitiesAsync(warehouseId, productIds, ...)` | **不带批次**（汇总读账面：按批次商品的盘点需按批次读，见下） |
| `GetBatchQuantitiesAsync(warehouseId, productId, ...)` | 新增：读某商品在某仓的 `(批次 → 数量)` 字典（按批次盘点 / 批次下拉用） |
| `UpdateSafetyStockAsync(productId, warehouseId, safetyStock, ...)` | 不变（阈值按 `(商品, 仓)`） |

- `GetPagedAsync`（库存查询）追加 `Guid? batchId` 与 `bool expandBatch`：`expandBatch = true` 时按 `(商品, 仓, 批次)` 展开行（批次号 / 到期日 / 近效期标记），否则维持 `038` 的按 `(商品, 仓)` 汇总（**汇总行的安全库存判定用 `SUM(Quantity)` 与 `MAX(SafetyStock)`**）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40127 | `BatchRequired` | 按批次管理的商品必须指定批次 |
| 40128 | `BatchExpired` | 批次已过期，禁止出库（message 含批次号与到期日） |
| 40129 | `BatchNoExists` | 同商品下批次号已存在 |

> 复用：`40107 ProductDisabled`、`40110 OrderItemsEmpty`、`40400` / `40000`；批次停用复用 `40108 PartnerDisabled`？——**不**：批次停用语义与往来单位不同，新增无必要；停用批次在用于新单时按 `40000` 提示「批次已停用」（Validator 无法查库，故由 Handler 抛 `40000`）——见 §3.4。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/batches` | GET | `Batches/GetBatches` | `PagedResult<BatchListItemDto>` | `batches.view` / 40000 |
| `/api/batches` | POST | `Batches/CreateBatch` | `BatchDetailDto` | `batches.create` / 40000 / 40129 / 40400 |
| `/api/batches/{id:guid}` | GET | `Batches/GetBatchById` | `BatchDetailDto` | `batches.view` / 40400 |
| `/api/batches/{id:guid}` | PUT | `Batches/UpdateBatch` | `BatchDetailDto` | `batches.update` / 40000 / 40400 |
| `/api/batches/{id:guid}/status` | PUT | `Batches/UpdateBatchStatus` | `BatchDetailDto` | `batches.update` / 40000 / 40400 |
| `/api/batches/pick` | GET | `Batches/GetBatchPickList` | `IReadOnlyList<BatchPickDto>` | `batches.view` / 40000 |

- 路由注意：`/api/batches/pick` 为固定段，置于 `{id:guid}` 之前；`pick` 必传 `productId` + `warehouseId`。
- 单据侧不新增端点（批次作为既有创建请求的明细字段），但**请求 / 出参 DTO 追加 `batchId` / `batchNo`**。

### 3.4 关键用例流程（Handler）

**CreateBatch**：取商品（不存在 → `40400`；**未开启按批次 → `40000`**「该商品未启用批次管理」）→ 批次号唯一（`40129`）→ 日期校验（到期日 < 生产日期 → `40000`）→ 新增（`Status = Enabled`、审计）。
**UpdateBatch**：取批次（`40400`）→ 更新生产 / 到期日 / 备注（**批次号不可改**）→ 审计。
**UpdateBatchStatus**：取批次（`40400`）→ 置状态（停用后不可用于新单）→ 审计。
**GetBatchPickList**：`GetPickListAsync`（按到期日升序）→ 计算 `IsExpired` / `IsNearExpiry`（`BatchFieldConstraints.NearExpiryDays`）→ 映射 DTO。

**单据改造（以 CreateSalesOrder 为例，其余同类）**：
1. 逐行商品校验（既有）→ 若商品 `IsBatchManaged`：`batchId` 必填（缺失 → `40127`）→ 取批次（不存在 → `40400`；`ProductId` 不匹配 → `40400`；停用 → `40000`「批次已停用」）→ **过期校验**（`ExpiryDate < 今天` → `40128`，message 含批次号与到期日）。
2. 库存操作带批次：`TryDecrementAsync(productId, warehouseId, batchId, qty)`（不足 → `40103`，message 含仓名与批次号）。
3. 明细落 `BatchId` + `BatchNo` 快照；流水 `AppendAsync` 带 `BatchId`。
4. 非按批次商品传了 `batchId` → `40000`（防串数据，Validator 无法判商品开关，故在 Handler）。

**就地新建批次**（采购入库 / 销售退货）：明细可带 `newBatchNo`（＋`productionDate` / `expiryDate`）代替 `batchId`；Handler 在同一事务内先建批次（唯一性校验 → `40129`）再使用；两者**不可同时提供**（Validator 拦，`40000`）。

**CreateStockTake（改造）**：按批次商品的明细必填 `batchId`；账面数量按 `(仓, 商品, 批次)` 读取（`GetBatchQuantitiesAsync`）；`Difference != 0` 的行 `SetQuantityAsync(productId, warehouseId, batchId, actual)` + 流水带批次；期初建账的 `hasMovements` 判定按 `(商品, 仓, 批次)` 维度（批次首次建账允许）。

**GetInventory（改造）**：`expandBatch` 为真时按批次展开（含 `batchNo` / `expiryDate` / `IsExpired` / `IsNearExpiry`）；安全库存列在展开视图不展示（判定口径见 §0）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.6 常量）

| 请求 | 规则 |
|---|---|
| `CreateBatchRequest` | `productId` 必填；`batchNo` 必填 1–50、`BatchNoPattern`；`productionDate` / `expiryDate` 可空，同时提供时 `expiryDate >= productionDate`；`remark` ≤200 |
| `UpdateBatchRequest` | 去掉 `batchNo`；其余同创建 |
| `UpdateBatchStatusRequest` | `status` ∈ {0, 1} |
| `GetBatchesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50；`productId` 可空；`status` 可空；`onlyExpiring` 可空 |
| `GetBatchPickListRequest` | `productId` / `warehouseId` 必填 |
| 各单据 `Create*Request`（改造） | 明细行追加 `batchId`（可空，Handler 按商品开关校验必填）与 `newBatchNo` / `newProductionDate` / `newExpiryDate`（可空；与 `batchId` **互斥**，同时提供 → `40000`） |
| `GetInventoryRequest`（改造） | 追加 `batchId` 可空、`expandBatch` 可空（默认 `false`） |

### 3.6 Swagger

- **不分组**（同既有约定）：6 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── batch.ts                        # 批次接口层
└── views/
    └── BatchManagement/
        ├── BatchesView.vue             # 批次列表
        ├── BatchFormDrawer.vue         # 新增 / 编辑抽屉（商品 / 批次号 / 生产日期 / 到期日 / 备注）
        └── BatchPickSelect.vue         # 域内复用：批次选择控件（开单页明细行用）
```

- 改造既有页面（不新建）：`ProductFormDrawer.vue`（「按批次管理」开关）、五类开单页（明细批次列 + 就地新建）、`InventoryView.vue`（展开批次）、`StockMovementsView.vue`（批次列）、`StockTakeFormPage.vue`（按批次盘点）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `batches` | `batches` | `BatchesView` |

- `AppLayout.vue`「库存」分组追加「批次管理」（`025` §0.2 总表已预留 `batches`）。

### 4.3 页面交互

**批次列表 `BatchesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（批次号关键词 + 商品下拉 + 状态 + 「仅看近效期 / 过期」`a-checkbox`）；操作行（新增 + 刷新 + 列设置）；列：序号、商品编码、商品名称、批次号、生产日期、**到期日**（近效期 `a-tag warning`「近效期」、过期 `a-tag danger`「已过期」，正常显示 `-`）、库存合计（跨仓汇总，批次维度视图见库存查询）、状态、创建时间、操作列（编辑 / 停用或启用）；服务端分页。

**批次选择控件 `BatchPickSelect.vue`**（开单页明细行内使用）：

- `a-select`（`allow-search`，可选「新建批次」入口按钮）：数据源 `getBatchPickList(productId, warehouseId)`，选项展示「批次号（到期 yyyy-MM-dd，可用 x）」；**过期批次 `disabled` 并标注「已过期」**（出库类单据）/ 入库类不禁用但标注；默认选中**最早到期且未过期且库存 > 0** 的批次（FEFO 辅助）；按到期日升序。
- 「+ 新建批次」→ 就地 `a-modal`（批次号 / 生产日期 / 到期日）→ 创建成功后自动选中；`batchSubmitting` loading。

**商品抽屉（改造）**：新增「按批次管理」`a-switch`（编辑时可按需开启；**已发生库存的商品关闭开关需谨慎**：本期允许关闭，仅影响后续单据，历史批次库存行保留（决策见 §5）。

**库存查询页（改造）**：操作行追加「展开批次」`a-checkbox`（切换后重新查询，列追加「批次号」「到期日」；安全库存列在展开视图隐藏）；筛选行追加「批次号」输入（可空）。

**库存流水页（改造）**：表格追加「批次号」列（可列设置隐藏）。

**盘点页（改造）**：明细行追加「批次」列（按批次商品必填，选择后账面按批次带出）。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 批次列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 批次抽屉提交 | `submitting` | 提交按钮 |
| 批次启停 | `togglingId` | 行内按钮 / popconfirm |
| 就地新建批次 Modal | `batchSubmitting` | Modal `ok-loading` |
| 开单页批次下拉加载 | `batchLoading` | 明细行（下拉 loading） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 批次维度可空 | `BatchId` 可空 + 部分唯一索引 | 让「不分批次」与「按批次」在同一结构共存；既有商品与数据零影响（`038` 语义不变） |
| 批次跨仓共享 | `Batches` 只挂商品 | 批次是生产批号（商品属性），同一批次可能分放多仓；按仓拆分批次会人为造出伪批次 |
| 过期判定用日期粒度 | `ExpiryDate < 今天` | 保质期到日不到时（行业惯例）；当天仍可出库避免争议 |
| 过期只拦出库 | `40128` 仅在出库类 | 收到临期货是现实（拒收会造成库存与账实冲突）；合规要点在「不把过期品发出去」 |
| 不做 FEFO 自动分配 | 手工选 + 到期日升序 + 默认选最早未过期 | 自动分配需出库策略（是否允许跨批次拆行、是否允许超发）与拆行逻辑，属独立能力；排序 + 默认值已能覆盖大多数场景 |
| 安全库存不打散到批次 | 按 `(商品, 仓)` 汇总（`SUM` 与 `MAX`） | 批次级阈值语义混乱（同一商品同仓多个阈值），且用户心智是「这个仓该备多少」 |
| 明细存批次号快照 | `BatchNo` | 同商品名 / 单位快照：列表 / 详情 / 打印 / 导出免联查 |
| 支持就地新建批次 | 采购入库 / 销售退货 | 入库时批次往往是「货到了才知道」；强制先去批次管理页建档会让开单流程被切断 |
| 新增 3 个错误码 | `40127` / `40128` / `40129` | 分别对应「缺批次」「过期」「批次号重复」，语义独立，复用既有码会产生含糊文案 |
| 批次停用复用 `40000` | 不新增码 | 停用是「不可用」状态提示，与参数类错误同层；避免为单一场景再占业务码 |
| 不做批次成本 | 组织级均价不变 | 分批次成本需要成本层与出库批次绑定计价，属独立能力；且与 `026` 的口径冲突需整体重设计 |
| 序列号不做 | 范围外 | 逐件粒度与全链路追溯是另一套模型（一码一物、出入库逐码核对），塞进本规格会显著放大复杂度 |
| 商品开关可关闭 | 允许（仅影响后续） | 误开的开关应能纠正；历史批次行与单据保留（数据不丢），只在后续单据不再要求批次 |
| 盘点按批次 | 明细必填批次 | 账实核对必须落到批次（否则批次库存无法校正）；代价是盘点明细行数增加（按批次拆分） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（过期 / 近效期判定一律用入参「今天」）。

- **批次用例**：`CreateBatch` 成功 / 商品不存在 `40400` / 商品未开启批次 `40000` / 批次号重复 `40129` / 到期日早于生产日期 `40000`；`UpdateBatch` 成功（不改批次号）/ `40400`；`UpdateBatchStatus` 成功 / `40400`；`GetBatches` 筛选（含 `onlyExpiring`）/ 分页；`GetBatchPickList` 排序（到期日升序、无到期日最后）、`IsExpired` / `IsNearExpiry` 边界（到期日 = 今天、今天 + 30、今天 + 31）。
- **库存按批次**：`IncrementAsync` / `TryDecrementAsync` / `SetQuantityAsync` 带批次的行定位；同商品不同批次相互隔离；`GetBatchQuantitiesAsync` 字典正确；非批次路径（`batchId = null`）行为与 `038` 一致。
- **单据改造（五类各一例）**：按批次商品缺批次 → `40127`；过期批次出库 → `40128`（入库类不拦）；批次不属于该商品 → `40400`；停用批次 → `40000`；非按批次商品传批次 → `40000`；幂等：明细 `BatchNo` 快照落库；流水带 `BatchId`。
- **就地新建**：采购入库按 `newBatchNo` 建批次并使用（同一事务）；`batchId` 与 `newBatchNo` 同时提供 → `40000`；新建失败（重复批次号）→ `40129` 且单据未落库。
- **盘点按批次**：按批次读账面、按批次设定；差异行写流水带批次；无差异行不动。
- **对账一致性**：任一「商品 × 仓 × 批次」`Σ 流水 Quantity == Inventory.Quantity`；按仓 / 组织级汇总仍成立。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Batches.BatchNo` 50 == 常量；明细 `BatchNo` 快照列长 50 一致；`keyword` 50 / 51；`NearExpiryDays` 单点定义（无第二处硬编码 30）。

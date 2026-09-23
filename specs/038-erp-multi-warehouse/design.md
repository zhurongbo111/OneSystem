---
created: 2026-09-17
updated: 2026-09-23
---

# 设计规格：多仓库（erp-multi-warehouse）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-partner`（档案域模板）+ `erp-purchase`（单据域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格**升级库存维度**：`Inventory` 唯一键加仓、流水加仓、单据带仓，并为既有数据回填默认仓（对应 `specs/ROADMAP.md` §6.2 的既定动作）。

## 0. 仓库与库存维度约定（唯一事实源）

| 概念 | 规则 |
|---|---|
| 仓库标识 | `Warehouses.Code`（唯一，`varchar(20)`，创建后**不可改**，同商品编码原则）；`Name` 唯一（大小写不敏感，可改） |
| 默认仓 | 全系统有且仅有一个 `IsDefault = true` 的仓库；**不可停用、不可删除**（`40124`）；设置默认时同一事务内清空其他仓的默认标记 |
| 仓库生命周期 | **只停用不删除**（停用仓不可被新单据选择，已入库 / 已发货数据与流水保留） |
| 库存唯一键 | `Inventory` 唯一键 `(ProductId, WarehouseId)`；同一商品在不同仓各自一行 |
| 仓级安全库存 | `Inventory.SafetyStock`（`integer`，≥ 0，默认 0）为**判定唯一来源**：低库存判定 = `SafetyStock > 0 && Quantity < SafetyStock`；商品档案的 `Products.SafetyStock` 降级为「新建库存行时的初始值」 |
| 单据仓库 | 采购入库 = 入库仓；销售出库 = 出库仓；采购退货 = 出库仓；销售退货 = 入库仓；库存盘点 = 盘点仓；**参数可空 → 取默认仓**（兼容存量调用方） |
| 单据仓库不可改 | 单据一步式（保存即生效、不可编辑），仓库随单据固化；纠错靠作废重开（同 `015` 口径） |
| 流水仓库 | `StockMovements.WarehouseId`（NOT NULL）；一条流水的仓 = 其数量实际变动的仓（调拨将产生两条不同仓的流水，见 `039`） |
| 成本口径 | **按仓口径**：成本列（`CostAmount` / `AverageCost`）随库存行落到「商品 × 仓」，即仓级移动加权平均；**组织级 = 各仓合计**（报表按商品聚合 Σ 各仓金额与数量）。`026` §0.1 的计价公式不变，只是「库存行」自本规格起 = 商品 × 仓（`026` 留演进指针） |
| 对账口径（按仓） | 任一「商品 × 仓」满足 `Σ 流水 Quantity == Inventory.Quantity`；全组织汇总仍等于 `026` 的组织级口径 |
| 报表维度 | `025` 的进销存报表 / 库存余额表、`026` 的成本报表增加「仓库」筛选与分组维度（本规格落地时在 `025` §0.1 / §0.2 续行，页面加筛选） |
| 审计摘要 | 单据类摘要追加仓库名（`029` §0.1 摘要模板续行） |

## 1. 总体设计

```
仓库档案（前端 /warehouses，归「基础档案」分组）
  → WarehousesController
    → App.Core/Features/Warehouses/<Action>/*RequestHandler
      → IWarehouseRepository → PostgreSQL（Warehouses）

库存按仓（既有仓储维度升级）
  各单据 / 盘点 Handler
    → IInventoryRepository.<方法>(productId, warehouseId, ...)（全部方法追加仓）
    → PostgreSQL（Inventory 唯一键 (ProductId, WarehouseId)）

单据 / 流水带仓
  单据表 + StockMovements 增 WarehouseId → 全部写入路径透传（参数可空 → 默认仓）
```

核心原则：

- **仓是库存的第二维**：库存的所有读写（含成本列）都以 `(productId, warehouseId)` 定位；不存在「无仓库存」。
- **默认仓兜底**：所有 `warehouseId` 入参可空（空 → 默认仓），保证既有前端 / e2e / 第三方调用零改造可继续工作；新前端一律显式传仓（默认仓预选）。
- **一次性迁移到位**：加列 + 回填 + 唯一键升级 + 索引重建在同一迁移内完成（`ROADMAP` §6.2 已论证可比性）。
- **不改单据一步式语义**：仓库是单据字段，不改变「保存即生效、不可编辑、可作废回冲」的任何行为。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；状态枚举复用既有（见下）。

### 2.1 实体 `App.Core/Entities/Warehouse.cs` 与表 `Warehouses`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 仓库编码，创建后不可改 |
| `Name` | `string` | `varchar(50)` | NOT NULL，唯一索引 | 仓库名称 |
| `Address` | `string?` | `varchar(100)` | NULL | 地址 |
| `Contact` | `string?` | `varchar(20)` | NULL | 联系人 |
| `Phone` | `string?` | `varchar(20)` | NULL | 联系电话 |
| `IsDefault` | `bool` | `boolean` | NOT NULL，默认 `false` | 默认仓（全系统唯一，应用层保证） |
| `Status` | `PartnerStatus` | `smallint` | NOT NULL，默认 `1` | 启用 / 停用（**复用** `PartnerStatus`：仓库与往来单位同属「业务档案」，不新建同形枚举） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

### 2.2 `Inventory` 维度升级

| 变化 | 内容 |
|---|---|
| 新增列 | `WarehouseId`（`uuid`，NOT NULL，FK → `Warehouses(Id)`，索引） |
| 新增列 | `SafetyStock`（`integer`，NOT NULL，默认 0）——仓级阈值（原 `Products.SafetyStock` 的值迁移时复制） |
| 唯一键 | `ProductId` 唯一索引 → **删除**，改建 `(ProductId, WarehouseId)` 唯一索引 |
| 实体 | `Inventory` 追加 `WarehouseId` / `SafetyStock`；`Products.SafetyStock` 保留（语义改为「新建库存行初始值」，`012` 留演进注记） |

### 2.3 `StockMovements` 维度升级

| 变化 | 内容 |
|---|---|
| 新增列 | `WarehouseId`（`uuid`，NOT NULL，FK → `Warehouses(Id)`） |
| 索引 | 新增 `(WarehouseId, ProductId, CreatedAt)`（按仓 + 商品下钻）；保留 `CreatedAt` / `SourceNo` 索引 |
| 实体 | `StockMovement` 追加 `WarehouseId`；`StockMovementItem`（读模型）追加 `WarehouseId` / `WarehouseName`（联查 `Warehouses`） |

### 2.4 单据带仓（五类单据）

| 表 | 新增列 | 含义 |
|---|---|---|
| `PurchaseOrders`（`024` 后 `PurchaseReceipts`） | `WarehouseId`（`uuid`，NOT NULL）+ `WarehouseName`（`varchar(50)`，快照） | 入库仓 |
| `SalesOrders`（`024` 后 `SalesShipments`） | 同上 | 出库仓 |
| `PurchaseReturns` | 同上 | 出库仓 |
| `SalesReturns` | 同上 | 入库仓 |
| `StockTakes` | `WarehouseId` + `WarehouseName` | 盘点仓 |

- 主表存 `WarehouseName` 快照（同 `PartnerName` 快照原则）：列表 / 详情 / 打印免联查，且仓改名后历史单据保持当时名称（决策见 §5）。
- 明细表**不加仓**（一张单据只有一个仓，明细继承主表）。

### 2.5 迁移（`AddErpMultiWarehouse`，一次到位）

- 生成：`dotnet ef migrations add AddErpMultiWarehouse -p src/App.Infrastructure -s src/App.Api`（增量迁移），内容与顺序：

| # | 动作 | 说明 |
|---|---|---|
| 1 | 建 `Warehouses` 表 | 编码 / 名称唯一索引 |
| 2 | 插入默认仓 | **固定主键** `00000000-0000-0000-0000-000000000001`（便于后续 SQL 回填直接引用；`Code = DEFAULT`、`Name = 默认仓`、`IsDefault = true`、`Status = 1`）；用 `migrationBuilder.InsertData` |
| 3 | `Inventory` 加 `WarehouseId` / `SafetyStock` → 回填 | 先加可空列，`UPDATE "Inventory" SET "WarehouseId" = '<默认仓 id>', "SafetyStock" = (SELECT "SafetyStock" FROM "Products" WHERE ...)`，再改 NOT NULL |
| 4 | `Inventory` 唯一键切换 | 删 `ProductId` 唯一索引 → 建 `(ProductId, WarehouseId)` 唯一索引 |
| 5 | `StockMovements` 加 `WarehouseId` → 回填默认仓 → NOT NULL + 新索引 | |
| 6 | 五类单据加 `WarehouseId` / `WarehouseName` → 回填默认仓与「默认仓」名称 → NOT NULL | |
| 7 | 文档化：迁移内使用 `migrationBuilder.Sql` 仅限数据回填（业务代码仍禁止裸 SQL，后端规则 §5.1） | |

- **Down**：逆向执行（删除列 / 索引切换回 `ProductId` 唯一 / 删 `Warehouses` 表）；Down 仅用于本地回退，不做数据保护（已在迁移注释说明）。
- 迁移后既有前端 / e2e 不传 `warehouseId` → 由「参数可空取默认仓」兜底，行为与迁移前一致（验收标准 3 / 6）。

### 2.6 字段约束单一来源（`App.Core/Entities/WarehouseFieldConstraints.cs`）

| 常量 | 值 | 来源 |
|---|---|---|
| `CodeMinLength` / `CodeMaxLength` | 2 / 20 | 本域定义 |
| `CodePattern` | `^[A-Za-z0-9_-]{2,20}$` | 本域定义（同 `ProductFieldConstraints.CodePattern` 形态，长度不同故自建） |
| `NameMinLength` / `NameMaxLength` | 1 / 50 | 引用 `PartnerFieldConstraints.NameMinLength/NameMaxLength`（同为「业务档案名称」） |
| `AddressMaxLength` / `ContactMaxLength` / `PhoneMaxLength` / `PhonePattern` | 100 / 20 / 20 / `^1[3-9]\d{9}$` | 引用 `PartnerFieldConstraints` 同名字段（同源引用，不复制数值） |
| `RemarkMaxLength` | 200 | 引用 `OrderFieldConstraints.RemarkMaxLength` |
| `KeywordMaxLength` | 50 | 本域定义（查询关键词对齐被匹配列：`Code`(20) / `Name`(50) 取最大值 50） |
| `SafetyStockMinValue` / `SafetyStockMaxValue` | 0 / 999999 | 引用 `ProductFieldConstraints.SafetyStockMinValue/MaxValue` |

- EF 配置与各 `RequestValidator` 一律用常量；一致性由 `FieldValidationConsistencyTests` 扩展守护。

## 3. 后端设计

### 3.1 仓储接口

`IWarehouseRepository`（新增）：

| 方法 | 说明 |
|---|---|
| `Task<Warehouse?> GetByIdAsync(Guid id, ...)` | 按 id（含跟踪） |
| `Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, ...)` / `Task<bool> ExistsByNameAsync(string name, Guid? excludeId, ...)` | 唯一性（大小写不敏感） |
| `Task<(IReadOnlyList<Warehouse> Items, int Total)> GetPagedAsync(string? keyword, PartnerStatus? status, int page, int pageSize, ...)` | 列表（`keyword` 匹配编码 / 名称；`CreatedAt` 升序保证默认仓在前？——固定 `IsDefault DESC, Code ASC`，默认仓置顶） |
| `Task<IReadOnlyList<Warehouse>> GetEnabledAsync(...)` | 开单下拉（仅启用，量小全量） |
| `Task<Warehouse> GetDefaultAsync(...)` | 取默认仓（兜底用；不存在 → `50000`，属数据异常） |
| `Task AddAsync(Warehouse, ...)` / `Task UpdateAsync(Warehouse, ...)` | 新增 / 更新 |
| `Task ClearDefaultAsync(Guid? exceptId, ...)` | 清空其他仓的默认标记（与 `SetDefault` 同一事务） |
| `Task<bool> HasBusinessDataAsync(Guid id, ...)` | 是否有库存 / 流水 / 单据引用（停用前置校验可选，见 §3.4） |

`IInventoryRepository`（`012` 定义，本规格**全部方法追加 `warehouseId`**）：

| 方法（改造后签名） | 说明 |
|---|---|
| `Task EnsureRowAsync(Guid productId, Guid warehouseId, int safetyStock, ...)` | 确保库存行存在（新建商品时为**每个启用仓**建 0 行；安全库存取商品的 `SafetyStock` 初始值） |
| `Task IncrementAsync(Guid productId, Guid warehouseId, int delta, ...)` | 原子累加；**行不存在时先创建再累加**（仓后建场景） |
| `Task<bool> TryDecrementAsync(Guid productId, Guid warehouseId, int amount, ...)` | 条件扣减（`WHERE ProductId = @p AND WarehouseId = @w AND Quantity >= @amount`）；行不存在 → 返回 `false`（视为该仓库存 0） |
| `Task SetQuantityAsync(Guid productId, Guid warehouseId, int quantity, ...)` | 盘点 / 期初设定 |
| `Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(Guid warehouseId, IReadOnlyList<Guid> productIds, ...)` | 批量读某仓账面 |
| `Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(string? keyword, Guid? categoryId, Guid? warehouseId, int page, int pageSize, ...)` | 库存查询：`warehouseId` 为**新增筛选**（可空 = 全部仓，行含仓名与仓级阈值） |
| `Task UpdateSafetyStockAsync(Guid productId, Guid warehouseId, int safetyStock, ...)` | 仓级安全库存维护 |
| 成本方法（`026`） | `GetAverageCostAsync` / `ApplyInboundCostAsync` / `ApplyOutboundCostAsync` / `SetCostAsync` **全部追加 `warehouseId`**（成本随库存行按仓维护）；`026` 的成本重算按「商品 × 仓」分账推演（`StockMovementCostRow` 追加 `WarehouseId`） |

`IStockMovementRepository`（`019` 定义，改造）：

| 方法 | 变化 |
|---|---|
| `AppendAsync` | `StockMovement` 增 `WarehouseId`（由各写入方赋值） |
| `GetPagedAsync` | 追加 `Guid? warehouseId` 筛选；读模型含 `WarehouseName` |
| `GetAllForCostAsync` / `GetProductIdsWithMovementsAsync` / `SumQuantityAsync` | 追加 `Guid? warehouseId` 可选参数（`038` 后按仓对账用；成本重算不带仓 = 组织级） |

- 各单据仓储：`GetPagedAsync` 追加 `Guid? warehouseId` 筛选；`AddAsync` 写入仓与仓名快照；`GetDetailAsync` 返回含仓字段。
- 事务：单据 + 库存（按仓）+ 流水（按仓）+ 单据明细仍在**同一 `IUnitOfWork`**（既有约定不变）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40122 | `WarehouseCodeExists` | 仓库编码已存在 |
| 40123 | `WarehouseDisabled` | 仓库已停用，不可用于开单 |
| 40124 | `WarehouseDefaultImmutable` | 默认仓不可停用、不可删除 |
| 40125 | `WarehouseNameExists` | 仓库名称已存在 |

> 库存不足按仓复用 `40103`（message 追加仓名，如「库存不足：上海仓 商品 A（当前 5，需要 10）」）；`40400` / `40000` 复用全局。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

**仓库档案（`Features/Warehouses`）**

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/warehouses` | GET | `Warehouses/GetWarehouses` | `PagedResult<WarehouseListItemDto>` | `warehouses.view` / 40000 |
| `/api/warehouses` | POST | `Warehouses/CreateWarehouse` | `WarehouseDetailDto` | `warehouses.create` / 40000 / 40122 / 40125 |
| `/api/warehouses/{id:guid}` | GET | `Warehouses/GetWarehouseById` | `WarehouseDetailDto` | `warehouses.view` / 40400 |
| `/api/warehouses/{id:guid}` | PUT | `Warehouses/UpdateWarehouse` | `WarehouseDetailDto` | `warehouses.update` / 40000 / 40125 / 40400 |
| `/api/warehouses/{id:guid}/status` | PUT | `Warehouses/UpdateWarehouseStatus` | `WarehouseDetailDto` | `warehouses.status` / 40000 / 40124 / 40400 |
| `/api/warehouses/{id:guid}/default` | PUT | `Warehouses/SetDefaultWarehouse` | `WarehouseDetailDto` | `warehouses.update` / 40000 / 40400 |
| `/api/warehouses/pick` | GET | `Warehouses/GetWarehousePickList` | `IReadOnlyList<WarehousePickDto>`（id / 编码 / 名称 / 是否默认） | `warehouses.view` / 40000 |
| `/api/stock-takes/pick-products` | GET | `StockTakes/GetStockTakePickProducts`（改造：追加 `warehouseId` 查询参数） | `IReadOnlyList<StockTakeProductPickDto>`（账面与 `hasMovements` 均为**所选仓**口径） | `stockTakes.view` / 40000 |

- 路由注意：`/api/warehouses/pick` 为固定段，置于 `{id:guid}` 之前。
- 库存侧新增 1 个写端点：`PUT /api/inventory/safety-stock`（`Inventory/UpdateInventorySafetyStock`，body `{ productId, warehouseId, safetyStock }`，权限点 `inventory.update`——`028` §0.2 续行，见 `tasks.md` §联动）。

### 3.4 关键用例流程（Handler）

**CreateWarehouse**：`ExistsByCodeAsync` → `40122`；`ExistsByNameAsync` → `40125`；组实体（`IsDefault = false`、`Status = Enabled`、审计）→ 新增。
**UpdateWarehouse**：取仓（不存在 → `40400`）→ 名称唯一（排除自身）→ 更新名称 / 地址 / 联系人 / 电话 / 备注（**编码不可改**）→ 审计。
**UpdateWarehouseStatus**：取仓（`40400`）→ `IsDefault` → `40124`；置状态 → 审计。
**SetDefaultWarehouse**：取仓（`40400`）→ 已停用 → `40123`（默认仓必须可用）→ `IUnitOfWork`：`ClearDefaultAsync(id)` + 置 `IsDefault = true` → 提交。

**单据 / 盘点带仓（改造既有用例，五类一致）**：
1. 解析仓：`warehouseId` 为空 → `IWarehouseRepository.GetDefaultAsync()`；非空 → 取仓（不存在 → `40400`；停用 → `40123`）。
2. 业务校验（往来 / 商品 / 金额重算）不变；库存操作全部改为按仓：
   - 采购入库 / 销售退货：`IncrementAsync(productId, warehouseId, +qty)`；
   - 销售出库 / 采购退货：`TryDecrementAsync(productId, warehouseId, qty)`，失败 → `40103`（message 含仓名）；
   - 盘点：`GetQuantitiesAsync(warehouseId, productIds)` 读账面、`SetQuantityAsync(productId, warehouseId, actual)`。
3. 流水 `AppendAsync` 带 `WarehouseId`；单据落库带 `WarehouseId` + `WarehouseName` 快照；`029` 审计摘要含仓名。

**CreateProduct（`012` 改造）**：同一事务内为**每个启用仓**建 `Inventory` 行（`Quantity = 0`，`SafetyStock` 取商品阈值）；仓数量为 0 时不可能（默认仓必存在）。

**GetInventory / GetStockMovements / 报表（改造）**：追加 `warehouseId` 可选筛选；出参含仓名；`025` 的进销存报表与余额表支持「按仓筛选」（分组维度仍为商品 / 分类，按仓筛选后数值即为该仓口径）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.6 常量）

| 请求 | 规则 |
|---|---|
| `CreateWarehouseRequest` | `code` 必填 2–20、`CodePattern`；`name` 必填 1–50；`address` ≤100；`contact` ≤20；`phone` 选填 `PhonePattern`；`remark` ≤200 |
| `UpdateWarehouseRequest` | 同创建，去掉 `code`（不可改） |
| `UpdateWarehouseStatusRequest` | `status` ∈ {0, 1} |
| `GetWarehousesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50；`status` 可空合法值 |
| 各单据 `Create*Request`（改造） | 追加 `warehouseId` **可空**（`Guid?`；空 = 默认仓，不校验） |
| 各单据 / 流水 / 库存 `Get*Request`（改造） | 追加 `warehouseId` 可空 |
| `UpdateInventorySafetyStockRequest` | `productId` / `warehouseId` 必填；`safetyStock` 0–999999 |

### 3.6 Swagger

- **不分组**（同既有约定）：8 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── warehouse.ts                   # 仓库档案接口层
└── views/
    └── WarehouseManagement/
        ├── WarehousesView.vue         # 仓库列表
        └── WarehouseFormDrawer.vue    # 新增 / 编辑抽屉（字段 ≤ 8）
```

- 改造既有页面（不新建文件）：各域开单页（`PurchaseFormPage` / `SaleFormPage` / 退货开单页 / `StockTakeFormPage`）、`InventoryView`、`StockMovementsView`、4 个报表页、单据详情页与打印页（展示仓名）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `warehouses` | `warehouses` | `WarehousesView` |

- `AppLayout.vue`「基础档案」分组追加「仓库管理」（`025` §0.2 总表已预留 `warehouses`）；`MENU_ROUTE_MAP` 无详情页映射。

### 4.3 页面交互

**仓库列表 `WarehousesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（关键词 + 状态）；操作行（新增 + 刷新 + 列设置）；列：序号、编码、名称（默认仓带 `a-tag blue`「默认」）、地址、联系人、电话、状态（`a-tag`）、创建时间、操作列（编辑 / 停用或启用（popconfirm）/ 设为默认（`IconStar`，仅非默认且启用时显示；`settingDefaultId`））；服务端分页。

**仓库抽屉 `WarehouseFormDrawer.vue`**：新增（编码 / 名称 / 地址 / 联系人 / 电话 / 备注）与编辑（去掉编码，只读展示）；打开先重置再回填（`009` 防串台约定）；提交 `submitting` + 防重入。

**各开单页（改造）**：表头新增「仓库」下拉（`getWarehousePickList`，仅启用仓，**默认仓预选**；采购入库 / 销售退货标注「入库仓」，销售出库 / 采购退货标注「出库仓」）；提交 payload 带 `warehouseId`。

**库存查询页（改造）**：筛选行追加「仓库」下拉（全部仓 / 指定仓）；表格「安全库存」列改为展示**仓级阈值**并追加「仓库」列（全部仓时显示仓名）；操作列在既有「流水」之外追加 1 个「安全库存」（`IconAdjustments`，单字段 Modal 输入 0–999999，`saveSafetyStockSubmitting` + popconfirm 不需；权限 `inventory.update`）。

**库存流水页（改造）**：筛选行追加「仓库」下拉；表格追加「仓库」列（可列设置隐藏）。

**盘点页（改造）**：新建页表头「盘点仓」下拉（必选，默认仓预选；期初建账同样按仓建账）；商品下拉的「账面」取所选仓数量；列表页追加「仓库」列与筛选；详情页追加仓库描述项。

**报表页（`025` / `026` 改造）**：进销存报表 / 库存余额表追加「仓库」筛选（可空 = 全部仓合并）；成本与毛利报表不按仓（组织级成本）并在页面提示「成本为全组织口径」。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询（仓库 / 库存 / 流水 / 单据 / 报表） | `loading` | 搜索 / 翻页 + 表格 |
| 仓库抽屉提交 | `submitting` | 提交按钮 |
| 仓库启停 / 设为默认 | `togglingId` / `settingDefaultId` | popconfirm / 按钮 |
| 安全库存保存 | `saveSafetyStockSubmitting` | Modal `ok-loading` |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 一次迁移到位（加列 + 回填 + 唯一键升级） | 单迁移 | 与本规格发布的时点一致，避免中间态（无仓列但代码已按仓）长期存在；`ROADMAP` §6.2 已论证增量加列成本低 |
| 默认仓用固定 GUID 插入 | `0000…0001` | 迁移内 SQL 回填可直接引用该 id，无需查询；可读、可预测（仅此一处硬编码常量，标注来源） |
| `warehouseId` 入参可空 + 默认仓兜底 | 兼容层 | 既有前端 / e2e / 调用方零改造；新前端显式传（默认预选），迁移期风险最小 |
| 仓库只停用不删除 | 无 DELETE 接口 | 库存 / 流水 / 单据全引用仓库，删除会破坏审计；与商品 / 往来单位一致 |
| 默认仓不可停用 | `40124` | 兜底目标必须始终可用，否则「不传仓」的开单会失败 |
| 仓级安全库存以 `Inventory.SafetyStock` 为准 | 判定唯一来源在库存行 | 商品级阈值无法表达「上海 100 / 北京 20」；保留商品阈值为初始值，避免商品新建后逐仓手填 |
| 新建商品只为「启用仓」建行，其余仓按需创建 | `IncrementAsync` 内含 upsert | 避免「新建商品 × N 仓」的行爆炸（多仓多为 2–5 个，仍可控）；后期新开仓时老商品自动补齐 |
| 单据存仓名快照 | `WarehouseName` | 同 `PartnerName`：列表 / 详情 / 打印免联查；仓改名后历史单据保持当时名称（与「档案改名不影响历史凭证」一致） |
| 成本**按仓**维护（行级） | 移动加权公式与 `026` 一致，只是作用域为「商品 × 仓」；组织级口径 = 各仓合计 | 库存行自本规格起即「商品 × 仓」，成本列与数量列同属一行，**行级自洽**（Σ 流水 `TotalCost` == 该行 `CostAmount`）；若强留"组织级成本镜像到每行"，则每行均价与出库结转单价都会失真（同一商品各仓数量不同）。跨仓成本流转（调拨定价）仍不在本期：`039` 按转出仓成本结转即可满足「成本不丢」 |
| 明细表不加仓 | 仓只在主表 | 一张单据只操作一个仓，明细继承主表；避免冗余列与不一致风险 |
| 库存不足按仓 | `40103` message 含仓名 | 复用既有码（前端文案统一），信息层面补足仓维度 |
| 报表按仓筛选而非新增报表 | 追加筛选参数 | 避免 5 个报表页 × 仓维度 = 一套新报表；筛选后可得到「某仓的进销存」 |
| 仓库不用新枚举 | 复用 `PartnerStatus` | 同形枚举（0/1 启用停用）已存在两个（`ProductStatus` / `PartnerStatus`），再建第三个属重复定义（后端规则 §5.3 精神） |
| 权限点续行 | `028` §0.2 追加 `warehouses.*` 与 `inventory.update` | 权限清单是唯一事实源，新增功能必须续行登记（`028` 已有清单守卫测试） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **仓库用例**：`CreateWarehouse` 成功 / 编码重复 `40122` / 名称重复 `40125`；`UpdateWarehouse` 成功（不改编码）/ 名称重复（排除自身）/ `40400`；`UpdateWarehouseStatus` 成功 / 默认仓 `40124` / `40400`；`SetDefaultWarehouse` 成功（断言 `ClearDefaultAsync` + 置默认同事务）/ 停用仓 `40123` / `40400`；`GetWarehouses` 筛选与排序（默认仓置顶）；`GetWarehousePickList` 仅启用。
- **库存按仓**：`IncrementAsync` 带仓 / 行不存在时创建后累加；`TryDecrementAsync` 按仓条件（A 仓不足即失败，B 仓充足不影响）；`SetQuantityAsync` 按仓；`GetQuantitiesAsync` 按仓批量；`GetPagedAsync` 的 `warehouseId` 筛选与仓级安全库存低库存判定；`UpdateSafetyStockAsync` 成功 / 边界 0 与 999999。
- **单据带仓（五类各一例）**：不传 `warehouseId` → 走默认仓（断言 `GetDefaultAsync` 被调用且落库为默认仓）；传停用仓 → `40123`；传不存在仓 → `40400`；库存操作与流水均带所选仓；单据出参含 `warehouseName`。
- **盘点带仓**：账面取所选仓；期初建账按仓判断「无变动」（`hasMovements` 按仓）；差异只影响所选仓。
- **对账一致性（按仓）**：构造 A / B 两仓链路 → 断言「任一 (商品, 仓) 的 `Σ 流水 Quantity == Inventory.Quantity`」；同时断言组织级汇总仍等于 `026` 口径。
- **`CreateProduct` 改造**：为每个启用仓建 0 行且 `SafetyStock` 取商品阈值；仓不存在场景不出现。
- **迁移相关**：回填后「既有库存行 → 默认仓且数量 / 阈值与迁移前一致」由迁移测试（InMemory 不适用，改为对迁移 SQL 的静态断言 + 集成库冒烟）覆盖，详见 `tasks.md` 的手工验证项。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Warehouses.Code` 20 / `Name` 50 / `Address` 100 / `Remark` 200 与常量一致；`keyword` 50 通过 / 51 拒绝（对齐 `Name` 列长）；`safetyStock` 边界。

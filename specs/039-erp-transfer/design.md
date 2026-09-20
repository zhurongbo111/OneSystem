---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：仓库调拨（erp-transfer）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase-return`（双向库存操作 + 流水）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 维度与成本口径不重复定义：仓库维度见 `specs/038-erp-multi-warehouse/design.md` §0；成本见 `specs/026-erp-cost/design.md` §0.1；流水类型文案见 `specs/019-erp-stock-movement/design.md` §0。

## 0. 调拨口径约定（唯一事实源）

| 概念 | 规则 |
|---|---|
| 调拨方向 | 单向：`FromWarehouseId`（转出仓）→ `ToWarehouseId`（转入仓）；**两者必须不同**（`40126`） |
| 生效方式 | **一步式**：保存即生效（转出仓 −、转入仓 +，同一事务）；不可编辑，可作废 |
| 数量守恒 | 同一调拨单：Σ 转出数量 == Σ 转入数量（同商品逐行一一对应，不产生损耗） |
| 库存约束 | 转出仓逐行 `TryDecrementAsync`，任一行不足 → 整单回滚 `40103`（message 含仓名，`038` §3.2 约定）；转入仓 `IncrementAsync` 无上限 |
| 成本结转 | 每行按**转出时的组织级均价**（`026` §0.1）结转：`TransferOut` 用该单价与负金额，`TransferIn` **用同一单价**与正金额 → 组织级 `CostAmount` 与 `AverageCost` 净变化为 0（调拨不改变成本） |
| 流水 | 每行两条：`TransferOut`(11)（转出仓，`Quantity = -q`，`SourceId`/`SourceNo` = 本单）/ `TransferIn`(12)（转入仓，`Quantity = +q`，同来源） |
| 作废 | 反向：逐行转入仓 `IncrementAsync(-q)`、转出仓 `IncrementAsync(+q)`，并写两条反向流水（`TransferOutVoid = 13` / `TransferInVoid = 14`，文案在 `019` §0 续行）；**允许冲负**（货可能已被卖掉） |
| 金额 | 调拨单**不落业务金额**（无价格概念）；列表展示「数量合计」与「商品行数」 |
| 单据仓库快照 | `FromWarehouseName` / `ToWarehouseName` 快照（同 `038` 单据快照口径）；列表 / 详情 / 导出免联查 |

## 1. 总体设计

```
调拨单（前端 /transfers 列表 + /transfers/new 开单 + /transfers/detail/:id 详情）
  → TransfersController
    → App.Core/Features/Transfers/<Action>/*RequestHandler
      → ITransferRepository（单据 + 明细）
        + IWarehouseRepository（仓校验）
        + IProductRepository（商品校验与快照）
        + IInventoryRepository.TryDecrementAsync / IncrementAsync（按仓）
        + IStockMovementRepository.AppendAsync（双仓双流水）
        + IUnitOfWork（同一事务）
        → PostgreSQL（Transfers / TransferItems / Inventory / StockMovements）
```

核心原则：

- **调拨 = 同组织内的库存位移**：只改「哪个仓」，不改组织级总量、不改组织级成本。
- **两条流水、一个来源**：转出与转入各一条流水、指向同一调拨单，便于按仓对账（`038` §0）与按单追溯。
- **一步式、可作废**：与采购 / 销售 / 退货一致（`015` / `021` 口径），不引入状态机（在途见范围外）。
- **明细不做快照单价**：调拨无价格；成本已落在流水（`026`），明细不存成本（避免第二口径）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；状态枚举复用 `OrderStatus`（`1=正常 0=已作废`）。

### 2.1 实体 `App.Core/Entities/Transfer.cs` 与表 `Transfers`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `TransferNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单号 `TR + yyyyMMdd + 4 位序号`（`ROADMAP` §6.7 分配 `TR`） |
| `FromWarehouseId` | `Guid` | `uuid` | NOT NULL，FK → `Warehouses(Id)` | 转出仓 |
| `FromWarehouseName` | `string` | `varchar(50)` | NOT NULL | 转出仓名称**快照** |
| `ToWarehouseId` | `Guid` | `uuid` | NOT NULL，FK → `Warehouses(Id)` | 转入仓 |
| `ToWarehouseName` | `string` | `varchar(50)` | NOT NULL | 转入仓名称**快照** |
| `TransferDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 业务日期（UTC 午夜） |
| `ItemCount` | `int` | `integer` | NOT NULL | 明细行数（列表展示） |
| `TotalQuantity` | `int` | `integer` | NOT NULL | 数量合计（列表展示） |
| `Status` | `OrderStatus` | `smallint` | NOT NULL，默认 `1` | 复用（1=正常 0=已作废） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

### 2.2 实体 `App.Core/Entities/TransferItem.cs` 与表 `TransferItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `TransferId` | `Guid` | `uuid` | NOT NULL，FK → `Transfers(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductCode` / `ProductName` | `string` | `varchar(32)` / `varchar(50)` | NOT NULL | 编码 / 名称**快照** |
| `Unit` | `string` | `varchar(10)` | NOT NULL | 单位**快照** |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | 调拨数量 |
| `BatchId` | `Guid?` | `uuid` | NULL | 批次（`040` 落地时启用；`040` 前恒为空） |

- 明细不软删除（作废保留）；同一单据内**不允许重复「商品 + 批次」组合**（Validator 拦重复）。

### 2.3 流水类型追加（`019` §0 续行）

| 枚举值 | 业务文案 | 库存方向 | 颜色 |
|---|---|---|---|
| `TransferOut = 11` | 调拨转出 | 减少 | `geekblue` |
| `TransferIn = 12` | 调拨转入 | 增加 | `lime` |
| `TransferOutVoid = 13` | 调拨转出作废 | 增加 | `volcano` |
| `TransferInVoid = 14` | 调拨转入作废 | 减少 | `magenta` |

- 枚举值追加**不涉及迁移**（`smallint`）；前端类型下拉与文案表随本规格续行。

### 2.4 迁移与字段约束

- 迁移：`dotnet ef migrations add AddErpTransfer -p src/App.Infrastructure -s src/App.Api`（增量迁移，两张表 + 索引；外键不级联删除）。
- **不新建常量类**：`TransferNo` 20 / `keyword` 20 / `Remark` 200 / 明细行数上限 100 引用 `OrderFieldConstraints`；`Quantity` 上界引用 `ProductFieldConstraints.QuantityMaxValue`（单据域同源，后端规则 §5.3）。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/ITransferRepository.cs`）

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<Transfer> Items, int Total)> GetPagedAsync(string? keyword, Guid? fromWarehouseId, Guid? toWarehouseId, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, ...)` | 列表（`keyword` 匹配 `TransferNo`；`TransferDate` 闭区间；`CreatedAt DESC`；含作废；`AsNoTracking`） |
| `Task<(Transfer? Transfer, IReadOnlyList<TransferItem> Items)> GetDetailAsync(Guid id, ...)` | 详情（主表 + 明细，按插入顺序） |
| `Task AddAsync(Transfer, IReadOnlyList<TransferItem>, ...)` | 新增（同一仓储内一次 `SaveChangesAsync`） |
| `Task UpdateStatusAsync(Guid id, OrderStatus s, ...)` | 作废 |
| `Task<string> GenerateTransferNoAsync(DateTimeOffset transferDate, ...)` | 生成单号（`TR`，机制同 `erp-purchase` §3.6：当天 `COUNT(*)` + 4 位补零 + 唯一索引重试） |

- 库存与流水走既有仓储（`IInventoryRepository` / `IStockMovementRepository`，均为 `038` 后的按仓签名）；跨仓储写用 `IUnitOfWork`（后端规则 §4.4）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40126 | `TransferSameWarehouse` | 转出仓与转入仓不能相同 |

> 复用：`40103 InsufficientStock`（转出仓不足，message 含仓名）、`40104 OrderVoided`、`40107 ProductDisabled`、`40110 OrderItemsEmpty`、`40123 WarehouseDisabled`、`40400` / `40000`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/transfers` | GET | `Transfers/GetTransfers` | `PagedResult<TransferListItemDto>` | `transfers.view` / 40000 |
| `/api/transfers` | POST | `Transfers/CreateTransfer` | `TransferDetailDto` | `transfers.create` / 40000 / 40103 / 40107 / 40110 / 40123 / 40126 / 40400 |
| `/api/transfers/{id:guid}` | GET | `Transfers/GetTransferById` | `TransferDetailDto` | `transfers.view` / 40400 |
| `/api/transfers/{id:guid}/void` | PUT | `Transfers/VoidTransfer` | `TransferDetailDto` | `transfers.void` / 40104 / 40400 |

### 3.4 关键用例流程（Handler）

**CreateTransfer**：

1. 明细为空 → `40110`（Validator 双保险）；`fromWarehouseId == toWarehouseId` → `40126`。
2. 取转出仓 / 转入仓：不存在 → `40400`；停用 → `40123`（转出与转入都要求启用）。
3. 逐行取商品：不存在 → `40400`；停用 → `40107`；写编码 / 名称 / 单位快照。
4. `GenerateTransferNoAsync(transferDate)`；统计 `ItemCount` / `TotalQuantity`。
5. `IUnitOfWork`：
   - `BeginTransactionAsync`；
   - 逐行：`avgCost = GetAverageCostAsync(productId)`（`026`）→ `TryDecrementAsync(productId, fromWarehouseId, q)`，失败 → `RollbackAsync` + `40103`（message 含转出仓名）→ `ApplyOutboundCostAsync(productId, Round(q × avgCost))` → `AppendAsync(TransferOut, -q, fromWarehouseId, unitCost = avgCost)`；
   - 逐行：`IncrementAsync(productId, toWarehouseId, q)` → `ApplyInboundCostAsync(productId, q, avgCost)`（**同一单价**）→ `AppendAsync(TransferIn, +q, toWarehouseId, unitCost = avgCost)`；
   - `AddAsync(transfer, items)`；
   - `CommitAsync`。

> 顺序说明：先完成全部转出（含成本与流水），再执行转入——保证「转出未成功时绝不产生转入」，且成本单价取自转出时的均价（转入用同一单价，保证净额为 0）。

**VoidTransfer**：

1. `GetDetailAsync`：不存在 → `40400`；`Status = Voided` → `40104`。
2. `IUnitOfWork`：逐行 → 转入仓 `IncrementAsync(-q)` + `ApplyOutboundCostAsync`（按原转入流水的 `UnitCost`）+ `AppendAsync(TransferInVoid, -q)`；转出仓 `IncrementAsync(+q)` + `ApplyInboundCostAsync(q, 原单价)` + `AppendAsync(TransferOutVoid, +q)`；`UpdateStatusAsync(Voided)` + 审计 → `CommitAsync`。
3. 原单价取法：`GetMovementUnitCostAsync(transferId, productId, TransferIn)`（`026` §3.1 既有能力）；取不到（异常数据）→ 按当前均价兜底。

**GetTransfers / GetTransferById**：筛选（含转出 / 转入仓、日期区间）与映射（快照透传，含 `Status` 供前端置灰）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量）

| 请求 | 规则 |
|---|---|
| `CreateTransferRequest` | `fromWarehouseId` / `toWarehouseId` 必填且**不相等**（`40126` 由 Handler 抛业务码，Validator 只校验「非空」——相等判定放 Handler 以统一错误码来源，避免两处；格式层校验「明细非空、1–100 行、行内 `productId` 必填、`quantity` 1–999999、`productId` 不重复」） |
| `GetTransfersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20；`fromWarehouseId` / `toWarehouseId` 可空；`start` / `end` 可空且 `start <= end` |

### 3.6 Swagger

- **不分组**（同既有约定）：4 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── transfer.ts                    # 调拨接口层
└── views/
    └── TransferManagement/
        ├── TransfersView.vue          # 调拨单列表
        ├── TransferFormPage.vue       # 开单调拨（独立页，含明细子表格）
        └── TransferDetailView.vue     # 详情（含作废）
```

- 开单为独立页面（含明细子表格，`007` §0 形态选择）；详情为独立页面。

### 4.2 接口层

- `src/api/transfer.ts`：类型与后端 DTO 一一对应；`getTransfers` / `createTransfer` / `getTransferById` / `voidTransfer`。
- 仓库下拉复用 `src/api/warehouse.ts` 的 `getWarehousePickList`；商品下拉复用 `src/api/product.ts` 的 `getProductPickList`（含各仓库存需按仓查询 —— 决策：`pick` 返回库存合计即可，明细行选择转出仓后**逐行按需查询该仓库存**（复用 `getInventory({ productId, warehouseId })`，`pageSize = 1`），避免为调拨新增专用接口（见 §5）。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `transfers` | `transfers` | `TransfersView` |
| `transfers/new` | `transferNew` | `TransferFormPage` |
| `transfers/detail/:id` | `transferDetail` | `TransferDetailView` |

- `AppLayout.vue`「库存」分组追加「调拨单」（`025` §0.2 总表已预留）；`MENU_ROUTE_MAP` 增加 `transferDetail: 'transfers'`。

### 4.4 页面交互

**列表 `TransfersView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（单号关键词 + 转出仓 + 转入仓 + 日期范围 + 搜索 / 重置）；操作行（「新调拨」primary + 刷新 + 列设置）；列：序号、单号、转出仓、转入仓、调拨日期、商品行数、数量合计、状态（正常绿 / 已作废红，作废行整体置灰）、创建时间、操作列（详情 / 作废（`status="danger"` + popconfirm + `voidingId`，仅正常单显示））；服务端分页。

**开单页 `TransferFormPage.vue`**：表头「转出仓」（`a-select`，默认仓预选）+ 「转入仓」（默认仓预选，**:disabled 排除已选转出仓** 或提交时提示）+ 调拨日期（默认当天）+ 备注；明细子表格（商品下拉 / 单位 / 可用库存（选中转出仓后按行查询并展示）/ 数量 `a-input-number :min="1"` / 行删除 / 「添加行」）；底部数量合计（computed，仅展示）+ 提交（`submitting`）+ 取消。

**详情页 `TransferDetailView.vue`**：`a-page-header` + `a-descriptions`（单号 / 转出仓 / 转入仓 / 日期 / 行数 / 数量合计 / 状态 / 备注 / 创建人 / 创建时间）+ 明细只读表格（编码 / 名称 / 单位 / 数量）+ 底部作废（`status="danger"` + popconfirm + `voidingId`）；id 不存在 → `a-result status="404"`。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 明细行可用库存加载 | `stockLoading` | 明细区（`a-spin`，非按钮） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| **一期一步式（不含在途）** | 保存即生效 | 路线图原文含在途；本规格明确一期不做并写入范围外（需用户确认）。理由：在途需状态机 + 「已发出未到达」口径，与 `024` 两段式单据同属一类能力，宜共用模板；先解决「有无凭证」（当前完全缺失）的痛点 |
| 在途演进路径（记录） | `Transfers` 加 `Status`（待收货 / 部分收货 / 已完成）+ 转出 / 转入两张执行单 | 与 `024` 的订单 → 出入库同构；届时 `TransferOut` 流水在「发出」时写、`TransferIn` 在「收到」时写，在途 = 已发出未收到数量 |
| 转出即入（同事务） | 一次事务完成双仓改动 | 一步式下不应存在中间态（转出了但没转入）；任一步失败整体回滚 |
| 成本用同一单价结转 | `TransferIn.UnitCost = TransferOut.UnitCost` | 组织级均价下，调拨只改「货在哪个仓」，总成本与均价必须不变；若两端各按当时均价计算，会因期间其他业务导致差额漂移 |
| 不落业务金额 | 单据无 `TotalAmount` | 调拨无价格概念；写入金额会引入「内部结算价」的歧义（属范围外） |
| 明细不存成本单价 | 成本只在流水 | `026` 已把成本落在流水；明细再存一份就是第二口径（后端规则 §5.3 精神） |
| 单仓对（一进一出） | 不建中间表 | 一张调拨单只有一对来源 / 目标仓，中间表纯冗余 |
| 作废允许冲负 | `IncrementAsync` 无下限 | 与采购 / 销售 / 退货作废对称：期间货可能已被卖掉，作废必须可执行 |
| 可用库存按需查询 | 复用 `getInventory({ productId, warehouseId })` | 避免为调拨新增「按仓 + 商品」专用接口；`028` 的 `inventory.view` 权限天然覆盖，前端实现简单 |
| 单号 `TR` | `ROADMAP` §6.7 分配 | 新增单据类型前先查前缀表，避免冲突（本规格落地时在表中把 `039` 行标记为已启用） |
| 权限点 | `transfers.view/create/void/export`（`028` §0.2 已登记） | 权限清单唯一事实源，无需新增行 |
| 无 RBAC 特例 | 复用现有权限点 | 同上 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreateTransfer 成功**：断言单号前缀 `TR` + 日期；明细快照（编码 / 名称 / 单位）；`ItemCount` / `TotalQuantity` 统计；逐行调用顺序「`GetAverageCostAsync` → `TryDecrementAsync(转出仓)` → `ApplyOutboundCostAsync` → `AppendAsync(TransferOut)` → `IncrementAsync(转入仓)` → `ApplyInboundCostAsync` → `AppendAsync(TransferIn)`」；两条流水 `UnitCost` 相同、`Quantity` 互为相反数且仓正确；`Commit` 被调用。
- **异常**：同仓 `40126`；转出仓不足 `40103`（message 含仓名）且断言**未发生任何转入**、`RollbackAsync` 被调用、`AddAsync` 未调用；转出 / 转入仓不存在 `40400` / 停用 `40123`；商品不存在 `40400` / 停用 `40107`；明细空 `40110`；`Commit` 抛异常 → `RollbackAsync`。
- **VoidTransfer**：成功（断言转入仓 `-q` + 转出仓 `+q`、两条反向流水、`UnitCost` 取原流水、`UpdateStatusAsync(Voided)`）；已作废 `40104`；不存在 `40400`；原单价缺失时兜底当前均价。
- **GetTransfers / GetTransferById**：筛选传参（转出仓 / 转入仓 / 日期 / 关键词）、分页映射（含 `Status`）、明细快照、不存在 `40400`。
- **对账一致性**：调拨 → 作废链路后，按仓 `Σ 流水 Quantity == 该仓 Inventory.Quantity`，且组织级 `Σ Quantity` 与 `Σ TotalCost` 在调拨前后不变（`038` / `026` 口径回归）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Transfers.TransferNo` `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；明细列长（32 / 50 / 10）与商品域常量一致；`quantity` 999999 / 1e6 边界；`keyword` 20 / 21。

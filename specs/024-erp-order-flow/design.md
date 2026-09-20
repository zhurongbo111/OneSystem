---
created: 2026-09-16
updated: 2026-09-17
---

# 设计规格：两段式单据（erp-order-flow）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> **实施前置**：§5 中标注「需用户确认」的两项（既有单据表重命名、历史单号前缀改写）涉及不可逆的命名与数据变更，**须先经用户确认再开工**；未确认前先实现其余部分无意义（改动面互相耦合），故整体置于确认后。
> 本规格改造既有 `erp-purchase` / `erp-sale`（重命名 + 关联订单 + 状态回写），并消费 `erp-stock-movement`（流水）与 `erp-settlement`（结算端点已在其中改造完毕）。

## 0. 订单状态与展示约定（唯一事实源）

订单状态文案与颜色全项目唯一来源；采购 / 销售列表、详情、出入库单开单页与 e2e 断言均以此为准。

| 枚举值 | 采购侧文案 | 销售侧文案 | `a-tag` 颜色 |
|---|---|---|---|
| `Voided = 0` | 已作废 | 已作废 | `red` |
| `Pending = 1` | 待收货 | 待发货 | `blue` |
| `Partial = 2` | 部分收货 | 部分发货 | `orange` |
| `Completed = 3` | 已完成 | 已完成 | `green` |
| `Closed = 4` | 已关闭 | 已关闭 | `gray` |

- 明细累计量字段统一命名 `FulfilledQuantity`（采购侧展示为「已收数量」、销售侧为「已发数量」）；未执行量 = `Quantity − FulfilledQuantity`（采购侧「未收数量」、销售侧「未发数量」，**推导不落列**）。

## 1. 总体设计

```
采购订单（前端 /purchase-orders）
  → PurchaseOrdersController
    → App.Core/Features/PurchaseOrders/<Action>/*RequestHandler
      → IPurchaseOrderRepository（订单 + 明细，含 AddFulfilledQuantityAsync 原子累加）
        → PostgreSQL（PurchaseOrders / PurchaseOrderItems）      ← 不动库存、不写流水

采购入库（前端 /purchases，改造）
  → PurchaseReceiptsController（原 PurchaseOrdersController）
    → App.Core/Features/PurchaseReceipts/<Action>/*RequestHandler
      → IPurchaseReceiptRepository（入库单 + 明细）
        + IPurchaseOrderRepository（关联订单时的校验与累计量回写）
        + IInventoryRepository.IncrementAsync + IStockMovementRepository.AppendAsync
        + IUnitOfWork（同一事务）
        → PostgreSQL（PurchaseReceipts / PurchaseReceiptItems / PurchaseOrders / PurchaseOrderItems / Inventory / StockMovements）
```

核心原则：

- **订单不动库存、不写流水**：订单是计划；库存与流水仍由出入库单驱动（`019` 口径不变）。
- **关联可选、回写必准**：出入库单 `OrderId` 可空；关联时按明细校验「本次数量 ≤ 未执行量」并原子累加订单明细的 `FulfilledQuantity`，随后推导订单状态。
- **订单状态单字段**：`OrderFlowStatus`（含 `Voided`），由累计量与显式操作（作废 / 关闭）共同决定，不引入第二个状态列。
- **出入库单语义不变**：仍是一步式、不可编辑、可作废；作废时同步回退订单累计量。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 重命名与新增（对应 §5 待确认决策）

| 现状 | 024 后 | 说明 |
|---|---|---|
| `PurchaseOrders` / `PurchaseOrder`（一步式采购单，前缀 `PO`） | `PurchaseReceipts` / `PurchaseReceipt`（采购入库单，前缀 `GR`） | 表 / 实体重命名；用例目录 `Features/Purchases` → `Features/PurchaseReceipts`；接口 `/api/purchase-orders` → `/api/purchase-receipts` |
| `PurchaseOrderItems` / `PurchaseOrderItem` | `PurchaseReceiptItems` / `PurchaseReceiptItem` | 明细同上（`OrderId` 列 → `ReceiptId`） |
| `SalesOrders` / `SalesOrder`（前缀 `SO`） | `SalesShipments` / `SalesShipment`（销售出库单，前缀 `GI`） | 同上（`Features/Sales` → `Features/SalesShipments`；`/api/sales-orders` → `/api/sales-shipments`） |
| `SalesOrderItems` / `SalesOrderItem` | `SalesShipmentItems` / `SalesShipmentItem` | 明细（`OrderId` 列 → `ShipmentId`） |
| — | 新增 `PurchaseOrders` / `PurchaseOrderItems`（**订单**，前缀 `PO`） | 本规格新增 |
| — | 新增 `SalesOrders` / `SalesOrderItems`（**订单**，前缀 `SO`） | 本规格新增 |

- 单号前缀全域分配见 `specs/ROADMAP.md` §6.7。
- 列重命名：原主表 `OrderNo` → `ReceiptNo` / `ShipmentNo`（避免与订单号混淆）；明细 `OrderId` → `ReceiptId` / `ShipmentId`。

### 2.2 订单实体（采购 `PurchaseOrder` / 表 `PurchaseOrders`；销售同构）

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `PO + yyyyMMdd + 4`（销售 `SO`） |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 供应商 / 客户 |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 名称**快照** |
| `OrderDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 下单日期（UTC 午夜） |
| `ExpectedDate` | `DateTimeOffset?` | `timestamptz` | NULL | 预计到货 / 发货日期 |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 总金额 = Σ 小计（后端计算） |
| `FlowStatus` | `OrderFlowStatus` | `smallint` | NOT NULL，默认 `1` | 见 §0 |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

订单明细（`PurchaseOrderItem` / `PurchaseOrderItems`；销售同构）：

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderId` | `Guid` | `uuid` | NOT NULL，FK → `PurchaseOrders(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductName` / `Unit` | `string` | `varchar(50)` / `varchar(10)` | NOT NULL | 名称 / 单位**快照** |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | 订购数量 |
| `UnitPrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 单价**快照** |
| `Subtotal` | `decimal` | `numeric(18,2)` | NOT NULL | 后端计算 |
| `FulfilledQuantity` | `int` | `integer` | NOT NULL，默认 0，0 ≤ x ≤ `Quantity` | 累计已收 / 已发（由出入库单回写） |

- 枚举（采购 / 销售共用，`App.Core/Entities/OrderFlowStatus.cs`）：`Voided = 0` / `Pending = 1` / `Partial = 2` / `Completed = 3` / `Closed = 4`。
- 出入库单**不再使用** `OrderStatus`（`Normal/Voided`）；作废语义统一由订单侧的 `FlowStatus = Voided` 与出入库单的 `OrderStatus`（既有，保留）表达。

### 2.3 出入库单改造（在既有表上追加）

| 表 | 新增 / 变更 |
|---|---|
| `PurchaseReceipts`（原 `PurchaseOrders`） | 新增 `OrderId`（`Guid?`，FK → `PurchaseOrders(Id)`，索引）、`OrderNo`（`varchar(20)?`，订单号快照）；`OrderNo` 列重命名为 `ReceiptNo` |
| `PurchaseReceiptItems`（原 `PurchaseOrderItems`） | 新增 `OrderItemId`（`Guid?`，FK → `PurchaseOrderItems(Id)`）；`OrderId` 列重命名为 `ReceiptId` |
| `SalesShipments` / `SalesShipmentItems` | 同构（`OrderId` / `OrderNo` / `OrderItemId`；`ShipmentNo` / `ShipmentId`） |

- 快照 `OrderNo` 使入库单列表 / 详情免 join 订单表（同单据快照原则）。

### 2.4 迁移

- 迁移：`dotnet ef migrations add AddErpOrderFlow -p src/App.Infrastructure -s src/App.Api`（增量迁移），内容包含：
  1. `RenameTable` / `RenameColumn`（§2.1、§2.3）；
  2. 新建订单两张表（采购 / 销售各主表 + 明细表，共 4 张）；
  3. 出入库单新增列（`OrderId` / `OrderNo` / `OrderItemId`）；
  4. **历史单号前缀改写**（数据迁移，`migrationBuilder.Sql`，仅迁移内使用；业务代码仍禁止裸 SQL —— 后端规则 §5.1）：
     `UPDATE "PurchaseReceipts" SET "ReceiptNo" = 'GR' || substring("ReceiptNo" from 3)`、`UPDATE "SalesShipments" SET "ShipmentNo" = 'GI' || substring("ShipmentNo" from 3)`。
- 订单表初始为空（历史单据**不**回溯生成订单）。

### 2.5 字段约束单一来源（**不新建常量类**）

| 用途 | 常量来源 |
|---|---|
| 订单 / 出入库单 `OrderNo` 长度（20）/ 查询 `keyword`（20）/ `Remark`（200）/ 明细行数上限（100） | `OrderFieldConstraints` |
| 明细 `Quantity` 1–999999 / `UnitPrice` 0–9999999.99 | `ProductFieldConstraints` |

- 一致性由单测守护：EF 实际 `HasMaxLength` == 常量；`FulfilledQuantity` 的「0 ≤ x ≤ Quantity」在 Handler 与 Validator 双处一致（不为该推导约束新建常量，业务约束在 Handler）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 | 变更 |
|---|---|
| `IPurchaseOrderRepository` | **语义替换**（原为入库单仓储）：`GetPagedAsync`（keyword / partnerId / flowStatus / 日期范围；`CreatedAt DESC`）/ `GetDetailAsync` / `AddAsync` / `UpdateAsync`（明细全量替换）/ `UpdateFlowStatusAsync` / `GetLinesAsync(Guid orderId, ...)`（明细 + 未执行量，供开单页）/ `GetPicksAsync(Guid partnerId, ...)`（可关联订单候选：`Pending` / `Partial`）/ `AddFulfilledQuantityAsync(Guid orderItemId, int delta, ...)`（`ExecuteUpdateAsync` 原子累加，回退用负值）/ `GenerateOrderNoAsync` |
| `IPurchaseReceiptRepository` | **由原 `IPurchaseOrderRepository` 重命名**：原方法保留（`GetPagedAsync` 增加 `Guid? orderId` 筛选），单号前缀 `GR` |
| `ISalesOrderRepository` | 同采购订单（`SO`） |
| `ISalesShipmentRepository` | 同采购入库（`GI`） |

- **跨仓储写**（出入库单 + 库存 + 流水 + 订单累计量与状态）必须用 `IUnitOfWork` 包成同一事务（后端规则 §4.4）。
- 订单仓储的 `AddFulfilledQuantityAsync` 与出入库单仓储的写入同事务，保证「单已落库但订单累计量未更新」不会发生。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40115 | `OrderFulfillExceeded` | 本次数量超过订单未执行数量（message 含订单号、商品名与未执行数量） |
| 40116 | `OrderStateInvalid` | 订单当前状态不允许该操作（编辑 / 作废 / 关闭 / 关联收货；message 说明当前状态） |
| 40117 | `OrderPartnerMismatch` | 出入库单的往来单位与所关联订单不一致 |

> 复用：`40104 OrderVoided`（订单 / 出入库单已作废）、`40107` / `40108` / `40109` / `40110` / `40103`（销售扣减不足）/ `40400` / `40000`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

**采购订单（`Features/PurchaseOrders`）**

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/purchase-orders` | GET | `PurchaseOrders/GetPurchaseOrders` | `PagedResult<PurchaseOrderListItemDto>` | 40000 |
| `/api/purchase-orders` | POST | `PurchaseOrders/CreatePurchaseOrder` | `PurchaseOrderDetailDto` | 40000 / 40107 / 40108 / 40109 / 40110 / 40400 |
| `/api/purchase-orders/{id:guid}` | GET | `PurchaseOrders/GetPurchaseOrderById` | `PurchaseOrderDetailDto` | 40400 |
| `/api/purchase-orders/{id:guid}` | PUT | `PurchaseOrders/UpdatePurchaseOrder` | `PurchaseOrderDetailDto` | 40000 / 40104 / 40107 / 40116 / 40400 |
| `/api/purchase-orders/{id:guid}/void` | PUT | `PurchaseOrders/VoidPurchaseOrder` | `PurchaseOrderDetailDto` | 40104 / 40116 / 40400 |
| `/api/purchase-orders/{id:guid}/close` | PUT | `PurchaseOrders/ClosePurchaseOrder` | `PurchaseOrderDetailDto` | 40104 / 40116 / 40400 |

**采购入库（`Features/PurchaseReceipts`，由原 `Features/Purchases` 重命名）**

| 接口 | 方法 | 用例目录 | 变化 |
|---|---|---|---|
| `/api/purchase-receipts` | GET | `PurchaseReceipts/GetPurchaseReceipts` | 新增 `orderId` 筛选 |
| `/api/purchase-receipts` | POST | `PurchaseReceipts/CreatePurchaseReceipt` | 新增 `orderId` / `orderItemId` 校验与订单回写 |
| `/api/purchase-receipts/{id:guid}` | GET | `PurchaseReceipts/GetPurchaseReceiptById` | 出参新增订单号 |
| `/api/purchase-receipts/{id:guid}/void` | PUT | `PurchaseReceipts/VoidPurchaseReceipt` | 新增订单累计量回退与状态重算 |
| `/api/purchase-receipts/pick-orders` | GET | `PurchaseReceipts/GetPurchaseOrderPicks` | 候选订单（按供应商，`Pending` / `Partial`） |
| `/api/purchase-receipts/order-lines` | GET | `PurchaseReceipts/GetPurchaseOrderLines` | 订单明细 + 未收数量 |

路由注意：`pick-orders` / `order-lines` 为固定段，置于 `{id:guid}` 之前注册。

**销售侧同构**：`/api/sales-orders`（订单，`GetSalesOrders` / `CreateSalesOrder` / `GetSalesOrderById` / `UpdateSalesOrder` / `VoidSalesOrder` / `CloseSalesOrder`）与 `/api/sales-shipments`（出库单，原 `/api/sales-orders`，含 `pick-orders` / `order-lines`）。

### 3.4 关键用例流程（Handler）

**CreatePurchaseOrder**（订单）：明细空 → `40110`；供应商校验（`40400` / `40108` / `40109`）；逐行商品校验（`40400` / `40107`）+ 金额重算 + 快照；`GenerateOrderNoAsync("PO", orderDate)`；明细 `FulfilledQuantity = 0`、主表 `FlowStatus = Pending`；`AddAsync`。**不触碰库存与流水**。

**UpdatePurchaseOrder**：取单（`40400`）→ `FlowStatus = Voided` → `40104` → `FlowStatus != Pending` → `40116`；供应商 / 商品校验与金额重算同创建；明细全量替换（`FulfilledQuantity` 恒为 0，仅此状态可改）；`UpdateAsync` + 审计。

**VoidPurchaseOrder**：取单（`40400` / `40104`）→ `FlowStatus != Pending` → `40116`；`UpdateFlowStatusAsync(Voided)` + 审计。

**ClosePurchaseOrder**：取单（`40400` / `40104`）→ `FlowStatus ∈ {Pending, Partial}` 之外 → `40116`；`UpdateFlowStatusAsync(Closed)` + 审计。

**CreatePurchaseReceipt**（改造）：

1. 明细空 → `40110`（Validator 双保险）。
2. 供应商校验（`40400` / `40108` / `40109`）；逐行商品校验（`40400` / `40107`）+ 金额重算 + 快照。
3. `orderId` 非空时（关联订单）：
   - 取订单 → `40400`；订单 `FlowStatus = Voided` → `40104`；`FlowStatus ∈ {Completed, Closed}` → `40116`；订单供应商 != 入库单供应商 → `40117`。
   - 逐行：`orderItemId` 必填且属于该订单（否则 `40400`）；`quantity ≤ Quantity − FulfilledQuantity` 否则 `40115`（含订单号、商品名、未收数量）。
4. `IUnitOfWork`：`BeginTransactionAsync` → `GenerateOrderNoAsync("GR", orderDate)` → `AddAsync`（入库单 + 明细）→ 逐行 `IncrementAsync(+quantity)` + `AppendAsync` 流水（`PurchaseInbound`，来源为入库单 id / `ReceiptNo`）→ 关联订单时逐行 `AddFulfilledQuantityAsync(orderItemId, +quantity)` → 用「本次累计后的未执行量」重算订单 `FlowStatus`（全部为 0 → `Completed`；部分 → `Partial`；否则保持 `Pending`）→ `UpdateFlowStatusAsync` → `CommitAsync`。

**VoidPurchaseReceipt**（改造）：取单（`40400` / `40104`）→ `IUnitOfWork`：逐行 `IncrementAsync(-quantity)` + 流水（`PurchaseVoid`）→ 关联订单时逐行 `AddFulfilledQuantityAsync(orderItemId, -quantity)` → 重算订单状态（**仅当订单当前为 `Partial` / `Completed`**；`Closed` 保持关闭、`Voided` 不出现）→ `UpdateStatusAsync(Voided)` + 审计 → `CommitAsync`。

**GetPurchaseOrderPicks / GetPurchaseOrderLines**：候选订单（按供应商 + 状态）与订单明细（含未收数量）只读查询。

**销售侧差异**：出库单（`SalesShipment`）保存时**先扣库存**（`TryDecrementAsync`，失败 `40103`，同既有销售单顺序），成功后插单 + 流水（`SalesOutbound`）+ 订单回写（`FulfilledQuantity` 为已发量）；作废时回增库存 + 流水（`SalesVoid`）+ 回退累计量。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.5 常量）

| 请求 | 规则 |
|---|---|
| `CreatePurchaseOrderRequest` / `UpdatePurchaseOrderRequest` | `partnerId` 必填；`orderDate` 必填；`expectedDate` 可空且 ≥ `orderDate`；`items` 1–100 行；每行 `productId` 必填、`quantity` 1–999999、`unitPrice` 0–9999999.99；`remark` ≤ 200 |
| `GetPurchaseOrdersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20；`partnerId` / `flowStatus` 可空合法值；`start` / `end` 可空且 `start <= end` |
| `CreatePurchaseReceiptRequest` | 既有规则 + `orderId` 可空；`orderId` 非空时每行 `orderItemId` 必填，`orderId` 为空时每行 `orderItemId` 必须为空（Validator 校验一致性，违反 → `40000`） |
| `GetPurchaseOrderLinesRequest` | `orderId` 必填 |

- 存在性 / 状态 / 未执行量等业务约束一律在 Handler（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（同既有约定）：新增与重命名后的接口按现有方式出现在单文档 Swagger 中；`specs/003-api-swagger` 的端点断言需同步（重命名 + 新增）。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── purchaseOrder.ts      # 采购订单（列表 / 详情 / 保存 / 作废 / 关闭 / 候选订单 / 订单明细）
│   ├── purchase.ts           # 采购入库（改造：+ orderId / orderItemId 与候选订单接口）
│   ├── saleOrder.ts          # 销售订单
│   └── sale.ts               # 销售出库（改造）
└── views/
    ├── PurchaseOrderManagement/
    │   ├── PurchaseOrdersView.vue        # 订单列表
    │   ├── PurchaseOrderFormPage.vue     # 新建 / 编辑订单（独立页面）
    │   └── PurchaseOrderDetailView.vue   # 订单详情（含关联入库单）
    ├── PurchaseManagement/               # 采购入库（保留目录：同一功能域）
    ├── SalesOrderManagement/             # 同构
    └── SalesManagement/                  # 销售出库（保留目录）
```

- 入库 / 出库单的目录与路由**保留**（`purchases` / `sales`）：域语义仍为「采购入库 / 销售出库」，与后端 `PurchaseReceipts` / `SalesShipments` 属同一功能域（前端规则 §4.1 不要求字面同名）；改路由会使既有书签失效并大改 e2e（见 §5 决策）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `purchase-orders` | `purchaseOrders` | `PurchaseOrdersView` |
| `purchase-orders/new` | `purchaseOrderNew` | `PurchaseOrderFormPage` |
| `purchase-orders/edit/:id` | `purchaseOrderEdit` | `PurchaseOrderFormPage` |
| `purchase-orders/detail/:id` | `purchaseOrderDetail` | `PurchaseOrderDetailView` |
| `sales-orders` / `sales-orders/new` / `sales-orders/edit/:id` / `sales-orders/detail/:id` | 同构 | `SalesOrderManagement/` |

- `AppLayout.vue`「进销存」分组追加子项「采购订单」`purchaseOrders` 与「销售订单」`salesOrders`；`MENU_ROUTE_MAP` 增加 `purchaseOrderEdit` / `purchaseOrderDetail` / `saleOrderEdit` / `saleOrderDetail` 归入各自父菜单。
- 既有「销售开单」菜单文案改为「销售出库」（与「采购入库」对称）。
- 菜单项数量增长后的分组升级见范围外（`specs/024-erp-order-flow/requirement.md` §5）。

### 4.3 页面交互

**订单列表 `PurchaseOrdersView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：单号关键词 + 供应商 + 状态下拉（§0 文案）+ 日期范围 + 搜索 / 重置；操作行：新建订单 / 刷新 / 列设置。
- 表格列：序号、单号、供应商、订单日期、预计到货、总金额、**未收数量**（Σ 未执行量，> 0 时常规色、= 0 时灰色）、状态（`a-tag` §0）、创建时间、操作列（4 个：详情 → 编辑（仅 `Pending` 显示）→ 关闭（`Pending` / `Partial` 显示，警示）→ 作废（仅 `Pending`，危险）；按 `specs/011-action-column` §0 阈值：前 3 平铺、其余收纳「更多」）。
- 服务端分页；已作废 / 已关闭行整体置灰。

**订单新建 / 编辑 `PurchaseOrderFormPage.vue`**（独立页面）：供应商下拉 + 订单日期 + 预计到货（可空）+ 备注 + 明细子表格（商品 / 数量 / 单价 / 小计 / 行删除 / 添加行）+ 底部总额与提交；编辑时回填（明细深拷贝），`submitting` 防重入；提交成功跳详情。

**订单详情 `PurchaseOrderDetailView.vue`**：`a-page-header` + `a-descriptions`（单号 / 供应商 / 订单日期 / 预计到货 / 总额 / 状态 / 备注 / 创建人 / 创建时间）+ 明细只读表格（商品 / 单位 / 订购数量 / 已收数量 / **未收数量** / 单价 / 小计）+ **关联入库单列表**（按 `orderId` 查入库单列表接口，列：单号 / 日期 / 状态）；底部操作：编辑 / 关闭 / 作废（显示条件同上）+ **「去入库」**（跳 `/purchases/new?orderId=xxx`，仅 `Pending` / `Partial` 显示）。

**入库开单页 `PurchaseFormPage.vue`（改造）**：

- 表头新增「关联订单」下拉（可选；数据源 `pick-orders` 按所选供应商过滤，`Pending` / `Partial`）；选择后调用 `order-lines` 带出明细。
- 关联状态下：明细行**固定为订单明细**（不可新增商品行，可删除行表示本次不收该行），每行展示订购数量 / 已收 / 未收，本次数量 `a-input-number` 上限为未收数量（超出标红并提示）；单价取订单单价（只读）。
- 未关联状态下：行为与既有完全一致（`orderLinesLoading` 仅在选择订单时置位）。

**销售侧同构**：`SalesOrderManagement/` 与 `SaleFormPage.vue` 按同样规则改造（客户 / 销售价 / 库存预警保留）。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 订单列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 订单表单提交 | `submitting` | 提交按钮 |
| 订单作废 / 关闭（列表 / 详情） | `voidingId` / `closingId` | popconfirm 确认按钮 |
| 入库 / 出库单订单明细加载 | `orderLinesLoading` | 选择订单后的明细区 |
| 「去入库 / 去出库」跳转 | 不置 loading | 同步路由跳转 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 需用户确认 | 理由 / 取舍 |
|---|---|---|---|
| 既有单据表重命名 | `PurchaseOrders` → `PurchaseReceipts`（`GR`）、`SalesOrders` → `SalesShipments`（`GI`） | **是** | 「订单」这个名字必须归订单语义；否则长期并存「采购单 / 采购订单」两个易混概念。代价：表 / 实体 / 用例目录 / 接口路径 / 前端 api 文件 / e2e 的一次性调整（行为不变） |
| 历史单号前缀改写 | 迁移内 `UPDATE`（`PO…`→`GR…`、`SO…`→`GI…`） | **是** | 不改写会出现「历史 `PO` 是入库单、新 `PO` 是订单」的歧义与重号观感；本仓库为 MVP 阶段、单号无外部引用，改写成本低。若已有外部系统引用单号，应改为「不改写历史 + 双前缀并存」 |
| 备选方案：保留现有表名与前缀，订单另起前缀（如 `PY` / `SY`） | 不采用 | — | 语义别扭（「采购单」是入库单、「采购订单」是订单），且长期两套概念并存，属技术债 |
| 出入库单关联订单可选 | `OrderId` 可空 | 否 | 兼容既有数据与「货到即入账」直通用法；强制关联会让简单场景变重（每笔都要先下单） |
| 订单不动库存、不写流水 | 仅计划数据 | 否 | 在途量是统计概念，不是库存；`019` 的流水口径保持不变 |
| 明细累计量统一命名 | `FulfilledQuantity` | 否 | 采购 / 销售共用一套实现（避免两套字段名与两套代码），展示文案按侧别区分（§0） |
| 订单仅 `Pending` 可编辑 / 作废 | `40116` 拒绝 | 否 | 已开始收货后改数量 / 删除行会让在途量与历史收货失真；`024` §4.4（ROADMAP）已定「订单可编辑、出入库单不可编辑」，此处限定在未执行状态 |
| 订单可手动关闭 | `ClosePurchaseOrder` | 否 | 供应商无法交齐是常态，需要显式「不再收了」；关闭后保留累计量、不再接受关联收货 |
| 作废出入库单回退累计量 | `AddFulfilledQuantityAsync(-q)` | 否 | 保持订单状态与在途量与实际一致；订单已 `Closed` 时**不回退状态**（关闭是人工决策） |
| 订单状态单字段 | `OrderFlowStatus`（含 `Voided`） | 否 | 避免 `Status` + `FlowStatus` 双字段导致列表展示与筛选组合爆炸（`Normal & 部分收货 & 已关闭` 之类） |
| 前端目录 / 路由保留 | 目录与 `purchases` / `sales` 路由不变 | 否 | 域语义一致（不要求字面同名）；改路由收益低、破坏书签与 e2e |
| 供应商一致性 | 新增 `40117` | 否 | 关联订单时供应商必须一致，否则累计量会记到错误订单；语义明确的独立码优于复用结算域 `40113` |
| 库存与流水顺序（销售） | 出库单仍先扣库存再插单 | 否 | 与既有销售单一致（防「单已落库但扣减失败」）；订单回写在插单之后（需要单据 id） |
| 菜单分组升级 | 范围外 | 否 | 菜单项将超 10 项，需二级分组（`005-app-layout` 的布局约定演进），与本规格解耦 |
| 无 RBAC | 登录即可见订单菜单 | 否 | 同既有功能（权限由 `028-erp-rbac` 接入） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **订单用例（采购 / 销售各一套）**：
  - `CreatePurchaseOrder`：成功（单号 `PO` + `orderDate`、快照、金额重算、`FlowStatus = Pending`、`FulfilledQuantity = 0`、**断言不触碰库存 / 流水仓储**）；异常（`40110` / `40400` / `40108` / `40109` / `40107`）。
  - `UpdatePurchaseOrder`：`Pending` 可改（明细全量替换断言）；`Partial` / `Completed` / `Closed` → `40116`；`Voided` → `40104`；不存在 → `40400`。
  - `VoidPurchaseOrder`：`Pending` 成功；`Partial` → `40116`；已作废 → `40104`。
  - `ClosePurchaseOrder`：`Pending` / `Partial` 成功；`Completed` / `Closed` / `Voided` → `40116` / `40104`。
  - `GetPurchaseOrders`：`flowStatus` / 供应商 / 日期范围筛选传参 + 未执行量聚合映射；`GetPurchaseOrderById`：明细（含 `FulfilledQuantity`）与 404；`GetPurchaseOrderPicks` / `GetPurchaseOrderLines`：候选状态过滤与未收数量计算。
- **出入库单改造（既有测试扩展）**：
  - 关联订单成功：断言 `AddFulfilledQuantityAsync(orderItemId, +quantity)`、订单状态推导三态（全收 → `Completed`、部分 → `Partial`）、`UpdateFlowStatusAsync`、库存与流水断言不变。
  - 异常：超出未执行量 → `40115`（含订单号 / 商品 / 未收数量）；订单 `Completed` / `Closed` → `40116`；供应商不一致 → `40117`；`orderItemId` 不属于该订单 → `40400`；订单已作废 → `40104`；失败路径**不退不写**（无库存 / 流水 / 累计量变化）+ `RollbackAsync`。
  - 不关联订单：`OrderId` 为空时不调用任何订单仓储方法（既有行为回归）。
  - 作废：回退累计量 + 状态重算；订单 `Closed` 时不回退状态。
- **单号生成**：`PO` / `SO` / `GR` / `GI` 四个前缀各自当天序号连续、唯一冲突重试。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：订单与出入库单 `OrderNo` / `ReceiptNo` / `ShipmentNo` `HasMaxLength` 20 == 常量；明细列长与数量 / 单价边界与既有单据同源；`expectedDate ≥ orderDate` 边界。

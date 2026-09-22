---
created: 2026-09-13
updated: 2026-09-22
---

# 设计规格：销售出库（erp-sale）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `user-management` 为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> **本规格继承 erp-purchase design §0「单据域共用约定」**：`SalesShipments` / `SalesShipmentItems` 与 `PurchaseReceipts` / `PurchaseReceiptItems` 结构完全同构，实现时照抄 erp-purchase 模板并按 §1 替换规则替换差异点；共用枚举（`OrderStatus`）、常量（`OrderFieldConstraints` + `ProductFieldConstraints` 的 quantity / unitPrice 边界）、单号生成（`GenerateOrderNoAsync`，前缀参数化）、校验结构**均不重复定义**。本规格只定义销售特有差异。
> **演进（`023-erp-settlement` / `024-erp-order-flow`）**：本规格单据域已被两者改写（结算金额化 + 已核销禁作废 + 核销取数只查主表；表 / 路由 / 单号前缀重命名 + 可关联销售订单 + 列表数量合计）。**本正文已按现行为准**，决策与判据分别见 `specs/023-erp-settlement/design.md` §0 / §3.1.1 / §3.6 与 `specs/024-erp-order-flow/design.md` §3 / §4。
> **演进（erp-rbac）**：本域动作接入权限校验，权限点 `sales.view` / `create` / `void` / `export`（`export` 由 `027` 的导出动作标注）；菜单可见性与列表页操作按钮（新增 / 作废 / 导出）由前端按权限过滤。清单唯一来源见 `specs/028-erp-rbac/design.md` §0.2。
> **演进（erp-audit-log）**：本域销售出库（创建 / 作废）的写操作已接入操作日志（`specs/029-erp-audit-log/design.md` §0.1）。

## 1. 相对 erp-purchase 的替换规则

> 完整同构项（实体字段 / 表结构 / 仓储方法签名 / 校验规则 / 页面结构 / loading 约定）见 erp-purchase design 对应章节，以下仅列**差异点**：

| 项 | 采购（erp-purchase） | 销售（本规格） |
|---|---|---|
| 表 / 实体 | `PurchaseReceipts` / `PurchaseReceipt`、`PurchaseReceiptItems` / `PurchaseReceiptItem` | `SalesShipments` / `SalesShipment`、`SalesShipmentItems` / `SalesShipmentItem` |
| 单号前缀 | `GR` | `GI` |
| 往来方向 | 供应商：`Type in (Supplier, Both)` 且启用 | 客户：`Type in (Customer, Both)` 且启用 |
| 单价带出 | 商品采购价 `PurchasePrice` | 商品销售价 `SalePrice` |
| 库存操作（保存） | 每行 `IncrementAsync(+quantity)` | 每行 `TryDecrementAsync(quantity)`，失败 `40103` 回滚（见 §3.4） |
| 库存操作（作废） | 每行 `IncrementAsync(-quantity)`（回冲，允许冲负） | 每行 `IncrementAsync(+quantity)`（回冲） |
| 结算语义 | `SettledAmount` + 推导状态，由收付款单核销累加 / 作废回退 | 同左（核销方向为**收款**，即我们收客户钱） |
| 新增错误码 | 40104 / 40108 / 40109 / 40110（erp-purchase 定义） | **40103**（本规格新增，见 §3.2） |
| 用例目录 | `Features/PurchaseReceipts/<Action>` | `Features/SalesShipments/<Action>` |
| 接口路由 | `/api/purchase-receipts` | `/api/sales-shipments` |
| 前端目录 | `views/PurchaseManagement/` | `views/SalesManagement/` |
| 前端特化 | — | 开单页明细行**数量 > 库存行内标红预警**（见 §4.4） |

## 2. 总体设计

```
销售出库单（前端 /sales 列表 + /sales/new 开单页 + /sales/detail/:id 详情）
  → SalesShipmentsController
    → App.Core/Features/SalesShipments/<Action>/*RequestHandler
      → ISalesShipmentRepository（单据 + 明细）+ IProductRepository / IPartnerRepository（校验）
        + IInventoryRepository.TryDecrementAsync（原子条件扣减）+ IUnitOfWork（同一事务）
        → PostgreSQL（SalesShipments / SalesShipmentItems / Inventory）
```

核心原则（同 erp-purchase，仅库存方向不同）：

- **单据一步式**：保存即生效（库存立即减少 + 应收口径产生）；不支持编辑，只支持**作废回冲**。
- **禁止负库存**：保存时对每行调用 `TryDecrementAsync`（数据库条件更新 `WHERE Quantity >= amount`，行锁内原子完成判断 + 扣减，无竞态窗口，并发下无需显式行锁），任一行返回 `false` → 抛 `40103` 并回滚整单。
- **明细单价快照** / **金额后端重算** / **单号生成**：同 erp-purchase（`GI` 前缀）。
- **销售扣库存顺序**：事务内**先扣库存、成功后再插单 + 明细**（避免「单已落库但库存扣失败」；任一环节失败整事务回滚，数据一致）。
- **订单可选关联**：`orderId` 为空即既有「直接出库」路径；非空时按 `024` §3 校验归属并回写订单明细累计已发数量 + 推导订单状态（同一事务）。

## 3. 后端设计

### 3.1 数据模型

- `SalesShipment` / `SalesShipmentItem` 实体字段与列类型、约束**完全同 erp-purchase design §2.1 / §2.2**（`ShipmentNo` 唯一索引、`PartnerName` 为客户快照、`OrderDate`、明细快照字段、可空 `OrderId` / `OrderNo` / `OrderItemId`）。
- 实体文件：`App.Core/Entities/SalesShipment.cs`、`SalesShipmentItem.cs`；枚举 `OrderStatus` **复用**（`OrderSettlementStatus` 已随 `023` 删除，结算状态由 `SettlementState` 推导）；`OrderFlowStatus` 由 `024` 定义。
- 实体配置：`Persistence/Configurations/SalesShipmentConfiguration.cs`、`SalesShipmentItemConfiguration.cs`（照抄 Purchase 配置，改实体类型）。
- `AppDbContext` 新增 2 个 `DbSet`：`SalesShipments`、`SalesShipmentItems`。
- 迁移链：`AddErpSale`（建表）→ `AddErpRenameReceiptsShipments`（`024`：表 / 列重命名 + 历史单号前缀改写）→ `AddErpOrders`（`024`：新增 `OrderId` / `OrderNo` / `OrderItemId`）→ `AddErpSettlement`（`023`：`SettlementStatus` → `SettledAmount` 并回填）。
- 字段约束：`OrderFieldConstraints`（erp-purchase 已定义）+ `ProductFieldConstraints.Quantity*/Price*`（erp-product 已定义），**均不新建常量**。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40103 | `InsufficientStock` | 库存不足（message 含商品名称，如「库存不足：商品 X（当前 5，需要 10）」） |

> `40104 / 40107 / 40108 / 40109 / 40110` / `40400` / `40000` 复用（前四码由 erp-purchase / erp-product 已定义）；`40120`（`023` 定义，本规格作废校验消费）；订单关联校验码 `40115` / `40116` / `40117`（`024` 定义）。

### 3.3 仓储接口（`App.Core/Abstractions/`）

`ISalesShipmentRepository`：**方法签名与 `IPurchaseReceiptRepository` 完全同构**（`GetPagedAsync`（返回 `(SalesShipment Order, int TotalQuantity)`）/ `GetDetailAsync`（含 `bool includeItems = true`）/ `AddAsync` / `AddSettledAmountAsync` / `UpdateStatusAsync` / `GenerateOrderNoAsync` / `GetItemsByOrderIdsAsync`），仅实体类型为 `SalesShipment` / `SalesShipmentItem`、单号前缀 `GI`（前缀参数化，照抄 Purchase 实现）。

- 单一仓储写由仓储自身 `SaveChangesAsync` 保证；**跨仓储写**（库存 N 行扣减 + 单据主表 + 明细 [+ 订单累计量回写]）用 `IUnitOfWork` 同一事务（后端规则 §4.4）。
- 审计字段统一由 Handler 经 `ICurrentUser` 获取后随实体 / 方法参数（`operatorId`）传入，仓储不感知当前用户。

### 3.4 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/sales-shipments` | GET | `SalesShipments/GetSalesShipments` | `PagedResult<SalesShipmentListItemDto>` | 40000 |
| `/api/sales-shipments` | POST | `SalesShipments/CreateSalesShipment` | `SalesShipmentDetailDto` | 40000 / 40103 / 40107 / 40108 / 40109 / 40110 / 40115 / 40116 / 40117 / 40400 |
| `/api/sales-shipments/{id:guid}` | GET | `SalesShipments/GetSalesShipmentById` | `SalesShipmentDetailDto` | 40400 |
| `/api/sales-shipments/{id:guid}/void` | PUT | `SalesShipments/VoidSalesShipment` | `SalesShipmentDetailDto` | 40104 / 40120 / 40400 |
| `/api/sales-shipments/{id:guid}/pick-orders` | GET | `SalesShipments/GetSalesOrderPicks` | `PagedResult<SalesOrderPickDto>` | 40000 |
| `/api/sales-shipments/{id:guid}/order-lines` | GET | `SalesShipments/GetSalesOrderLines` | `IReadOnlyList<SalesOrderLineDto>` | 40400 |

> 手工结算端点 `PUT .../settlement`（`UpdateSalesOrderSettlement`）已随 `023` **移除**。固定段路由（`pick-orders` / `order-lines`）置于 `{id:guid}` 之前注册。

### 3.5 关键用例流程（Handler）

**CreateSalesShipment**：
1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取客户：不存在 → `40400`；`Status=Disabled` → `40108`；`Type` 不含 Customer（纯供应商）→ `40109`。
3. 逐行取商品：不存在 → `40400`；`Status=Disabled` → `40107`；后端重算 `Subtotal = Quantity * UnitPrice`、`TotalAmount = Σ Subtotal`（不信任前端小计 / 总额）。
4. `orderId` 非空时按 `024` §3.1 校验关联（`40400` / `40104` / `40115` / `40116` / `40117`）。
5. `IUnitOfWork`：`BeginTransactionAsync` → **逐行 `TryDecrementAsync(productId, quantity)`**，任一 `false` → `RollbackAsync` + 抛 `40103`（message 含**首个**不足商品名）→ 全部成功 → `GenerateOrderNoAsync("GI", orderDate)` → 插单 + 明细 → `orderId` 非空时逐行 `AddFulfilledQuantityAsync(+q)` 并推导订单状态（`Closed` 不回退）→ `CommitAsync`。

**VoidSalesShipment**：
1. `GetDetailAsync` 取单：不存在 → `40400`；`Status=Voided` → `40104`；`SettledAmount > 0` → `40120`（**不开事务、不动库存与流水**，须先作废对应收款单，`023` §3.6）。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐行 `IncrementAsync(productId, +quantity)`（回冲）→ `orderId` 非空时 `AddFulfilledQuantityAsync(-q)` 并状态重算 → `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**GetSalesShipments / GetSalesShipmentById**：同 erp-purchase 对应 Handler（筛选 / 映射 / 快照透传；含 `totalQuantity`、`settledAmount` / `unsettledAmount` / `settlementState`）。

### 3.6 校验规则（FluentValidation，引用同 erp-purchase 的常量类，不新建）

| 请求 | 规则 |
|---|---|
| `CreateSalesShipmentRequest` | `partnerId`（客户）必填；`orderDate` 必填（`DateTimeOffset`）；`items` 必填非空、1–100 行（`ItemsMaxCount`）；每行 `productId` 必填、`quantity` 1–999999（`QuantityMinValue/MaxValue`）、`unitPrice` 0–9999999.99（`PriceMinValue/MaxValue`）；`remark` ≤200；`orderId` / 明细 `orderItemId` 可空 |
| `GetSalesShipmentsRequest` | `keyword` ≤ 20（`OrderFieldConstraints.KeywordMaxLength`）；`partnerId` / `orderId` 可空；`settlementState` ∈ {0,1,2}；闭区间 `start <= end`；分页取值按 `AGENTS.md` §4.3（不重复列出） |

- 存在性 / 唯一性 / 类型匹配 / 库存等业务约束一律在 Handler 判断（后端规则 §4.1）。

### 3.7 Swagger

- **不分组**（唯一来源见 `specs/003-api-swagger/design.md`）：新增接口按现有方式出现在单文档 Swagger 中；已移除的结算端点同步消失。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── sale.ts                 # 销售出库单接口层
└── views/
    └── SalesManagement/
        ├── SalesView.vue             # 销售出库单列表页
        ├── SaleFormPage.vue          # 开单独立页（/sales/new）
        └── SaleDetailView.vue        # 详情页（/sales/detail/:id，含作废 + 收付款明细）
```

- 页面结构与 `PurchaseManagement/` 完全同构（照抄模板，客户 / 销售价 / 库存预警三处差异），形态选择、独立页面理由同 erp-purchase design §4.1。

### 4.2 接口层

- `src/api/sale.ts`：TS 类型与后端 DTO（camelCase）一一对应；封装 / 金额 / 日期范围约定同 erp-purchase design §4.2（开单提交不传小计 / 总额；日期范围转本地边界 UTC ISO）。
- 提交 payload：`partnerId` / `orderDate` / `orderId` / `items[].productId/orderItemId/quantity/unitPrice` / `remark`；列表行类型含 `totalQuantity` 与 `settledAmount` / `unsettledAmount` / `settlementState`。
- 客户下拉数据源：`src/api/partner.ts` 的 `getPartners`（`status=1`，取 `Type in (2,3)`）；商品下拉数据源：`src/api/product.ts` 的 `getProductPickList`（含当前库存，供预警标红）；关联订单数据源：`pick-orders` / `order-lines` 两个只读端点。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `sales` | `sales` | `SalesView` |
| `sales/new` | `saleNew` | `SaleFormPage` |
| `sales/detail/:id` | `saleDetail` | `SaleDetailView` |

`AppLayout.vue` 侧边菜单追加子项「销售出库」`sales`；`MENU_ROUTE_MAP` 增加 `saleDetail: 'sales'`（详情页高亮归属父菜单）。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2。

### 4.4 页面交互（与 erp-purchase 同构处省略，仅列差异）

**开单页 `SaleFormPage.vue`**：同 `PurchaseFormPage.vue` 结构，差异：
- 表头：**客户**下拉（仅启用 + `Type in (2,3)`）；关联订单下拉同样接入（仅 `Pending` / `Partial` 的销售订单，按客户过滤，可清空 = 直接出库）。
- 明细区：单价选商品后默认带出**销售价**；商品下拉显示「编码 名称（库存 x）」；**数量 > 库存时该行数量输入框标红**（`status="error"` 或红色文字提示「库存不足，当前库存 x」，前端预警，最终以后端 `40103` 为准）。

**列表 `SalesView.vue` / 详情 `SaleDetailView.vue`**：同采购对应页，差异：往来列 / 筛选为**客户**；结算状态标签文案与颜色统一取 `specs/023-erp-settlement/design.md` §0（未结算 / 部分结算（未结 x）/ 已结算）；操作列的「收付款 / 去收付款」与「作废」显隐判据见 `src/utils/settlement.ts`（`023` §4.4）；详情含「收付款明细」只读区块。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 开单页订单明细带出 | `orderLinesLoading` | 明细区 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 详情「收付款明细」 | `loading` | 区块内容区 |

## 5. 关键技术决策与取舍

> 单据不可编辑只可作废、明细快照、金额后端重算、单号生成、开单独立页面、无 RBAC、时间处理等决策**同 erp-purchase design §5**；结算金额化 / 已核销禁作废取舍见 `023` §5，订单关联取舍见 `024` §5。此处仅列销售特有决策：

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 防超卖 | `TryDecrementAsync` 条件更新（`WHERE Quantity >= amount`） | 数据库行锁内原子完成判断 + 扣减，无竞态窗口；两请求并发扣减合计超库存时最多一个成功；不引入显式行锁 / 乐观版本号 |
| 销售扣库存顺序 | 事务内先扣库存、成功后再插单 + 明细 | 避免「单已落库但库存扣失败」；任一环节失败整事务回滚，数据一致 |
| 整体拒绝而非部分保存 | 任一行不足 → 整单拒绝，报首个不足商品 | 部分保存会留下半成品单据与不一致库存；整单拒绝 + 回滚最简单可靠 |
| 前端库存预警仅提示不拦截 | 数量 > 库存行内标红，仍可提交（由后端 `40103` 兜底） | 前端库存是开单时快照，提交时可能已变化；以数据库条件更新为唯一事实源，前端标红只提升体验 |
| 作废回冲直接加回 | `IncrementAsync(+quantity)` 无前置校验 | 作废必须可执行（与采购回冲对称）；库存行 1:1 永存在 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同 `user-management` 测试约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（同用户模块）。

- **CreateSalesShipment**：
  - 成功：断言每行 `TryDecrementAsync` **先于**单据插入、单号前缀 `GI` + orderDate、明细快照（`ProductName` / `Unit`）、`Subtotal` / `TotalAmount` 后端重算（前端传小计被忽略）、`IUnitOfWork.Commit`。
  - 库存不足：任一行 `TryDecrementAsync` 返回 `false` → `40103`，message 含该商品名，`RollbackAsync` 被调用、`AddAsync` 未被调用（单据未插入）。
  - 客户 / 商品校验同采购（客户不存在 `40400` / 停用 `40108` / 纯供应商 `40109` / 商品停用 `40107` / 商品不存在 `40400` / 明细空 `40110`）。
  - 关联订单与取数范围用例见 `024` §3.6 与 `023` §6（本规格只做回归，不重复定义）。
- **VoidSalesShipment**：成功（断言每行 `IncrementAsync(+quantity)` 回冲 + `UpdateStatusAsync(Voided)`；关联订单时回退累计量与状态重算，`Closed` 不回退）；已作废 → `40104`；不存在 → `40400`；已核销（`SettledAmount > 0`）→ `40120` 且未开事务 / 未回冲 / 不写流水 / 状态不变。
- **GetSalesShipments / GetSalesShipmentById**：同 erp-purchase 对应用例（筛选传参（含 `orderId` / `settlementState`）/ 明细映射 / `totalQuantity` 聚合数量透传 / 结算金额与状态推导）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：销售 Validator 与采购 Validator 同字段边界一致（quantity 999999 / unitPrice 10000000 / items 101 / keyword 21 均拒绝）；EF `SalesShipments.ShipmentNo` `HasMaxLength` == `OrderFieldConstraints.OrderNoMaxLength`。

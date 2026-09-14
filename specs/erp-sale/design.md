# 设计规格：销售出库（erp-sale）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.5、测试 §6）与后端 / 前端专项规则。
> 按后端规则第 3 节「每 API 一个用例」组织，以 `user-management` 为结构参照；字段约束单一来源（后端规则 §4.3）同样适用。
> **本规格继承 erp-purchase design §0「单据域共用约定」**：`SalesOrders` / `SalesOrderItems` 与 `PurchaseOrders` / `PurchaseOrderItems` 结构完全同构，实现时照抄 erp-purchase 模板并按 §1 替换规则做三处差异替换；共用枚举（`OrderStatus` / `OrderSettlementStatus`）、常量（`OrderFieldConstraints` + `ProductFieldConstraints` 的 quantity / unitPrice 边界）、单号生成（`GenerateOrderNoAsync`，前缀参数化）、校验结构**均不重复定义**。本规格只定义销售特有差异。

## 1. 相对 erp-purchase 的替换规则

> 完整同构项（实体字段 / 表结构 / 仓储方法签名 / 校验规则 / 页面结构 / loading 约定）见 erp-purchase design 对应章节，以下仅列**差异点**：

| 项 | 采购（erp-purchase） | 销售（本规格） |
|---|---|---|
| 表 / 实体 | `PurchaseOrders` / `PurchaseOrder`、`PurchaseOrderItems` / `PurchaseOrderItem` | `SalesOrders` / `SalesOrder`、`SalesOrderItems` / `SalesOrderItem` |
| 单号前缀 | `PO` | `SO` |
| 往来方向 | 供应商：`Type in (Supplier, Both)` 且启用 | 客户：`Type in (Customer, Both)` 且启用 |
| 单价带出 | 商品采购价 `PurchasePrice` | 商品销售价 `SalePrice` |
| 库存操作（保存） | 每行 `IncrementAsync(+quantity)` | 每行 `TryDecrementAsync(quantity)`，失败 `40103` 回滚（见 §3.4） |
| 库存操作（作废） | 每行 `IncrementAsync(-quantity)`（回冲，允许冲负） | 每行 `IncrementAsync(+quantity)`（回冲） |
| 结算语义 | 0=未付 1=已付 | 0=未收 1=已收（同一枚举 `OrderSettlementStatus`，仅前端文案不同） |
| 新增错误码 | 40104 / 40108 / 40109 / 40110（已定义） | **40103**（本规格新增，见 §3.2） |
| 用例目录 | `Features/Purchases/<Action>` | `Features/Sales/<Action>` |
| 接口路由 | `/api/purchase-orders` | `/api/sales-orders` |
| 前端目录 | `views/PurchaseManagement/` | `views/SalesManagement/` |
| 前端特化 | — | 开单页明细行**数量 > 库存行内标红预警**（见 §4.4） |

## 2. 总体设计

```
销售单（前端 /sales 列表 + /sales/new 开单页 + /sales/detail/:id 详情）
  → SalesOrdersController
    → App.Core/Features/Sales/<Action>/*RequestHandler
      → ISalesOrderRepository（单据 + 明细）+ IProductRepository / IPartnerRepository（校验）
        + IInventoryRepository.TryDecrementAsync（原子条件扣减）+ IUnitOfWork（同一事务）
        → PostgreSQL（SalesOrders / SalesOrderItems / Inventory）
```

核心原则（同 erp-purchase，仅库存方向不同）：

- **单据一步式**：保存即生效（库存立即减少 + 应收口径产生）；不支持编辑，只支持**作废回冲**。
- **禁止负库存**：保存时对每行调用 `TryDecrementAsync`（数据库条件更新 `WHERE Quantity >= amount`，行锁内原子完成判断 + 扣减，无竞态窗口，并发下无需显式行锁），任一行返回 `false` → 抛 `40103` 并回滚整单。
- **明细单价快照** / **金额后端重算** / **单号生成**：同 erp-purchase（`SO` 前缀）。
- **销售扣库存顺序**：事务内**先扣库存、成功后再插单 + 明细**（避免「单已落库但库存扣失败」；任一环节失败整事务回滚，数据一致）。

## 3. 后端设计

### 3.1 数据模型

- `SalesOrder` / `SalesOrderItem` 实体字段与列类型、约束**完全同 erp-purchase design §2.1 / §2.2**（`OrderNo` 唯一索引、`PartnerName` 为客户快照、明细快照字段、`OrderId` 索引）。
- 实体文件：`App.Core/Entities/SalesOrder.cs`、`SalesOrderItem.cs`；枚举 `OrderStatus` / `OrderSettlementStatus` **复用**（不新建）。
- 实体配置：`Persistence/Configurations/SalesOrderConfiguration.cs`、`SalesOrderItemConfiguration.cs`（照抄 Purchase 配置，改实体类型）。
- `AppDbContext` 新增 2 个 `DbSet`：`SalesOrders`、`SalesOrderItems`。
- 迁移：`dotnet ef migrations add AddErpSale -p src/App.Infrastructure -s src/App.Api`（增量迁移）。
- 字段约束：`OrderFieldConstraints`（erp-purchase 已定义）+ `ProductFieldConstraints.Quantity*/Price*`（erp-product 已定义），**均不新建常量**。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40103 | `InsufficientStock` | 库存不足（message 含商品名称，如「库存不足：商品 X（当前 5，需要 10）」） |

> `40104 / 40107 / 40108 / 40109 / 40110` / `40400` / `40000` 复用（前四码由 erp-purchase / erp-product 已定义）。

### 3.3 仓储接口（新增，`App.Core/Abstractions/`）

`ISalesOrderRepository`：**方法签名与 `IPurchaseOrderRepository` 完全同构**（`GetPagedAsync` / `GetDetailAsync` / `AddAsync` / `UpdateSettlementAsync` / `UpdateStatusAsync` / `GenerateOrderNoAsync`），仅实体类型为 `SalesOrder` / `SalesOrderItem`、默认单号前缀 `SO`（前缀参数化，照抄 Purchase 实现）。
- 单一仓储写由仓储自身 `SaveChangesAsync` 保证；**跨仓储写**（库存 N 行扣减 + 单据主表 + 明细）用 `IUnitOfWork` 同一事务（后端规则 §3）。
- 仓储构造函数注入 `ICurrentUser` 填充审计字段。

### 3.4 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/sales-orders` | GET | `Sales/GetSalesOrders` | `PagedResult<SalesOrderListItemDto>` | 40000 |
| `/api/sales-orders` | POST | `Sales/CreateSalesOrder` | `SalesOrderDetailDto` | 40000 / 40103 / 40107 / 40108 / 40109 / 40110 / 40400 |
| `/api/sales-orders/{id:guid}` | GET | `Sales/GetSalesOrderById` | `SalesOrderDetailDto` | 40400 |
| `/api/sales-orders/{id:guid}/void` | PUT | `Sales/VoidSalesOrder` | `SalesOrderDetailDto` | 40104 / 40400 |
| `/api/sales-orders/{id:guid}/settlement` | PUT | `Sales/UpdateSalesOrderSettlement` | `SalesOrderDetailDto` | 40000 / 40104 / 40400 |

### 3.5 关键用例流程（Handler）

**CreateSalesOrder**：
1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取客户：不存在 → `40400`；`Status=Disabled` → `40108`；`Type` 不含 Customer（纯供应商）→ `40109`。
3. 逐行取商品：不存在 → `40400`；`Status=Disabled` → `40107`；后端重算 `Subtotal = Quantity * UnitPrice`、`TotalAmount = Σ Subtotal`（不信任前端小计 / 总额）。
4. `IUnitOfWork`：`BeginTransactionAsync` → **逐行 `TryDecrementAsync(productId, quantity)`**，任一 `false` → `RollbackAsync` + 抛 `40103`（message 含**首个**不足商品名）→ 全部成功 → `GenerateOrderNoAsync("SO", orderDate)` → 插单 + 明细 → `CommitAsync`。

**VoidSalesOrder**：
1. `GetDetailAsync` 取单：不存在 → `40400`；`Status=Voided` → `40104`。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐行 `IncrementAsync(productId, +quantity)`（回冲）→ `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**UpdateSalesOrderSettlement**：取单 → 不存在 `40400`；`Status=Voided` → `40104`；`UpdateSettlementAsync` + 审计。

**GetSalesOrders / GetSalesOrderById**：同 erp-purchase 对应 Handler（筛选 / 映射 / 快照透传）。

### 3.6 校验规则（FluentValidation，引用同 erp-purchase 的常量类，不新建）

| 请求 | 规则 |
|---|---|
| `CreateSalesOrderRequest` | `customerId` 必填；`orderDate` 必填（`DateTimeOffset`）；`items` 必填非空、1–100 行（`ItemsMaxCount`）；每行 `productId` 必填、`quantity` 1–999999（`QuantityMinValue/MaxValue`）、`unitPrice` 0–9999999.99（`PriceMinValue/MaxValue`）；`remark` ≤200 |
| `GetSalesOrdersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20（`OrderFieldConstraints.KeywordMaxLength`）；`customerId` / `settlement` 可空或合法值；`start` / `end` 可空，闭区间 `start <= end` |
| `UpdateSalesOrderSettlementRequest` | `settlementStatus` ∈ {0, 1} |

- 存在性 / 唯一性 / 类型匹配 / 库存等业务约束一律在 Handler 判断（后端规则 §3）。

### 3.7 Swagger

- **不分组**（用户已确认）：维持现有单文档 Swagger，本规格 5 个新增接口按现有方式正常出现在文档中，不使用 `ApiExplorerSettings.Group`。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── sale.ts                 # 销售单接口层
└── views/
    └── SalesManagement/
        ├── SalesView.vue             # 销售单列表页
        ├── SaleFormPage.vue          # 开单独立页（/sales/new）
        └── SaleDetailView.vue        # 详情页（/sales/detail/:id，含作废 + 结算操作）
```

- 页面结构与 `PurchaseManagement/` 完全同构（照抄模板，客户 / 销售价 / 库存预警三处差异），形态选择、独立页面理由同 erp-purchase design §4.1。

### 4.2 接口层

- `src/api/sale.ts`：TS 类型与后端 DTO（camelCase）一一对应；封装 / 金额 / 日期范围约定同 erp-purchase design §4.2（开单提交不传小计 / 总额；日期范围转本地边界 UTC ISO）。
- 客户下拉数据源：`src/api/partner.ts` 的 `getPartners`（`status=1`，取 `Type in (2,3)`）；商品下拉数据源：`src/api/product.ts` 的 `getProductPickList`（含当前库存，供预警标红）。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `sales` | `sales` | `SalesView` |
| `sales/new` | `saleNew` | `SaleFormPage` |
| `sales/detail/:id` | `saleDetail` | `SaleDetailView` |

`AppLayout.vue` 侧边菜单「进销存」分组追加子项「销售开单」`sales`；`MENU_ROUTE_MAP` 增加 `saleDetail: 'sales'`（详情页高亮归属父菜单）。

### 4.4 页面交互（与 erp-purchase 同构处省略，仅列差异）

**开单页 `SaleFormPage.vue`**：同 `PurchaseFormPage.vue` 结构，差异：
- 表头：**客户**下拉（仅启用 + `Type in (2,3)`）。
- 明细区：单价选商品后默认带出**销售价**；商品下拉显示「编码 名称（库存 x）」；**数量 > 库存时该行数量输入框标红**（`status="error"` 或红色文字提示「库存不足，当前库存 x」，前端预警，最终以后端 `40103` 为准）。

**列表 `SalesView.vue` / 详情 `SaleDetailView.vue`**：同采购对应页，差异：往来列 / 筛选为**客户**；结算文案「未收 / 已收」「标记为已收?」。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 结算切换（列表 / 详情） | `settlingId` | popconfirm 确认按钮 |

## 5. 关键技术决策与取舍

> 单据不可编辑只可作废、明细快照、金额后端重算、单号生成、开单独立页面、结算状态位、无 RBAC、时间处理等决策**同 erp-purchase design §5**（共用约定），此处仅列销售特有决策：

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 防超卖 | `TryDecrementAsync` 条件更新（`WHERE Quantity >= amount`） | 数据库行锁内原子完成判断 + 扣减，无竞态窗口；两请求并发扣减合计超库存时最多一个成功；不引入显式行锁 / 乐观版本号 |
| 销售扣库存顺序 | 事务内先扣库存、成功后再插单 + 明细 | 避免「单已落库但库存扣失败」；任一环节失败整事务回滚，数据一致 |
| 整体拒绝而非部分保存 | 任一行不足 → 整单拒绝，报首个不足商品 | 部分保存会留下半成品单据与不一致库存；整单拒绝 + 回滚最简单可靠 |
| 前端库存预警仅提示不拦截 | 数量 > 库存行内标红，仍可提交（由后端 `40103` 兜底） | 前端库存是开单时快照，提交时可能已变化；以数据库条件更新为唯一事实源，前端标红只提升体验 |
| 作废回冲直接加回 | `IncrementAsync(+quantity)` 无前置校验 | 作废必须可执行（与采购回冲对称）；库存行 1:1 永存在 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同 `user-management` 测试约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（同用户模块）。

- **CreateSalesOrder**：
  - 成功：断言每行 `TryDecrementAsync` **先于**单据插入、单号前缀 `SO` + orderDate、明细快照（`ProductName` / `Unit`）、`Subtotal` / `TotalAmount` 后端重算（前端传小计被忽略）、`IUnitOfWork.Commit`。
  - 库存不足：任一行 `TryDecrementAsync` 返回 `false` → `40103`，message 含该商品名，`RollbackAsync` 被调用、`AddAsync` 未被调用（单据未插入）。
  - 客户 / 商品校验同采购（客户不存在 `40400` / 停用 `40108` / 纯供应商 `40109` / 商品停用 `40107` / 商品不存在 `40400` / 明细空 `40110`）。
- **VoidSalesOrder**：成功（断言每行 `IncrementAsync(+quantity)` 回冲 + `UpdateStatusAsync(Voided)`）；已作废 → `40104`；不存在 → `40400`。
- **UpdateSalesOrderSettlement / GetSalesOrderById / GetSalesOrders**：同 erp-purchase 对应用例（成功 / 已作废 `40104` / 不存在 `40400` / 筛选传参 / 明细映射）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：销售 Validator 与采购 Validator 同字段边界一致（quantity 999999 / unitPrice 10000000 / items 101 / keyword 21 均拒绝）；EF `SalesOrders.OrderNo` `HasMaxLength` == `OrderFieldConstraints.OrderNoMaxLength`。

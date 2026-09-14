# 设计规格：采购入库（erp-purchase）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则第 3 节「每 API 一个用例」组织，以 `user-management` 为结构参照；字段约束单一来源（后端规则 §4.3）同样适用。
> 本规格为进销存功能组**单据域首个规格**，其数据模型 / 仓储 / 单号 / 校验约定为采购 / 销售共用，erp-sale 照抄本规格模板实现（见 §0 替换规则）。

## 0. 单据域共用约定（erp-sale 继承）

采购 / 销售单据**结构同构**，以下约定两者共用，erp-sale 实现时按「替换规则」表替换三处差异，其余逐条一致：

| 项 | 采购（本规格） | 销售（erp-sale 替换） |
|---|---|---|
| 表 / 实体 | `PurchaseOrders` / `PurchaseOrder`、`PurchaseOrderItems` / `PurchaseOrderItem` | `SalesOrders` / `SalesOrder`、`SalesOrderItems` / `SalesOrderItem` |
| 单号前缀 | `PO` | `SO` |
| 往来方向 | 供应商：`Type in (Supplier, Both)` 且启用 | 客户：`Type in (Customer, Both)` 且启用 |
| 单价带出 | 商品采购价 `PurchasePrice` | 商品销售价 `SalePrice` |
| 库存操作（保存） | 每行 `IncrementAsync(+quantity)` | 每行 `TryDecrementAsync(quantity)`，失败 `40103` 回滚（erp-sale 定义） |
| 库存操作（作废） | 每行 `IncrementAsync(-quantity)`（回冲，允许冲负） | 每行 `IncrementAsync(+quantity)`（回冲） |
| 结算语义 | 0=未付 1=已付 | 0=未收 1=已收（同一枚举 `OrderSettlementStatus`） |
| 共用错误码 | 40104 / 40108 / 40109 / 40110（本规格定义，见 §3.2） | 同左 + 40103（erp-sale 定义） |
| 用例目录 | `Features/Purchases/<Action>` | `Features/Sales/<Action>` |
| 接口路由 | `/api/purchase-orders` | `/api/sales-orders` |

## 1. 总体设计

```
采购单（前端 /purchases 列表 + /purchases/new 开单页 + /purchases/detail/:id 详情）
  → PurchaseOrdersController
    → App.Core/Features/Purchases/<Action>/*RequestHandler
      → IPurchaseOrderRepository（单据 + 明细）+ IProductRepository / IPartnerRepository（校验）
        + IInventoryRepository.IncrementAsync（原子增）+ IUnitOfWork（同一事务）
        → PostgreSQL（PurchaseOrders / PurchaseOrderItems / Inventory）
```

核心原则：

- **单据一步式**：保存即生效（库存立即增加 + 应付口径产生）；不支持编辑，只支持**作废回冲**。
- **明细单价快照**：明细行保存 `ProductName` / `Unit` / `UnitPrice`（默认带出商品当时采购价、可修改），后续改商品档案不影响历史单据。
- **金额后端重算**：小计 / 总额均为 `decimal`（PG `numeric(18,2)`）；后端按 `数量 × 单价` 重算，**不信任前端传值**，前端仅做展示。
- **库存 1:1 台账**：消费 erp-product 的 `Inventory` 与 `IncrementAsync`；本规格保存（+）与作废回冲（-）都走 `IncrementAsync`，回冲允许冲负（§5 决策）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset`（实体 / DTO / 仓储签名 / 请求入参），Npgsql 映射 `timestamptz`（后端规则 §4.2）。
> 状态枚举统一小整数，PG `smallint`。

### 2.1 实体 `App.Core/Entities/PurchaseOrder.cs` 与表 `PurchaseOrders`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单号 `PO + yyyyMMdd + 4 位序号`（如 `PO202609110001`），后端生成 |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 供应商 |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 供应商名称**快照**（列表 / 审计免 join，同登录日志快照原则） |
| `OrderDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 业务日期（UTC 午夜） |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 总金额 = Σ 小计（后端计算） |
| `SettlementStatus` | `OrderSettlementStatus` | `smallint` | NOT NULL，默认 `0` | 0=未付 1=已付 |
| `Status` | `OrderStatus` | `smallint` | NOT NULL，默认 `1` | 1=正常 0=已作废 |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | |

### 2.2 实体 `App.Core/Entities/PurchaseOrderItem.cs` 与表 `PurchaseOrderItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderId` | `Guid` | `uuid` | NOT NULL，FK → `PurchaseOrders(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductName` | `string` | `varchar(50)` | NOT NULL | 商品名称**快照** |
| `Unit` | `string` | `varchar(10)` | NOT NULL | 单位**快照** |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | 数量 |
| `UnitPrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 单价**快照** |
| `Subtotal` | `decimal` | `numeric(18,2)` | NOT NULL | 小计 = 数量 × 单价（后端计算） |

- 枚举（采购 / 销售共用，放 `App.Core/Entities/`）：`OrderStatus { Voided = 0, Normal = 1 }`、`OrderSettlementStatus { Unsettled = 0, Settled = 1 }`（语义：采购=未付 / 已付，销售=未收 / 已收，由 erp-sale 继承）。
- 明细不软删除：作废时保留明细（审计需要），仅主表 `Status` 置 0。
- 实体配置：`Persistence/Configurations/PurchaseOrderConfiguration.cs`、`PurchaseOrderItemConfiguration.cs`。

### 2.3 EF Core 与迁移

- `AppDbContext` 新增 2 个 `DbSet`：`PurchaseOrders`、`PurchaseOrderItems`。
- 外键均不级联删除（业务对象只停用 / 作废不删除）。
- 迁移：`dotnet ef migrations add AddErpPurchase -p src/App.Infrastructure -s src/App.Api`（**增量迁移**，不动既有迁移）。
- 无种子数据（业务数据全部经界面录入）。

### 2.4 字段约束单一来源（`App.Core/Entities/OrderFieldConstraints.cs`）

| 常量 | 值 |
|---|---|
| `OrderNoMaxLength` | 20 |
| `RemarkMaxLength` | 200 |
| `KeywordMaxLength` | 20（单号查询关键词，对齐 `OrderNo` 列长） |
| `ItemsMaxCount` | 100（单张单据明细行数上限） |

- 明细行的 `Quantity` / `UnitPrice` 边界引用 `ProductFieldConstraints.QuantityMinValue / QuantityMaxValue / PriceMinValue / PriceMaxValue`（erp-product 已定义，禁止复制常量，后端规则 §4.3 同一规则同源）。
- EF 实体配置与全部 `RequestValidator` 均引用上述常量，禁止硬编码。
- 一致性由单测守护（扩展 `FieldValidationConsistencyTests`）：EF 模型实际 `HasMaxLength` == 常量；Validator「边界值通过 / 越界拒绝」一致；`keyword`（单号）长度不超 `OrderNo` 列长。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`IPurchaseOrderRepository`（`ISalesOrderRepository` 结构同构，erp-sale 各一份接口与实现）：

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<OrderListItem> Items, int Total)> GetPagedAsync(string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end, OrderSettlementStatus? settlement, int page, int pageSize, ...)` | 列表（keyword 匹配单号 / 往来名称快照；含作废单据；`CreatedAt DESC`）；日期范围对 `OrderDate` 闭区间比较 |
| `Task<OrderDetail?> GetDetailAsync(Guid id, ...)` | 详情（主表 + 明细行，按明细插入顺序） |
| `Task AddAsync(PurchaseOrder, IReadOnlyList<PurchaseOrderItem>, ...)` | 新增单据 + 明细（同一仓储内 SaveChanges） |
| `Task UpdateSettlementAsync(Guid id, OrderSettlementStatus s, ...)` | 更新结算状态 |
| `Task UpdateStatusAsync(Guid id, OrderStatus s, ...)` | 更新状态（作废） |
| `Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, ...)` | 生成单号（见 §3.6） |

- 单一仓储写（主表 + 明细一次 SaveChanges）由仓储自身保证；**跨仓储写**（单据主表 + 明细 + 库存 N 行）必须用 `IUnitOfWork` 包成同一事务：`BeginTransactionAsync` → 各仓储写 → `CommitAsync`，异常 `RollbackAsync` 后重抛（后端规则 §3）。
- 仓储构造函数注入 `ICurrentUser` 填充审计字段（同 `UserRepository` 模式）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40104 | `OrderVoided` | 单据已作废，禁止再操作 |
| 40108 | `PartnerDisabled` | 往来单位已停用，不可用于开单 |
| 40109 | `PartnerTypeMismatch` | 往来单位类型与单据不匹配（如拿客户开采购单） |
| 40110 | `OrderItemsEmpty` | 单据明细不能为空 |

> `40400 NotFound` / `40000 Validation` 复用全局；`40107 ProductDisabled` 由 erp-product 定义（本规格消费）；`40103 InsufficientStock` 由 erp-sale 定义。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/purchase-orders` | GET | `Purchases/GetPurchaseOrders` | `PagedResult<PurchaseOrderListItemDto>` | 40000 |
| `/api/purchase-orders` | POST | `Purchases/CreatePurchaseOrder` | `PurchaseOrderDetailDto` | 40000 / 40107 / 40108 / 40109 / 40110 / 40400 |
| `/api/purchase-orders/{id:guid}` | GET | `Purchases/GetPurchaseOrderById` | `PurchaseOrderDetailDto` | 40400 |
| `/api/purchase-orders/{id:guid}/void` | PUT | `Purchases/VoidPurchaseOrder` | `PurchaseOrderDetailDto` | 40104 / 40400 |
| `/api/purchase-orders/{id:guid}/settlement` | PUT | `Purchases/UpdatePurchaseOrderSettlement` | `PurchaseOrderDetailDto` | 40000 / 40104 / 40400 |

### 3.4 关键用例流程（Handler）

**CreatePurchaseOrder**：
1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取供应商：不存在 → `40400`；`Status=Disabled` → `40108`；`Type` 不含 Supplier（纯客户）→ `40109`。
3. 逐行取商品：不存在 → `40400`；`Status=Disabled` → `40107`；后端重算 `Subtotal = Quantity * UnitPrice`、`TotalAmount = Σ Subtotal`（**不信任前端小计 / 总额**）。
4. `GenerateOrderNoAsync("PO", orderDate)` 生成单号；`OrderDate` 取前端传入的 UTC 午夜值。
5. `IUnitOfWork`：`BeginTransactionAsync` → 插单 + 明细 → 逐行 `IInventoryRepository.IncrementAsync(productId, +quantity)` → `CommitAsync`。

**VoidPurchaseOrder**：
1. `GetDetailAsync` 取单：不存在 → `40400`；`Status=Voided` → `40104`。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐行 `IncrementAsync(productId, -quantity)`（回冲，允许冲负）→ `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**UpdatePurchaseOrderSettlement**：`GetDetailAsync` 取单 → 不存在 `40400`；`Status=Voided` → `40104`；`UpdateSettlementAsync` + 审计。

**GetPurchaseOrders**：仓储分页筛选（keyword 匹配单号 / 往来名称快照；`OrderDate` 闭区间；`CreatedAt DESC`）→ Handler 映射 DTO（含 `Status`，供前端作废行置灰）。

**GetPurchaseOrderById**：`GetDetailAsync`（不存在 → `40400`）→ 映射表头 + 明细 DTO（快照字段原样返回）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量与 `ProductFieldConstraints`）

| 请求 | 规则 |
|---|---|
| `CreatePurchaseOrderRequest` | `partnerId` 必填；`orderDate` 必填（`DateTimeOffset`）；`items` 必填非空、1–100 行（`ItemsMaxCount`）；每行 `productId` 必填、`quantity` 1–999999（`QuantityMinValue/MaxValue`）、`unitPrice` 0–9999999.99（`PriceMinValue/MaxValue`）；`remark` ≤200 |
| `GetPurchaseOrdersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20（`OrderFieldConstraints.KeywordMaxLength`）；`partnerId` / `settlement` 可空或合法值；`start` / `end` 可空，闭区间 `start <= end` |
| `UpdatePurchaseOrderSettlementRequest` | `settlementStatus` ∈ {0, 1} |

- 存在性 / 唯一性 / 类型匹配 / 状态流转等业务约束一律在 Handler 判断（后端规则 §3）。

### 3.6 单号生成 `GenerateOrderNoAsync`（采购 / 销售共用实现）

- 格式：`<前缀><yyyyMMdd><seq4>`，前缀采购 `PO` / 销售 `SO`；日期段取 `orderDate` 的 UTC 日期。
- 序号：`SELECT COUNT(*)` 当天同前缀已有单号（`OrderNo LIKE '前缀+日期段%'`，EF 表达 `StartsWith`），`seq = count + 1`，4 位补零。
- 并发兜底：单号唯一索引，冲突时 `catch`（唯一约束异常）后重试（最多 3 次）；MVP 并发量下概率极低，重试足够。
- 单号在事务内生成、随单据落库；作废单据的单号不复用（序号只增不减）。

### 3.7 Swagger

- **不分组**（用户已确认）：维持现有单文档 Swagger，本规格 5 个新增接口按现有方式正常出现在文档中，不使用 `ApiExplorerSettings.Group`。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── purchase.ts           # 采购单接口层
└── views/
    └── PurchaseManagement/
        ├── PurchasesView.vue     # 采购单列表页
        ├── PurchaseFormPage.vue  # 开单独立页（/purchases/new）
        └── PurchaseDetailView.vue# 详情页（/purchases/detail/:id，含作废 + 结算操作）
```

- 开单为**独立页面**（多行明细子表格，符合前端规则 §5.5 形态选择：字段 > 8 含子表格 → 独立页面）；详情为独立页面（`a-page-header` + `a-descriptions` + 明细只读表格）。
- erp-sale 的 `SalesView.vue` / `SaleFormPage.vue` / `SaleDetailView.vue` 结构同构（`SalesManagement/` 目录），实现时照抄本规格模板（客户 / 销售价 / 库存预警三处差异）。

### 4.2 接口层

- `src/api/purchase.ts`：TS 类型与后端 DTO（camelCase）一一对应；函数经 `src/api/request.ts` 统一封装（解包 `data`、40100 处理）。
- 金额字段类型：`number`（后端 `numeric(18,2)` JSON 序列化为数字）；展示统一 `toFixed(2)`。
- 日期参数（列表日期范围）：`a-range-picker` 选值 → 接口层转**本地当天 00:00:00 / 23:59:59 的 UTC ISO 串**（同登录日志约定）。
- **开单提交不传小计 / 总额**：明细行本地类型 `{ productId, productName, unit, quantity, unitPrice, subtotal }`，`subtotal` 为前端实时计算（`quantity * unitPrice`）仅用于展示；提交 payload 只含 `partnerId` / `orderDate` / `items[].productId/quantity/unitPrice` / `remark`。
- 供应商下拉数据源：`src/api/partner.ts` 的 `getPartners`（`status=1` + `type` 筛选，取 `Type in (1,3)`）；商品下拉数据源：`src/api/product.ts` 的 `getProductPickList`。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `purchases` | `purchases` | `PurchasesView` |
| `purchases/new` | `purchaseNew` | `PurchaseFormPage` |
| `purchases/detail/:id` | `purchaseDetail` | `PurchaseDetailView` |

`AppLayout.vue` 侧边菜单「进销存」分组追加子项「采购入库」`purchases`；`MENU_ROUTE_MAP` 增加 `purchaseDetail: 'purchases'`（详情页高亮归属父菜单）。

### 4.4 页面交互

**采购开单页 `PurchaseFormPage.vue`**（独立页面，底部操作栏 提交 / 取消）：
- 表头：供应商下拉（仅启用 + `Type in (1,3)`，`a-select` 远程全量拉取）、单据日期 `a-date-picker`（默认当天）、备注。
- 明细区：`a-table` 可编辑行——商品下拉（`getProductPickList`，显示「编码 名称（库存 x）」）、数量 `a-input-number :min="1"`、单价 `a-input-number`（选商品后默认带出采购价，可改）、小计（computed 展示）、行删除按钮；「添加行」按钮。
- 底部操作栏：总金额（computed = Σ 小计，仅展示）+ 提交（`submitting`）/ 取消（回列表）。
- 提交成功 → `Message.success` + 跳详情页；失败 → 统一错误提示，页面停留。

**采购列表 `PurchasesView.vue`**：
- 筛选行：单号关键词 + 供应商下拉 + 日期范围 + 结算状态下拉 + 搜索 / 重置。
- 表格列：序号、单号、供应商、日期、总金额、结算状态（`a-tag`：未付橙 / 已付绿）、单据状态（正常 / 已作废 灰，**作废行整体置灰**）、创建时间、操作列（详情 / 作废（popconfirm，仅正常单显示）/ 结算切换（popconfirm 文案「标记为已付?」））。
- 服务端分页。

**详情页 `PurchaseDetailView.vue`**：
- `a-page-header`（返回）+ `a-descriptions`（单号 / 供应商 / 日期 / 总额 / 结算 / 状态 / 创建人 / 创建时间）+ 明细只读表格 + 底部操作（正常单：作废按钮（`a-popconfirm` + `voidingId` loading）、结算切换）。
- id 不存在 → `a-result status="404"` + 返回列表。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 结算切换（列表 / 详情） | `settlingId` | popconfirm 确认按钮 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 单据不可编辑，只可作废 | 无 `PUT /orders/{id}` | 一步式 + 快照设计下，改单 = 新单；作废保留完整审计轨迹，模型最简 |
| 明细快照名称 / 单位 / 单价 | 明细冗余 `ProductName` / `Unit` / `UnitPrice` | 历史单据展示不受档案后续修改影响（同登录日志快照原则） |
| 采购作废回冲允许冲负 | `IncrementAsync(delta 负值)` 不设下限 | 采购入库后商品可能已被卖光，回冲必须可执行；库存为负作为数据异常展示（库存页标红可见，由 erp-inventory-query 呈现），不做修复工具（范围外） |
| 库存增加与插单同一事务 | `IUnitOfWork` 包插单 + 明细 + N 行库存 | 杜绝「单已落库但库存未增」的中间态；任一环节失败整体回滚 |
| 单号后端生成 `前缀+日期+序号` | `GenerateOrderNoAsync`，唯一索引兜底 + 重试 | 单号可读、可审计；并发冲突概率低，3 次重试足够，不引入独立序列表 |
| 金额后端重算 | 小计 / 总额一律 Handler 按 `数量 × 单价` 计算 | 前端传值仅作展示参考，杜绝篡改与精度误差 |
| 开单页独立页面而非抽屉 | 多行明细子表格 > 8 字段 | 符合前端规则 §5.5 形态选择 |
| 结算简化为单据状态位 | `SettlementStatus` 0/1 手动切换 | 用户确认不做收付款单 / 部分结算；后续升级时该状态位可平滑迁移为「已付金额」 |
| 重复提交幂等 | 前端防重入 + 单号唯一索引兜底 | MVP 并发量低，不引入独立幂等 token（范围外） |
| 无 RBAC | 登录即可见采购入库菜单 | 用户确认本期不做权限；后续权限模块统一接入 |
| 时间处理 | 单据日期 = 前端所选日期的 UTC 午夜 `DateTimeOffset`；列表范围筛选本地当天边界转 UTC ISO | 同登录日志既有约定，避免时区漂移 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同 `user-management` 测试约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（Handler 时间来源统一为仓储传入的 `utcNow` 参数，同用户模块）。

- **GetPurchaseOrders**：单号关键词 / 供应商 / 结算状态 / 时间范围筛选传参断言；分页映射（含 `Status` 字段透传）。
- **CreatePurchaseOrder**：
  - 成功：断言单号生成调用（前缀 `PO` + orderDate）、明细快照（`ProductName` / `Unit`）、`Subtotal` / `TotalAmount` 后端重算（**前端传小计被忽略**）、每行 `IncrementAsync(+quantity)` 调用、`IUnitOfWork.Commit`。
  - 明细为空 → `40110`；供应商不存在 → `40400`；供应商停用 → `40108`；供应商类型不含 Supplier（纯客户）→ `40109`；商品停用 → `40107`；商品不存在 → `40400`。
  - 事务失败（`Commit` 抛异常）→ 异常上抛（回滚断言：`RollbackAsync` 被调用）。
- **VoidPurchaseOrder**：成功（断言每行 `IncrementAsync(-quantity)` + `UpdateStatusAsync(Voided)`）；不存在 → `40400`；已作废 → `40104`。
- **UpdatePurchaseOrderSettlement**：成功；已作废 → `40104`；不存在 → `40400`。
- **GetPurchaseOrderById**：存在（含明细映射）/ 不存在 `40400`。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`OrderNo` EF `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；quantity 999999 通过 / 1000000 拒绝；unitPrice 0 通过（允许 0 元）/ 10000000 拒绝；items 100 行通过 / 101 行拒绝；keyword（单号）20 通过 / 21 拒绝。

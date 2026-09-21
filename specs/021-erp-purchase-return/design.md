---
created: 2026-09-16
updated: 2026-09-21
---

# 设计规格：采购退货（erp-purchase-return）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase` / `erp-sale` 为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格为其消费方 `erp-purchase`（单据域共用约定 §0）、`erp-product`（商品与库存）、`erp-stock-movement`（流水）的**新增单据类型**，不修改既有用例语义。
> **演进（`023-erp-settlement` / `024-erp-order-flow` / `026-erp-cost`）**：退货单结算已金额化（`SettledAmount` + 推导状态，手工切换端点移除，已核销禁作废，核销取数只查主表）；其消费的采购入库单表已重命名（`PurchaseOrders` → `PurchaseReceipts`，路径 `/api/purchase-receipts`，前缀 `GR`）；流水写入同时回填成本列。**本正文已按现行为准**，决策与判据见 `specs/023-erp-settlement/design.md` §0 / §3.1.1 / §3.6、`specs/024-erp-order-flow/design.md` §3 与 `specs/026-erp-cost/design.md` §3。

## 0. 退货单域共用约定（erp-sale-return 继承）

采购退货与销售退货**结构同构**，以下约定两者共用，`022-erp-sale-return` 实现时按「替换规则」表替换差异，其余逐条一致（模式同 `erp-purchase` §0 → `erp-sale`）：

| 项 | 采购退货（本规格） | 销售退货（022 替换） |
|---|---|---|
| 表 / 实体 | `PurchaseReturns` / `PurchaseReturn`、`PurchaseReturnItems` / `PurchaseReturnItem` | `SalesReturns` / `SalesReturn`、`SalesReturnItems` / `SalesReturnItem` |
| 单号前缀 | `PR` | `SR` |
| 往来方向 | 供应商：`Type in (Supplier, Both)` 且启用 | 客户：`Type in (Customer, Both)` 且启用 |
| 单价带出 | 商品采购价 `PurchasePrice` | 商品销售价 `SalePrice` |
| 库存操作（保存） | 每行 `TryDecrementAsync(quantity)`，失败 `40103` | 每行 `IncrementAsync(+quantity)` |
| 库存操作（作废） | 每行 `IncrementAsync(+quantity)`（回冲） | 每行 `IncrementAsync(-quantity)`（回冲，允许冲负） |
| 流水类型 | `PurchaseReturnOut`（退货）/ `PurchaseReturnVoid`（作废） | `SalesReturnIn`（退货）/ `SalesReturnVoid`（作废） |
| 结算语义 | `SettledAmount` + 推导状态（未结 / 部分 / 结清），由收付款单核销累加 / 作废回退；核销方向为**收款**（供应商退我们钱） | 同左（核销方向为**付款**） |
| 错误码 | 全部复用（无新增，见 §3.2） | 同左 |
| 用例目录 | `Features/PurchaseReturns/<Action>` | `Features/SalesReturns/<Action>` |
| 接口路由 | `/api/purchase-returns` | `/api/sales-returns` |
| 前端目录 | `views/PurchaseReturnManagement/` | `views/SalesReturnManagement/` |

> 单号前缀的全域分配见 `specs/ROADMAP.md` §6.7；本规格不自行定义其他域前缀。

## 1. 总体设计

```
采购退货单（前端 /purchase-returns 列表 + /purchase-returns/new 开单页 + /purchase-returns/detail/:id 详情）
  → PurchaseReturnsController
    → App.Core/Features/PurchaseReturns/<Action>/*RequestHandler
      → IPurchaseReturnRepository（单据 + 明细）+ IPartnerRepository / IProductRepository（校验）
        + IInventoryRepository.TryDecrementAsync（原子条件扣减）
        + IStockMovementRepository.AppendAsync（写流水，含成本列）
        + IUnitOfWork（同一事务）
        → PostgreSQL（PurchaseReturns / PurchaseReturnItems / Inventory / StockMovements）
```

核心原则：

- **单据一步式**：保存即生效（库存立即减少 + 应付口径冲减）；不支持编辑，只支持**作废回冲**（同 `erp-purchase` / `erp-sale`）。
- **禁止负库存**：退货退回的是实物，账上必须有货；每行 `TryDecrementAsync`（数据库条件更新），任一行失败 → `40103` 整单回滚。
- **明细单价快照** / **金额后端重算** / **单号后端生成**：同 `erp-purchase`。
- **库存与流水同事务**：保存（扣减 + 流水）与作废（回冲 + 流水）都在同一 `IUnitOfWork` 事务内完成。

## 2. 数据模型

> 时间字段按后端规则 §5.2（`DateTimeOffset` → `timestamptz`）；枚举统一小整数 → `smallint`。
> 主表除下列字段外，结构与约束与 `erp-purchase` design §2.1 / §2.2 **逐项一致**（`ReceiptNo` 唯一索引 → 此处为 `ReturnNo`；`PartnerName` 快照；明细 `ReceiptId` 索引 → 此处为 `ReturnId`）。

### 2.1 实体 `App.Core/Entities/PurchaseReturn.cs` 与表 `PurchaseReturns`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ReturnNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单号 `PR + yyyyMMdd + 4 位序号`（如 `PR202609160001`），后端生成 |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 供应商 |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 供应商名称**快照** |
| `ReturnDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 业务日期（UTC 午夜） |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 总金额 = Σ 小计（后端计算） |
| `SettledAmount` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0，默认 `0` | 已结算金额，由收付款单核销累加 / 作废回退，**不允许手工修改**（`023` §0） |
| `Status` | `OrderStatus` | `smallint` | NOT NULL，默认 `1` | 复用枚举（1=正常 0=已作废） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注（可写原采购单号 / 退货原因） |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

### 2.2 实体 `App.Core/Entities/PurchaseReturnItem.cs` 与表 `PurchaseReturnItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ReturnId` | `Guid` | `uuid` | NOT NULL，FK → `PurchaseReturns(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductName` | `string` | `varchar(50)` | NOT NULL | 商品名称**快照** |
| `Unit` | `string` | `varchar(10)` | NOT NULL | 单位**快照** |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | 退货数量 |
| `UnitPrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 单价**快照**（默认带出商品当前采购价，可改） |
| `Subtotal` | `decimal` | `numeric(18,2)` | NOT NULL | 小计 = 数量 × 单价（后端计算） |

- 枚举复用 `OrderStatus`（`erp-purchase` 已定义），**不新建**；`OrderSettlementStatus` 已随 `023` 删除，结算状态由 `SettlementState` 推导。
- 明细不软删除：作废保留明细（审计需要），仅主表 `Status` 置 0。

### 2.3 EF Core 与迁移

- `AppDbContext` 新增 2 个 `DbSet`：`PurchaseReturns`、`PurchaseReturnItems`；配置 `Persistence/Configurations/PurchaseReturnConfiguration.cs`、`PurchaseReturnItemConfiguration.cs`。
- 外键不级联删除；迁移 `AddErpPurchaseReturn`（建表）+ `AddErpSettlement`（`023`：`SettlementStatus` → `SettledAmount` 并回填历史数据）。

### 2.4 字段约束单一来源（**不新建常量类**）

| 用途 | 常量来源 |
|---|---|
| `ReturnNo` 长度（20） / 查询 `keyword`（20） / `Remark`（200） / 明细行数上限（100） | `OrderFieldConstraints`（`OrderNoMaxLength` / `KeywordMaxLength` / `RemarkMaxLength` / `ItemsMaxCount`） |
| 明细 `Quantity` 1–999999 / `UnitPrice` 0–9999999.99 | `ProductFieldConstraints`（`QuantityMinValue` / `QuantityMaxValue` / `PriceMinValue` / `PriceMaxValue`） |

- 退货单是单据域的一种，单号 / 备注 / 明细行数与数量单价规则与采购 / 销售**完全同源**，另立常量类会制造第二处定义（后端规则 §5.3 禁止）。
- 一致性由单测守护：EF 实际 `HasMaxLength` == 常量；Validator 边界（quantity 999999 通过 / 1000000 拒绝、items 100 行通过 / 101 行拒绝、keyword 20 通过 / 21 拒绝）与采购 / 销售一致。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`IPurchaseReturnRepository`（与 `IPurchaseReceiptRepository` 同构，仅实体类型与单号前缀不同）：

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<PurchaseReturn> Items, int Total)> GetPagedAsync(string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end, SettlementState? settlementState, int page, int pageSize, ...)` | 列表（`keyword` 匹配 `ReturnNo` / 供应商名称快照；含作废单据；`CreatedAt DESC`；`AsNoTracking`） |
| `Task<(PurchaseReturn? Return, IReadOnlyList<PurchaseReturnItem> Items)> GetDetailAsync(Guid id, bool includeItems = true, ...)` | 详情（主表 + 明细，按明细插入顺序）；`includeItems = false` 时只查主表、`Items` 恒为空集合（收付款核销校验用，调用方不得消费，`023` §3.1.1）；不存在时 `Return` 为 null |
| `Task AddAsync(PurchaseReturn, IReadOnlyList<PurchaseReturnItem>, ...)` | 新增单据 + 明细（同一仓储内一次 `SaveChangesAsync`） |
| `Task AddSettledAmountAsync(Guid id, decimal delta, Guid? operatorId, ...)` | 原子累加已结算金额（核销 +delta / 作废回退 −delta），只允许收付款单核销 / 作废调用（`023`） |
| `Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, ...)` | 更新状态（作废） |
| `Task<string> GenerateReturnNoAsync(string prefix, DateTimeOffset returnDate, ...)` | 生成单号（`PR`，机制同 `erp-purchase` §3.6：当天同前缀 `COUNT(*)` + 4 位补零 + 唯一索引冲突重试） |
| `Task<IReadOnlyList<PurchaseReturnItem>> GetItemsByReturnIdsAsync(IReadOnlyCollection<Guid> returnIds, ...)` | 按单据 id 集合批量查明细（导出用，避免逐单 N+1，`027`） |

- **跨仓储写**（单据 + 明细 + 库存 N 行 + 流水 N 条）必须用 `IUnitOfWork` 包成同一事务：`BeginTransactionAsync` → 各仓储写 → `CommitAsync`，异常 `RollbackAsync` 后重抛（后端规则 §4.4）。
- 审计字段由 Handler 经 `ICurrentUser` 获取后随实体 / 参数传入，仓储不感知当前用户。

### 3.2 错误码

> **无新增错误码**，全部复用既有：`40103 InsufficientStock`（`erp-sale` 定义，退货扣减不足时同样使用）、`40104 OrderVoided`、`40107 ProductDisabled`、`40108 PartnerDisabled`、`40109 PartnerTypeMismatch`、`40110 OrderItemsEmpty`、`40120 OrderSettledCannotVoid`（`023` 定义，本规格作废校验消费）、`40400` / `40000`（全局）。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/purchase-returns` | GET | `PurchaseReturns/GetPurchaseReturns` | `PagedResult<PurchaseReturnListItemDto>` | 40000 |
| `/api/purchase-returns` | POST | `PurchaseReturns/CreatePurchaseReturn` | `PurchaseReturnDetailDto` | 40000 / 40103 / 40107 / 40108 / 40109 / 40110 / 40400 |
| `/api/purchase-returns/{id:guid}` | GET | `PurchaseReturns/GetPurchaseReturnById` | `PurchaseReturnDetailDto` | 40400 |
| `/api/purchase-returns/{id:guid}/void` | PUT | `PurchaseReturns/VoidPurchaseReturn` | `PurchaseReturnDetailDto` | 40104 / 40120 / 40400 |

> 手工结算端点 `PUT .../settlement`（`UpdatePurchaseReturnSettlement`）已随 `023` **移除**。

### 3.4 关键用例流程（Handler）

**CreatePurchaseReturn**：

1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取供应商：不存在 → `40400`；`Status = Disabled` → `40108`；`Type` 不含 Supplier（纯客户）→ `40109`。
3. 逐行取商品：不存在 → `40400`；`Status = Disabled` → `40107`；后端重算 `Subtotal = Quantity × UnitPrice`、`TotalAmount = Σ Subtotal`（**不信任前端小计 / 总额**）；明细行写 `ProductName` / `Unit` / `UnitPrice` 快照。
4. `IUnitOfWork`：`BeginTransactionAsync` → **逐行 `TryDecrementAsync(productId, quantity)`**，任一行 `false` → `RollbackAsync` + 抛 `40103`（message 含**首个**不足商品名与当前 / 需要数量）→ 全部成功 → `GenerateReturnNoAsync("PR", returnDate)` → `AddAsync`（主表 + 明细）→ 逐行 `AppendAsync` 流水（`PurchaseReturnOut`，`Quantity = -quantity`，`SourceId` = 退货单 id、`SourceNo` = `ReturnNo`）→ `CommitAsync`。

**VoidPurchaseReturn**：

1. `GetDetailAsync` 取单：不存在 → `40400`；`Status = Voided` → `40104`；`SettledAmount > 0` → `40120`（**不开事务、不动库存与流水**，须先作废对应收款单，`023` §3.6）。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐行 `IncrementAsync(productId, +quantity)`（回冲）+ 逐行 `AppendAsync` 流水（`PurchaseReturnVoid`，`Quantity = +quantity`，来源同上）→ `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**GetPurchaseReturns**：仓储分页筛选（`keyword` 匹配单号 / 供应商名称快照；`ReturnDate` 闭区间；`CreatedAt DESC`）→ Mapper 映射 DTO（含 `Status` 供前端作废行置灰、`settledAmount` / `unsettledAmount` / `settlementState` 推导）。

**GetPurchaseReturnById**：`GetDetailAsync`（不存在 → `40400`）→ 映射表头 + 明细 DTO（快照字段原样返回）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量）

| 请求 | 规则 |
|---|---|
| `CreatePurchaseReturnRequest` | `partnerId` 必填；`returnDate` 必填（`DateTimeOffset`）；`items` 必填非空、1–100 行（`ItemsMaxCount`）；每行 `productId` 必填、`quantity` 1–999999、`unitPrice` 0–9999999.99；`remark` ≤ 200 |
| `GetPurchaseReturnsRequest` | `keyword` ≤ 20；`partnerId` 可空；`settlementState` ∈ {0,1,2}；闭区间 `start <= end`；分页取值按 `AGENTS.md` §4.3（不重复列出） |

- 存在性 / 类型匹配 / 库存等业务约束一律在 Handler（后端规则 §4.1）。

### 3.6 流水接入（消费 `erp-stock-movement` 的仓储；写入点见 §3.4）

| 动作 | `MovementType` | `Quantity` | `SourceId` / `SourceNo` |
|---|---|---|---|
| 开退货单 | `PurchaseReturnOut` | `-quantity`（每行一条） | 退货单 `Id` / `ReturnNo` |
| 作废退货单 | `PurchaseReturnVoid` | `+quantity`（每行一条） | 退货单 `Id` / `ReturnNo` |

- 变动类型的文案与颜色在 `specs/019-erp-stock-movement/design.md` §0 表内续行（唯一事实源），本规格不重复定义。
- **成本列一并回填**（`026`）：写入流水时填 `UnitCost` / `TotalCost`——采购退货按均价出、作废回冲按原流水单价还原；成本缺失兜底按 0 计入且不阻断，判据见 `specs/026-erp-cost/design.md` §3。退货单页面不展示成本（成本仅在流水页 / 成本毛利报表呈现）。

### 3.7 Swagger

- **不分组**（唯一来源见 `specs/003-api-swagger/design.md`）：新增接口按现有方式出现在单文档 Swagger 中；已移除的结算端点同步消失。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── purchaseReturn.ts            # 采购退货接口层
└── views/
    └── PurchaseReturnManagement/
        ├── PurchaseReturnsView.vue       # 退货单列表页
        ├── PurchaseReturnFormPage.vue    # 开退货单独立页（/purchase-returns/new）
        └── PurchaseReturnDetailView.vue  # 详情页（含作废 + 收付款明细）
```

- 页面结构与 `PurchaseManagement/` 完全同构（照抄模板），形态选择、独立页面理由同 `erp-purchase` design §4.1。

### 4.2 接口层

- `src/api/purchaseReturn.ts`：TS 类型与后端 DTO（camelCase）一一对应；封装 / 金额 / 日期范围约定同 `erp-purchase` design §4.2（提交不传小计 / 总额；日期范围转本地边界 UTC ISO）；列表行类型含 `settledAmount` / `unsettledAmount` / `settlementState`。
- 供应商下拉数据源：`src/api/partner.ts` 的 `getPartners`（`status=1`，取 `Type in (1,3)`）；商品下拉数据源：`src/api/product.ts` 的 `getProductPickList`（含当前库存，供退货数量预警）。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `purchase-returns` | `purchaseReturns` | `PurchaseReturnsView` |
| `purchase-returns/new` | `purchaseReturnNew` | `PurchaseReturnFormPage` |
| `purchase-returns/detail/:id` | `purchaseReturnDetail` | `PurchaseReturnDetailView` |

`AppLayout.vue` 侧边菜单追加子项「采购退货」`purchaseReturns`；`MENU_ROUTE_MAP` 增加 `purchaseReturnDetail: 'purchaseReturns'`。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2。

### 4.4 页面交互（与 `PurchaseManagement/` 同构处省略，仅列差异）

- 开单页 `PurchaseReturnFormPage.vue`：表头供应商下拉；明细区商品下拉显示「编码 名称（库存 x）」，**退货数量 > 当前库存时该行数量输入框标红**并提示「库存不足，当前库存 x」（前端预警，最终以后端 `40103` 为准）；单价默认带出商品采购价可改；小计与总金额 computed 展示。
- 列表 `PurchaseReturnsView.vue`：筛选行（单号关键词 + 供应商 + 日期范围 + 结算状态（未结算 / 部分结算 / 已结算）+ 搜索 / 重置）；表格列（序号、单号、供应商、退货日期、总金额、结算状态标签 + 未结金额、单据状态、创建时间、操作列：详情 → 收付款 → 作废）；作废行整体置灰；结算标签文案与颜色取 `specs/023-erp-settlement/design.md` §0。
- 详情 `PurchaseReturnDetailView.vue`：`a-page-header` + `a-descriptions`（单号 / 供应商 / 日期 / 总额 / 结算 / 状态 / 创建人 / 创建时间）+ 明细只读表格 + 「收付款明细」只读区块 + 底部作废 / 去收付款操作；id 不存在 → `a-result status="404"`。
- 「收付款 / 去收付款」与「作废」的显隐 / 可用判据统一取 `src/utils/settlement.ts`（`canStartSettlement` / `canVoidOrder`，`023` §4.4）。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 详情「收付款明细」 | `loading` | 区块内容区 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 退货用独立单据，不复用采购单作废 | 新建 `PurchaseReturns` 域 | 作废面向误录，会抹掉采购发生的事实；退货是真实业务事件，需要自己的凭证、可部分退货、可单独结算 |
| 不关联原采购单 | 无 `SourceOrderId` | 关联需跟踪每张原单的「已退数量」并在开单时校验累计不超原单量，成本高；本期退货单独立开具，备注可写原单号（后续增强见范围外） |
| 退货扣减用 `TryDecrementAsync` | 数据库条件更新 | 退回供应商的是实物，账上必须有货，与销售出库同一防超卖机制；不足报 `40103` |
| 库存不足整单拒绝 | 任一行失败 → 回滚 | 与 `erp-sale` 一致：部分保存会留下半成品单据与不一致库存 |
| 作废回冲无前置校验 | `IncrementAsync(+quantity)` | 与采购 / 销售作废对称，作废必须可执行 |
| 退货单结算金额化 | `SettledAmount` + 推导 `SettlementState`，无手工切换端点 | 退货冲减应付，须与收付款口径单一；原 0/1 状态位无法表达部分结算（由 `023` 改写） |
| 已核销退货单禁止作废 | 作废前校验 `SettledAmount > 0` → `40120` | 见 `023` §5（不自动反核销） |
| 流水类型每业务动作一个 | `PurchaseReturnOut` / `PurchaseReturnVoid` | 与既有「采购入库 / 采购作废 / 销售出库 / 销售作废」模式一致，流水页文案与方向清晰，不合并为通用「调整」 |
| 不新建字段约束常量类 | 复用 `OrderFieldConstraints` + `ProductFieldConstraints` | 退货单是单据域的一种，长度与数量 / 单价规则完全同源（后端规则 §5.3） |
| 单号前缀 `PR` | 与全域前缀分配对齐 | 见 `specs/ROADMAP.md` §6.7（避免与 `024` 的订单 / 出入库单前缀冲突） |
| 不做退货原因字典 | 备注自由文本 | 字典属主数据治理，不阻塞退货闭环 |
| 无 RBAC | 登录即可见「采购退货」菜单 | 同既有功能（权限统一由 `028-erp-rbac` 接入） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreatePurchaseReturn**：
  - 成功：断言每行 `TryDecrementAsync` **先于**单据插入、单号前缀 `PR` + `returnDate`、明细快照（`ProductName` / `Unit` / `UnitPrice`）、`Subtotal` / `TotalAmount` 后端重算（前端传值被忽略）、每行流水（`PurchaseReturnOut`，`Quantity` 为负、来源为本单、含成本列）、`Commit` 被调用。
  - 库存不足：任一行 `false` → `40103`（message 含商品名与当前 / 需要数量）、`RollbackAsync` 被调用、`AddAsync` 未被调用、**无流水写入**。
  - 其他异常：明细空 `40110`；供应商不存在 `40400` / 停用 `40108` / 纯客户 `40109`；商品不存在 `40400` / 停用 `40107`。
  - 事务：`Commit` 抛异常 → `RollbackAsync` 被调用。
- **VoidPurchaseReturn**：成功（断言每行 `IncrementAsync(+quantity)` + 每行流水 `PurchaseReturnVoid` 正方向 + `UpdateStatusAsync(Voided)`）；已作废 → `40104`；不存在 → `40400`；已核销（`SettledAmount > 0`）→ `40120` 且未开事务 / 未回冲 / 不写流水 / 状态不变。
- **GetPurchaseReturns / GetPurchaseReturnById**：筛选传参组合（含 `settlementState`）、分页映射（含 `Status` / 结算金额与状态）、明细快照透传、不存在 `40400`；核销校验取数传 `includeItems: false`（主表查 1 次 / 明细查 0 次）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`PurchaseReturns.ReturnNo` `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；明细列长（50 / 10）== 商品域常量；quantity / unitPrice / items / keyword 边界与采购 / 销售**同源同值**。
- **对账一致性**：采购入库 → 退货 → 退货作废链路后 `Σ 流水变动量 == Inventory.Quantity`（行为型假实现累计断言）。

---
created: 2026-09-16
updated: 2026-09-17
---

# 设计规格：库存流水（erp-stock-movement）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase` 为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格消费 `erp-product` 的 `Products` 表与 `IInventoryRepository` 的原子增减（`IncrementAsync` / `TryDecrementAsync`），并改造 `erp-purchase` / `erp-sale` 的四个既有用例（**不改变其业务语义**，只在其既有事务内追加流水）。

## 0. 库存流水展示约定（唯一事实源）

变动类型的文案与颜色全项目唯一来源；前端下拉 / 表格与 e2e 断言文案均以此为准，`020-erp-stock-take` 追加类型时在本表续行。

| 枚举值 | 业务文案 | 库存方向 | `a-tag` 颜色 | 变动量展示 |
|---|---|---|---|---|
| `PurchaseInbound = 1` | 采购入库 | 增加 | `green` | `+N`（绿字） |
| `PurchaseVoid = 2` | 采购作废 | 减少 | `red` | `-N`（红字） |
| `SalesOutbound = 3` | 销售出库 | 减少 | `blue` | `-N`（红字） |
| `SalesVoid = 4` | 销售作废 | 增加 | `orange` | `+N`（绿字） |
| `InitialStock = 5` | 期初建账 | 增加 | `purple` | `+N`（绿字） |
| `StockTakeAdjust = 6` | 盘点调整 | 双向 | `gold` | `+N`（绿字）/ `-N`（红字） |
| `PurchaseReturnOut = 7` | 采购退货 | 减少 | `orangered` | `-N`（红字） |
| `PurchaseReturnVoid = 8` | 采购退货作废 | 增加 | `magenta` | `+N`（绿字） |
| `SalesReturnIn = 9` | 销售退货 | 增加 | `cyan` | `+N`（绿字） |
| `SalesReturnVoid = 10` | 销售退货作废 | 减少 | `pinkpurple` | `-N`（红字） |
| `TransferOut = 11` | 调拨转出 | 减少 | `geekblue` | `-N`（红字） |
| `TransferIn = 12` | 调拨转入 | 增加 | `lime` | `+N`（绿字） |
| `TransferOutVoid = 13` | 调拨转出作废 | 增加 | `volcano` | `+N`（绿字） |
| `TransferInVoid = 14` | 调拨转入作废 | 减少 | `magenta` | `-N`（红字） |

> 取值 5 / 6 由 `specs/020-erp-stock-take/` 追加；7 / 8 由 `specs/021-erp-purchase-return/` 追加；9 / 10 由 `specs/022-erp-sale-return/` 追加；11–14 由 `specs/031-erp-transfer/` 追加（各自落地时同步启用；前端类型下拉以本表为准）。
> **演进（erp-report）**：进销存报表按变动类型将流水归类「期间入 / 期间出」的归类口径见 `specs/025-erp-report/design.md` §0.1（`StockTakeAdjust` 按符号双向拆分、两侧各计一次；`Transfer*` 11–14 落地时在 §0.1 续行）。

- 变动量列展示**带符号整数**（`+N` / `-N`），入库 / 回增绿字、出库 / 回冲红字；e2e 断言该文本。
- 空值渲染：来源单号为空显示 `-`（后续盘点 / 期初场景），操作人为空显示 `-`（系统操作）。

## 1. 总体设计

```
写入（既有单据驱动，无新增写接口）
  采购入库 / 采购作废 / 销售出库 / 销售作废 Handler
    → IUnitOfWork 事务内：IInventoryRepository 原子增减（既有）
                          + IStockMovementRepository.AppendAsync（新增，每行明细一条流水）
    → PostgreSQL（Inventory / StockMovements / 单据表，同一事务）

查询（前端 /stock-movements）
  → StockMovementsController
    → App.Core/Features/StockMovements/GetStockMovements/*RequestHandler
      → IStockMovementRepository.GetPagedAsync（联查 Products / Users）
        → PostgreSQL
```

核心原则：

- **流水纯追加**：只有写入（由单据驱动）与查询，无更新 / 删除接口；纠错一律通过新的反向流水（作废回冲即此模式），保证审计轨迹不可篡改。
- **写入与库存变化同事务**：复用调用方既有的 `IUnitOfWork` 事务，杜绝「库存变了但无流水」或反之的中间态。
- **只落变动量**：`Quantity` 带符号，不落变动前后库存（决策见 §5）。
- **展示字段一律联查**：商品编码 / 名称 / 单位、操作人姓名由仓储联查带出（与 `erp-inventory-query` 同口径），不新增快照列。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset`（实体 / DTO / 仓储签名 / 请求入参），Npgsql 映射 `timestamptz`（后端规则 §5.2）。
> 枚举统一小整数，PG `smallint`。

### 2.1 实体 `App.Core/Entities/StockMovement.cs` 与表 `StockMovements`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | 变动商品 |
| `MovementType` | `StockMovementType` | `smallint` | NOT NULL | 变动类型（§2.2） |
| `Quantity` | `int` | `integer` | NOT NULL，**≠ 0** | 变动量（带符号：入库 / 回增为正，出库 / 回冲为负） |
| `SourceId` | `Guid?` | `uuid` | NULL | 来源单据 id |
| `SourceNo` | `string?` | `varchar(20)` | NULL | 来源单据号（单号是不可变标识，存值使列表免 join） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注（本期无写入来源，预留展示位） |
| `CreatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 变动时间 |
| `CreatedBy` | `Guid?` | `uuid` | NULL | 操作人用户 id（系统操作可空） |

- **纯追加表**：无 `UpdatedAt` / `UpdatedBy`、无软删除、无更新与删除接口。
- 索引：`(ProductId, CreatedAt)`（按商品下钻流水）、`CreatedAt`（全局列表排序）、`SourceNo`（按单号查询）。
- 外键不级联删除；商品停用不影响历史流水。

### 2.2 枚举 `App.Core/Entities/StockMovementType.cs`

`PurchaseInbound = 1`、`PurchaseVoid = 2`、`SalesOutbound = 3`、`SalesVoid = 4`。

> `020-erp-stock-take` 追加期初建账 / 盘点调整取值（PG `smallint`，追加枚举值**不涉及迁移**）；文案与颜色在 §0 表内续行。

### 2.3 EF Core 与迁移

- `AppDbContext` 新增 `DbSet<StockMovement> StockMovements`；配置 `Persistence/Configurations/StockMovementConfiguration.cs`（列长 / 必填 / 索引按 §2.1、§2.4）。
- 迁移：`dotnet ef migrations add AddErpStockMovement -p src/App.Infrastructure -s src/App.Api`（**增量迁移**，不动既有迁移）。
- 无种子数据。存量数据不回填流水（流水自本规格上线日起记录；上游「Σ 流水 == 当前库存」的对账口径从上线日起成立，历史差异由 `020` 盘点消化）。

### 2.4 字段约束单一来源（**不新建常量类**）

| 字段 | 规则 | 常量来源 |
|---|---|---|
| `SourceNo` | ≤ 20 | `OrderFieldConstraints.OrderNoMaxLength`（来源即单据单号，同源） |
| `Remark` | ≤ 200 | `OrderFieldConstraints.RemarkMaxLength` |
| 查询 `keyword`（匹配 `SourceNo`） | ≤ 20 | `OrderFieldConstraints.KeywordMaxLength` |

- 流水的来源就是单据单号，长度规则与单据域同源；另立 `StockMovementFieldConstraints` 会制造第二处定义（后端规则 §5.3 禁止）。
- 一致性由单测守护：EF 实际 `HasMaxLength` == 常量；`keyword` 长度 == 实际匹配列 `SourceNo` 列长。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`IStockMovementRepository`：

| 方法 | 说明 |
|---|---|
| `Task AppendAsync(StockMovement, ...)` | 追加一条流水（单一仓储写由自身 `SaveChangesAsync` 保证；跨仓储的原子性由调用方 `IUnitOfWork` 提供） |
| `Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(string? keyword, Guid? productId, StockMovementType? type, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, ...)` | 列表：联查 `Products`（`Code` / `Name` / `Unit`）与 `Users`（`DisplayName` 作为操作人姓名）；`keyword` 匹配 `SourceNo`；`CreatedAt DESC`；`start` / `end` 对 `CreatedAt` 闭区间 |
| `Task<int> SumQuantityAsync(Guid productId, ...)` | 某商品流水变动量合计（对账 / 一致性校验用） |

- 新增读模型 `App.Core/Abstractions/StockMovementItem.cs`（`sealed record` + `required` + `init`）：`ProductId` / `ProductCode` / `ProductName` / `Unit` / `MovementType` / `Quantity` / `SourceNo` / `Remark` / `CreatedAt` / `CreatedByName`（`Users.DisplayName`，无匹配用户或 `CreatedBy` 为空时为 `null`）。
- 联查用 EF Core 投影（`Join` / 子查询均可，以可读为先），不写裸 SQL。

### 3.2 错误码

> **无新增错误码**。写入侧由既有单据用例的业务码决定（`40103` / `40104` / `40107` / `40108` / `40109` / `40110` 等），流水追加本身无失败分支；查询侧正常路径无业务失败（`40000` 复用全局）。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/stock-movements` | GET | `StockMovements/GetStockMovements` | `PagedResult<StockMovementListItemDto>` | 40000 |

### 3.4 用例流程（Handler）

**GetStockMovements**：仓储 `GetPagedAsync(keyword, productId, type, start, end, page, pageSize)` → `StockMovementsDtoMapper` 映射 `StockMovementListItemDto`（含变动符号与文案由前端按 §0 渲染，后端只回原始枚举 + 带符号数值）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量）

| 请求 | 规则 |
|---|---|
| `GetStockMovementsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20（`OrderFieldConstraints.KeywordMaxLength`）；`productId` / `type` 可空（`type` 须为合法枚举值）；`start` / `end` 可空且 `start <= end` |

- 严格筛选条件（如商品是否存在）不做校验：查询类接口按「条件不命中则空列表」处理，与既有列表接口一致。

### 3.6 Swagger

- **不分组**（同既有约定）：新增接口 `GET /api/stock-movements` 按现有方式出现在单文档 Swagger 中。

### 3.7 既有用例改造（写入接入，四处方）

| 用例 | 追加时机 | `MovementType` | `Quantity` | `SourceId` / `SourceNo` |
|---|---|---|---|---|
| `Purchases/CreatePurchaseOrder` | 每行 `IncrementAsync(+q)` 之后 | `PurchaseInbound` | `+q` | 单据 `Id` / 生成的 `OrderNo` |
| `Purchases/VoidPurchaseOrder` | 每行 `IncrementAsync(-q)` 之后 | `PurchaseVoid` | `-q` | 该单 `Id` / 该单 `OrderNo` |
| `Sales/CreateSalesOrder` | 每行 `TryDecrementAsync(q)` 成功之后 | `SalesOutbound` | `-q` | 单据 `Id` / 生成的 `OrderNo` |
| `Sales/VoidSalesOrder` | 每行 `IncrementAsync(+q)` 之后 | `SalesVoid` | `+q` | 该单 `Id` / 该单 `OrderNo` |

约束（四处一致）：

- **粒度 1:1**：每条明细行追加一条流水，与库存增减一一对应（单据多行 → 多条流水，同 `SourceId`）。
- **同一事务**：写入点位于既有 `IUnitOfWork` 事务块内（`BeginTransactionAsync` 与 `CommitAsync` 之间），不得在事务外另行提交。
- **操作人与时间**：`CreatedBy` 取 Handler 已有的 `ICurrentUser` 解析结果（`operatorId`）；`CreatedAt` 取用例既有的 `utcNow` 参数（禁止 `DateTime.Now` / 直接读时钟）。
- **不改变既有业务判定**：商品启用校验、库存不足 `40103`、已作废 `40104`、事务回滚等语义保持原样；业务失败路径**不产生流水**。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── stockMovement.ts                    # 库存流水接口层（新域，独立文件）
└── views/
    └── StockMovementManagement/
        └── StockMovementsView.vue          # 库存流水列表（只读）
```

> 另改造既有域 `views/InventoryManagement/InventoryView.vue`（新增操作列「流水」，见 §4.4）。

### 4.2 接口层

- `src/api/stockMovement.ts`：`getStockMovements(query)` + TS 类型（`StockMovementListItem` / `StockMovementQuery`），与后端 DTO（camelCase）一一对应；经 `src/api/request.ts` 统一封装（解包 `data`、40100 处置）。
- 日期范围参数：`a-range-picker` 选值 → 接口层转本地当天 `00:00:00` / `23:59:59` 的 UTC ISO 串（同登录日志 / 采购列表约定）。
- 商品下拉数据源复用 `src/api/product.ts` 的 `getProductPickList`（全量，量小）。

### 4.3 路由与菜单

`src/router/index.ts` 新增（`meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `stock-movements` | `stockMovements` | `StockMovementsView` |

`AppLayout.vue` 侧边菜单「进销存」分组追加子项「库存流水」`stockMovements`；无详情页，`MENU_ROUTE_MAP` 不新增映射。

### 4.4 页面交互

**库存流水 `StockMovementsView.vue`**（只读列表，参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：单号关键词 + 商品下拉 + 变动类型下拉（取 §0 文案）+ 时间范围 + 搜索 / 重置。
- 表格列（每列设 `width`）：序号、变动时间、商品编码、商品名称、变动类型（`a-tag` 按 §0 颜色）、变动量（§0 符号与颜色）、来源单号（空显示 `-`）、操作人（空显示 `-`）、备注（`ellipsis` + `tooltip`）。
- 排序固定 `CreatedAt DESC`（后端），服务端分页；无操作列（流水不可改）。
- 路由 query 带 `productId` 时：以该商品初始化筛选并直接查询一次（供库存页下钻）；筛选行正常展示该商品，用户可清除。

**库存查询页改造 `InventoryView.vue`**：

- 新增操作列（1 个按钮）：「流水」（`type="text" size="small"` + Tabler `IconListDetails`）→ `router.push({ name: 'stockMovements', query: { productId: row.productId } })`。
- 该改动**修订** `specs/014-erp-inventory-query/design.md` §4.4 / §5 的「纯只读无操作列」决策：库存本身仍不可手工改（写入只由单据 / 盘点驱动），新增入口仅为只读下钻；在 `014` 规格加「演进」注记（格式同 `specs/012-erp-product/design.md` §4.1）。

### 4.5 按钮 loading（遵循前端规则 §4.6 与 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 库存页「流水」跳转 | 不置 loading | 同步路由跳转（§0 判据：瞬时动作不置 loading） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 只落变动量，不落变动前后库存 | `Quantity` 带符号 + 唯一来源 | 现有库存增减是**原子条件更新**（`012` §2.3：`ExecuteUpdateAsync` 表达，数据库层防超卖）。要落「变动前 / 变动后」必须在同一事务内先锁定库存行读值，等于把成熟的原子实现换成行锁方案并引入裸 SQL（后端规则 §5.1 限制）；流水只需保证「变动量与来源准确」，结存可由变动量序列累计推导（同一商品 `Σ Quantity == Inventory.Quantity`） |
| 流水与库存变化同事务 | 复用调用方既有 `IUnitOfWork` | 杜绝「库存变了但无流水」或反之；写入点紧跟库存增减，不新增事务边界 |
| 商品名称 / 操作人姓名联查，不存快照 | 联查 `Products` / `Users` | 与 `erp-inventory-query` 同口径（同一概念全项目一种口径）；人员、商品改名后统一显示当前名称，避免两套口径分叉；单据明细的快照是「交易当时的计价 / 计量依据」，性质不同 |
| 来源单号存值、不做跳转 | `SourceNo` `varchar(20)`，列表仅展示文本 | 单号是不可变标识，存值使用户免去二次查询；按类型跳详情会在流水域引入单据路由耦合（`024` 两段式还会改路由），本期不做 |
| 不新建字段约束常量类 | 复用 `OrderFieldConstraints` | 来源即单据单号，长度规则同源（后端规则 §5.3 禁止同一规则两处定义） |
| 纯追加表，无更新 / 删除接口 | 只有 `AppendAsync` + 查询 | 审计要求：流水被篡改即失去对账价值；纠错靠反向流水（作废回冲已是此模式） |
| 存量数据不回填流水 | 迁移不带数据回填 | 无法从现有单据准确重建作废时序；对账口径自上线日成立，历史差异由 `020` 盘点一次性消化（期初 / 盘点本就是为此存在） |
| 流水页独立页面而非抽屉 | 多条件筛选 + 跨商品查询 + 分页 | 抽屉只适合单商品窄表；单商品下钻用路由 query 预置筛选，复用同一页面 |
| 库存查询页新增「流水」入口 | 操作列 1 个只读按钮 | 修订 `014` 决策；库存写入仍只由单据 / 盘点驱动，不开放手工改库存 |
| 错误码不新增 | 写入复用既有单据业务码 | 流水追加无独立失败分支；避免为「无错误」凑码 |
| 无 RBAC | 登录即可见「库存流水」菜单 | 同既有功能（权限统一由 `028-erp-rbac` 接入，见 `specs/ROADMAP.md` §4.5） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **GetStockMovements**：无筛选 / 关键词 / 商品 / 类型 / 时间范围组合筛选正确传参仓储（`page` / `pageSize` / `keyword` / `productId` / `type` / `start` / `end` 断言）；映射断言（`Quantity` 符号与数值、`SourceNo` 为空 → `null` 透传、`CreatedByName` 为空 → `null`）；分页 `total` 透传。
- **既有用例扩展（四个 Handler 的流水写入）**，每处覆盖：
  - 正常路径：断言 `AppendAsync` 每行明细调用一次，参数含 `MovementType`（对应取值）、`Quantity` 符号与数值、`SourceId` == 单据 id、`SourceNo` == 单号、`CreatedBy` == 当前用户、`CreatedAt` == 用例时间参数；
  - 失败路径**不写流水**：采购商品停用 `40107` / 供应商停用 `40108` / 已作废 `40104`、销售库存不足 `40103`；
  - 事务性：`Commit` 抛异常 → `RollbackAsync` 被调用（库存与流水一起回滚）。
- **对账一致性**：以行为型假实现累计 `AppendAsync` 的 `Quantity`，断言 `Σ 变动量 == Inventory.Quantity`（覆盖采购入库 → 作废、销售出库 → 作废四条链路的正负抵消）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`SourceNo` EF `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；`GetStockMovementsRequest` 的 `keyword` 20 通过 / 21 拒绝，且与 `SourceNo` 列长一致（后端规则 §5.3 第 ③ 条）。

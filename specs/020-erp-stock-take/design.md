---
created: 2026-09-16
updated: 2026-09-18
---

# 设计规格：期初建账与库存盘点（erp-stock-take）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase`（单据域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 变动类型的**文案与颜色**见 `specs/019-erp-stock-movement/design.md` §0（唯一事实源，本规格新增两行）；本文件不重复该表。

## 1. 总体设计

```
期初建账 / 库存盘点（前端 /stock-takes 列表 + /stock-takes/new 新建 + /stock-takes/detail/:id 详情）
  → StockTakesController
    → App.Core/Features/StockTakes/<Action>/*RequestHandler
      → IStockTakeRepository（单据 + 明细）+ IProductRepository（商品校验）
        + IInventoryRepository.SetQuantityAsync（原子设定）+ IInventoryRepository.GetQuantitiesAsync（读账面）
        + IStockMovementRepository.AppendAsync（写流水）
        + IUnitOfWork（同一事务）
        → PostgreSQL（StockTakes / StockTakeItems / Inventory / StockMovements）
```

核心原则：

- **一步式提交**：保存即生效（库存按实盘数量设定 + 差异写流水）；无草稿、无编辑、无作废。
- **差异后端重算**：`Difference = 实盘 − 账面`，账面数量在**事务开始后由后端读取**（前端带出的账面仅用于录入参考），不信任前端传的差异值。
- **只有差异行产生变动**：`Difference == 0` 的明细行只落库为盘点记录，不改库存、不写流水。
- **两种用途、一套机制**：期初建账与盘点共用同一单据结构与提交流程，仅差异在流水类型、可选商品范围（期初限「无任何库存变动」的商品）与单号用途标识上。
- **所有库存调整必须有凭证**：不提供「直接改库存」的入口，调整一律经本规格的单据。

## 2. 数据模型

> 时间字段按后端规则 §5.2（`DateTimeOffset` → `timestamptz`）。
> 枚举统一小整数，PG `smallint`。

### 2.1 实体 `App.Core/Entities/StockTake.cs` 与表 `StockTakes`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `TakeNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单号 `ST + yyyyMMdd + 4 位序号`（如 `ST202609160001`），后端生成 |
| `Type` | `StockTakeType` | `smallint` | NOT NULL | 0=期初建账 1=库存盘点 |
| `TakeDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 业务日期（UTC 午夜） |
| `ItemCount` | `int` | `integer` | NOT NULL | 明细行数（列表展示，提交时统计） |
| `DiffItemCount` | `int` | `integer` | NOT NULL | 差异行数（`Difference != 0`，列表展示） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注（盘点说明 / 差异原因） |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 无状态字段：一步式提交，落库即在效（决策见 §5）。
- 枚举 `App.Core/Entities/StockTakeType.cs`：`public enum StockTakeType { Initial = 0, Take = 1 }`。

### 2.2 实体 `App.Core/Entities/StockTakeItem.cs` 与表 `StockTakeItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `StockTakeId` | `Guid` | `uuid` | NOT NULL，FK → `StockTakes(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductCode` | `string` | `varchar(32)` | NOT NULL | 商品编码**快照** |
| `ProductName` | `string` | `varchar(50)` | NOT NULL | 商品名称**快照** |
| `Unit` | `string` | `varchar(10)` | NOT NULL | 单位**快照** |
| `BookQuantity` | `int` | `integer` | NOT NULL，≥ 0 | 账面数量（提交时后端读取） |
| `ActualQuantity` | `int` | `integer` | NOT NULL，≥ 0 | 实盘数量（前端录入） |
| `Difference` | `int` | `integer` | NOT NULL | 差异 = `ActualQuantity − BookQuantity`（后端计算，可负） |
| `UnitCost` | `decimal` | `numeric(18,4)` | NOT NULL，默认 0 | 成本单价（期初建账必填；库存盘点固定 0） |

- 明细不软删除、不可改（一步式）；同一单据内**不允许重复商品**（Validator 拦重复 `productId`）。
- 实体配置：`Persistence/Configurations/StockTakeConfiguration.cs`、`StockTakeItemConfiguration.cs`；`AppDbContext` 新增 2 个 `DbSet`。
- 迁移：`dotnet ef migrations add AddErpStockTake -p src/App.Infrastructure -s src/App.Api`（增量迁移，不动既有迁移）；外键不级联删除。

### 2.3 库存与流水（复用 + 两处扩展）

| 组件 | 变更 |
|---|---|
| `IInventoryRepository` | **追加** `Task SetQuantityAsync(Guid productId, int quantity, ...)`（原子设定 `Quantity` 并刷新 `UpdatedAt`，EF Core `ExecuteUpdateAsync` 表达，无裸 SQL）；**追加** `Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(IReadOnlyList<Guid> productIds, ...)`（批量读账面，无库存行的商品视为 0） |
| `IStockMovementRepository` | **追加** `Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(IReadOnlyList<Guid> productIds, ...)`（期初建账限制校验，批量） |
| `StockMovementType` | **追加** `InitialStock = 5`、`StockTakeAdjust = 6`（PG `smallint`，追加枚举值不涉及迁移） |
| `StockMovement.SourceId` / `SourceNo` | 指向盘点单 id 与 `TakeNo`（期初建账同样填写，便于从流水回溯凭证） |
| 成本结转 | 期初建账按录入单价结转（`Inventory.AverageCost` 设为该单价、`CostAmount` = 实盘 × 单价）；库存盘点按当前均价（盘盈入 / 盘亏出）。口径见 `specs/026-erp-cost/design.md` §3 |

### 2.4 字段约束单一来源（`App.Core/Entities/StockTakeFieldConstraints.cs`）

| 常量 | 值 | 来源 |
|---|---|---|
| `ActualQuantityMinValue` | 0 | 本域定义（实盘下限为 0；单据明细数量下限 `ProductFieldConstraints.QuantityMinValue = 1` 语义不同，不可复用） |
| `ActualQuantityMaxValue` | `ProductFieldConstraints.QuantityMaxValue`（999999） | 引用商品域上界（禁止复制数值） |
| `TakeNoMaxLength` | `OrderFieldConstraints.OrderNoMaxLength`（20） | 引用单据域（同一种「单号」概念） |
| `RemarkMaxLength` | `OrderFieldConstraints.RemarkMaxLength`（200） | 同上 |
| `KeywordMaxLength` | `OrderFieldConstraints.KeywordMaxLength`（20） | 同上（对齐实际匹配列 `TakeNo`） |
| `ItemsMaxCount` | `OrderFieldConstraints.ItemsMaxCount`（100） | 同上（单张单据明细行数上限） |

- 本域常量类只定义「无既有常量可引用」的项（实盘下限），其余取引用；EF 配置与各 `RequestValidator` 一律用常量，禁止硬编码。
- 一致性由单测守护（扩展 `FieldValidationConsistencyTests`）：EF 实际 `HasMaxLength` == 常量；`actualQuantity` 边界通过 / 越界拒绝；`keyword` 20 通过 / 21 拒绝且不超 `TakeNo` 列长。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`IStockTakeRepository`：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(StockTake, IReadOnlyList<StockTakeItem>, ...)` | 新增单据 + 明细（同一仓储内一次 `SaveChangesAsync`） |
| `Task<(IReadOnlyList<StockTake> Items, int Total)> GetPagedAsync(string? keyword, StockTakeType? type, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, ...)` | 列表（`keyword` 匹配 `TakeNo`；`TakeDate` 闭区间；`CreatedAt DESC`；`AsNoTracking`，直接返回实体） |
| `Task<(StockTake? Take, IReadOnlyList<StockTakeItem> Items)> GetDetailAsync(Guid id, ...)` | 详情（主表 + 明细，按明细插入顺序返回） |
| `Task<string> GenerateTakeNoAsync(DateTimeOffset takeDate, ...)` | 生成单号（`ST` 前缀，机制与实现见 `erp-purchase` §3.6：`COUNT(*)` 当天同前缀 + 4 位补零，唯一索引兜底重试） |

- **跨仓储写**（单据 + 明细 + 库存设定 + 流水）必须用 `IUnitOfWork` 包成同一事务：`BeginTransactionAsync` → 各仓储写 → `CommitAsync`，异常 `RollbackAsync` 后重抛（后端规则 §4.4）。
- 审计字段由 Handler 经 `ICurrentUser` 获取后随实体 / 参数传入，仓储不感知当前用户。
- 商品选择（`GetStockTakePickProducts`）不新增仓储方法：由 `IProductRepository.GetPickListAsync`（启用商品 + 当前库存，`012` 已交付）+ `IStockMovementRepository.GetProductIdsWithMovementsAsync` 组合得到。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40111 | `StockInitialNotAllowed` | 期初建账只允许从未发生库存变动的商品，所选商品已有库存变动（已建账 / 已有单据业务） |

> 复用：`40110 OrderItemsEmpty`（明细不能为空，`erp-purchase` 定义）、`40107 ProductDisabled`、`40400 NotFound`、`40000 Validation`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/stock-takes` | GET | `StockTakes/GetStockTakes` | `PagedResult<StockTakeListItemDto>` | 40000 |
| `/api/stock-takes` | POST | `StockTakes/CreateStockTake` | `StockTakeDetailDto` | 40000 / 40107 / 40110 / 40111 / 40400 |
| `/api/stock-takes/{id:guid}` | GET | `StockTakes/GetStockTakeById` | `StockTakeDetailDto` | 40400 |
| `/api/stock-takes/pick-products` | GET | `StockTakes/GetStockTakePickProducts` | `IReadOnlyList<StockTakeProductPickDto>` | 40000 |

路由注意：`/api/stock-takes/pick-products` 为固定段，置于 `{id:guid}` 之前注册（`{id:guid}` 约束本身已排除 `pick-products`，双保险）。

### 3.4 关键用例流程（Handler）

**CreateStockTake**：

1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 逐行取商品：不存在 → `40400`；`Status = Disabled` → `40107`。
3. 逐行取商品编码 / 名称 / 单位作**快照**写入明细。
4. `Type = Initial` 时：`GetProductIdsWithMovementsAsync(productIds)` 非空 → `40111`。
5. `GetQuantitiesAsync(productIds)` 读账面（无库存行视为 0）→ 逐行重算 `Difference = ActualQuantity − BookQuantity`；统计 `ItemCount` / `DiffItemCount`。
6. `GenerateTakeNoAsync(takeDate)` 生成单号；`Type` 决定流水类型（`Initial` → `InitialStock`，`Take` → `StockTakeAdjust`）。
7. `IUnitOfWork`：`BeginTransactionAsync` → `IStockTakeRepository.AddAsync(take, items)` → 对 `Difference != 0` 的行：`SetQuantityAsync(productId, ActualQuantity)` + `AppendAsync(流水)` → `CommitAsync`。

**GetStockTakes**：仓储分页筛选（`keyword` 匹配单号、`TakeDate` 闭区间、`CreatedAt DESC`）→ Mapper 映射 DTO。

**GetStockTakeById**：`GetDetailAsync`（不存在 → `40400`）→ 映射表头 + 明细 DTO（快照字段原样返回）。

**GetStockTakePickProducts**：`GetPickListAsync`（启用商品 + 当前库存）→ 叠加 `GetProductIdsWithMovementsAsync` 结果为 `hasMovements` 标记 → 映射 DTO。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量）

| 请求 | 规则 |
|---|---|
| `CreateStockTakeRequest` | `type` ∈ {0, 1}；`takeDate` 必填；`items` 必填非空、1–100 行（`ItemsMaxCount`）、**`productId` 不重复**；每行 `productId` 必填、`actualQuantity` 0–999999（`ActualQuantityMinValue` / `ActualQuantityMaxValue`）、`unitCost` 期初建账必填（0–`ProductFieldConstraints.PriceMaxValue`）、库存盘点模式**禁止传入**（Validator 拦截）；`remark` ≤ 200 |
| `GetStockTakesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20（`KeywordMaxLength`）；`type` 可空合法值；`start` / `end` 可空，闭区间 `start <= end` |

- 存在性 / 停用 / 期初限制 / 差异计算等业务约束一律在 Handler（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（同既有约定）：4 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── stockTake.ts                     # 盘点 / 期初建账接口层
└── views/
    └── StockTakeManagement/
        ├── StockTakesView.vue           # 盘点单列表
        ├── StockTakeFormPage.vue        # 新建（类型：盘点 / 期初建账）独立页
        └── StockTakeDetailView.vue      # 详情（只读明细）
```

- 新建为**独立页面**（含明细子表格，按 `specs/007-form-detail-showcase/design.md` §0 形态选择：含子表格 → 独立页面），详情为独立页面。
- 期初建账**不单列菜单**：作为新建页的类型选项（决策见 §5）。

### 4.2 接口层

- `src/api/stockTake.ts`：类型与后端 DTO（camelCase）一一对应；`getStockTakes` / `getStockTakeById` / `createStockTake` / `getStockTakePickProducts`；请求统一经 `src/api/request.ts`（约定见前端规则 §3）。
- 日期范围参数转 UTC ISO（同既有约定）；日期字段展示统一 `utils/datetime.ts`。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `stock-takes` | `stockTakes` | `StockTakesView` |
| `stock-takes/new` | `stockTakeNew` | `StockTakeFormPage` |
| `stock-takes/detail/:id` | `stockTakeDetail` | `StockTakeDetailView` |

`AppLayout.vue` 侧边菜单「库存」分组下提供子项「库存盘点」`stockTakes`；`MENU_ROUTE_MAP` 增加 `stockTakeDetail: 'stockTakes'`。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2。

### 4.4 页面交互

**盘点单列表 `StockTakesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：单号关键词 + 类型下拉（全部 / 期初建账 / 库存盘点）+ 日期范围 + 搜索 / 重置。
- 操作行：新建（primary，跳 `/stock-takes/new`）、刷新、列设置。
- 表格列：序号、单号、类型（`a-tag`：期初建账 `purple` / 库存盘点 `gold`）、盘点日期、明细行数、差异行数（> 0 标橙）、备注、创建时间、操作列（1 个「详情」，Tabler `IconListDetails`）；服务端分页。

**新建页 `StockTakeFormPage.vue`**（底部操作栏 提交 / 取消）：

- 表头：类型（`a-radio-group`：库存盘点 / 期初建账，默认「库存盘点」）、盘点日期（`a-date-picker`，默认当天）、备注。
- 明细区：`a-table` 可编辑行——商品下拉（`getStockTakePickProducts`，显示「编码 名称（账面 x）」；**期初建账模式下 `hasMovements` 的商品禁用并标注「已建账」**）、账面数量（只读，带出）、实盘数量 `a-input-number :min="0" :precision="0"`、差异（computed = 实盘 − 账面，正绿负红）、**成本单价（仅期初建账模式列，必填，`a-input-number :precision="4"`）**、行删除；「添加行」按钮；期初建账模式下底部展示**期初金额** computed（= Σ 实盘 × 单位成本）。模式切换时清空成本列。
- 期初建账模式提示文案：仅可选未发生库存变动的商品（已建账 / 有单据业务的商品请用库存盘点调整）。
- 提交成功 → `Message.success` + 跳详情页；失败 → 统一错误提示，页面停留。

**详情页 `StockTakeDetailView.vue`**：

- `a-page-header`（返回）+ `a-descriptions`（单号 / 类型 / 盘点日期 / 明细行数 / 差异行数 / 备注 / 创建人 / 创建时间）+ 明细只读表格（商品编码 / 名称 / 单位 / 账面数量 / 实盘数量 / 差异）+ 底部「查看库存流水」入口（跳 `/stock-movements` 并带 `keyword` = 本单号，复用 `019` 页面）。
- id 不存在 → `a-result status="404"` + 返回列表。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 新建页提交 | `submitting` | 提交按钮 |
| 商品下拉 / 明细行加载 | 不单独置位 | 并入页面初始化（一次性加载，无按钮触发） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 期初建账与盘点共用一套单据 | `StockTakes.Type` 区分 | 两者机制相同（把库存设定为录入值 + 写流水），差异只在流水类型与可选商品范围；拆两套表 / 页面是重复建设 |
| 一步式提交，无草稿态 | 无 `Draft` 状态 | 与 `015` / `016` 单据一致；草稿需引入「录入中」状态与并发编辑问题，成本大于收益 |
| 不可编辑 / 不可作废，纠错靠再盘 | 无 `PUT` / 无 void 接口 | 作废一次盘点的语义就是「按当时账面再盘一次」；引入作废会带来状态机与流水冲销的复杂度，却不增加信息量（`015` 的作废针对「交易」，盘点只是「校正」） |
| 只有差异行写流水 | `Difference == 0` 只存明细 | 流水记录库存变动，无变动即无流水；盘点凭证仍保留全部行，便于「盘过但相符」的追溯 |
| 差异后端重算，账面在事务内读 | 前端账面仅展示 | 录入到提交之间可能发生采购 / 销售；以后端读值为准，避免差异被外部变动掩盖 |
| 期初建账限「无任何库存变动」商品 | `40111` | 已有变动的商品说明业务已开始，期初应改为盘点调整；否则期初会覆盖既有库存与流水口径 |
| 明细存编码 / 名称 / 单位快照 | 同 `015` §5 快照原则 | 盘点凭证需反映盘当时的商品信息，档案改名不影响历史凭证 |
| 本域只定义实盘下限常量 | `StockTakeFieldConstraints.ActualQuantityMinValue = 0` + 上界引用商品域 | 实盘可为 0 而单据明细数量下限为 1，无既有常量可用；上界同源引用避免复制数值（后端规则 §5.3） |
| 新增商品选择接口 | `GET /api/stock-takes/pick-products` | 期初模式需标注「是否已建账」，复用 `products/pick` 无法表达；一个只读接口换掉 N 次查询与「提交才报错」的体验 |
| 期初建账不单列菜单 | 新建页的类型选项 | 期初是一次性开账动作，长期挂在菜单会误导日常使用；同为盘点域，同页面切换类型即可 |
| 详情页「查看库存流水」复用流水页 | 跳 `/stock-movements?keyword=单号` | 不为盘点单单独做流水视图，复用 `019` 页面（筛选已支持单号关键词） |
| 无 RBAC | 登录即可见「库存盘点」菜单 | 同既有功能（权限统一由 `028-erp-rbac` 接入） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreateStockTake**：
  - 成功（盘点）：断言单号生成调用（`ST` + `takeDate`）、明细快照（编码 / 名称 / 单位）、`Difference` 后端重算（**前端传差异被忽略**）、`ItemCount` / `DiffItemCount` 统计、差异行 `SetQuantityAsync` + `AppendAsync(StockTakeAdjust, delta = difference, SourceId, SourceNo)` 调用、无差异行**不调用** 设定与追加、`Commit` 被调用。
  - 成功（期初）：流水类型为 `InitialStock`；`GetProductIdsWithMovementsAsync` 返回空时通过。
  - 异常：明细为空 → `40110`；商品不存在 → `40400`；商品停用 → `40107`；期初 + 已有变动商品 → `40111`（并断言不写库存 / 不写流水 / 不落单）。
  - 事务：`Commit` 抛异常 → `RollbackAsync` 被调用。
- **GetStockTakes**：筛选传参组合（keyword / type / 日期范围）+ 分页映射（`ItemCount` / `DiffItemCount` / `Type` 透传）。
- **GetStockTakeById**：存在（表头 + 明细映射，含快照字段）/ 不存在 → `40400`。
- **GetStockTakePickProducts**：`hasMovements` 标记正确（有流水商品为 true）；仅返回启用商品（仓储已过滤，Handler 不重复过滤）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`StockTakeItem` EF `HasMaxLength`（32 / 50 / 10）== 商品域常量；`TakeNo` 20 == `OrderFieldConstraints.OrderNoMaxLength`；`actualQuantity` 0 通过 / 999999 通过 / 1000000 拒绝；items 100 行通过 / 101 行拒绝；`keyword` 20 通过 / 21 拒绝。
- **对账一致性**：期初 + 盘点调整后，`Σ 流水变动量 == Inventory.Quantity`（行为型假实现累计断言）。



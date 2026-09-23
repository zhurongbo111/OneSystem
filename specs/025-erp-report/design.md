---
created: 2026-09-17
updated: 2026-09-23
---

# 设计规格：进销存报表（erp-report）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase`（单据域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格**纯只读**：不新增表 / 实体 / 迁移，不改变任何写入路径；消费 `019` 的 `StockMovements` 与既有单据表。
> **演进（erp-rbac）**：报表域动作接入权限校验，权限点 `reports.view` / `export`（5 个报表页共用，`export` 由 `027` 的导出动作标注）；菜单分组可见性取组内任一子项 `.view` 命中（判据见 `specs/028-erp-rbac/design.md` §0.1）。清单唯一来源见其 §0.2。
> **演进（erp-audit-log）**：本域报表均为只读聚合（无写路径），不在操作日志范围内（`specs/029-erp-audit-log/design.md` §0.1「范围外动作」）。

## 0. 约定正文（唯一事实源）

### 0.1 报表口径

**库存流量口径**（进销存报表）：全部取自 `StockMovements`（`019` design §0 的类型表），按变动类型归入「入 / 出」两列：

| 归类 | 变动类型 | 说明 |
|---|---|---|
| 期初 | 期间起点**之前**的全部流水 | `期初 = Σ Quantity WHERE CreatedAt < start`（商品维度） |
| 期间入 | `PurchaseInbound`(1) / `InitialStock`(5) / `PurchaseReturnVoid`(8) / `SalesReturnIn`(9) | 取正变动 |
| 期间出 | `PurchaseVoid`(2) / `SalesOutbound`(3) / `PurchaseReturnOut`(7) / `SalesReturnVoid`(10) | 取负变动的绝对值 |
| 期间入 / 出（双向） | `StockTakeAdjust`(6) | **按符号拆分**：`Quantity > 0` 计入期间入、`Quantity < 0` 的绝对值计入期间出，两侧各计一次，不得整条计入一侧 |
| 期间入 / 出（后续类型） | `TransferOut`(11) / `TransferIn`(12) / `TransferOutVoid`(13) / `TransferInVoid`(14)（`039-erp-transfer` 追加） | 转出 / 转出作废按「出 / 入」，转入 / 转入作废按「入 / 出」；落地时在本表续行并同步实现 |
| 期末 | 推导 | `期末 = 期初 + 期间入 − 期间出` |

- **恒等式**：`期末 = 期初 + 期间入 − 期间出`；当 `end` 不早于当前时间时，`期末` 必等于 `Inventory.Quantity`（对账断言，单测守护）。
- 期间为**左闭右开**：`start <= CreatedAt < end`（前端传本地当天 00:00:00 与「结束日次日 00:00:00」的 UTC ISO 串，与既有列表区间约定一致但语义为半开区间，避免同一天跨区间重复计入）。
- 期初口径**固定为该商品的起点前累计**，不随筛选集合变化：按分类筛选时，期初仍取「该分类下商品」的累计，而非重新基线。

**金额口径**（采购 / 销售汇总）：

| 指标 | 口径 |
|---|---|
| 采购入库单数 / 数量 / 金额 | 未作废（`Status = Normal`）的 `PurchaseReceipts` + 明细；金额取 `TotalAmount`，数量取 Σ 明细 `Quantity` |
| 采购退货数量 / 金额 | 未作废的 `PurchaseReturns`（`021`） |
| 采购净额 | `Σ 入库金额 − Σ 退货金额`（净数量同理） |
| 销售出库 / 销售退货 / 销售净额 | 同上，替换为 `SalesShipments`（`016`）与 `SalesReturns`（`022`），往来维度为客户 |
| 作废单据 | **全部不计入**（作废即视为业务未发生，与库存回冲一致） |
| 分组维度 | 往来单位（默认）或商品；商品维度下往来列不展示 |

- 退货金额按**正数**落库（`021` / `022` 的 `TotalAmount` 为正），净额在聚合时做减法——汇总列分别展示，`净额` 为计算列。

**库存占比口径**（库存余额表）：`占比 = 该分类库存合计 ÷ 全量筛选结果的库存总量`（分母取 `summary.totalQuantity`，**不是当前页合计**）；分母为 0 时占比按 0 处理。

- 出参为 **0–1** 比例（`quantityRatio`，由 `ReportsDtoMapper` 计算），页面以「进度条 + `xx.xx%` 文本」展示（小数位与导出侧 `FormatRatio` 一致）。
- 进度条传参口径见前端规则 §4.9：`a-progress` 的 `percent` 本身就是 0–1（组件内部 ×100 既用于文本也用于条宽），**不得再乘 100**。

### 0.2 菜单分组与归属（唯一事实源）

> `024` 已预告「进销存」分组项数将超过 10 项需二次分组（`specs/024-erp-order-flow/requirement.md` §5）。本规格落地时**改为多个顶级分组**（不嵌套子菜单，规避 Arco 多级子菜单的 `openKeys` 复杂度），本表为 `023`–`045` 全部菜单归属的唯一来源；后续规格只在本表续行，不再各自发明分组。

| 顶级分组（key） | 子项（key） | 引入规格 |
|---|---|---|
| 示例页面（`showcase`） | 组件示例（`components`）/ 列表示例（`list`）/ 表单与详情示例（`form`） | `004` / `005` |
| 基础档案（`basedata`） | 商品管理（`products`）/ 分类管理（`categories`）/ 往来单位（`partners`）/ 仓库管理（`warehouses`） | `012` / `017` / `013` / `038` |
| 采购（`purchase`） | 采购订单（`purchaseOrders`，`024`）/ 采购入库（`purchases`）/ 采购退货（`purchaseReturns`） | `015` / `021` / `024` |
| 销售（`sale`） | 报价单（`quotations`，`037`）/ 销售订单（`salesOrders`，`024`）/ 销售出库（`sales`）/ 销售退货（`salesReturns`） | `016` / `022` / `024` / `037` |
| 库存（`stock`） | 库存查询（`inventory`）/ 库存流水（`stockMovements`）/ 库存盘点（`stockTakes`）/ 调拨单（`transfers`）/ 批次管理（`batches`） | `014` / `019` / `020` / `039` / `040` |
| 资金（`fund`） | 收付款（`settlements`）/ 往来对账（`reconciliation`）/ 发票登记（`invoices`）/ 客户价格（`partnerPrices`） | `023` / `032` / `036` |
| 财务（`finance`） | 会计科目（`accounts`）/ 税率（`taxRates`）/ 凭证（`vouchers`）/ 财务报表（`financialReports`）/ 银行账户（`bankAccounts`）/ 资金日记账（`cashJournals`） | `031` / `033` / `034` |
| 报表（`report`） | 进销存报表（`inventoryFlowReport`）/ 库存余额表（`stockBalanceReport`）/ 采购汇总（`purchaseSummaryReport`）/ 销售汇总（`salesSummaryReport`）/ 成本与毛利（`costProfitReport`，`026`） | `025` / `026` |
| 系统（`system`） | 用户管理（`users`）/ 登录日志（`loginLogs`）/ 角色权限（`roles`）/ 操作日志（`auditLogs`）/ 部门管理（`departments`）/ 岗位管理（`positions`）/ 员工档案（`employees`）/ 单据审批（`approvals`）/ 站内消息（`notifications`，`041` 落地后入口为**顶栏铃铛**、不进侧边菜单，见 `specs/041-erp-stock-alert/design.md` §4.3） | `009` / `028` / `029` / `030` / `042` / `041` |

- 分组与子项均**默认折叠**，仅当前路由所属分组自动展开（`005` 既有交互不变，`watch(route.name, { immediate: true })` 只增不减）。
- 分组顺序即上表顺序；子项顺序即行内顺序（「商品管理 → 分类管理 → …」）。
- `MENU_ROUTE_MAP`（详情页 → 父菜单项）随之更新；`e2e/helpers/menu.ts` 的「菜单项 → 分组」映射以本表为准。
- 本表落地时，`005-app-layout` / `009-user-management` 的 `design.md` 正文同步改写为最终态（菜单结构由「示例页面 + 进销存」两组改为多组），并各留一行演进指针。

## 1. 总体设计

```
报表（前端 /reports/*，4 个页面）
  → ReportsController
    → App.Core/Features/Reports/<Action>/*RequestHandler
      → IReportQueryRepository（新增，只读跨表聚合）
        → PostgreSQL（StockMovements / Inventory / Products / Categories
                        / PurchaseReceipts(+Items) / PurchaseReturns(+Items)
                        / SalesShipments(+Items) / SalesReturns(+Items)）
```

核心原则：

- **报表读流水与单据，不落第二套口径**：库存类报表只读 `StockMovements`（`019`），金额类报表只读单据表；不新增物化表、不做写入侧改造。
- **聚合在数据库侧完成**：分组与求和由 EF Core 投影 + `GroupBy` 表达（`SUM` / `COUNT` 在 SQL 执行），只在需要「先算期初再与期间合并」时用两次查询在内存拼接（商品数量为单组织量级）。
- **期末必须可对账**：`期末` 与 `Inventory.Quantity` 的恒等关系由单测与 e2e 双覆盖（本规格的对账断言口径）。
- **只读仓储独立**：跨表聚合不属于任何单一单据仓储，按读模型 / 查询职责独立成 `IReportQueryRepository`（同 `023` 的 `ISettlementQueryRepository` 模式）。

## 2. 数据模型

> **本规格不新增表 / 实体 / 迁移**。复用 `019` / `012` / `013` / `015` / `016` / `021` / `022` 既有表。
> 读模型为新增（`App.Core/Abstractions/`，`sealed record` + `required` + `init`，后端规则 §4.3）。

| 读模型 | 字段 | 用途 |
|---|---|---|
| `InventoryFlowItem` | `ProductId` / `Code` / `Name` / `CategoryName` / `Unit` / `OpeningQuantity` / `InboundQuantity` / `OutboundQuantity` / `ClosingQuantity` | F1 进销存报表行 |
| `StockBalanceItem` | `CategoryId` / `CategoryName` / `ProductCount` / `TotalQuantity` / `ZeroStockCount` / `BelowSafetyCount` | F2 库存余额表行（分类聚合） |
| `PurchaseSummaryItem` | `Key`（`Guid?`，往来或商品 id）/ `Name` / `Unit`? / `OrderCount` / `InboundQuantity` / `InboundAmount` / `ReturnQuantity` / `ReturnAmount` | F3 采购汇总行 |
| `SalesSummaryItem` | 同上 | F4 销售汇总行 |
| `InventoryFlowTotal` | `OpeningQuantity` / `InboundQuantity` / `OutboundQuantity` / `ClosingQuantity` | F1 合计行 |
| `StockBalanceTotal` / `PurchaseSummaryTotal` / `SalesSummaryTotal` | 各自行的字段合计（汇总合计不含净额，净额在出参层由 Mapper 计算） | F2 / F3 / F4 合计行 |

- `ClosingQuantity` 由仓储计算（`期初 + 入 − 出`），保证与单测断言一致；`BelowSafetyCount` 口径 = `Products.SafetyStock > 0 AND Inventory.Quantity < Products.SafetyStock`（与 `014` 同口径，仅统计启用商品）。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/IReportQueryRepository.cs`）

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<InventoryFlowItem> Items, int Total, InventoryFlowTotal Total2)> GetInventoryFlowAsync(DateTimeOffset start, DateTimeOffset end, Guid? productId, Guid? categoryId, bool onlyChanged, int page, int pageSize, ...)` | 期初（`CreatedAt < start` 聚合）+ 区间入 / 出（`start <= CreatedAt < end`，`StockTakeAdjust` 按符号拆分）两段查询按 `ProductId` 合并；`onlyChanged` 为真时只返回期间有变动的商品；按 `Code` 升序；合计行对**全量筛选结果**聚合（不分页） |
| `Task<(IReadOnlyList<StockBalanceItem> Items, int Total)> GetStockBalanceAsync(string? keyword, Guid? categoryId, int page, int pageSize, ...)` | 按分类聚合：商品数（启用商品）/ 库存合计 / 零库存数 / 低库存数；按分类名升序；同时返回全库合计（由 Handler 单独查询一次或并入 `Total` 元组） |
| `Task<(IReadOnlyList<PurchaseSummaryItem> Items, int Total)> GetPurchaseSummaryAsync(DateTimeOffset start, DateTimeOffset end, Guid? partnerId, bool groupByProduct, int page, int pageSize, ...)` | 采购入库 / 采购退货按分组维度聚合（`status = Normal` 过滤），合并为净额行 |
| `Task<(IReadOnlyList<SalesSummaryItem> Items, int Total)> GetSalesSummaryAsync(...)` | 同采购，销售口径 |

- 实现 `App.Infrastructure/Repositories/ReportQueryRepository.cs`：EF Core 投影 + `GroupBy`（不写裸 SQL，后端规则 §5.1）；只读查询一律 `AsNoTracking`。
- 本规格**不新增仓储之外的写路径**：既有的 `IInventoryRepository` / `IStockMovementRepository` / 各单据仓储均不改动。

### 3.2 错误码

> **无新增错误码**。4 个接口均为只读查询，正常路径无业务失败；参数不合法统一走全局 `40000`，无数据返回空列表（不返回 `40400`）。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/reports/inventory-flow` | GET | `Reports/GetInventoryFlow` | `{ items, total, page, pageSize, summary }`（`PagedResult` + `summary` 合计字段，见下） | 40000 |
| `/api/reports/stock-balance` | GET | `Reports/GetStockBalance` | `{ items, total, page, pageSize, summary }` | 40000 |
| `/api/reports/purchase-summary` | GET | `Reports/GetPurchaseSummary` | `{ items, total, page, pageSize, summary }` | 40000 |
| `/api/reports/sales-summary` | GET | `Reports/GetSalesSummary` | `{ items, total, page, pageSize, summary }` | 40000 |

- 分页结构遵循 `AGENTS.md` §4.3；**合计字段**用出参包装类型 `ReportPageDto<TItem, TSummary>`（`items` / `total` / `page` / `pageSize` / `summary`）表达，`summary` 为对应合计模型（不进 `PagedResult<T>`，避免污染全局分页契约）。该类型放 `Features/Reports/`，仅本规格使用。
- 路由注意：`/api/reports/*` 为固定段，无 `{id}` 冲突。

### 3.4 关键用例流程（Handler）

**GetInventoryFlow**：
1. 校验期间（Validator 已保证 `start < end`）。
2. 调仓储 `GetInventoryFlowAsync`；仓储内部两段查询（期初 / 区间）+ 按商品合并 + 计算 `ClosingQuantity`。
3. `ReportsDtoMapper` 映射 DTO；`summary` 由仓储的合计查询结果映射（全量筛选口径，不是当前页）。

**GetStockBalance**：仓储按分类聚合 → Mapper 映射 → `summary` 为全库合计（商品总数 / 库存总量 / 零库存数 / 低库存数）。

**GetPurchaseSummary / GetSalesSummary**：入参 `groupBy`（`partner` / `product`，默认 `partner`）由 Request 表达（`string` 或枚举，取 `SummaryGroupBy` 枚举，`smallint` 不落库）；仓储按维度聚合单据 + 明细，退货侧左连接合并（无退货的分组退货列为 0）；Handler 计算 `NetAmount = InboundAmount − ReturnAmount` 与 `NetQuantity`。

**GetStockBalance / 汇总**：只读，无事务。

### 3.5 校验规则（FluentValidation，仅格式层，引用既有常量）

| 请求 | 规则 |
|---|---|
| `GetInventoryFlowRequest` | `start` / `end` 必填且 `start < end`；`productId` / `categoryId` 可空；`onlyChanged` 可空（默认 `false`）；`page ≥ 1`；`pageSize` 1–100 |
| `GetStockBalanceRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（引用 `ProductFieldConstraints.KeywordMaxLength`，对齐 `Products.Code`(32) / `Name`(50) 的匹配列长）；`categoryId` 可空 |
| `GetPurchaseSummaryRequest` / `GetSalesSummaryRequest` | `start` / `end` 必填且 `start < end`；`partnerId` 可空；`groupBy` 为合法枚举值（默认 `partner`）；`page ≥ 1`；`pageSize` 1–100 |

- 期间长度上限：一次查询最多 **366 天**（`40000`）——防止误传超大区间拖垮聚合；上限常量放 `Features/Reports/ReportFieldConstraints.cs`（本域新增，无既有同源常量可引用）。
- 存在性（商品 / 分类 / 往来是否存在）不校验：查询类接口按「条件不命中则空列表」处理（同 `019`）。

### 3.6 Swagger

- **不分组**（同既有约定）：4 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── report.ts                     # 报表接口层（4 个查询 + 类型）
└── views/
    └── ReportManagement/
        ├── InventoryFlowReportView.vue      # 进销存报表
        ├── StockBalanceReportView.vue       # 库存余额表
        ├── PurchaseSummaryReportView.vue    # 采购汇总
        └── SalesSummaryReportView.vue        # 销售汇总（`026` 另增 CostProfitReportView.vue）
```

- 「报表」为独立功能域（对齐后端 `Features/Reports`、路由前缀 `reports`、e2e `report.spec.ts`、菜单分组「报表」四者一致，前端规则 §4.1）。

### 4.2 接口层

- `src/api/report.ts`：类型与后端 DTO 一一对应（含 `ReportPage<T>` 泛型出参）；函数 `getInventoryFlow` / `getStockBalance` / `getPurchaseSummary` / `getSalesSummary`。
- 日期区间参数：`a-range-picker` 选值 → 接口层转**本地当天 00:00:00（起）与结束日次日 00:00:00（止）** 的 UTC ISO 串（半开区间，见 §0.1）；默认区间为「本月 1 日 ~ 今天」。
- 金额展示统一 `toFixed(2)`；数量为整数。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `reports/inventory-flow` | `inventoryFlowReport` | `InventoryFlowReportView` |
| `reports/stock-balance` | `stockBalanceReport` | `StockBalanceReportView` |
| `reports/purchase-summary` | `purchaseSummaryReport` | `PurchaseSummaryReportView` |
| `reports/sales-summary` | `salesSummaryReport` | `SalesSummaryReportView` |

- `AppLayout.vue` 按 §0.2 总表**重构为多顶级分组**（新增「基础档案」「采购」「销售」「库存」「资金」「报表」「系统」，迁移既有子项）；`MENU_ROUTE_MAP` 增加 `inventoryFlowReport` 等无详情页映射（无详情页，不新增映射项）。
- `e2e/helpers/menu.ts` 的「菜单项 → 分组」映射同步为 §0.2 的分组名。

### 4.4 页面交互

**进销存报表 `InventoryFlowReportView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：期间范围（`a-range-picker`，必填，默认本月）、商品下拉（`getProductPickList`，可空）、分类下拉（`getCategories`，可空）、「只看有变动」（`a-checkbox`）、搜索 / 重置。
- 操作行：导出（`027-erp-export` 交付后启用：`IconDownload` + `exporting`，导出当前筛选全量的 xlsx 且含与页面一致的合计行；交付前的 `:disabled` 占位已移除）、刷新、列设置。
- 表格列（每列设 `width`）：序号、商品编码、商品名称、分类、单位、**期初数量**、**期间入**（绿字）、**期间出**（红字）、**期末数量**（粗体）；`row-key` 用 `productId`；服务端分页；`showTotal` + `showPageSize`（`pageSizeOptions` 同 §0 约定）。
- 合计区：表格上方一行统计（`a-statistic` 或描述行）：期初合计 / 入合计 / 出合计 / 期末合计（取 `summary`，为全量筛选结果口径，标注「全量」）。

**库存余额表 `StockBalanceReportView.vue`**：筛选（分类、关键词）；列：序号、分类、商品数、库存合计、零库存商品数、低库存商品数、库存占比（`a-progress` 或百分比文本）；操作列 1 个「查看明细」（`IconListDetails`）→ `router.push({ name: 'inventory', query: { categoryId } })`（`014` 页面已支持分类筛选）。

**采购 / 销售汇总 `PurchaseSummaryReportView.vue` / `SalesSummaryReportView.vue`**：筛选（期间、往来单位、分组维度 `a-radio-group`：往来单位 / 商品）；列：序号、分组名称（往来单位或商品）、单据数、出 / 入数量、出 / 入金额、**退货数量 / 金额**、**净数量 / 净金额**（净额列加粗）；合计行；服务端分页。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 报表查询（4 页） | `loading` | 搜索 / 翻页 + 表格 |
| 「查看明细」跳转 | 不置 loading | 同步路由跳转 |
| 导出（`027` 交付后） | `exporting` | 导出按钮 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 报表数据源 = 流水 + 单据 | 不落物化表 | 与 `019` 对账口径同源（`Σ 流水 == Inventory.Quantity`）；避免第二套库存口径漂移；单组织数据量下实时聚合可接受 |
| 期初用「起点前累计」而非快照 | `SUM(Quantity) WHERE CreatedAt < start` | 无需变动前后结存列（`019` §5 已定不落结存）；一次性聚合成本低 |
| 期间为半开区间 | `start <= t < end` | 相邻区间拼接无重叠、无遗漏；前端传「结束日次日 00:00」 |
| `StockTakeAdjust` 按符号拆分 | 双向计入 | 单条调整可能是盘盈或盘亏；整条计入一侧会让入 / 出列失真 |
| 库存余额表只做分类聚合 | 明细仍走 `014` | 避免与 `014` 重复建设：`014` 是「逐商品操作视图（含下钻）」，`025` 是「分类汇总视图（占比 / 低库存集中度）」；本规格不重复列明细 |
| 库存金额列延后 | 由 `026` 追加 | 金额依赖成本单价，`026` 落地时在本规格 §0 与页面追加列（演进注记） |
| 合计行为全量口径 | `summary` 独立字段 | 分页页内的合计会误导用户；合计必须是筛选结果全量 |
| 只读查询独立仓储 | `IReportQueryRepository` | 跨表聚合不属于任何单据仓储；同 `023` 的 `ISettlementQueryRepository` 模式 |
| 期间上限 366 天 | 常量约束 | 防误传超大区间；如需年度以上报表，后续按需放开 |
| 菜单改多顶级分组（不嵌套） | 顶级分组替代二级嵌套 | 进销存分组已超 10 项（`024` 预告）；Arco 多级子菜单的 `openKeys` 与 e2e 定位复杂度高，顶级分组改动可控且导航更扁平 |
| 无 RBAC | 登录即可见「报表」分组 | 同既有功能（`028` 落地时把报表纳入权限点，本规格页面在 `028` 的权限点清单中登记） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **GetInventoryFlow**：筛选传参组合（期间 / 商品 / 分类 / `onlyChanged` / 分页）断言；`ClosingQuantity = 期初 + 入 − 出`；`summary` 为全量口径（与当前页无关）；空结果返回空列表且 `total = 0`。
- **GetStockBalance**：分类聚合映射（商品数 / 合计 / 零库存 / 低库存）；低库存口径与 `014` 同源（`SafetyStock > 0 && Quantity < SafetyStock`）；`summary` 全库合计。
- **GetPurchaseSummary / GetSalesSummary**：`groupBy` 两个维度各一例；净额 = 入 − 退；无退货分组退货列为 0；作废过滤在仓储层（Handler 不重复过滤，断言透传）。
- **Validator 边界**：`start < end` 边界（相等拒绝、366 天内通过、367 天拒绝）；`pageSize` 100 / 101；`keyword` 50 / 51（对齐 `ProductFieldConstraints.KeywordMaxLength`）。
- **对账一致性**：以既有 `019` 的「流水 → 库存」行为型假实现构造「期初 + 采购 + 销售 + 盘点」链路，断言报表 `期末 == Inventory.Quantity`（本规格的核心口径用例）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`ReportFieldConstraints.MaxRangeDays` 生效于 4 个请求的 Validator（同源一致）；`keyword` 上限 == `ProductFieldConstraints.KeywordMaxLength`。

## 7. 演进（erp-cost，`026`）

- 库存余额表追加**库存金额 / 均价 / 成本异常**列与合计：`StockBalanceItem` 由 `026` 扩展 `TotalCostAmount` / `AverageCost` / `HasCostAnomaly`，页面 `StockBalanceReportView.vue` 追加对应列（成本异常以 `a-tag` 标注）。
- 成本毛利报表（`CostProfitReportView`，路由 `reports/cost-profit`）由 `026` 独立交付；毛利 = 收入 − 成本、毛利率口径以 `specs/026-erp-cost/design.md` §0.3 为准。
- 本规格 §5 已预告「库存金额列延后，由 `026` 追加」，本演进即落实该注记。

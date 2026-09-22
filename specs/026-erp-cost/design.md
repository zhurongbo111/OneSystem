---
created: 2026-09-17
updated: 2026-09-22
---

# 设计规格：成本核算与销售毛利（erp-cost）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-stock-take`（单据 + 库存 + 流水同事务）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格**改造既有写入路径**（`019` / `020` / `021` / `022` 的库存写入点加成本），改造限于「同一事务内追加成本更新」，不改变数量语义与业务判定。
> **演进（erp-rbac）**：成本重算动作接入权限校验，权限点 `costs.recalculate`（该点**不在**内置 `Staff` 默认权限内，见 `specs/028-erp-rbac/design.md` §0.3）；成本报表页操作行按钮由前端按权限过滤。清单唯一来源见其 §0.2。
> **演进（erp-audit-log）**：成本重算（`RecalculateCosts`，动作 `Recalculate`，无明确业务对象 → `ResourceId` 为空）已接入操作日志（`specs/029-erp-audit-log/design.md` §0.1）。
> **演进（erp-general-ledger）**：本规格的**移动加权成本**是 `033` 销售出库自动凭证「成本结转分录」的唯一来源（成本额在写路径现场取流水单价，与成本报表同源）；`033` 利润表口径与本规格「成本与毛利」报表同源。口径与勾稽见 `specs/033-erp-general-ledger/design.md` §2.3 / §2.4。

## 0. 成本口径约定（唯一事实源）

### 0.1 计价方法与公式（移动加权平均）

| 概念 | 规则 |
|---|---|
| 结存成本额 | `Inventory.CostAmount`（`numeric(18,4)`，可负=成本异常） |
| 移动加权平均单价 | `Inventory.AverageCost`（`numeric(18,4)`）= `CostAmount / Quantity`（`Quantity = 0` 时保留最后值，见下） |
| 入库（数量增加） | `CostAmount += Quantity × UnitCost`；随后 `AverageCost = CostAmount / Quantity`（**先加数量再加金额**，否则均价基数错） |
| 出库（数量减少） | `出库 UnitCost = 变动前 AverageCost`；`CostAmount −= TotalCost`；**均价不变**（按均价出库不改变均值） |
| 流水成本列 | `StockMovements.UnitCost`（本次变动单价，`numeric(18,4)`）、`TotalCost`（本次变动成本金额，`numeric(18,4)`，与 `Quantity` 同号） |
| 精度与舍入 | 存储 4 位小数；计算统一 `Math.Round(x, 4, MidpointRounding.AwayFromZero)`（财务惯例，非 .NET 默认的银行家舍入）；**展示**为金额 2 位 / 单价 2~4 位 |
| 结存数量为 0 | `CostAmount` 归 0（消除尾差）、`AverageCost` 保留最后均价（供下次入库前展示与兜底），下次入库按归零后的金额重新加权 |
| 结存数量为负 | 允许（`021` / `022` 作废回冲可冲负）：`AverageCost` 仍按 `CostAmount / Quantity` 计算，标记 `hasCostAnomaly = Quantity < 0 || CostAmount < 0`，报表标红提示（不做自动修复） |
| 缺价兜底 | 历史流水中「单价无法推算」（如 `026` 上线前建立的期初）按 0 计入，并在重算响应与报表中计数提示，由人工重开盘点调整修正 |

### 0.2 成本单价来源表（按变动类型）

`019` design §0 的变动类型表在成本侧的续行；`038` / `039` / `040` 追加类型时在本表续行：

| 变动类型 | 方向 | `UnitCost` 来源 |
|---|---|---|
| `PurchaseInbound`(1) | 入 | 采购入库单明细 `UnitPrice`（该单据该商品行） |
| `PurchaseVoid`(2) | 出 | **被作废采购单原入库流水的 `UnitCost`**（同一 `SourceId` + `ProductId` 的 `PurchaseInbound`）——作废必须原样回冲，用当前均价冲销会让金额漂移 |
| `SalesOutbound`(3) | 出 | 变动前 `AverageCost` |
| `SalesVoid`(4) | 入 | 被作废销售单原出库流水的 `UnitCost`（原样回冲） |
| `InitialStock`(5) | 入 | 期初建账录入的 `StockTakeItems.UnitCost` |
| `StockTakeAdjust`(6) | 双向 | 变动前 `AverageCost`（盘盈 / 盘亏同）；无均价（首次变动）时按 0 并在响应中计缺价 |
| `PurchaseReturnOut`(7) | 出 | 变动前 `AverageCost` |
| `PurchaseReturnVoid`(8) | 入 | 该退货单原出库流水的 `UnitCost`（原样回冲） |
| `SalesReturnIn`(9) | 入 | 被退销售单原出库流水的 `UnitCost`（按原销售成本退回）；查不到时兜底按当前 `AverageCost` |
| `SalesReturnVoid`(10) | 出 | 该销售退货原入库流水的 `UnitCost`（原样回冲） |

> 原则：**「冲销类」一律复用原方向的成本单价**（保证「入 → 冲回」金额净额为 0），**「新增交易类」按均价或录入价**。

### 0.3 销售毛利口径

| 指标 | 口径 |
|---|---|
| 销售收入 | 未作废 `SalesShipments.TotalAmount` 之和 − 未作废 `SalesReturns.TotalAmount` 之和（与 `025` §0.1 销售净额同源） |
| 销售成本 | 上述单据对应流水的 `TotalCost` 之和（出库为负、退货入库为正，天然冲减） |
| 毛利 | `销售收入 − 销售成本` |
| 毛利率 | `销售收入 = 0` 时为 `null`（前端显示 `-`）；否则 `毛利 / 销售收入`（按 4 位计算、展示 2 位百分比） |
| 成本缺失标注 | 期间内存在 `UnitCost` 为空或 0 的出库流水时，该行 / 合计标注「成本不完整」 |

## 1. 总体设计

```
成本写入（既有路径改造，无新增写接口）
  采购 / 销售 / 退货 / 盘点 Handler
    → IUnitOfWork 事务内：IInventoryRepository 数量原子增减（既有）
                          + 成本读 / 写（新增：GetAverageCostAsync / ApplyInboundCostAsync / ApplyOutboundCostAsync）
                          + IStockMovementRepository.AppendAsync（`019` 既有，追加 UnitCost / TotalCost）
    → PostgreSQL（Inventory / StockMovements / 单据表，同一事务）

成本重算（运维 / 初始化）
  POST /api/costs/recalculate → Costs/RecalculateCosts
    → IStockMovementRepository.GetAllForCostAsync（按时间顺序）
    → 内存推演结存（数量 / 金额 / 均价）→ 批量写回流水成本列 + Inventory 成本列

成本与毛利报表（前端 /reports/cost-profit）
  → Reports/GetCostProfitReport → IReportQueryRepository（`025` 既有仓储追加方法）
```

核心原则：

- **成本随数量走**：任何改变 `Quantity` 的地方都在同一事务内同步成本，不出现「有数量无成本」；「Σ 流水 `TotalCost` == `Inventory.CostAmount`」为本规格的对账恒等式（单测守护，同 `019` 的数量口径）。
- **均价是派生值**：`AverageCost` 只由 `CostAmount / Quantity` 派生，不作为独立事实源（避免两处不一致）；保留列只是为了查询与展示免二次计算。
- **冲销还原成本**：作废 / 退货作废一律取原方向流水的成本单价，保证「入 + 冲回 = 0」。
- **重算幂等**：重算完全按流水推演，与当前 `Inventory` 成本列无关，可反复执行。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；金额统一 `numeric(18,4)`（存储）/ 展示 2 位。

### 2.1 既有表改造

| 表 | 新增列 | 说明 |
|---|---|---|
| `Inventory` | `CostAmount`（`numeric(18,4)`，NOT NULL，默认 0） | 结存成本额 |
| | `AverageCost`（`numeric(18,4)`，NOT NULL，默认 0） | 当前移动加权平均单价（派生值） |
| `StockMovements` | `UnitCost`（`numeric(18,4)`，NOT NULL，默认 0） | 本次变动成本单价 |
| | `TotalCost`（`numeric(18,4)`，NOT NULL，默认 0） | 本次变动成本金额（与 `Quantity` 同号） |
| `StockTakeItems` | `UnitCost`（`numeric(18,4)`，NOT NULL，默认 0） | 期初建账成本单价（盘点模式下为 0，不参与成本） |

- 实体同步：`Inventory` / `StockMovement` / `StockTakeItem` 追加对应属性；`AverageCost` 与 `CostAmount` 标注为派生 / 保守更新（后端规则 §5.3 字段约束：本规格**不新增常量类**，金额上界复用 `ProductFieldConstraints.PriceMaxValue`）。
- EF 配置：列类型 `numeric(18,4)`；`StockMovementConfiguration` 追加成本列（无索引需求）；`InventoryConfiguration` 追加两列。

### 2.2 迁移

- 迁移：`dotnet ef migrations add AddErpCost -p src/App.Infrastructure -s src/App.Api`（增量迁移）。
- 迁移内**不做历史成本回填**（移动加权需要按时序推演，SQL 无法表达）：历史数据由上线后执行一次「成本重算」（§3.4）补齐；迁移后所有新写入即有成本。

### 2.3 读模型（`App.Core/Abstractions/`）

| 读模型 | 字段 | 用途 |
|---|---|---|
| `CostProfitItem`（新增） | `Key` / `Name` / `SalesQuantity` / `SalesAmount` / `CostAmount` / `GrossProfit` / `GrossProfitRate` / `HasMissingCost` | F5 成本与毛利报表行 |
| `StockBalanceItem`（`025` 追加字段） | `TotalCostAmount` / `AverageCost` / `HasCostAnomaly` | 库存余额表金额列 |
| `StockMovementItem`（`019` 追加字段） | `UnitCost` / `TotalCost` | 库存流水页追加成本列（可选展示，见 §4.4） |

## 3. 后端设计

### 3.1 仓储接口

`IInventoryRepository`（`012` 定义，本规格**追加** 3 个方法）：

| 方法 | 说明 |
|---|---|
| `Task<decimal> GetAverageCostAsync(Guid productId, ...)` | 读当前移动加权平均单价（无库存行返回 0） |
| `Task ApplyInboundCostAsync(Guid productId, int quantity, decimal unitCost, ...)` | 入库成本：`CostAmount += Round(quantity × unitCost, 4)`，`AverageCost = CostAmount / Quantity`（`ExecuteUpdateAsync` 原子表达，无裸 SQL） |
| `Task ApplyOutboundCostAsync(Guid productId, decimal totalCost, ...)` | 出库成本：`CostAmount -= totalCost`；`AverageCost` 不变；数量为 0 时 `CostAmount` 归 0 |

`IStockMovementRepository`（`019` 定义，本规格**追加** 2 个方法）：

| 方法 | 说明 |
|---|---|
| `Task<decimal?> GetMovementUnitCostAsync(Guid sourceId, Guid productId, StockMovementType type, ...)` | 取某来源单据 + 商品的指定类型流水的 `UnitCost`（冲销类还原成本用） |
| `Task<IReadOnlyList<StockMovementCostRow>> GetAllForCostAsync(Guid? productId, DateTimeOffset? start, DateTimeOffset? end, ...)` | 成本重算用的全量流水行（`Id` / `ProductId` / `MovementType` / `Quantity` / `SourceId` / `SourceNo` / `CreatedAt` / 关联单价来源信息），按 `CreatedAt, Id` 升序 |

- 单价来源解析（§0.2）在 **Handler** 内完成（业务判定，后端规则 §4.1）；仓储只提供「取原成本」「取流水行」两类数据能力。
- `StockMovementCostRow` 为读模型（`sealed record` + `required` + `init`），仅重算用例使用。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40118 | `CostRecalculationRunning` | 成本重算正在进行，请稍后重试（并发拒绝） |

> 期初成本单价的必填 / 区间校验属格式层（Validator，`40000`），不占业务码；`40400` / `40000` 复用全局。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/costs/recalculate` | POST | `Costs/RecalculateCosts` | `CostRecalculateResultDto { movementCount, missingCostCount, productCount }` | 40000 / 40118 |
| `/api/reports/cost-profit` | GET | `Reports/GetCostProfitReport` | `ReportPageDto<CostProfitListItemDto>`（`025` 既有包装类型） | 40000 |

> 本规格的**写入路径改造**不新增接口（`POST /api/stock-takes`、`POST /api/purchase-orders` 等既有端点不变，仅内部加成本）。

### 3.4 关键用例流程（Handler）

**RecalculateCosts**（重算 / 初始化）：

1. 获取重算锁（Singleton `SemaphoreSlim` 或等价内存锁），已在执行 → `40118`；`try/finally` 释放。
2. `GetAllForCostAsync(productId?, start?, end?)` 取流水（按 `CreatedAt, Id` 升序）；按商品分组推演：
   - 维护结存 `qty` / `amount`（结存为 0 时 `amount` 归 0）；逐条按 §0.2 解析单价（需 `GetMovementUnitCostAsync` 反查原方向成本时按商品一次性预取，避免 N+1）→ 计算 `UnitCost` / `TotalCost` → 累积。
3. `IUnitOfWork`：`BeginTransactionAsync` → 逐条更新流水成本列（`UpdateCostAsync` / 批量 `ExecuteUpdateAsync`）→ 按商品写回 `Inventory.CostAmount` / `AverageCost` → `CommitAsync`。
4. 返回统计：`movementCount`（重算流水数）、`missingCostCount`（单价无法推算条数）、`productCount`。
5. **不改变任何 `Quantity`**（`019` 的数量口径不受影响）。

**GetCostProfitReport**：仓储按分组维度聚合「销售收入（单据）/ 销售成本（流水 `TotalCost`）/ 成本缺失标记」→ Handler 计算 `GrossProfit` / `GrossProfitRate` → Mapper 映射。

**既有写入路径改造**（同一事务内追加成本，改造点在 `019` §3.7 的四个写入表 + `020` / `021` / `022`）：

| 用例 | 追加动作（在既有 `IncrementAsync` / `TryDecrementAsync` 之后） |
|---|---|
| `PurchaseReceipts/CreatePurchaseReceipt` | 每行 `ApplyInboundCostAsync(productId, qty, 单据明细 UnitPrice)`；流水写 `UnitCost = 单价` / `TotalCost = Round(qty × 单价)` |
| `PurchaseReceipts/VoidPurchaseReceipt` | 每行 `GetMovementUnitCostAsync(单据 id, productId, PurchaseInbound)` 取原单价 → `ApplyOutboundCostAsync(productId, Round(qty × 原单价))`；流水写原单价与负金额（查不到 → 缺价，按 0 并计缺价） |
| `SalesShipments/CreateSalesShipment` | 每行先 `GetAverageCostAsync` 取均价 → `ApplyOutboundCostAsync(productId, Round(qty × 均价))`；流水写均价与负金额 |
| `SalesShipments/VoidSalesShipment` | 每行取原出库流水单价 → `ApplyInboundCostAsync(productId, qty, 原单价)`；流水写正金额 |
| `StockTakes/CreateStockTake` | 期初（`InitialStock`）：`ApplyInboundCostAsync(productId, qty, 录入 UnitCost)`；盘点（`StockTakeAdjust`）：按当前均价（`GetAverageCostAsync`），盘盈 `ApplyInboundCostAsync`、盘亏 `ApplyOutboundCostAsync` |
| `PurchaseReturns/CreatePurchaseReturn` | 每行按当前均价 → `ApplyOutboundCostAsync`；流水写均价与负金额 |
| `PurchaseReturns/VoidPurchaseReturn` | 每行取原出库流水单价 → `ApplyInboundCostAsync` |
| `SalesReturns/CreateSalesReturn` | 每行取被退销售单原出库流水单价（查不到兜底均价）→ `ApplyInboundCostAsync`；流水写正金额 |
| `SalesReturns/VoidSalesReturn` | 每行取原入库流水单价 → `ApplyOutboundCostAsync`；流水写负金额 |

- 成本操作与数量操作**同一事务**、同一 `IUnitOfWork`；业务失败路径不写成本（与 `019` 的流水约束一致）。
- 数量为 0 或负的场景按 §0.1 处理，不抛业务错误（成本异常仅标注不阻断，避免把库存数据异常升级为不可开单）。

### 3.5 校验规则（FluentValidation，仅格式层，引用既有常量）

| 请求 | 规则 |
|---|---|
| `CreateStockTakeRequest`（`020` 既有请求，本规格扩展） | `type = Initial(0)`：每行 `unitCost` 必填、0 ~ 9999999.99（`ProductFieldConstraints.PriceMinValue/PriceMaxValue`）；`type = Take(1)`：每行 `unitCost` 必须为空（传入即 `40000`，避免误传入库成本） |
| `RecalculateCostsRequest` | `productId` 可空；`start` / `end` 可空且同时提供时 `start <= end`；区间上限 366 天（引用 `Features/Reports/ReportFieldConstraints.MaxRangeDays`，`025` 定义） |
| `GetCostProfitReportRequest` | `start` / `end` 必填且 `start < end`（半开区间，`025` §0.1）；`productId` / `categoryId` 可空；`groupBy` ∈ {`order`, `product`, `partner`}（默认 `order`）；`page ≥ 1`；`pageSize` 1–100 |

- 存在性 / 成本可得性等业务约束在 Handler（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（同既有约定）：2 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── cost.ts                       # 成本接口层（重算 + 成本毛利查询；报表域文件见下）
└── views/
    └── ReportManagement/
        ├── CostProfitReportView.vue  # 成本与毛利报表（归「报表」分组）
        └── …                         # 期初成本录入改造在 StockTakeManagement/StockTakeFormPage.vue
```

- 接口归属决策（前端规则 §3 要求写明）：**成本重算（`recalculateCosts`）单独放 `api/cost.ts`**（运维动作，非报表查询）；**成本与毛利查询**并入 `api/report.ts`（与 `025` 的 4 个报表同域、同页面目录、共用 `ReportPage<T>` 出参）。
- 期初成本录入改造在既有 `views/StockTakeManagement/StockTakeFormPage.vue`（不新建文件）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `reports/cost-profit` | `costProfitReport` | `CostProfitReportView` |

- `AppLayout.vue`「报表」分组追加子项「成本与毛利」（`025` §0.2 总表已预留 `costProfitReport` 行，本规格启用）。

### 4.3 页面交互

**成本与毛利报表 `CostProfitReportView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：期间范围（必填，默认本月）、商品下拉、分类下拉、分组维度（`a-radio-group`：单据 / 商品 / 往来单位）、搜索 / 重置。
- 操作行：**「重算成本」**（`a-button`，`IconCalculator` + `a-popconfirm`「重算将按流水顺序重新计算全部成本，期间不要开单，确认继续？」+ `recalculating` loading）、导出（`027-erp-export` 交付后启用：`IconDownload` + `exporting`，导出当前筛选全量且含合计行；交付前的 `:disabled` 占位已移除）、刷新、列设置。
- 表格列：序号、分组名称、销售数量、销售收入、销售成本、**毛利**（负数红字）、毛利率、成本完整性（`HasMissingCost` → `a-tag warning`「成本不完整」，否则 `-`）；合计行（`summary`，全量口径）。
- 重算成功后展示结果提示（`Message.success('重算完成：流水 N 条，缺价 M 条')`），缺价条数 > 0 时改用 `Message.warning` 并提示「请补充期初成本或执行盘点调整」。

**期初建账页改造 `StockTakeFormPage.vue`**：

- 明细子表格在**期初建账**模式下新增列「成本单价」（`a-input-number :min="0" :precision="4"`，必填）与「期初金额」（computed = 实盘数量 × 成本单价）；切换到「库存盘点」模式时隐藏成本单价列并清空已填值（避免误传）。
- 页头提示：期初成本单价是库存成本的基线，未填无法计算毛利。

**库存余额表（`025` 页面）追加列**：库存金额（`TotalCostAmount`，2 位）、均价（`AverageCost`）、成本异常标记（`HasCostAnomaly` → `a-tag danger`「成本异常」）；合计行追加金额合计。

**库存流水页（`019` 页面）追加列**：成本单价、成本金额（可关闭的列，默认展示；列为只读展示，无操作）。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 成本报表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 重算成本 | `recalculating` | 操作行按钮 + 防重入 |
| 期初建账提交 | `submitting` | 提交按钮（既有） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 计价方法 = 移动加权平均 | 唯一方法 | 进销存场景最常用、无需批次 / 序列号即可计算；FIFO 需要批次粒度（`040`），个别计价需要逐件（不做） |
| 成本落在流水上 | `StockMovements.UnitCost` / `TotalCost` | 毛利需要「按单据 / 商品汇总出库成本」；落流水后与 `019` 的对账口径同源（`Σ TotalCost == Inventory.CostAmount`），报表无需联查单据明细 |
| `AverageCost` 作为派生列保留 | 由 `CostAmount / Quantity` 派生 | 查询与展示免二次计算；写入侧只在 `ApplyInboundCostAsync` 同步更新，禁止其他路径直接改（避免第二事实源） |
| 冲销类复用原成本单价 | 作废 / 退货作废取原方向流水 `UnitCost` | 保证「入 + 冲回 = 0」；若用当前均价冲销，加权均价会因冲销而漂移，历史成本失真 |
| 出库按均价、均价不变 | `ApplyOutboundCostAsync` 不动 `AverageCost` | 数学上按均价出库不改变均值，减少一次除法与舍入误差 |
| 结存为 0 时金额归零、均价保留 | 消尾差 | 否则长期小额尾差会残留（如 0.0001），导致均价虚假；保留均价便于展示与兜底 |
| 库存为负不阻断、只标注 | `HasCostAnomaly` | 负库存是 `021` / `022` 作废回冲的既定行为（`012` §5）；成本异常应可见而非阻断业务 |
| 提供重算而非迁移回填 | `POST /api/costs/recalculate` | 移动加权必须按时序推演，SQL 迁移无法表达；重算幂等，同时兼作运维修复手段 |
| 重算并发拒绝用内存锁 | `40118` | 单实例部署（当前形态）足够；不引入数据库 advisory lock（跨实例场景另议） |
| 期初必须录入成本 | Validator 必填 | 期初是成本基线，缺价会让后续所有均价失真；历史已建期初由重算兜底并计数提示 |
| 成本报表归「报表」分组 | 与 `025` 同域 | 成本与毛利是报表口径，不是操作页；重算按钮放在该页操作行（与数据同屏） |
| 无 RBAC | 登录即可见报表与重算入口 | 同既有功能；`028` 落地时「成本重算」纳入按钮级权限（高风险操作） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口（含 `IInventoryRepository` 的成本方法）；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **加权平均链路**（按 §0.1 逐条断言）：期初 10×10 → 入库 10×20（均价 15、金额 300）→ 出库 5（成本 75、均价仍 15、金额 225）→ 采购作废原入库（原单价 20 回冲、金额 25）。
- **冲销还原成本**：`PurchaseVoid` / `SalesVoid` / `PurchaseReturnVoid` / `SalesReturnVoid` 四路径断言取原方向 `UnitCost`；原流水缺失时按 0 并计入缺价。
- **销售退货成本**：`SalesReturnIn` 优先取原出库单价；原流水缺失时兜底当前均价。
- **盘点成本**：盘盈 / 盘亏按当前均价；期初按录入单价；`Difference == 0` 的行不改成本也不写流水（回归 `020` 既有语义）。
- **既有用例扩展**：`019` 的四个写入路径 + `021` / `022` 各两路径，断言「数量操作与成本操作同事务、失败路径不写成本」；`Commit` 抛异常 → `RollbackAsync`。
- **期初成本必填**：`type = Initial` 缺 `unitCost` → `40000`；`type = Take` 传 `unitCost` → `40000`；边界 0 通过 / 9999999.99 通过 / 10000000 拒绝。
- **RecalculateCosts**：按构造流水重算后与逐条写入的结果一致；**幂等**（连续两次结果相同）；并发调用第二次 → `40118`；返回统计（流水数 / 缺价数 / 商品数）正确；不改 `Quantity`。
- **GetCostProfitReport**：毛利 = 收入 − 成本；毛利率在收入为 0 时为 `null`；退货冲减；成本缺失标注；`groupBy` 三个维度映射。
- **对账一致性**：`Σ 流水 TotalCost == Inventory.CostAmount`（行为型假实现累计断言，覆盖含作废与退货的链路）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`unitCost` 边界与 `ProductFieldConstraints.PriceMaxValue` 同源；`Inventory.CostAmount` / `StockMovements.UnitCost` EF 精度 `numeric(18,4)` == 规格口径（新增精度断言辅助）。

---
created: 2026-09-16
updated: 2026-09-22
---

# 设计规格：销售退货（erp-sale-return）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> **本规格继承 `specs/021-erp-purchase-return/design.md` §0「退货单域共用约定」**：`SalesReturns` / `SalesReturnItems` 与 `PurchaseReturns` / `PurchaseReturnItems` 结构完全同构，实现时照抄 `erp-purchase-return` 模板并按 §1 替换规则做差异替换；共用枚举、常量、仓储方法结构、校验结构、页面结构**均不重复定义**。本规格只定义销售特化差异。
> 变动类型的文案与颜色见 `specs/019-erp-stock-movement/design.md` §0（唯一事实源），本文件不重复该表。
> **演进（`023-erp-settlement` / `024-erp-order-flow` / `026-erp-cost`）**：退货单结算已金额化（`SettledAmount` + 推导状态，手工切换端点移除，已核销禁作废，核销取数只查主表）；其消费的销售出库单表已重命名（`SalesOrders` → `SalesShipments`，路径 `/api/sales-shipments`，前缀 `GI`）；流水写入同时回填成本列。**本正文已按现行为准**，决策与判据见 `specs/023-erp-settlement/design.md` §0 / §3.1.1 / §3.6、`specs/024-erp-order-flow/design.md` §3 与 `specs/026-erp-cost/design.md` §3。
> **演进（erp-rbac）**：本域动作接入权限校验，权限点 `salesReturns.view` / `create` / `void` / `settle` / `export`（`export` 由 `027` 的导出动作标注）；菜单可见性与列表页操作按钮由前端按权限过滤。清单唯一来源见 `specs/028-erp-rbac/design.md` §0.2。

## 1. 相对 erp-purchase-return 的替换规则

| 项 | 采购退货（`021`） | 销售退货（本规格） |
|---|---|---|
| 表 / 实体 | `PurchaseReturns` / `PurchaseReturn`、`PurchaseReturnItems` / `PurchaseReturnItem` | `SalesReturns` / `SalesReturn`、`SalesReturnItems` / `SalesReturnItem` |
| 单号前缀 | `PR` | `SR` |
| 往来方向 | 供应商：`Type in (Supplier, Both)` 且启用 | 客户：`Type in (Customer, Both)` 且启用 |
| 单价带出 | 商品采购价 `PurchasePrice` | 商品销售价 `SalePrice` |
| 库存操作（保存） | 每行 `TryDecrementAsync(quantity)`，失败 `40103` | 每行 `IncrementAsync(+quantity)`（**无上限校验**） |
| 库存操作（作废） | 每行 `IncrementAsync(+quantity)` | 每行 `IncrementAsync(-quantity)`（**允许冲负**） |
| 流水类型 | `PurchaseReturnOut` / `PurchaseReturnVoid` | `SalesReturnIn` / `SalesReturnVoid` |
| 结算语义 | `SettledAmount` + 推导状态；核销方向为**收款** | 同左；核销方向为**付款**（我们退客户钱） |
| 错误码 | 无新增（`40103` 参与） | **无新增**，且 `40103` 不参与（退货入库无库存约束） |
| 用例目录 | `Features/PurchaseReturns/<Action>` | `Features/SalesReturns/<Action>` |
| 接口路由 | `/api/purchase-returns` | `/api/sales-returns` |
| 前端目录 | `views/PurchaseReturnManagement/` | `views/SalesReturnManagement/` |
| 前端特化 | 明细行**退货数量 > 库存标红预警** | 无库存预警（退货增加库存） |

## 2. 总体设计

```
销售退货单（前端 /sales-returns 列表 + /sales-returns/new 开单页 + /sales-returns/detail/:id 详情）
  → SalesReturnsController
    → App.Core/Features/SalesReturns/<Action>/*RequestHandler
      → ISalesReturnRepository（单据 + 明细）+ IPartnerRepository / IProductRepository（校验）
        + IInventoryRepository.IncrementAsync（原子回增）
        + IStockMovementRepository.AppendAsync（写流水，含成本列）
        + IUnitOfWork（同一事务）
        → PostgreSQL（SalesReturns / SalesReturnItems / Inventory / StockMovements）
```

核心原则（同 `021`，仅库存方向相反）：

- **一步式单据**：保存即生效（库存回增 + 应收口径冲减）；不可编辑，只可作废回冲。
- **退货入库无库存约束**：`IncrementAsync` 不设上限（库房能放多少不是库存账的限制）。
- **明细单价快照 / 金额后端重算 / 单号后端生成**：同 `021`。
- **库存与流水同事务**：保存（回增 + 流水）与作废（回冲 + 流水）均在 `IUnitOfWork` 事务内。

## 3. 后端设计

### 3.1 数据模型

- `SalesReturn` / `SalesReturnItem` 实体字段、列类型与约束**完全同 `021` design §2.1 / §2.2**（`ReturnNo` 唯一索引；`PartnerName` 为**客户**名称快照；`ReturnDate`；明细 `ReturnId` 索引；快照字段；`SettledAmount`；`Subtotal` / `TotalAmount` 后端计算）。
- 实体文件：`App.Core/Entities/SalesReturn.cs`、`SalesReturnItem.cs`；枚举 `OrderStatus` **复用**（不新建；`OrderSettlementStatus` 已随 `023` 删除，结算状态由 `SettlementState` 推导）。
- 实体配置：`Persistence/Configurations/SalesReturnConfiguration.cs`、`SalesReturnItemConfiguration.cs`；`AppDbContext` 新增 2 个 `DbSet`。
- 迁移：`AddErpSaleReturn`（建表）+ `AddErpSettlement`（`023`：`SettlementStatus` → `SettledAmount` 并回填历史数据）。
- 字段约束：**不新建常量**，引用 `OrderFieldConstraints`（`ReturnNo` 20 / `Keyword` 20 / `Remark` 200 / `ItemsMaxCount` 100）与 `ProductFieldConstraints`（quantity / unitPrice 边界）。

### 3.2 错误码

> **无新增错误码**。`40103 InsufficientStock` **不参与**本规格（退货回增库存无上限约束）；其余复用 `40104` / `40107` / `40108` / `40109` / `40110` / `40120 OrderSettledCannotVoid`（`023` 定义，本规格作废校验消费）/ `40400` / `40000`。

### 3.3 仓储接口（新增，`App.Core/Abstractions/`）

`ISalesReturnRepository`：方法签名与 `IPurchaseReturnRepository` **完全同构**（`GetPagedAsync`（含 `SettlementState? settlementState` 筛选）/ `GetDetailAsync`（含 `bool includeItems = true`）/ `AddAsync` / `AddSettledAmountAsync` / `UpdateStatusAsync` / `GenerateReturnNoAsync` / `GetItemsByReturnIdsAsync`），仅实体类型不同、单号前缀 `SR`（前缀参数化，照抄实现）。

### 3.4 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/sales-returns` | GET | `SalesReturns/GetSalesReturns` | `PagedResult<SalesReturnListItemDto>` | 40000 |
| `/api/sales-returns` | POST | `SalesReturns/CreateSalesReturn` | `SalesReturnDetailDto` | 40000 / 40107 / 40108 / 40109 / 40110 / 40400 |
| `/api/sales-returns/{id:guid}` | GET | `SalesReturns/GetSalesReturnById` | `SalesReturnDetailDto` | 40400 |
| `/api/sales-returns/{id:guid}/void` | PUT | `SalesReturns/VoidSalesReturn` | `SalesReturnDetailDto` | 40104 / 40120 / 40400 |

> 手工结算端点 `PUT .../settlement`（`UpdateSalesReturnSettlement`）已随 `023` **移除**。

### 3.5 关键用例流程（Handler）

**CreateSalesReturn**：

1. 明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取客户：不存在 → `40400`；`Status = Disabled` → `40108`；`Type` 不含 Customer（纯供应商）→ `40109`。
3. 逐行取商品：不存在 → `40400`；`Status = Disabled` → `40107`；后端重算 `Subtotal = Quantity × UnitPrice`、`TotalAmount = Σ Subtotal`；明细行写 `ProductName` / `Unit` / `UnitPrice` 快照。
4. `IUnitOfWork`：`BeginTransactionAsync` → `GenerateReturnNoAsync("SR", returnDate)` → `AddAsync`（主表 + 明细）→ 逐行 `IncrementAsync(productId, +quantity)`（回增）+ 逐行 `AppendAsync` 流水（`SalesReturnIn`，`Quantity = +quantity`，`SourceId` = 退货单 id、`SourceNo` = `ReturnNo`）→ `CommitAsync`。

> 与 `021` 的顺序差异：采购退货因有库存约束须**先扣减再插单**；销售退货无约束，按「生成单号 → 插单 → 回增库存 + 写流水」顺序执行（同一事务，任一环节失败整体回滚）。

**VoidSalesReturn**：

1. `GetDetailAsync` 取单：不存在 → `40400`；`Status = Voided` → `40104`；`SettledAmount > 0` → `40120`（**不开事务、不动库存与流水**，须先作废对应付款单，`023` §3.6）。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐行 `IncrementAsync(productId, -quantity)`（回冲，**允许冲负**）+ 逐行 `AppendAsync` 流水（`SalesReturnVoid`，`Quantity = -quantity`）→ `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**GetSalesReturns / GetSalesReturnById**：同 `021` 对应 Handler（客户维度筛选 / 映射 / 快照透传；含 `settledAmount` / `unsettledAmount` / `settlementState` 推导）。

### 3.6 校验规则（FluentValidation，仅格式层，引用 §3.1 常量）

| 请求 | 规则 |
|---|---|
| `CreateSalesReturnRequest` | `partnerId`（客户）必填；`returnDate` 必填；`items` 必填非空、1–100 行；每行 `productId` 必填、`quantity` 1–999999、`unitPrice` 0–9999999.99；`remark` ≤ 200 |
| `GetSalesReturnsRequest` | `keyword` ≤ 20；`partnerId` 可空；`settlementState` ∈ {0,1,2}；闭区间 `start <= end`；分页取值按 `AGENTS.md` §4.3（不重复列出） |

### 3.7 流水接入（消费 `erp-stock-movement` 的仓储）

| 动作 | `MovementType` | `Quantity` | `SourceId` / `SourceNo` |
|---|---|---|---|
| 开退货单 | `SalesReturnIn` | `+quantity`（每行一条） | 退货单 `Id` / `ReturnNo` |
| 作废退货单 | `SalesReturnVoid` | `-quantity`（每行一条） | 退货单 `Id` / `ReturnNo` |

- **成本列一并回填**（`026`）：写入流水时填 `UnitCost` / `TotalCost`——销售退货按原销售成本入、作废回冲按原流水单价还原；成本缺失兜底按 0 计入且不阻断，判据见 `specs/026-erp-cost/design.md` §3。退货单页面不展示成本。

### 3.8 Swagger

- **不分组**（唯一来源见 `specs/003-api-swagger/design.md`）：新增接口按现有方式出现在单文档 Swagger 中；已移除的结算端点同步消失。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── saleReturn.ts                # 销售退货接口层
└── views/
    └── SalesReturnManagement/
        ├── SalesReturnsView.vue       # 退货单列表页
        ├── SalesReturnFormPage.vue    # 开退货单独立页（/sales-returns/new）
        └── SalesReturnDetailView.vue  # 详情页（含作废 + 收付款明细）
```

- 页面结构与 `PurchaseReturnManagement/` 完全同构（照抄模板，客户 / 销售价 / 无库存预警三处差异）。

### 4.2 接口层

- `src/api/saleReturn.ts`：TS 类型与后端 DTO（camelCase）一一对应；封装 / 金额 / 日期范围约定同 `021`；列表行类型含 `settledAmount` / `unsettledAmount` / `settlementState`。
- 客户下拉数据源：`src/api/partner.ts` 的 `getPartners`（`status=1`，取 `Type in (2,3)`）；商品下拉数据源：`src/api/product.ts` 的 `getProductPickList`。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `sales-returns` | `salesReturns` | `SalesReturnsView` |
| `sales-returns/new` | `saleReturnNew` | `SalesReturnFormPage` |
| `sales-returns/detail/:id` | `saleReturnDetail` | `SalesReturnDetailView` |

`AppLayout.vue` 侧边菜单追加子项「销售退货」`salesReturns`；`MENU_ROUTE_MAP` 增加 `saleReturnDetail: 'salesReturns'`。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2。

### 4.4 页面交互（与 `PurchaseReturnManagement/` 同构处省略，仅列差异）

- 开单页：表头客户下拉；明细区单价默认带出商品销售价；**不做库存预警**（退货回增库存，无上限）；小计与总金额 computed 展示。
- 列表 / 详情：往来列与筛选为**客户**；其余（结算状态标签文案与颜色取 `specs/023-erp-settlement/design.md` §0、作废行置灰、操作列顺序、收付款明细区块、404 空态）同 `021`；「收付款 / 去收付款」与「作废」判据取 `src/utils/settlement.ts`（`023` §4.4）。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 开单页提交 | `submitting` | 提交按钮 |
| 单据作废（列表 / 详情） | `voidingId` | popconfirm 确认按钮 |
| 详情「收付款明细」 | `loading` | 区块内容区 |

## 5. 关键技术决策与取舍

> 独立单据而非作废原销售单、不关联原单、明细快照、金额后端重算、单号后端生成、不新建字段约束常量、不做原因字典、无 RBAC 等决策**同 `specs/021-erp-purchase-return/design.md` §5**（退货域共用约定）；结算金额化与已核销禁作废取舍见 `023` §5。此处仅列销售特有决策：

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 退货入库无库存上限 | `IncrementAsync(+quantity)` | 库存账不应限制「能放多少」（库容是仓储作业问题，见范围外）；与采购入库同机制 |
| 作废回冲允许冲负 | `IncrementAsync(-quantity)` 不设下限 | 退回入库的货可能已被再次卖出，作废必须可执行（与采购作废回冲对称）；库存为负由库存页标红呈现 |
| 无 `40103` | 退货路径不校验库存 | 退货是回增库存，不存在「不足」语义；避免给出误导性错误码 |
| 提交顺序与采购退货不同 | 生成单号 → 插单 → 回增 + 流水 | 采购退货因库存约束需先扣减（防「单已落库但扣减失败」）；销售退货无约束，顺序不影响一致性，以「单据 id 可用后再写流水」为组织方式 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreateSalesReturn**：
  - 成功：断言单号前缀 `SR` + `returnDate`、明细快照、`Subtotal` / `TotalAmount` 后端重算（前端传值被忽略）、每行 `IncrementAsync(+quantity)` 与流水（`SalesReturnIn`，正方向、来源本单、含成本列）、`Commit` 被调用。
  - 异常：明细空 `40110`；客户不存在 `40400` / 停用 `40108` / 纯供应商 `40109`；商品不存在 `40400` / 停用 `40107`；失败路径**不写流水**、不写库存。
  - 事务：`Commit` 抛异常 → `RollbackAsync` 被调用。
- **VoidSalesReturn**：成功（断言每行 `IncrementAsync(-quantity)` + 流水 `SalesReturnVoid` 负方向 + `UpdateStatusAsync(Voided)`）；已作废 → `40104`；不存在 → `40400`；已核销（`SettledAmount > 0`）→ `40120` 且未开事务 / 未回冲 / 不写流水 / 状态不变。
- **GetSalesReturns / GetSalesReturnById**：同 `021` 对应用例（客户维度筛选传参（含 `settlementState`）/ 分页映射 / 结算金额与状态推导 / 明细快照 / 不存在 `40400`）；核销校验取数传 `includeItems: false`（主表查 1 次 / 明细查 0 次）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`SalesReturns.ReturnNo` `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；同一字段在采购退货 / 销售退货 Validator 中边界一致（quantity / unitPrice / items / keyword）。
- **对账一致性**：销售出库 → 退货 → 退货作废链路后 `Σ 流水变动量 == Inventory.Quantity`。

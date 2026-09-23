---
created: 2026-09-16
updated: 2026-09-23
---

# 设计规格：收付款与应收应付（erp-settlement）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase`（单据域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> **本规格改造既有单据的结算语义**：`015` / `016` / `021` / `022` 的 `SettlementStatus` 与手工切换接口在本规格落地时一并替换（等价于 `specs/ROADMAP.md` §6.3 预留的迁移路径），各规格需留演进注记。
> **演进（`024-erp-order-flow`）**：本规格核销的两类单据表 / 路由 / 单号前缀已重命名（`PurchaseOrders` → `PurchaseReceipts`、`SalesOrders` → `SalesShipments`），跨仓储累加的目标仓储随之改名（`IPurchaseReceiptRepository` / `ISalesShipmentRepository`）；`SettlementOrderType` 取值与结算 / 核销语义不变。**本正文已按现行为准**，重命名细节见 `specs/024-erp-order-flow/design.md` §3。
> **演进（erp-rbac）**：本域动作接入权限校验，权限点 `settlements.view` / `create` / `void` / `export`，往来对账为 `reconciliation.view`（`export` 由 `027` 的导出动作标注）；菜单可见性与列表页操作按钮由前端按权限过滤。清单唯一来源见 `specs/028-erp-rbac/design.md` §0.2。
> **演进（erp-audit-log）**：本域收付款单（创建核销 / 作废）的写操作已接入操作日志；被核销单据的结算态变更由收付款单日志体现，不单独记录（`specs/029-erp-audit-log/design.md` §0.1）。
> **演进（erp-invoice）**：`SettlementOrderType` 语义已泛化为「**可关联单据类型**」并被 `specs/032-erp-invoice/design.md` §0.2 复用（发票关联单据共用该枚举，不新建同形枚举）；取值与命名不变，后续若改名（如 `BusinessOrderType`）两规格同步。
> **演进（erp-general-ledger）**：本域收付款单的创建 / 作废自 `033` 起同事务生成 / 作废自动凭证（收款：借现金 / 银行存款、贷「应收账款」；付款：借「应付账款」、贷现金 / 银行存款，现金科目按结算方式选取）；期间已结账或科目映射缺失则整单失败回滚。分录科目与勾稽口径见 `specs/033-erp-general-ledger/design.md` §2.3 / §2.4。
> **演进（erp-cash）**：收付款单增可空 `BankAccountId`（外键 → `BankAccounts(Id)`），结算方式与账户类型需匹配（现金 ↔ 现金账户 / 银行转账 ↔ 银行账户，不匹配 `40162`）；列表 / 详情出参增 `bankAccountName`（联查带出，故仓储出参由 `Settlement` 实体改为读模型 `SettlementListItem` / `SettlementDetail`）。账户本体与资金日记账见 `specs/034-erp-cash/design.md` §2 / §3。
> **演进（erp-partner-price）**：`ISettlementQueryRepository` 追加 `GetReceivableAmountAsync(partnerId)`（应收口径与 §0 完全一致，供 `036` 的信用额度校验复用，不另立口径）；往来对账读模型与出参追加账期与逾期字段（`paymentTermDays` / `earliestDueDate` / `maxOverdueDays` / `overdueOrderCount`）+ `overdueOnly` 筛选，未结单据候选出参加 `dueDate`（到期日一律由后端按「单据日期 + 账期」推导，前端不做日期加减）。字段与判据见 `specs/036-erp-partner-price/design.md` §0.3 / §2.3.1。

## 0. 结算口径约定（唯一事实源）

| 概念 | 规则 |
|---|---|
| 单据已结金额 | `SettledAmount`（`numeric(18,2)`，≥ 0，默认 0），由收付款单核销累加 / 作废回退，**不允许手工直接改** |
| 单据未结金额 | `UnsettledAmount = TotalAmount − SettledAmount`（推导，不落列） |
| 结算状态（展示） | `SettlementState`：`0 = 未结算`（`SettledAmount ≤ 0`）、`1 = 部分结算`（`0 < SettledAmount < TotalAmount`）、`2 = 已结算`（`SettledAmount ≥ TotalAmount`）；**推导，不落列** |
| 核销方向 | 收款单（`Receipt`）→ 核销销售出库单（`SalesOutbound`）/ 采购退货单（`PurchaseReturn`）；付款单（`Payment`）→ 核销采购入库单（`PurchaseInbound`）/ 销售退货单（`SalesReturn`） |
| 往来单位类型 | 与收付款方向**无关**：核销单据的往来单位须与收付款单一致（`40113`），其档案类型由被核销单据决定（销售出库单 / 销售退货单 → 客户或两者；采购入库单 / 采购退货单 → 供应商或两者）。收款可对客户（销售回款）或供应商（收回退款），付款同理；**不按方向校验档案类型**（`40109` 不再由本用例触发） |
| 业务类型（派生，不落列） | `Type` × `SettlementItem.OrderType`：收款 × 销售出库 = 销售回款；收款 × 采购退货 = 收回退款；付款 × 采购入库 = 采购付款；付款 × 销售退货 = 退出退款。主表**不新增**该列（可混合核销时单值无法表达，见 §5）；列表以**「单据类型」列**展示该单核销明细的类型集合（去重升序、多值「、」连接，见 §4.4） |
| 往来余额 | 应收 = Σ 销售出库单未结 + Σ 采购退货单未结；应付 = Σ 采购入库单未结 + Σ 销售退货单未结（未结 = `TotalAmount − SettledAmount`，仅计未作废单据）。**按被核销单据未结金额归集，不引用收付款单类型**——`SettledAmount` 已由有效收付款单核销累加 / 作废回退，差额即余额，天然支持「收款挂供应商 / 付款挂客户」 |
| 已核销单据作废 | `SettledAmount > 0` 的单据**禁止作废**（`40120`）：须先作废对应收付款单回退金额，方可作废（作废与核销互斥） |

- 前端结算状态标签文案与颜色：`未结算`（`gray`）/ `部分结算（未结 x）`（`orange`）/ `已结算`（`green`）；全项目唯一来源，单据列表 / 详情与收付款页共用。
- 被核销单据类型的**文案与详情路由**同样收敛到 `src/utils/settlement.ts`（`settlementOrderTypeLabel` / `settlementOrderTypeRouteName`）：收付款详情核销明细、往来对账未结单据抽屉的「单号」一律渲染为 `a-link` 超链接直达对应单据详情，**不另设「详情」按钮**。

## 1. 总体设计

```
收付款单（前端 /settlements 列表 + /settlements/new 新建 + /settlements/detail/:id 详情）
  → SettlementsController
    → App.Core/Features/Settlements/<Action>/*RequestHandler
      → ISettlementRepository（收付款单 + 核销明细）
        + IPurchaseReceiptRepository / ISalesShipmentRepository / IPurchaseReturnRepository / ISalesReturnRepository（`AddSettledAmountAsync` 原子累加）
        + ISettlementQueryRepository（未结单据候选 / 往来台账，跨四表只读）
        + IUnitOfWork（同一事务）
        → PostgreSQL（Settlements / SettlementItems / 四张单据表）

往来对账（前端 /reconciliation）
  → SettlementsController
    → Settlements/GetReconciliation → ISettlementQueryRepository.GetReconciliationAsync（按往来单位聚合）
```

核心原则：

- **单一结算入口**：单据的 `SettledAmount` 只能由收付款单核销改变（累加 / 作废回退），既有手工切换接口与按钮移除。
- **核销即快照**：核销明细记录单据号、单据日期、单据总额的快照，详情页免跨表联查。
- **整单事务**：核销累加（跨 1–4 类单据仓储）与收付款单落库在同一 `IUnitOfWork` 事务内；任一失败整体回滚。
- **金额由后端重算**：收付款单总额 = Σ 核销金额（不信任前端）。
- **作废与核销互斥**：作废是终态、核销是资金事实；已核销单据不允许作废，避免「单据作废但收付款单仍挂着核销」的悬空台账（处置顺序：先作废收付款单回退金额，再作废单据）。

## 2. 数据模型

> 时间字段按后端规则 §5.2（`DateTimeOffset` → `timestamptz`）；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/Settlement.cs` 与表 `Settlements`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `SettlementNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单号 `RC + yyyyMMdd + 4`（收款）/ `PY + yyyyMMdd + 4`（付款），后端按类型生成 |
| `Type` | `SettlementType` | `smallint` | NOT NULL | 0=收款 1=付款 |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 被核销单据的往来单位（收款可对客户或供应商，付款同理；不按方向限制档案类型） |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 往来名称**快照** |
| `SettlementDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 业务日期（UTC 午夜） |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 总额 = Σ 核销金额（后端计算） |
| `Method` | `SettlementMethod` | `smallint` | NOT NULL | 0=现金 1=银行转账 2=其他 |
| `Status` | `OrderStatus` | `smallint` | NOT NULL，默认 `1` | 复用（1=正常 0=已作废） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 新增枚举：`SettlementType { Receipt = 0, Payment = 1 }`、`SettlementMethod { Cash = 0, BankTransfer = 1, Other = 2 }`、`SettlementOrderType { PurchaseInbound = 0, SalesOutbound = 1, PurchaseReturn = 2, SalesReturn = 3 }`（放 `App.Core/Entities/`）。取值命名沿用「入库 / 出库」语义（与 `StockMovementType` 同源）：`024-erp-order-flow` 重命名单据表（入库单 / 出库单）后取值语义不变，无需改枚举。

### 2.2 实体 `App.Core/Entities/SettlementItem.cs` 与表 `SettlementItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `SettlementId` | `Guid` | `uuid` | NOT NULL，FK → `Settlements(Id)`，索引 | |
| `OrderType` | `SettlementOrderType` | `smallint` | NOT NULL | 被核销单据类型 |
| `OrderId` | `Guid` | `uuid` | NOT NULL，索引 | 被核销单据 id |
| `OrderNo` | `string` | `varchar(20)` | NOT NULL | 单据号**快照** |
| `OrderDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 单据日期**快照** |
| `OrderTotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 单据总额**快照** |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 本次核销金额 |

- 明细不软删除、不可改；同一收付款单内**同一单据不允许重复**（Validator 拦 `orderType + orderId` 重复）。
- 快照理由：被核销单据分属四张表，详情页若联查需 4 路 `UNION`；快照使详情页单表可读（同单据明细快照原则）。

### 2.3 单据表改造（四张表同一改造）

| 项 | 变化 |
|---|---|
| 列 | 四张单据表（`PurchaseReceipts` / `SalesShipments` / `PurchaseReturns` / `SalesReturns`）移除 `SettlementStatus`（`smallint`）→ 新增 `SettledAmount`（`numeric(18,2)`，NOT NULL，默认 `0`） |
| 实体 | `PurchaseOrder` / `SalesOrder` / `PurchaseReturn` / `SalesReturn` 同字段替换（`OrderSettlementStatus` 枚举**废弃删除**，由 `SettlementState`（DTO 推导值）取代） |
| 仓储 | 四类单据仓储**追加** `Task AddSettledAmountAsync(Guid id, decimal delta, ...)`（原子累加，EF Core `ExecuteUpdateAsync`，无裸 SQL）；**移除** `UpdateSettlementAsync` |
| 接口 | **移除** `PUT /api/purchase-orders/{id}/settlement`、`PUT /api/sales-orders/{id}/settlement`、`PUT /api/purchase-returns/{id}/settlement`、`PUT /api/sales-returns/{id}/settlement` 四个端点与对应用例（`UpdateXxxSettlement`） |
| 列表筛选 | 原 `settlement`（0/1）筛选参数改为 `settlementState`（0 未结 / 1 部分 / 2 结清，按 `SettledAmount` 与 `TotalAmount` 比较过滤） |

### 2.4 迁移

- 迁移：`dotnet ef migrations add AddErpSettlement -p src/App.Infrastructure -s src/App.Api`（增量迁移）。
- **历史数据回填**（同一迁移内）：四张单据表 `UPDATE ... SET "SettledAmount" = "TotalAmount" WHERE "SettlementStatus" = 1`（迁移前「已结算」→ 全额已结，保持展示口径不变；未结算 → 0）。
- 新增两张表 `Settlements` / `SettlementItems`（外键不级联删除）。

### 2.5 字段约束单一来源

| 用途 | 常量来源 |
|---|---|
| `SettlementNo` 长度（20）/ 查询 `keyword`（20）/ `Remark`（200）/ 核销明细行数上限（100） | `OrderFieldConstraints`（`OrderNoMaxLength` / `KeywordMaxLength` / `RemarkMaxLength` / `ItemsMaxCount`） |
| 金额上界（9999999.99） | `ProductFieldConstraints.PriceMaxValue`（单据单价同源；本域不新增常量类） |
| 金额精度 | `numeric(18,2)`（同单据金额口径） |

- 一致性由单测守护：EF 实际 `HasMaxLength` / `HasPrecision` == 常量与口径；核销金额边界（`0.01` 通过 / `0` 拒绝 / 超上界拒绝）。

## 3. 后端设计

### 3.1 仓储接口（新增 + 改造，`App.Core/Abstractions/`）

`ISettlementRepository`（新增）：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(Settlement, IReadOnlyList<SettlementItem>, ...)` | 新增收付款单 + 核销明细（同一仓储内一次 `SaveChangesAsync`） |
| `Task<(IReadOnlyList<Settlement> Items, int Total)> GetPagedAsync(string? keyword, SettlementType? type, Guid? partnerId, SettlementMethod? method, DateTimeOffset? start, DateTimeOffset? end, SettlementOrderType? orderType, Guid? orderId, int page, int pageSize, ...)` | 列表（`keyword` 匹配单号 / 往来名称快照；`SettlementDate` 闭区间；`CreatedAt DESC`；含作废）；`orderType` + `orderId` **成对传入**时按被核销单据反查（`SettlementItems.OrderId` 有索引），仅返回核销了该单据的收付款单 |
| `Task<(Settlement? Settlement, IReadOnlyList<SettlementItem> Items)> GetDetailAsync(Guid id, ...)` | 详情（主表 + 核销明细，按明细插入顺序） |
| `Task UpdateStatusAsync(Guid id, OrderStatus s, ...)` | 更新状态（作废） |
| `Task<string> GenerateSettlementNoAsync(SettlementType type, DateTimeOffset date, ...)` | 生成单号（`RC` / `PY`，机制同 `erp-purchase` §3.6） |

`ISettlementQueryRepository`（新增，只读跨表查询）：

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<SettlementCandidateItem> Items, int Total)> GetUnsettledAsync(Guid partnerId, SettlementType type, int page, int pageSize, ...)` | 未结单据候选：按方向返回可核销单据（`UnsettledAmount > 0` 且未作废），四表查询后在内存合并分页（数据量为「某往来单位的未结单据」，量小） |
| `Task<(IReadOnlyList<ReconciliationItem> Items, int Total)> GetReconciliationAsync(string? keyword, PartnerType? type, int page, int pageSize, ...)` | 往来台账：按往来单位聚合四表**未结金额**（`TotalAmount − SettledAmount`，仅未作废）与未结单据数——应收 = 销售出库 + 采购退货，应付 = 采购入库 + 销售退货（口径见 §0）；**不查询 `Settlements` 表**，天然支持收款挂供应商 |

- 读模型（`App.Core/Abstractions/`，`sealed record` + `required` + `init`）：
  - `SettlementCandidateItem`：`OrderType` / `OrderId` / `OrderNo` / `OrderDate` / `TotalAmount` / `SettledAmount` / `UnsettledAmount`；
  - `ReconciliationItem`：`PartnerId` / `PartnerName` / `PartnerType` / `ReceivableAmount`（应收）/ `PayableAmount`（应付）/ `UnsettledOrderCount`（应收 / 应付口径见 §0）。
- 四类单据仓储追加 `AddSettledAmountAsync(id, delta, ...)`（`ExecuteUpdateAsync`：`SettledAmount = SettledAmount + delta`）；**移除** `UpdateSettlementAsync`。

#### 3.1.1 四类单据仓储 `GetDetailAsync` 增 `includeItems` 参数（核销取数只查主表）

| 仓储 | 方法（改造后） |
|---|---|
| `IPurchaseReceiptRepository` | `Task<(PurchaseReceipt? Order, IReadOnlyList<PurchaseReceiptItem> Items)> GetDetailAsync(Guid id, bool includeItems = true, CancellationToken cancellationToken = default)` |
| `ISalesShipmentRepository` | 同形，实体为 `SalesShipment` / `SalesShipmentItem` |
| `IPurchaseReturnRepository` | 同形，实体为 `PurchaseReturn` / `PurchaseReturnItem` |
| `ISalesReturnRepository` | 同形，实体为 `SalesReturn` / `SalesReturnItem` |

**参数语义（须写入接口 XML 文档注释）**：

| 取值 | 行为 |
|---|---|
| `includeItems = true`（默认） | 主表 + 明细行（明细按 `Id` 升序还原插入顺序）——与现状完全一致 |
| `includeItems = false` | **只查主表**（`AsNoTracking` + 一次 `FirstOrDefaultAsync`），不发起明细查询；`Items` 恒为 `Array.Empty<TItem>()`，调用方**不得消费 `Items`**，只取主表字段 |

- 主表不存在时两种取值的 `Order` 均为 `null`、`Items` 均为空集合。

**决策与影响**：

- **不新增仓储方法 / 不新增接口**：核销校验与详情用例共用同一取数入口，仅以参数区分取数范围（仓储方法数不增长）。
- **默认值取 `true`**：既有调用点的语义与行为不变（明细仍可取），只有「只用主表字段」的核销校验显式传 `false`。
- **参数置于 `CancellationToken` 之前**（`CancellationToken` 保持末位）：既有位置调用 `GetDetailAsync(id, cancellationToken)` 会编译失败，须改为 `GetDetailAsync(id, cancellationToken: cancellationToken)`——机械改动、由编译器强制，不会漏改。
- **改造范围**：仅本规格用到的四类单据仓储（采购入库 / 销售出库 / 采购退货 / 销售退货）。`ISettlementRepository` / `IStockTakeRepository` / `ISalesShipmentRepository` / `IPurchaseReceiptRepository` 的同名方法**不加**该参数（未被核销校验使用，避免全仓铺开；订单域 `Void` / `Update` / `Close` 中丢弃明细的调用点本次同样不改）。
- **调用点清单（共 20 处，编译期可见）**：

| 用例 | 处数 | 改法 |
|---|---:|---|
| `CreateSettlement`（`LoadOrderAsync` 四个分支） | 4 | `GetDetailAsync(orderId, includeItems: false, cancellationToken)` |
| 四类单据 `CreateXxx` / `GetXxxById` / `VoidXxx`（每类 1 / 1 / 2） | 16 | `GetDetailAsync(id, cancellationToken: cancellationToken)`（仅补命名参数，行为不变） |

- `Items` 在 `includeItems = false` 时为空集合属**约定行为**，不是异常路径；若后续有调用点需要明细却又传了 `false`，由 code review 拦截（不引入运行时校验）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40112 | `SettlementAmountExceeded` | 核销金额超过单据未结金额（message 含单号与未结金额） |
| 40113 | `SettlementPartnerMismatch` | 核销单据的往来单位与收付款单不一致 |
| 40114 | `SettlementDirectionMismatch` | 收付款方向与单据类型不匹配（如收款单核销采购单） |
| 40120 | `OrderSettledCannotVoid` | 单据已被收付款单核销，禁止作废（message 含单号与已结金额） |

> 复用：`40104 OrderVoided`（被核销单据已作废 / 收付款单已作废）、`40110 OrderItemsEmpty`（未指定核销单据）、`40400`、`40000`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/settlements` | GET | `Settlements/GetSettlements` | `PagedResult<SettlementListItemDto>` | 40000 |
| `/api/settlements` | POST | `Settlements/CreateSettlement` | `SettlementDetailDto` | 40000 / 40104 / 40110 / 40112 / 40113 / 40114 / 40400 |
| `/api/settlements/{id:guid}` | GET | `Settlements/GetSettlementById` | `SettlementDetailDto` | 40400 |
| `/api/settlements/{id:guid}/void` | PUT | `Settlements/VoidSettlement` | `SettlementDetailDto` | 40104 / 40400 |
| `/api/settlements/unsettled-orders` | GET | `Settlements/GetUnsettledOrders` | `PagedResult<SettlementCandidateDto>` | 40000 |
| `/api/reconciliation` | GET | `Settlements/GetReconciliation` | `PagedResult<ReconciliationListItemDto>` | 40000 |

路由注意：`/api/settlements/unsettled-orders` 为固定段，置于 `{id:guid}` 之前注册。

### 3.4 关键用例流程（Handler）

**CreateSettlement**：

1. 核销明细为空 → `40110`（Validator 已拦非空，Handler 双保险）。
2. 取往来单位：不存在 → `40400`；停用 → `40108`。**不校验档案类型与方向**（收款可对客户或供应商，付款同理，`40109` 不再由本用例触发）；往来正确性由步骤 3 的「单据往来 == 收付款单往来 `40113`」+「方向与单据类型匹配 `40114`」保证。
3. 逐行取被核销单据（按 `OrderType` 分派四类单据仓储 `GetDetailAsync`，传 **`includeItems: false`**——只用主表字段，不查明细分片，见 §3.1.1）：不存在 → `40400`；已作废 → `40104`；往来单位不一致 → `40113`；方向与单据类型不匹配 → `40114`；核销金额 > `TotalAmount − SettledAmount` → `40112`（message 含单号与未结金额）。
4. 后端重算 `TotalAmount = Σ Amount`；`GenerateSettlementNoAsync(type, settlementDate)` 生成单号。
5. `IUnitOfWork`：`BeginTransactionAsync` → `ISettlementRepository.AddAsync` → 逐行按其 `OrderType` 调用对应单据仓储 `AddSettledAmountAsync(orderId, +amount)` → `CommitAsync`。

**VoidSettlement**：

1. `GetDetailAsync` 取单：不存在 → `40400`；已作废 → `40104`。
2. `IUnitOfWork`：`BeginTransactionAsync` → 逐条核销明细按其 `OrderType` 调用 `AddSettledAmountAsync(orderId, −amount)`（回退）→ `UpdateStatusAsync(id, Voided)` + 审计 → `CommitAsync`。

**GetUnsettledOrders**：入参 `partnerId` + `type`（收 / 付）→ 由 `type` 推导可核销单据类型集合 → `ISettlementQueryRepository.GetUnsettledAsync` → 映射 DTO（含未结金额，供新建页默认按未结金额填充）。

**GetReconciliation**：`ISettlementQueryRepository.GetReconciliationAsync(keyword, type, page, pageSize)` → 映射 DTO。余额按四表未结金额归集（§0），不查询收付款单表。

**GetSettlements / GetSettlementById**：同既有列表 / 详情模式（筛选 / 映射 / 快照透传）。列表在映射前用既有 `GetItemsBySettlementIdsAsync` **一次批量取本页核销明细**（空页跳过，非逐单查询），聚合出每单 `OrderTypes`（去重升序，供「单据类型」列）；同一次结果兼作按单据反查的 `OrderAmount` 汇总。

**GetSettlements（按被核销单据反查）**：`orderType` + `orderId` 成对传入时，仓储按核销明细过滤（命中该单据的收付款单，含已作废）；Handler 再用既有 `GetItemsBySettlementIdsAsync` 批量取本页收付款单的核销明细，挑出 `orderId`（及 `orderType`）匹配行的 `Amount` 之和，写入 `SettlementListItemDto.OrderAmount`（单据详情「收付款明细」的**本次核销金额**）；`orderId` 为空时不发起该次查询、`OrderAmount` 为 `null`（列表页口径不变）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.5 常量）

| 请求 | 规则 |
|---|---|
| `CreateSettlementRequest` | `type` ∈ {0,1}；`partnerId` 必填；`settlementDate` 必填；`method` ∈ {0,1,2}；`items` 必填非空、1–100 行、**`orderType + orderId` 不重复**；每行 `orderType` ∈ {0,1,2,3}、`orderId` 必填、`amount` > 0 且 ≤ 9999999.99；`remark` ≤ 200 |
| `GetSettlementsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20；`type` / `partnerId` / `method` 可空合法值；`start` / `end` 可空且 `start <= end`；`orderType` 可空且 ∈ {0,1,2,3}；**`orderType` 与 `orderId` 必须成对出现**（同空或同非空，非法返回 `40000`） |
| `GetUnsettledOrdersRequest` | `partnerId` 必填；`type` ∈ {0,1}；`page ≥ 1`；`pageSize` 1–100 |
| `GetReconciliationRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（往来名称 / 编码，对齐 `Partner` 列长）；`type` 可空合法值 |

### 3.6 既有单据侧改造（`015` / `016` / `021` / `022` 消费）

| 改造项 | 内容 |
|---|---|
| 接口 | 移除四个 `PUT .../settlement` 端点与 `UpdateXxxSettlement` 用例（`DependencyInjection` 注册同步移除） |
| 仓储 | 四类单据仓储移除 `UpdateSettlementAsync`，新增 `AddSettledAmountAsync` |
| DTO | 列表 / 详情出参把 `settlementStatus` 替换为 `settledAmount` + `unsettledAmount` + `settlementState`（推导值，`Mapper` 内计算） |
| 筛选 | 列表请求参数 `settlement`（0/1）→ `settlementState`（0/1/2） |
| Swagger | 四个结算端点从文档移除（`003-api-swagger` 的端点清单如有断言需同步） |
| 作废校验 | 四类单据作废用例（`VoidPurchaseReceipt` / `VoidSalesShipment` / `VoidPurchaseReturn` / `VoidSalesReturn`）在既有「已作废 `40104`」校验之后追加「已核销 `40120`」校验：`SettledAmount > 0` 直接拒绝（**不开事务、不动库存与流水**）；不改仓储与 DTO |

### 3.7 Swagger

- **不分组**（同既有约定）：6 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── settlement.ts                     # 收付款 + 往来对账接口层
└── views/
    └── SettlementManagement/
        ├── SettlementsView.vue           # 收付款单列表
        ├── SettlementFormPage.vue        # 新建收付款（核销）独立页
        ├── SettlementDetailView.vue      # 详情（含核销明细 + 作废）
        └── ReconciliationView.vue        # 往来对账（余额 + 未结单据下钻）
```

> 另改造既有域：`PurchaseManagement/`、`SalesManagement/`、`PurchaseReturnManagement/`、`SalesReturnManagement/` 的列表与详情（见 §4.4）。

### 4.2 接口层

- `src/api/settlement.ts`：类型与后端 DTO 一一对应；`getSettlements` / `createSettlement` / `getSettlementById` / `voidSettlement` / `getUnsettledOrders` / `getReconciliation`。`SettlementQuery` 含可选 `orderType` / `orderId`（成对），`SettlementListItem` 含可选 `orderAmount`（按单据反查时的本次核销金额）与 `orderTypes`（列表「单据类型」列；详情页用 `items` 自派生）。
- 单据侧 `api/purchase.ts` / `sale.ts` / `purchaseReturn.ts` / `saleReturn.ts`：移除 `updateXxxSettlement`，类型中 `settlementStatus` → `settledAmount` / `unsettledAmount` / `settlementState`。
- 结算状态标签文案与颜色统一由 `src/utils/` 的常量映射（依据 §0 表）复用，不在各页面重复硬编码。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `settlements` | `settlements` | `SettlementsView` |
| `settlements/new` | `settlementNew` | `SettlementFormPage` |
| `settlements/detail/:id` | `settlementDetail` | `SettlementDetailView` |
| `reconciliation` | `reconciliation` | `ReconciliationView` |

`AppLayout.vue`「资金」分组下提供子项「收付款」`settlements` 与「往来对账」`reconciliation`；`MENU_ROUTE_MAP` 增加 `settlementDetail: 'settlements'`。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2。

### 4.4 页面交互

**新建收付款 `SettlementFormPage.vue`**（独立页面，含子表格）：

- 表头：类型（`a-radio-group`：收款 / 付款）、往来单位（下拉取**全部启用往来**，不按类型过滤——收款需覆盖「收供应商退款」场景；placeholder 提示「选择与该方向存在未结单据的往来单位」；选定后按方向加载可核销单据）、收付日期、方式（下拉）、备注。
- 明细区：可核销单据列表（`getUnsettledOrders`）——勾选 / 添加行后展示单号、单据日期、单据总额、已结金额、**未结金额**、本次核销金额（`a-input-number`，默认填入未结金额，`max` = 未结金额）；「全部结清」按钮（一键把所选行填为未结金额）。
- 底部：总额（computed = Σ 核销金额）+ 提交（`submitting`）/ 取消；提交成功 → 跳详情页。
- 切类型 / 切往来单位时清空已选核销明细并重载候选（防串数据）；往来单位**不因切类型而清空**（下拉不再按类型过滤，同一往来在两个方向都可能有未结单据）。

**收付款列表 `SettlementsView.vue`**：筛选行（单号关键词 + 类型 + 往来 + 方式 + 日期范围 + 搜索 / 重置）；表格列（序号、单号、类型 `a-tag`、**单据类型**、往来单位、**收付日期**、总额、方式、状态、操作列：详情 / 打印 / 作废）；作废行置灰。

- 「单据类型」与「创建时间」均为**列设置可选列**，但默认显隐不同：「单据类型」**默认显示**（置于「类型」之后，宽 160、`ellipsis + tooltip`）：取 `orderTypes` 经 `settlementOrderTypeLabel` 映射后以「、」连接（混合核销时为多值）。
- 「创建时间」**默认不显示**（审计信息，需要时从列设置勾选）：列表主时间口径是**收付日期**（业务日期，可补录往日），创建时间仅在追溯"谁在何时录入"时使用；两者语义不同，均保留。

**详情 `SettlementDetailView.vue`**：`a-page-header` + `a-descriptions` + 核销明细只读表格（单据类型 / 单号（`a-link` 超链接 → 被核销单据详情，按 `OrderType` 映射）/ 单据日期 / 单据总额 / 本次核销金额）+ 底部作废按钮；id 不存在 → `a-result status="404"`。

**往来对账 `ReconciliationView.vue`**：筛选行（往来名称关键词 + 类型 + 搜索 / 重置）；表格列（序号、往来单位、类型、**应收余额**、**应付余额**、未结单据数、操作列「未结单据」）→ 点击打开抽屉（`getUnsettledOrders` 按该往来单位分别查应收 / 应付方向的未结单据，**单号为 `a-link` 超链接**直达对应单据详情，其余只读展示）。**应收余额** = 该往来应收类单据未结合计（销售出库单 + 采购退货单，收款方向）；**应付余额** = 应付类未结合计（采购入库单 + 销售退货单，付款方向）——与抽屉两个区块口径一一对应（§0）。

**单据侧改造（四类单据列表 / 详情）**：

- 移除「标记已结算 / 改回未结算」按钮与 `settlingId` 状态。
- 结算列 / 描述项改为：结算状态标签（§0 文案与颜色）+ 未结金额（`部分结算（未结 600.00）`）。
- 操作列新增「收付款」按钮（Tabler `IconCash`）：按单据类型推导方向（销售单 / 采购退货 → 收款；采购单 / 销售退货 → 付款）→ 跳 `/settlements/new` 并预置类型与往来单位（路由 query）。**显隐判据（唯一来源：`src/utils/settlement.ts` 的 `canStartSettlement`）**：单据正常（`status = 1`）**且未结算**（`settlementState ≠ 2`）——已结算单据没有可核销金额，`GetUnsettledOrders` 只会返回空候选，故不显示入口（避免"点进去是空列表"的死路）；部分结算仍显示（可继续收 / 付未结部分）；作废单不显示。**详情页底部「去收付款」同判据**（`canStartSettlement(detail)`），四类单据列表与详情共 8 处一致。
- 筛选行「结算状态」下拉取值改为 未结算 / 部分结算 / 已结算（映射 `settlementState` 0/1/2）。
- **「作废」按钮判据（唯一来源：`src/utils/settlement.ts` 的 `canVoidOrder`）**：单据正常且 `settledAmount = 0` 才可作废；已核销（`settledAmount > 0`）时按钮 **`disabled` + tooltip**（文案 `VOID_SETTLED_HINT`：「已被收付款单核销，请先作废对应收付款单」）——**前端提前拦截**，避免"点了确认才被拒"；后端 `40120`（§3.6）仍是兜底。作废单不显示（原有）。
- **详情新增「收付款明细」只读区块**（商品明细之下）：由**跨域共享组件** `components/SettlementRecords.vue` 承载（props：`orderType` + `orderId`），调 `getSettlements` 反查并展示 **单号（`a-link` 超链接 → `/settlements/detail/:id`）** / 收付方向 `a-tag`（收款绿 / 付款橙）/ 收付日期 / 本次核销金额（`orderAmount`）/ 状态（正常 / 已作废）——**不设「操作」列与「详情」按钮**，单号本身即入口；已作废行进灰（`row-voided` 同收付款列表口径）；查询 loading 用 `loading`。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询（收付款 / 对账 / 单据） | `loading` | 搜索 / 翻页 + 表格 |
| 新建页提交 | `submitting` | 提交按钮 |
| 收付款单作废 | `voidingId` | popconfirm 确认按钮 |
| 未结单据抽屉加载 | `candidatesLoading` | 抽屉内容区 |
| 单据页「收付款」跳转 | 不置 loading | 同步路由跳转 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 结算从状态位升级为金额 | `SettledAmount` + 推导状态 | 状态位无法表达部分结算与追溯实际收付；`015` §5 已预留该升级路径 |
| 移除手工结算切换 | 删除四个 settlement 端点与按钮 | 保留手工切换会绕过核销明细，导致单据金额与收付款单不一致；口径必须单一 |
| 收付款单可核销四类单据 | 方向由 `Type` 决定 | 退货单同样涉及资金（退客户钱 / 收回退款），若排除会让退货单的结算语义与主单据分叉（`021` / `022` 已按 0/1 设计，本规格统一升级） |
| 收款可对供应商（往来不由方向限制） | 移除 `CreateSettlement` 的「档案类型与方向匹配」校验（`40109`） | 采购退货的退款是「收供应商的钱」，按方向硬绑客户会让该场景无法录入（只能把供应商档案改成「两者」）；核销行已强制「单据往来 == 收付款单往来 `40113`」+「方向匹配 `40114`」，方向-类型校验属多余且自相矛盾（与 §0 核销方向表冲突） |
| 不新增「业务类型」字段 | 业务类型由 `Type` + 明细 `OrderType` 派生 | 一张单可混合核销（「两者」往来的收款单可同时核销销售出库单与采购退货单），单值字段无法表达且与明细冗余；现有 `SettlementType`（资金方向）+ `SettlementItem.OrderType`（业务类别）已足够，避免两处维护与不一致 |
| 对账余额按未结金额归集 | 应收 = Σ 销售出库未结 + Σ 采购退货未结；应付 = Σ 采购入库未结 + Σ 销售退货未结 | 原公式按收付款单 `Type` 拆分「已收 / 已付」，一旦收款挂供应商就会把钱冲错方向（退供应商的款算到应收）；用 `SettledAmount` 差额归集天然方向正确，且不再查询 `Settlements` 表，与「未结单据数」同源 |
| 核销明细存单据快照 | `OrderNo` / `OrderDate` / `OrderTotalAmount` | 被核销单据分属四表，联查需 4 路 `UNION`；快照让详情单表可读（同单据明细快照原则） |
| 不支持预收 / 预付 | 收付总额必须等于核销合计 | 挂账余额需要「未核销余额」台账与自动抵扣策略，属独立能力（范围外） |
| 新增 `ISettlementQueryRepository` | 未结候选 + 往来台账跨四表只读 | 跨表的只读聚合不属于任何单一单据仓储；按「读模型 / 查询职责」独立成接口，写侧仍由各单据仓储负责 |
| 未结候选分页在内存合并 | 四表查询后合并 | 候选集是「单个往来单位的未结单据」，量小（单往来未结单据数十量级），无需数据库层 `UNION` + 排序分页 |
| 单据列表筛选 `settlement` → `settlementState` | 0/1/2 推导过滤 | 金额字段无法直接做「部分」筛选，需与 `TotalAmount` 比较；保持前端筛选语义不变（未结 / 部分 / 结清） |
| 单据结算反查复用列表端点 | `GetSettlements` 增可选 `orderType` + `orderId`（不新增端点 / 用例） | 反查条件与列表筛选同构，复用同一仓储方法与 DTO；新增独立端点会多一套用例 / 注册 / Swagger，收益不抵成本。`orderAmount`（本单核销金额）用既有 `GetItemsBySettlementIdsAsync` 批量补齐，不新增仓储方法 |
| 保留 `OrderStatus` 作废语义 | 收付款单复用 `OrderStatus` | 作废模式与既有单据一致（不可改、可作废、作废回退影响） |
| 历史数据按全额回填 | 迁移内 `UPDATE` | 迁移前「已结算」即「全额已结」的口径，回填后展示与筛选行为不变，无人工对账成本 |
| 无 RBAC | 登录即可见「收付款」「往来对账」菜单 | 同既有功能（权限由 `028-erp-rbac` 接入）；资金相关菜单在 `028` 落地时应优先纳入按钮级权限 |
| 核销取被核销单据只查主表 | 既有 `GetDetailAsync` 增 `bool includeItems = true` 标记参数，核销校验传 `false` | 逐行校验只用主表字段（单号 / 日期 / `TotalAmount` / `SettledAmount` / `PartnerId` / `Status`），却会为每行多付一次明细查询；**不新增仓储方法与接口**，默认 `true` 使既有调用点语义不变，仅核销校验显式收窄。代价：`includeItems = false` 时 `Items` 恒为空集合，返回契约需靠注释约定「不得消费 `Items`」（§3.1.1） |
| 已核销单据禁止作废（而非自动反核销） | 作废前校验 `SettledAmount > 0` → `40120` | 自动连带作废 / 反核销需处理「一张收付款单核销多张单据」的级联语义，复杂且易生歧义；拒绝并提示「先作废对应收付款单」把决策留给用户，与「作废是终态、资金口径单一」一致（`015` / `016` / `021` / `022` / `024` 已留演进注记） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreateSettlement**：
  - 成功（收款核销销售单）：断言单号前缀 `RC` + 日期、`TotalAmount` 后端重算（前端传值被忽略）、每行按其 `OrderType` 调用 `AddSettledAmountAsync(+amount)`、核销明细快照、`Commit`。
  - 成功（付款核销采购入库单 / **收款核销采购退货单（往来为纯 `Supplier`）** / 付款核销销售退货单）：方向与往来类型组合各一例；断言不再因档案类型返回 `40109`。
  - 异常：明细空 `40110`；往来不存在 `40400` / 停用 `40108`；单据不存在 `40400` / 已作废 `40104` / 往来不一致 `40113` / 方向不匹配 `40114` / 超额 `40112`；失败路径**无任何金额累加**、`RollbackAsync` 断言。
  - 取数范围（§3.1.1）：核销校验调 `GetDetailAsync` 时 `includeItems = false`（假实现断言：主表查询 1 次 / 明细查询 0 次）；`includeItems = true`（默认）时明细正常返回（既有详情用例回归）。
- **VoidSettlement**：成功（断言逐行 `AddSettledAmountAsync(−amount)` + `UpdateStatusAsync(Voided)`）；已作废 → `40104`；不存在 → `40400`。
- **GetSettlements / GetSettlementById**：筛选传参组合、分页映射（含作废）、核销明细快照透传、不存在 `40400`；**单据类型列**：按本页核销明细去重升序聚合 `OrderTypes`（混合核销为多值、无明细为空集合）、空页不查明细、未指定被核销单据时明细仍只批量查一次（非逐单 N+1）；**按单据反查**：`orderType` + `orderId` 透传断言、`orderAmount` 取该单据核销金额之和（非该单据的明细不计入）、`orderId` 为空时 `OrderAmount` 为 `null`；仓储按 `orderType` + `orderId` 过滤（`SettlementRepositoryTests`）——命中单返回、未核销该单据的单被排除。
- **GetUnsettledOrders**：方向 → 单据类型集合映射正确（收款 → 销售单 + 采购退货单；付款 → 采购单 + 销售退货单）；仅返回未结且未作废（断言透传仓储过滤）。
- **GetReconciliation**：按未结金额归集的应收 / 应付计算正确（应收 = 销售出库未结 + 采购退货未结；应付 = 采购入库未结 + 销售退货未结；排除已作废单据）、未结单据数正确、分页 `total`；**收款挂供应商**用例：纯 `Supplier` 往来「采购入库 800 未结 + 采购退货 500 已收」→ 应收 0 / 应付 800（旧口径会把 500 算成负应收）。
- **单据侧回归**（`015` / `016` / `021` / `022` 既有测试调整）：`SettlementState` 推导三态（0 / 部分 / 结清边界：`SettledAmount = 0`、`0 < x < TotalAmount`、`x = TotalAmount`、`x > TotalAmount` 视为结清）；列表 `settlementState` 筛选传参；`UpdateXxxSettlement` 用例与其测试**删除**。
- **字段约束一致性**：`SettlementNo` EF `HasMaxLength` 20 == `OrderFieldConstraints.OrderNoMaxLength`；核销明细列长（20）与 `Amount` 精度（`numeric(18,2)`）与单据金额口径一致；`amount` 边界（`0.01` 通过 / `0` 拒绝 / 超 `PriceMaxValue` 拒绝）。
- **已核销禁作废**（`VoidPurchaseReceipt` / `VoidSalesShipment` / `VoidPurchaseReturn` / `VoidSalesReturn` 各一例）：`SettledAmount > 0` → `40120`，message 含单号与已结金额，且**未开启事务、未回冲库存、不写流水、单据状态不变**；`SettledAmount = 0` 时作废行为不变（既有成功用例回归）。

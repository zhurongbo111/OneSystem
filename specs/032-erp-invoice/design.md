---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：发票登记（erp-invoice）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `023-erp-settlement`（核销明细 + 单据快照 + 只读候选查询）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> **与 `023` 的关系**：结构同构（主表 + 单据明细快照 + 方向 / 往来 / 金额三重校验），但对象不同（资金核销 vs 发票登记），两者**互不影响**（同一单据既可核销收款、也可开票）。

## 0. 发票口径约定（唯一事实源）

### 0.1 金额与税额

| 概念 | 规则 |
|---|---|
| 不含税金额 | `AmountExcludingTax`（`numeric(18,2)`，> 0，用户录入） |
| 税率 | `TaxRate`（`numeric(5,4)`，0 ≤ x ≤ 1，如 `0.13`；用户录入，可为 0） |
| 税额 | `TaxAmount = Round(AmountExcludingTax × TaxRate, 2)`（`MidpointRounding.AwayFromZero`，与 `026` 舍入口径一致） |
| 价税合计 | `TotalAmount = AmountExcludingTax + TaxAmount`（后端重算，**不信任前端**） |
| 精度 | 存储与展示均 2 位小数（`numeric(18,2)`） |

### 0.2 方向与往来

| 发票类型 | 可关联单据类型 | 往来要求 |
|---|---|---|
| 进项（`Purchase = 0`，采购取得） | 采购入库单（`PurchaseInbound`）/ 采购退货单（`PurchaseReturn`） | 发票往来 = 单据往来（供应商），不一致 → `40135` |
| 销项（`Sales = 1`，销售开出） | 销售出库单（`SalesOutbound`）/ 销售退货单（`SalesReturn`） | 发票往来 = 单据往来（客户），不一致 → `40135` |

- 单据类型枚举**复用 `023` 的 `SettlementOrderType`**（`specs/023-erp-settlement/design.md` §2.1：`PurchaseInbound = 0` / `SalesOutbound = 1` / `PurchaseReturn = 2` / `SalesReturn = 3`）；语义为「可关联的单据类型」，两侧共用一套，**不新建同形枚举**（后端规则 §5.3 精神）。
  - 命名沿革：该枚举名带 `Settlement` 前缀但语义已泛化为「单据类型」，`032` 落地时若 `023` 已实现且改名为 `BusinessOrderType`，双方同步（在 `023` / `032` `design.md` 各留注记，见 `tasks.md`）。
- 方向错配 → `40134`；往来不一致 → `40135`；被关联单据已作废 → `40104`。

### 0.3 未开票金额（与 `023` 的「未结金额」并列、不混用）

| 概念 | 口径 |
|---|---|
| 单据已开票金额 | `Σ InvoiceItems.Amount`（`OrderType` + `OrderId` 相同、且**所属发票未作废**） |
| 单据未开票金额 | `单据 TotalAmount − 已开票金额`（推导，不落列） |
| 校验 | 每行开票金额 > 未开票金额 → `40133`（message 含单据号与未开票金额） |
| 作废释放 | 发票作废后其明细自动不计入「已开票金额」→ 未开票金额**自动回退**（无需写回单据，决策见 §5） |
| 与「未结金额」的区别 | 未结金额 = 资金维度（`023` §0）；未开票金额 = 票据维度；两者独立，**不得相互推导** |

### 0.4 展示约定

| 项 | 文案 / 颜色 |
|---|---|
| 发票类型 | 进项 `a-tag` `arcoblue`、销项 `a-tag` `green` |
| 发票状态 | 正常 `green`、已作废 `red`（作废行整体置灰） |
| 空值 | 关联单据为空时显示 `-`（本期不允许空：明细至少 1 行，`40110`） |

## 1. 总体设计

```
发票登记（前端 /invoices 列表 + /invoices/new 新建 + /invoices/detail/:id 详情）
  → InvoicesController
    → App.Core/Features/Invoices/<Action>/*RequestHandler
      → IInvoiceRepository（发票 + 关联明细）
        + IPartnerRepository / 四类单据仓储（校验存在 / 往来 / 作废 / 金额）
        + IInvoiceQueryRepository（可开票单据候选 + 已开票金额聚合，跨四表只读）
        + IUnitOfWork（同一事务）
        → PostgreSQL（Invoices / InvoiceItems / 四类单据表）
```

核心原则：

- **与 `023` 平行、互不耦合**：发票不改单据表、不写单据列、不影响结算与应收口径。
- **已开票金额按聚合推导**：不落单据列（与 `023` 落 `SettledAmount` 的取舍不同，理由见 §5）。
- **快照可读**：关联明细存单据号 / 单据日期 / 单据总额快照，详情页单表可读（同 `023` 核销明细）。
- **整单事务**：发票主表 + 明细 + 校验读取在同一 `IUnitOfWork` 内（校验与落库之间不留竞态窗口的有效手段是同一事务内的重复读取，见 §5）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/Invoice.cs` 与表 `Invoices`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `InvoiceNo` | `string` | `varchar(50)` | NOT NULL，唯一索引 | 发票号码（**全局唯一**，`40132`） |
| `Type` | `InvoiceType` | `smallint` | NOT NULL | 0 = 进项 1 = 销项 |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 供应商（进项）/ 客户（销项） |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 往来名称**快照** |
| `InvoiceDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 开票日期（UTC 午夜） |
| `AmountExcludingTax` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 不含税金额 |
| `TaxRate` | `decimal` | `numeric(5,4)` | NOT NULL，0–1 | 税率 |
| `TaxAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 税额（后端计算） |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 价税合计（后端计算） |
| `Status` | `OrderStatus` | `smallint` | NOT NULL，默认 `1` | 复用（1=正常 0=已作废） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

### 2.2 实体 `App.Core/Entities/InvoiceItem.cs` 与表 `InvoiceItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `InvoiceId` | `Guid` | `uuid` | NOT NULL，FK → `Invoices(Id)`，索引 | |
| `OrderType` | `SettlementOrderType` | `smallint` | NOT NULL | 被开票单据类型（复用 `023` 枚举，§0.2） |
| `OrderId` | `Guid` | `uuid` | NOT NULL，索引 | 被开票单据 id |
| `OrderNo` | `string` | `varchar(20)` | NOT NULL | 单据号**快照** |
| `OrderDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 单据日期**快照** |
| `OrderTotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 单据总额**快照** |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 本次开票金额 |

- 明细不软删除、不可改；同一发票内**同一单据不允许重复**（Validator 拦 `orderType + orderId` 重复）。
- 索引补充：`(OrderType, OrderId)` 复合索引（「已开票金额」聚合按此列查询，高频）。

### 2.3 枚举

`InvoiceType { Purchase = 0, Sales = 1 }`（`App.Core/Entities/InvoiceType.cs`）。单据类型复用 `SettlementOrderType`（§0.2）。

### 2.4 迁移与字段约束

- 迁移：`dotnet ef migrations add AddErpInvoice -p src/App.Infrastructure -s src/App.Api`（增量迁移，两张表 + 索引；外键不级联删除）。
- **不新建常量类**：`InvoiceNo` 长度 50（本域定义于 `InvoiceFieldConstraints.InvoiceNoMaxLength`，无既有同源常量）；查询 `keyword` 50（对齐 `InvoiceNo` 与 `PartnerName` 的最大匹配列长）；`Remark` 200 引用 `OrderFieldConstraints.RemarkMaxLength`；明细行数上限 100 引用 `OrderFieldConstraints.ItemsMaxCount`；金额上下界引用 `ProductFieldConstraints.PriceMinValue/PriceMaxValue`。

## 3. 后端设计

### 3.1 仓储接口

`IInvoiceRepository`（新增）：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(Invoice, IReadOnlyList<InvoiceItem>, ...)` | 发票 + 明细（同一仓储内一次 `SaveChangesAsync`） |
| `Task<(IReadOnlyList<InvoiceListItem> Items, int Total)> GetPagedAsync(string? keyword, InvoiceType? type, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, ...)` | 列表（`keyword` 匹配 `InvoiceNo` / 往来名称 / **关联单据号**（EXISTS 子查询）；`InvoiceDate` 闭区间；`CreatedAt DESC`；含作废） |
| `Task<(Invoice? Invoice, IReadOnlyList<InvoiceItem> Items)> GetDetailAsync(Guid id, ...)` | 详情（主表 + 明细，按插入顺序） |
| `Task<bool> ExistsByInvoiceNoAsync(string invoiceNo, ...)` | 发票号唯一 |
| `Task UpdateStatusAsync(Guid id, OrderStatus s, ...)` | 作废 |

`IInvoiceQueryRepository`（新增，跨四表只读）：

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<InvoicableOrderItem> Items, int Total)> GetInvoicableAsync(Guid partnerId, InvoiceType type, int page, int pageSize, ...)` | 可开票单据：按方向映射单据类型集合 → 四表查询（未作废且 `TotalAmount − 已开票金额 > 0`）→ 内存合并分页（同 `023` 未结候选的做法） |
| `Task<decimal> GetInvoicedAmountAsync(SettlementOrderType orderType, Guid orderId, ...)` | 某单据已开票金额（未作废发票的明细聚合） |

- 读模型（`App.Core/Abstractions/`，`sealed record` + `required` + `init`）：
  - `InvoicableOrderItem`：`OrderType` / `OrderId` / `OrderNo` / `OrderDate` / `TotalAmount` / `InvoicedAmount` / `UninvoicedAmount`；
  - `InvoiceListItem`（列表）：`Id` / `InvoiceNo` / `Type` / `PartnerName` / `InvoiceDate` / `AmountExcludingTax` / `TaxAmount` / `TotalAmount` / `Status` / `CreatedAt` / `OrderNoSummary`（关联单据号拼接，列表展示用）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40132 | `InvoiceNoExists` | 发票号已存在 |
| 40133 | `InvoiceAmountExceeded` | 开票金额超过单据未开票金额（message 含单据号与未开票金额） |
| 40134 | `InvoiceDirectionMismatch` | 发票类型与单据类型不匹配（如销项票关联采购入库单） |
| 40135 | `InvoicePartnerMismatch` | 发票往来单位与单据往来单位不一致 |

> 复用：`40104 OrderVoided`（发票已作废 / 被关联单据已作废）、`40110 OrderItemsEmpty`（未指定关联单据）、`40400` / `40000`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/invoices` | GET | `Invoices/GetInvoices` | `PagedResult<InvoiceListItemDto>` | `invoices.view` / 40000 |
| `/api/invoices` | POST | `Invoices/CreateInvoice` | `InvoiceDetailDto` | `invoices.create` / 40000 / 40104 / 40110 / 40132 / 40133 / 40134 / 40135 / 40400 |
| `/api/invoices/{id:guid}` | GET | `Invoices/GetInvoiceById` | `InvoiceDetailDto` | `invoices.view` / 40400 |
| `/api/invoices/{id:guid}/void` | PUT | `Invoices/VoidInvoice` | `InvoiceDetailDto` | `invoices.void` / 40104 / 40400 |
| `/api/invoices/invoicable-orders` | GET | `Invoices/GetInvoicableOrders` | `PagedResult<InvoicableOrderDto>` | `invoices.view` / 40000 |

- 路由注意：`invoicable-orders` 为固定段，置于 `{id:guid}` 之前。

### 3.4 关键用例流程（Handler）

**CreateInvoice**：
1. 关联明细为空 → `40110`（Validator 双保险）；发票号已存在 → `40132`。
2. 取往来单位：不存在 → `40400`；停用 → `40108`；类型与发票方向匹配（进项需 `Supplier/Both`、销项需 `Customer/Both`）→ 否则 `40109`。
3. 逐行取被关联单据（按 `OrderType` 分派四类单据仓储 `GetDetailAsync`）：不存在 → `40400`；已作废 → `40104`；往来不一致 → `40135`；方向不符 → `40134`；`Amount > OrderTotalAmount − GetInvoicedAmountAsync(...)` → `40133`（message 含单据号与未开票金额）。
4. 后端重算：`TaxAmount = Round(AmountExcludingTax × TaxRate, 2)`、`TotalAmount = AmountExcludingTax + TaxAmount`（忽略前端传值）。
5. `IUnitOfWork`：`BeginTransactionAsync` → `AddAsync`（发票 + 明细）→ `CommitAsync`。
6. `029` 审计：`Create` 动作，摘要「登记销项发票 12345678（乙客户，金额 500.00，关联 GI001）」。

**VoidInvoice**：`GetDetailAsync`（不存在 → `40400`；已作废 → `40104`）→ `UpdateStatusAsync(Voided)` + 审计 → 提交；**不写任何单据列**（未开票金额按聚合自动释放）。

**GetInvoicableOrders**：入参 `partnerId` + `type` → 方向映射单据类型集合（进项 → 采购入库 + 采购退货；销项 → 销售出库 + 销售退货）→ `IInvoiceQueryRepository.GetInvoicableAsync` → 映射 DTO（含未开票金额，前端默认按未开票金额填充）。

**GetInvoices / GetInvoiceById**：筛选（关键词含关联单据号）/ 映射 / 快照透传（`OrderNoSummary` 由 Mapper 拼接）。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.4 常量）

| 请求 | 规则 |
|---|---|
| `CreateInvoiceRequest` | `invoiceNo` 必填 1–50；`type` ∈ {0,1}；`partnerId` 必填；`invoiceDate` 必填；`amountExcludingTax` > 0 且 ≤ 9999999.99；`taxRate` 0–1（4 位小数）；`items` 必填非空、1–100 行、**`orderType + orderId` 不重复**；每行 `orderType` ∈ {0,1,2,3}、`orderId` 必填、`amount` > 0 且 ≤ 9999999.99；`remark` ≤200 |
| `GetInvoicesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50；`type` / `partnerId` 可空；`start` / `end` 可空且 `start <= end` |
| `GetInvoicableOrdersRequest` | `partnerId` 必填；`type` ∈ {0,1}；`page ≥ 1`；`pageSize` 1–100 |

- 存在性 / 方向 / 往来 / 金额等业务约束在 Handler（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（同既有约定）：5 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── invoice.ts                     # 发票接口层
└── views/
    └── InvoiceManagement/
        ├── InvoicesView.vue           # 发票列表
        ├── InvoiceFormPage.vue        # 新建发票（含关联单据子表格，独立页）
        └── InvoiceDetailView.vue      # 详情（含作废）
```

- 新建为独立页面（含关联单据子表格，`007` §0 形态选择）；详情为独立页面。

### 4.2 接口层

- `src/api/invoice.ts`：类型与后端 DTO 一一对应；`getInvoices` / `createInvoice` / `getInvoiceById` / `voidInvoice` / `getInvoicableOrders`。
- 日期范围参数转 UTC ISO（同既有约定）；金额展示 `toFixed(2)`、税率展示百分比（`13%`）。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `invoices` | `invoices` | `InvoicesView` |
| `invoices/new` | `invoiceNew` | `InvoiceFormPage` |
| `invoices/detail/:id` | `invoiceDetail` | `InvoiceDetailView` |

- `AppLayout.vue`「资金」分组追加「发票登记」（`025` §0.2 总表已预留 `invoices`）；`MENU_ROUTE_MAP` 增加 `invoiceDetail: 'invoices'`。

### 4.4 页面交互

**发票列表 `InvoicesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（关键词（发票号 / 往来 / 单据号）+ 类型下拉 + 往来 + 日期范围 + 搜索 / 重置）；操作行（「登记发票」primary + 导出（`027` 续行）+ 刷新 + 列设置）；列：序号、发票号、类型（`a-tag` §0.4）、往来单位、开票日期、不含税金额、税率、税额、**价税合计**、关联单据（`OrderNoSummary`，`ellipsis` + `tooltip`）、状态、创建时间、操作列（详情 / 作废（`status="danger"` + popconfirm + `voidingId`，仅正常发票））；服务端分页；作废行整体置灰。

**新建页 `InvoiceFormPage.vue`**：

- 表头：发票号、类型（`a-radio-group`：进项 / 销项）、往来单位（下拉：进项取供应商、销项取客户；选定后加载可开票单据）、开票日期（默认当天）、不含税金额（`a-input-number :min="0.01" :precision="2"`）、税率（`a-select`：0% / 1% / 3% / 6% / 9% / 13%，含「自定义」`a-input-number` 0–1 四位小数）、**税额与价税合计（computed 实时展示）**、备注。
- 关联区：可开票单据列表（`getInvoicableOrders`）——勾选后展示单号、单据日期、单据总额、已开票金额、**未开票金额**、本次开票金额（`a-input-number`，默认填未开票金额，`max` = 未开票金额）；「全部开票」按钮（一键填满所选行）。
- **一致性提示**：关联金额合计 ≠ 不含税金额时显示 `a-alert warning`「关联金额合计（X）与不含税金额（Y）不一致」（不阻断，仅提示，避免「一张票含多张单的部分金额」场景被堵死）；提交时仍按录入的不含税金额落库。
- 底部：提交（`submitting`）/ 取消；提交成功跳详情页；切类型 / 切往来时清空已选明细（防串数据）。

**详情页 `InvoiceDetailView.vue`**：`a-page-header` + `a-descriptions`（发票号 / 类型 / 往来 / 开票日期 / 不含税 / 税率 / 税额 / 价税合计 / 状态 / 备注 / 创建人 / 创建时间）+ 关联单据只读表格（单据类型 / 单号 / 单据日期 / 单据总额 / 本次开票金额）+ 底部作废按钮；id 不存在 → `a-result status="404"`。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 新建页提交 | `submitting` | 提交按钮 |
| 发票作废 | `voidingId` | popconfirm 确认按钮 |
| 可开票单据加载 | `candidatesLoading` | 关联区（`a-spin`，非按钮） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 不落单据「已开票金额」列 | 按 `InvoiceItems` 聚合推导 | `023` 落 `SettledAmount` 列是因为结算金额需在单据列表高频筛选与展示；发票金额**只在发票域使用**，聚合成本低（复合索引 `(OrderType, OrderId)`），且天然支持「作废自动释放」 |
| 作废自动释放金额 | 不写回单据 | 聚合口径下作废即不计入，无需补偿写入；不存在「释放失败导致金额永久占用」的不一致 |
| 复用 `SettlementOrderType` | 不新建枚举 | 同形枚举重复定义是本项目明确要避免的（后端规则 §5.3 精神，`038` 复用 `PartnerStatus` 同理）；若 `023` 落地时改名，双方同步（tasks 有联动项） |
| 税率存小数（0.13） | `numeric(5,4)` | 与行业计算口径一致；前端以百分比展示（13%）由前端换算，后端只做乘法 |
| 金额由后端重算 | 忽略前端 `taxAmount` / `totalAmount` | 与 `015` / `016` 的「金额后端重算」口径一致，避免篡改与精度差 |
| 只到单据级开票 | 不做到明细行 | 行级开票需要与实际发票的商品明细逐行对应（发票 PDF 解析 / 录入），手工登记场景下按单据金额更实用（如 1000 的单开 900） |
| 关联金额与不含税金额不一致仅提示 | 不阻断 | 现实中「一张发票包含多张单据的部分金额 + 其他费用」常见；硬一致会逼用户造数 |
| 方向 / 往来三重校验 | `40134` / `40135` / `40104` | 与 `023` 的核销校验同构（方向 / 往来 / 作废），保持两个平行能力的心智一致 |
| 候选单据查询独立仓储 | `IInvoiceQueryRepository` | 跨四表只读聚合不属于任何单据仓储（同 `023` 的 `ISettlementQueryRepository`） |
| 不做单据页展示已开票金额 | 范围外 | 需改 5 类单据页面与列表，收益低（发票域可检索）；后续如需再演进（在 `015` / `016` 等留注记） |
| 发票无「结算状态」 | 只有正常 / 作废 | 发票不存在「部分收付」语义；已开票金额按明细推进，主表状态只表达有效 / 作废 |
| 并发竞态 | 同事务内重复读取 + 校验 | 两请求同时给同一单据超开发票的窗口极小（单组织人工操作）；不引入行锁或额度台账（同 `036` 的取舍） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **CreateInvoice 成功**：断言税额与合计后端重算（前端传值被忽略）、明细快照（`OrderNo` / `OrderDate` / `OrderTotalAmount`）、`Commit` 被调用、审计 `RecordAsync` 参数。
- **CreateInvoice 异常**：明细空 `40110`；发票号重复 `40132`；往来不存在 `40400` / 停用 `40108` / 方向不符 `40109`；单据不存在 `40400` / 已作废 `40104` / 往来不一致 `40135` / 方向不符 `40134` / 超未开票金额 `40133`（message 含单号与未开票金额）；失败路径**无任何写入**且 `RollbackAsync` 被调用。
- **VoidInvoice**：成功（断言只 `UpdateStatusAsync`，不触碰任何单据仓储）；已作废 `40104`；不存在 `40400`。
- **金额口径**：`TaxAmount` 舍入（`0.13 × 33.33 = 4.3329 → 4.33`；`MidpointRounding.AwayFromZero` 边界如 `0.005 → 0.01`）；`TaxRate = 0` → 税额 0；`TaxRate` 边界 0 / 1 通过、1.0001 拒绝。
- **`GetInvoicableOrders`**：方向 → 单据类型集合映射（进项 → 采购入库 + 采购退货；销项 → 销售出库 + 销售退货）；仅返回未作废且未开票金额 > 0（断言透传仓储过滤）；`page` / `pageSize` 校验。
- **`GetInvoices` / `GetInvoiceById`**：筛选传参（含关键词命中关联单据号）、分页映射（含 `OrderNoSummary`）、快照透传、不存在 `40400`。
- **未开票金额恒等**：构造「开票 900 → 再开 100 → 作废第一张」链路，断言未开票金额序列为 `1000 → 100 → 0 → 900`（聚合口径的回归用例）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Invoices.InvoiceNo` `HasMaxLength` 50 == `InvoiceFieldConstraints.InvoiceNoMaxLength`；`TaxRate` 精度 `numeric(5,4)` == 规格口径；明细 `OrderNo` 列长 20 == `OrderFieldConstraints.OrderNoMaxLength`；`keyword` 50 / 51。

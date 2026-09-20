---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：报价单（erp-quotation）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `024-erp-order-flow`（订单域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制复用 `028`；取价复用 `036`（`GetEffectivePrices`），本规格**不重复定义取价优先级**。

## 0. 约定正文（唯一事实源）

### 0.1 报价单状态与流转

| 枚举 | 取值 | 文案 | `a-tag` | 允许操作 |
|---|---|---|---|---|
| `Draft = 0` | 草稿 | 草稿 | `blue` | 编辑 / 作废 / 转单 |
| `Converted = 1` | 已转订单 | 已转订单 | `green` | 仅查看 |
| `Voided = 2` | 已作废 | 已作废 | `gray` | 仅查看 |

- 派生展示：`Draft` 且 `ValidUntil < 今天` → 列表中额外标「已过期」（`orange`），**不改变状态**。
- 状态流转约束：编辑 / 作废仅限 `Draft`（否则 `40166`）；转单仅限 `Draft`（否则 `40167`）。

### 0.2 取价与金额口径

- **取价优先级**：客户协议价（`036`）→ 商品销售价（`012`）；前端按客户批量取价并标注来源，用户可改（改后标注消失）。**后端按前端传入单价落库**（与 `036` §0 一致，后端不二次取价）。
- **金额**：`Subtotal = Quantity × UnitPrice`、`TotalAmount = Σ Subtotal`，后端重算（`numeric(18,2)`），不信任前端。

### 0.3 转销售订单规则

- 转单生成一张销售订单（`024` 的 `SalesOrders` + `SalesOrderItems`），明细按报价单明细**原样复制**（商品 / 数量 / 单价），往来单位同报价客户，订单日期 = 转单当天。
- 报价单回写 `ConvertedOrderId` / `ConvertedOrderNo` 并置 `Converted`；同一报价单**只能转一次**。
- 转单**不动库存、不写流水、不产生应收**（订单本身是计划数据，`024` §0 已定）。

### 0.4 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 报价单 | `QuotationNo` | 唯一；`QT + yyyyMMdd + 4 位序号`（生成机制见 `015` §3.6 前缀参数化） | — |

### 0.5 菜单归属（在 `025` §0.2 表续行）

「销售（`sale`）」分组续行一子项（置于「销售订单」之前）：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 销售（`sale`） | 报价单（`quotations`） | `037` |

### 0.6 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 报价单 | `quotations` | `view` / `create` / `update` / `void` / `convert` | `/api/quotations*`、`/quotations` |

## 1. 总体设计

```
报价单（前端 /quotations 列表 + /quotations/new + /quotations/detail/:id）
  → QuotationsController
    → App.Core/Features/Quotations/<Action>/*RequestHandler
      → IQuotationRepository（报价单 + 明细）
        + ISalesOrderRepository（转单：创建销售订单）
        + IProductRepository / IPartnerRepository（校验）
        + IUnitOfWork（同一事务）
        → PostgreSQL（Quotations / QuotationItems / SalesOrders / SalesOrderItems）

取价（复用 033）
  开单页 → GET /api/partner-prices/effective?partnerId=&productIds=
```

核心原则：

- **报价是意向**：全程不碰库存 / 流水 / 结算；"生效"由转单后的销售订单承担。
- **转单原子**：报价单置 `Converted` 与销售订单落库在同一事务内。
- **模板复用**：表结构 / 校验 / 单号 / 快照沿用 `024` 订单域约定，不另立一套。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；`ValidUntil` 为纯日期用 `DateOnly` → `date`；金额 `numeric(18,2)`。

### 2.1 实体 `App.Core/Entities/Quotation.cs` 与表 `Quotations`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `QuotationNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `QT + yyyyMMdd + 4` |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 客户 |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 名称**快照** |
| `QuotationDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 报价日期 |
| `ValidUntil` | `DateOnly?` | `date` | NULL | 有效期至 |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | 总金额（后端计算） |
| `Status` | `QuotationStatus` | `smallint` | NOT NULL，默认 `Draft` | |
| `ConvertedOrderId` | `Guid?` | `uuid` | NULL | 转出的销售订单 id |
| `ConvertedOrderNo` | `string?` | `varchar(20)` | NULL | 订单号**快照** |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.2 实体 `App.Core/Entities/QuotationItem.cs` 与表 `QuotationItems`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `QuotationId` | `Guid` | `uuid` | NOT NULL，FK → `Quotations(Id)`，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductName` / `Unit` | `string` | `varchar(50)` / `varchar(20)` | NOT NULL | 名称 / 单位**快照** |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | |
| `UnitPrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 单价**快照** |
| `Subtotal` | `decimal` | `numeric(18,2)` | NOT NULL | 后端计算 |

### 2.3 字段约束（复用既有常量，**不新建常量类**）

| 用途 | 常量来源 |
|---|---|
| `QuotationNo` 长度（20）/ `keyword`（20）/ `Remark`（200）/ 明细行数上限（100） | `OrderFieldConstraints` |
| 明细 `Quantity`（1–999999）/ `UnitPrice`（0–9999999.99） | `ProductFieldConstraints` |

### 2.4 迁移

- 迁移：`dotnet ef migrations add AddErpQuotation -p src/App.Infrastructure -s src/App.Api`（建 2 表 + 唯一索引；外键不级联删除）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IQuotationRepository.GetPagedAsync(...)` | 列表（`keyword` 匹配单号 / 客户名快照；状态 / 日期闭区间；`CreatedAt DESC`） |
| `IQuotationRepository.GetDetailAsync(id)` | 主表 + 明细 |
| `IQuotationRepository.AddAsync(Quotation, IReadOnlyList<QuotationItem>, ...)` | 新增 |
| `IQuotationRepository.UpdateAsync(Quotation, IReadOnlyList<QuotationItem>, ...)` | 编辑（草稿；明细全量替换） |
| `IQuotationRepository.UpdateStatusAsync(id, status, convertedOrderId?, convertedOrderNo?, ...)` | 作废 / 转单状态回写 |
| `IQuotationRepository.GenerateNoAsync(date, ...)` | 单号 `QT` |

读模型：`QuotationListItem`（含客户名 / 明细数）、`QuotationDetail`、`EffectivePriceItem`（复用 `036`）。

### 3.2 错误码（`ErrorCode.cs`，从 `40166` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40166 | `QuotationNotEditable` | 报价单非草稿，不可编辑 / 作废 |
| 40167 | `QuotationNotConvertible` | 报价单不可转单（非草稿 / 已转 / 已作废） |

> 下一个可用业务码 → `40168`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/quotations` | GET | `Quotations/GetQuotations` | `PagedResult<QuotationListItemDto>` | `quotations.view` / 40000 |
| `/api/quotations` | POST | `Quotations/CreateQuotation` | `QuotationDetailDto` | `quotations.create` / 40000 / 40400 |
| `/api/quotations/{id:guid}` | GET | `Quotations/GetQuotationById` | `QuotationDetailDto` | `quotations.view` / 40400 |
| `/api/quotations/{id:guid}` | PUT | `Quotations/UpdateQuotation` | `QuotationDetailDto` | `quotations.update` / 40000 / 40166 / 40400 |
| `/api/quotations/{id:guid}/void` | PUT | `Quotations/VoidQuotation` | `QuotationDetailDto` | `quotations.void` / 40166 / 40400 |
| `/api/quotations/{id:guid}/convert` | POST | `Quotations/ConvertToOrder` | `{ orderId, orderNo }` | `quotations.convert` / 40167 / 40400 |

- 取价接口沿用 `036` 的 `GET /api/partner-prices/effective`（不改 `036`）。

### 3.4 关键用例流程（Handler）

- **CreateQuotation**：客户存在（`40400`）；明细非空、商品存在、数量 / 单价合法；后端重算小计 / 总额；生成单号；落库（同一事务）。
- **UpdateQuotation**：取报价单（`40400`）→ `Status != Draft` → `40166` → 校验同上 → 明细**全量替换**（先删后插）。
- **VoidQuotation**：`Status != Draft` → `40166` → `Status = Voided`。
- **ConvertToOrder**：取报价单（`40400`）→ `Status != Draft` → `40167` → 在 `IUnitOfWork` 内：创建销售订单（`SalesOrders` + 明细，沿用 `024` 仓储 `GenerateOrderNoAsync`）+ 回写报价单（`ConvertedOrderId` / `ConvertedOrderNo` / `Status = Converted`）→ `CommitAsync`。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateQuotationRequest` / `UpdateQuotationRequest` | `partnerId` 必填 GUID；`quotationDate` 必填；`validUntil` 可空且 ≥ `quotationDate`；`remark` ≤ 200；`items` 1–100 条；每条 `productId` 必填、`quantity` 1–999999、`unitPrice` 0–9999999.99 |
| `GetQuotationsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20；`status` 可选；日期范围可选 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── quotation.ts               # 报价单接口层（取价复用 partnerPrice.ts）
└── views/
    └── QuotationManagement/
        ├── QuotationsView.vue         # 报价单列表
        ├── QuotationFormPage.vue      # 新建 / 编辑（含明细子表 + 取价）
        └── QuotationDetailView.vue    # 详情（含转换信息 + 作废 / 转单）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `quotations` | `quotations` | `QuotationsView` |
| `quotations/new` | `quotationCreate` | `QuotationFormPage` |
| `quotations/detail/:id` | `quotationDetail` | `QuotationDetailView` |

- 「销售」分组续行「报价单」（置于「销售订单」前）；`meta.permission = 'quotations.view'`。

### 4.3 页面交互

- **`QuotationsView.vue`**：筛选（关键词 / 状态 / 日期范围）；列：报价单号、客户、报价日期、有效期（过期 `a-tag`）、金额、状态、操作列（查看 / 编辑（仅草稿）/ 作废 / 转订单）。
- **`QuotationFormPage.vue`**：主表（客户 / 报价日期 / 有效期 / 备注）+ 明细子表（选客户后批量取价、来源标注、可增删行）；提交成功跳详情。
- **`QuotationDetailView.vue`**：主表 + 明细 + 转换信息（订单号可点跳转订单详情）；草稿态显示编辑 / 作废 / 转订单。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 / 翻页 | `loading` | 搜索 / 翻页 + 表格 |
| 表单提交 | `submitting` | 提交按钮 |
| 作废 | `voidingId` | popconfirm 确认按钮 |
| 转订单 | `convertingId` | 转单按钮 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 报价不产生库存 / 资金 | 纯意向单 | 报价未成交，不应影响库存与应收（与订单"计划数据"一致） |
| 转单一次性 | 状态置 `Converted` | 避免多次转单产生重复订单；如需再报，新建报价单 |
| 明细全量替换 | 编辑先删后插 | 同 `023` / `028` 集合替换惯例，语义简单 |
| 有效期仅展示 | 不自动作废 | 过期是展示概念，自动改状态易误伤（客户可能仍接受） |
| 复用 `024` 订单约定 | 表 / 校验 / 单号同源 | 不另立一套订单风格 |

## 6. 单元测试设计

- **报价单用例**：`CreateQuotation` 金额重算 / 客户或商品不存在 `40400`；`UpdateQuotation` 非草稿 `40166` / 明细替换；`VoidQuotation` 非草稿 `40166`。
- **转单**：`ConvertToOrder` 成功（断言订单 + 报价单回写同一事务）/ 非草稿 `40167`；订单明细与报价明细一致；报价单不改变库存（假实现断言无库存调用）。
- **取价**：沿用 `036` 测试；本规格测"前端传入单价落库"（不二次取价）。
- **字段约束一致性**：`QuotationNo` 列长 == 常量；数量 / 单价边界。
- **清单守卫**：6 个端点纳入 `028` 既有守卫。

---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：客户价格、账期与信用额度（erp-partner-price）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-partner`（档案域）与 `erp-sale`（单据域 → 取价与授信校验）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> **应收口径不重复定义**：应收 = Σ 销售单总额 − Σ 销售退货总额 − Σ 已收，见 `specs/023-erp-settlement/design.md` §0；本规格只消费该口径。
> **与发票的关系**：信用额度看**应收**（资金维度），不看开票（票据维度）——`specs/032-erp-invoice/design.md` §0.3 的「未开票金额」与本域额度无推导关系，本规格落地时**不引入**按开票核减额度。

## 0. 取价、账期与授信口径（唯一事实源）

### 0.1 取价优先级

| 顺序 | 来源 | 说明 |
|---:|---|---|
| 1 | 客户协议价 | `PartnerPrices` 中该「客户 × 商品」的 `Price` |
| 2 | 商品销售价 | `Products.SalePrice`（兜底，未配置协议价时） |

- **取价是默认值、不是强约束**：单价由用户在开单页最终确定（业务允许临时议价），后端**照前端传入单价落库**（`015` / `016` 既有口径：单价为快照、金额后端重算）。本规格的价值在「默认带出正确 + 来源可见」，**不做**「后端强制回填协议价」——否则无法表达临时折扣（另见 §5）。
- 取价接口 `GetEffectivePrices` 为**批量**（入参 `partnerId` + `productIds`），一次请求返回 `productId → { unitPrice, source }`，`source ∈ {Agreement(0), Default(1)}`（前端用于「协议价 / 默认价」标注）。
- 协议价与商品价相等时 `source = Agreement`（有配置即视为协议价）。

### 0.2 往来单位授信字段

| 字段 | 语义 |
|---|---|
| `PaymentTermDays`（`integer`，≥ 0，默认 0） | 账期天数；`0` = 现结（无账期） |
| `CreditLimit`（`numeric(18,2)`，≥ 0，默认 0） | 信用额度；**`0` = 不限**（不做「额度为 0 即不能赊销」的解释，避免默认值把全部客户锁死） |

### 0.3 到期日与逾期

| 概念 | 规则 |
|---|---|
| 到期日 | `单据日期 + PaymentTermDays 天`（**推导，不落列**）；`PaymentTermDays = 0` 时到期日 = 单据日期 |
| 逾期判定 | `到期日 < 今天` 且**该单据未结清**（`UnsettledAmount > 0`，`023` §0）；结清即不再逾期 |
| 逾期天数 | `今天 − 到期日`（天，取整数） |
| 逾期粒度 | 往来对账按**客户**展示「逾期单据数 / 最早到期日 / 最大逾期天数」，下钻到未结单据（复用 `023` 的未结单据抽屉） |

### 0.4 信用额度校验

| 项 | 规则 |
|---|---|
| 校验时机 | **销售出库创建**时（`Sales/CreateSalesOrder`），在写单与扣库存之前；作废 / 退货不校验（回冲应收） |
| 校验对象 | 客户（仅 `Sales` 域；采购 / 供应商不校验） |
| 校验公式 | `当前应收余额 + 本单金额 ≤ 信用额度`（`CreditLimit = 0` 时跳过校验） |
| 应收余额 | `023` §0 口径：Σ 销售单总额 − Σ 销售退货总额 − Σ 已收（**不含本单**） |
| 超限错误 | `40130`，message 形如「客户 甲 超出信用额度（额度 1000.00，当前应收 800.00，本单 300.00）」 |
| 并发说明 | 校验与写单之间存在竞态（两单同时通过校验）；不做数据库级额度占用（决策见 §5），单组织人工开单场景可接受 |

## 1. 总体设计

```
客户价格（前端 /partner-prices）
  → PartnerPricesController
    → App.Core/Features/PartnerPrices/<Action>/*RequestHandler
      → IPartnerPriceRepository → PostgreSQL（PartnerPrices）

销售开单取价（前端调 → 后端返回默认价与来源）
  → GET /api/partner-prices/effective?partnerId=&productIds=...
    → PartnerPrices/GetEffectivePrices → IPartnerPriceRepository.GetEffectiveAsync
      + IProductRepository（取销售价兜底）

信用校验（改造既有用例）
  Sales/CreateSalesOrder
    → IPartnerRepository.GetByIdAsync（取 CreditLimit）
    → ISettlementQueryRepository.GetReceivableAmountAsync(partnerId)（`023` 仓储追加方法）
    → 超限 → 40130；否则照常写单 + 扣库存 + 流水 + 审计
```

核心原则：

- **取价是体验、授信是闸门**：取价只影响默认值（可议价），授信是硬拦截（不可绕过）——两者的严格程度刻意不同。
- **不复制应收口径**：额度校验消费 `023` 的口径（同一仓储方法），保证「对账页看到的应收」与「额度校验用的应收」永远一致。
- **不落到期日列**：到期日由账期推导（改账期会改变历史单据的到期日——本期接受该语义，见 §5）；避免冗余列与不一致。
- **改名与删除**：客户价删除即回退默认价；往来单位照旧只停用。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；金额 `numeric(18,2)`。

### 2.1 实体 `App.Core/Entities/PartnerPrice.cs` 与表 `PartnerPrices`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)` | 客户 |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | 商品 |
| `Price` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 协议单价 |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注（协议说明 / 生效范围） |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 唯一索引：`(PartnerId, ProductId)`；索引：`(PartnerId)`、`(ProductId)`。
- **只允许客户**（`PartnerType ∈ {Customer, Both}`）配置协议价（Handler 校验，`40000`）；供应商配置 → `40000`（采购价协议属范围外）。
- 外键不级联删除（商品 / 往来单位只停用）。

### 2.2 `Partners` 追加字段

| 列 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `PaymentTermDays` | `integer` | NOT NULL，默认 0 | 账期天数（0 = 现结） |
| `CreditLimit` | `numeric(18,2)` | NOT NULL，默认 0 | 信用额度（0 = 不限） |

- `Partner` 实体同步追加；`PartnerListItemDto` / `PartnerDetailDto` 追加两字段（列表展示账期，额度在详情 / 抽屉展示）。

### 2.3 读模型（`App.Core/Abstractions/`）

| 读模型 | 字段 | 用途 |
|---|---|---|
| `PartnerPriceListItem`（新增） | `Id` / `PartnerId` / `PartnerName` / `ProductId` / `ProductCode` / `ProductName` / `Unit` / `Price` / `SalePrice`（商品当前销售价，用于对比展示）/ `Remark` | 客户价格列表 |
| `EffectivePriceItem`（新增） | `ProductId` / `UnitPrice` / `Source`（`Agreement` / `Default`） | 批量取价 |
| `OverdueReceivableItem`（`023` 读模型扩展） | 在 `ReconciliationItem` 追加 `EarliestDueDate` / `MaxOverdueDays` / `OverdueOrderCount` | 往来对账逾期列（`023` 页面演进） |

### 2.4 迁移与字段约束

- 迁移：`dotnet ef migrations add AddErpPartnerPrice -p src/App.Infrastructure -s src/App.Api`（建 1 张表 + `Partners` 加 2 列，均有默认值）。
- **不新建常量类**：`Price` 上下界引用 `ProductFieldConstraints.PriceMinValue/PriceMaxValue`；`Remark` 200 引用 `OrderFieldConstraints.RemarkMaxLength`；`keyword` 上限 50（对齐 `Partners.Name`(50) / `Products.Code`(32) / `Name`(50) 的最大匹配列长）；`PaymentTermDays` 上限 3650（≈10 年，Validator 内联常量放 `PartnerFieldConstraints` 续行：`PaymentTermDaysMaxValue`）。

## 3. 后端设计

### 3.1 仓储接口

`IPartnerPriceRepository`（新增）：

| 方法 | 说明 |
|---|---|
| `Task<PartnerPrice?> GetByIdAsync(Guid id, ...)` | 按 id（含跟踪） |
| `Task<bool> ExistsAsync(Guid partnerId, Guid productId, Guid? excludeId, ...)` | 「客户 × 商品」唯一 |
| `Task<(IReadOnlyList<PartnerPriceListItem> Items, int Total)> GetPagedAsync(Guid? partnerId, Guid? productId, string? keyword, int page, int pageSize, ...)` | 列表：联查 `Partners` / `Products`（名称 / 编码 / 单位 / 当前销售价）；`CreatedAt DESC` |
| `Task<IReadOnlyList<EffectivePriceItem>> GetEffectiveAsync(Guid partnerId, IReadOnlyList<Guid> productIds, ...)` | 批量取价：左连接协议价 + 商品销售价，输出 `UnitPrice` 与 `Source` |
| `Task AddAsync(PartnerPrice, ...)` / `Task UpdateAsync(PartnerPrice, ...)` / `Task DeleteAsync(Guid id, ...)` | 新增 / 更新 / 删除 |

`IPartnerRepository`（`013` 定义，改造）：`UpdateAsync` 已支持全量字段；`GetPagedAsync` 与 DTO 追加账期 / 额度；`UpdatePartnerRequest` 追加两字段（全量覆盖语义，`AGENTS.md` §4.5）。

`ISettlementQueryRepository`（`023` 定义，本规格**追加 1 个方法**）：

| 方法 | 说明 |
|---|---|
| `Task<decimal> GetReceivableAmountAsync(Guid partnerId, ...)` | 客户当前应收余额（`023` §0 口径：销售净额 − 已收）；供额度校验 |
| `GetReconciliationAsync`（改造） | 返回项追加 `PaymentTermDays` / `EarliestDueDate` / `MaxOverdueDays` / `OverdueOrderCount`；签名追加 `overdueOnly` 与 `today`——逾期是派生值，「仅看逾期」无法下推 SQL，故在全部匹配往来上派生后再内存分页；基准日由用例传入，仓储不读系统时间（可测性） |

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40130 | `CreditLimitExceeded` | 客户超出信用额度（message 含额度 / 当前应收 / 本单金额） |
| 40131 | `PartnerPriceExists` | 该客户 + 商品的协议价已存在 |

> 复用：`40104 OrderVoided`（被校验客户不存在 / 停用走 `40400` / `40108`）、`40400` / `40000`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/partner-prices` | GET | `PartnerPrices/GetPartnerPrices` | `PagedResult<PartnerPriceListItemDto>` | `partnerPrices.view` / 40000 |
| `/api/partner-prices` | POST | `PartnerPrices/CreatePartnerPrice` | `PartnerPriceDetailDto` | `partnerPrices.create` / 40000 / 40131 / 40400 |
| `/api/partner-prices/{id:guid}` | GET | `PartnerPrices/GetPartnerPriceById` | `PartnerPriceDetailDto` | `partnerPrices.view` / 40400 |
| `/api/partner-prices/{id:guid}` | PUT | `PartnerPrices/UpdatePartnerPrice` | `PartnerPriceDetailDto` | `partnerPrices.update` / 40000 / 40131 / 40400 |
| `/api/partner-prices/{id:guid}` | DELETE | `PartnerPrices/DeletePartnerPrice` | `null` | `partnerPrices.delete` / 40400 |
| `/api/partner-prices/effective` | GET | `PartnerPrices/GetEffectivePrices` | `IReadOnlyList<EffectivePriceDto>` | `partnerPrices.view` / 40000 |

- 路由注意：`effective` 为固定段，置于 `{id:guid}` 之前。
- 往来单位侧：`Partners/UpdatePartner` 请求与出参追加 `paymentTermDays` / `creditLimit`（不新增端点）。

### 3.4 关键用例流程（Handler）

**CreatePartnerPrice**：取客户（不存在 → `40400`；停用 → `40108`；类型不含 Customer → `40000`「仅客户可配置协议价」）→ 取商品（不存在 → `40400`；停用 → `40107`）→ `ExistsAsync` → `40131`；新增（审计）。
**UpdatePartnerPrice**：取记录（`40400`）→ 更新 `price` / `remark`（**客户与商品不可改**：请求体不含 `partnerId` / `productId`，`AGENTS.md` §4.5 不可改字段）→ 审计。
**DeletePartnerPrice**：取记录（`40400`）→ 删除（回退默认价，无引用风险：历史单据单价已是快照）。
**GetEffectivePrices**：入参 `partnerId` + `productIds`（去重，≤ 100）→ 仓储批量取价 → 映射（缺商品 → `40400`；停用商品照常返回价格，前端过滤由 `pick` 列表负责）。

**CreateSalesOrder（改造：信用校验）**：
1. 既有校验（客户 / 商品 / 明细 / 金额重算）不变。
2. **信用校验（新增，位于库存扣减之前）**：`partner.CreditLimit > 0` 时 → `GetReceivableAmountAsync(partnerId)` → `receivable + totalAmount > creditLimit` → `40130`（message 含额度 / 应收 / 本单）。
3. 其余流程不变（先扣库存 → 插单 → 流水 → 审计）。

**GetReconciliation（改造）**：仓储返回项追加账期与逾期字段（`EarliestDueDate` = 该客户未结单据的最早到期日；`MaxOverdueDays` = `max(今天 − 到期日)` 仅计未结且已过期；`OverdueOrderCount`）→ Handler 与 Mapper 映射。

### 3.5 校验规则（FluentValidation，仅格式层，引用既有常量）

| 请求 | 规则 |
|---|---|
| `CreatePartnerPriceRequest` | `partnerId` / `productId` 必填；`price` 0 ~ 9999999.99（`ProductFieldConstraints.PriceMinValue/PriceMaxValue`）；`remark` ≤200 |
| `UpdatePartnerPriceRequest` | `price` / `remark` 同上（不含 `partnerId` / `productId`） |
| `GetPartnerPricesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50；`partnerId` / `productId` 可空 |
| `GetEffectivePricesRequest` | `partnerId` 必填；`productIds` 必填、去重后 1–100 项 |
| `UpdatePartnerRequest`（`013` 改造） | 追加 `paymentTermDays` 0–3650（`PartnerFieldConstraints.PaymentTermDaysMaxValue`）、`creditLimit` 0 ~ 9999999.99 |
| `GetReconciliationRequest`（`023` 改造） | 追加 `overdueOnly` 可空（默认 `false`） |

### 3.6 Swagger

- **不分组**（同既有约定）：6 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── partnerPrice.ts                # 客户价格接口层（含 getEffectivePrices）
└── views/
    └── PartnerPriceManagement/
        ├── PartnerPricesView.vue      # 客户价格列表
        └── PartnerPriceFormDrawer.vue # 新增 / 编辑抽屉
```

- 改造既有页面（不新建文件）：`PartnerManagement/PartnerFormDrawer.vue`（账期 / 额度）、`SalesManagement/SaleFormPage.vue`（批量取价与来源标注）、`SettlementManagement/ReconciliationView.vue`（逾期列与筛选）、`SalesManagement/SaleDetailView.vue`（到期日描述项）。

### 4.2 接口层

- `src/api/partnerPrice.ts`：`getPartnerPrices` / `getPartnerPrice` / `createPartnerPrice` / `updatePartnerPrice` / `deletePartnerPrice` / `getEffectivePrices` + 类型。
- `src/api/partner.ts` 扩展：`paymentTermDays` / `creditLimit` 字段。
- 到期日 / 逾期天数由**后端返回计算结果**（`EarliestDueDate` / `MaxOverdueDays`），前端不重复实现日期加减（避免前后端两套口径）。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `partner-prices` | `partnerPrices` | `PartnerPricesView` |

- `AppLayout.vue`「资金」分组追加「客户价格」（`025` §0.2 总表已预留 `partnerPrices`）。

### 4.4 页面交互

**客户价格列表 `PartnerPricesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（客户下拉（仅启用客户 / 两者）+ 商品下拉 + 关键词（客户名 / 商品编码 / 名称）+ 搜索 / 重置）；操作行（「新增协议价」primary + 导出（`027` 续行）+ 刷新 + 列设置）；列：序号、客户、商品编码、商品名称、单位、**协议价**、商品销售价（对比展示；协议价高于销售价时价格标橙提示「高于默认价」）、价差、备注、创建时间、操作列（编辑 / 删除（`status="danger"` + popconfirm + `deletingId`））；服务端分页。

**客户价格抽屉 `PartnerPriceFormDrawer.vue`**：新增（客户下拉 + 商品下拉（`getProductPickList`）+ 协议价 `a-input-number :min="0" :precision="2"` + 备注）与编辑（客户 / 商品只读展示，仅改价与备注）；打开先重置再回填（`009` 防串台约定）；提交 `submitting` + 防重入。

**往来单位抽屉（改造）**：追加「账期天数」（`a-input-number :min="0" :precision="0"`，单位「天」，提示「0 = 现结」）与「信用额度」（`a-input-number :min="0" :precision="2"`，提示「0 = 不限」）。

**销售开单页 `SaleFormPage.vue`（改造）**：选中客户后，对已选商品按 `getEffectivePrices(partnerId, productIds)` 批量取价（`pricesLoading`）；明细行单价默认填充生效价，并在单价下方标注来源（`a-tag`：`协议价` `arcoblue` / `默认价` `gray`）；用户手工改价后标注消失（视为议价）；切换客户时重新取价（已手工改过的行提示将被覆盖，采用「覆盖 + `Message.info`」）。

**往来对账页（改造）**：表格追加「账期 / 最早到期日 / 最大逾期天数」列（逾期 > 0 时标红）；筛选行追加「仅看逾期」`a-checkbox`；下钻抽屉沿用 `023` 的未结单据列表（追加到期日列）。

**销售出库详情（改造）**：`a-descriptions` 追加「到期日」（`单据日期 + 账期`，后端返回）。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询（客户价 / 对账） | `loading` | 搜索 / 翻页 + 表格 |
| 价格抽屉提交 | `submitting` | 提交按钮 |
| 行内删除 | `deletingId` | popconfirm 确认按钮 |
| 开单页批量取价 | `pricesLoading` | 明细区（`a-spin`，非按钮） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 取价为默认值而非强制 | 可议价（单价仍由用户决定） | 强制回填会让「临时折扣 / 促销」无法表达，业务会绕过系统改数据；取价的价值在「默认正确 + 来源可见」 |
| 后端不强制校验「单价 == 协议价」 | 不校验 | 同上一行；若将来需要审批例外，由 `042` 覆盖 |
| 授信为硬拦截 | 超限即 `40130` | 赊销风险必须由系统兜住（这是本规格的核心价值）；不可配置为仅提醒（范围外） |
| 额度 0 = 不限 | 语义约定 | 让存量客户（未配置）行为不变，避免升级后大面积无法开单 |
| 应收余额复用 `023` 口径 | `ISettlementQueryRepository.GetReceivableAmountAsync` | 对账页与额度校验必须同源；否则用户会看到「对账显示没超、开单却超了」 |
| 到期日不落列 | 推导（`单据日期 + 账期`） | 账期是客户属性，改账期应影响后续催收视角（本期接受「历史单据到期日随账期变化」的语义）；落列需在每张单据写快照，当前收益不足 |
| 逾期判定未结清优先 | 结清即不逾期 | 与 `023` 的「未结金额」直接联动，避免「已收款还标逾期」的荒谬提示 |
| 协议价删除即回退 | 无软删除 | 历史单据单价是快照（`015` §5），删除协议价不影响历史；避免软删除带来的唯一索引复杂度 |
| 唯一键（客户, 商品） | 单一协议价 | 多维（按仓 / 按业务员 / 按时间）定价属范围外；单值模型最简且覆盖主流场景 |
| 批量取价接口 | `effective` 一次请求 | 避免明细行 N 次请求；同时把「优先级 + 来源」口径放在后端一处 |
| 并发额度竞态不处理 | 校验 + 提示 | 数据库级额度占用需锁或额度台账，成本高；单组织人工开单场景下超限概率低，且超限后对账页可见 |
| 采购价协议不做 | 范围外 | 采购价受供应商谈判与审批影响（`042`），且采购成本口径已由 `026` 承担 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **客户价用例**：`CreatePartnerPrice` 成功 / 客户不存在 `40400` / 客户停用 `40108` / 客户类型非客户 `40000` / 商品不存在 `40400` / 商品停用 `40107` / 重复 `40131`；`UpdatePartnerPrice` 成功（不改客户与商品）/ `40131`（排除自身）/ `40400`；`DeletePartnerPrice` 成功 / `40400`；`GetPartnerPrices` 筛选与分页映射（含当前销售价对比）。
- **`GetEffectivePrices`**：有协议价 → `Agreement` 与协议价；无协议价 → `Default` 与 `SalesPrice`；协议价等于销售价 → `Agreement`；商品不存在 `40400`；`productIds` 去重与上限。
- **信用校验**：额度 0 → 不查询应收（断言未调用）；额度 > 0 且 `应收 + 本单 ≤ 额度` → 通过；`> 额度` → `40130`（message 含三个数字）；断言校验在**库存扣减之前**（失败路径无库存 / 流水 / 单据变化）；应收查询使用 `023` 口径方法（假实现断言传参）。
- **账期与逾期（`GetReconciliation`）**：`PaymentTermDays` 透传；`EarliestDueDate` / `MaxOverdueDays` / `OverdueOrderCount` 计算（含「已结清不逾期」与「账期 0 → 到期日 = 单据日期」两处边界）；`overdueOnly` 过滤。
- **往来单位改造**：`UpdatePartner` 全量覆盖账期 / 额度（缺字段 → 清空为 0）；边界 `paymentTermDays` 3650 / 3651、`creditLimit` 0 与上限。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`PartnerPrices.Price` 精度 `numeric(18,2)` 与商品价格同源；`Partners.CreditLimit` 精度一致；`PaymentTermDaysMaxValue` 单点定义；`keyword` 50 / 51。

---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：多币种与合并报表（erp-multi-currency）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；承接 `057-erp-multi-org`（多公司隔离），扩展 `033-erp-general-ledger`（凭证分录 + 报表）。
> 凭证 / 期间 / 报表取数见 `specs/033-erp-general-ledger/design.md` §0；公司模型见 `specs/057-erp-multi-org/design.md` §2.1。

## 0. 约定正文（唯一事实源）

### 0.1 币种 / 本位币 / 金额语义

| 项 | 规则 |
|---|---|
| 币种 | `Code`（3 位，如 `CNY` / `USD`）+ 名称 / 符号 / 小数位（0–4）/ 启停；预置 `CNY` 不可删 |
| 公司本位币 | `Company.BaseCurrencyId`（`057` 表扩展）；**已有任何业务 / 账务数据后不可改**（`40200`） |
| 金额列语义 | 单据 / 凭证的既有金额列（`TotalAmount` / 借贷金额等）= **本位币金额**（`原币 × 汇率`，`numeric(18,2)`） |
| 原币推导 | `原币 = 本位币 ÷ 汇率`（前端展示，后端不落原币列，避免双写不一致） |
| 汇率精度 | `numeric(18,6)` |

### 0.2 汇率取值与折算

| 项 | 规则 |
|---|---|
| 存续 | `ExchangeRate`：`(FromCurrencyId, ToCurrencyId, EffectiveDate)` 唯一；金额 = 1 单位 `From` 折为 `To` |
| 取值 | 取**生效日期不晚于业务日期**的最新一条；无记录 → `40199` |
| 同币种 | `From == To` → 汇率 `1`（无需维护记录） |
| 币种停用 | `From` 或 `To` 停用 → `40198` |
| 快照 | 单据 / 分录落库时**冻结** `ExchangeRate`；后续汇率变动不影响历史单据 |

### 0.3 受影响实体（唯一事实源）

| 分组 | 变更 |
|---|---|
| 单据（`057` §0.2 的采购 / 销售 / 结算与发票组） | 追加 `CurrencyId`（FK，NOT NULL，默认 = 公司本位币）+ `ExchangeRate`（`numeric(18,6)`，默认 1） |
| `VoucherEntry`（凭证分录） | 追加 `CurrencyId` + `ExchangeRate`（来源单据快照透传；手工凭证默认本位币 / 汇率 1） |
| `Company`（`057`） | 追加 `BaseCurrencyId`（FK，NOT NULL） |
| 明细表 | **不加**（金额随主表语义，为本位币） |

- **库存 / 资金余额不引入币种**：按本位币记录（§5 范围外：不做外币重估）。

### 0.4 合并报表口径

| 项 | 规则 |
|---|---|
| 输入 | `year` + 报告币种 `currencyId`（缺省 = 当前公司本位币） |
| 取数 | 各公司按其本位币出 `033` §0.4 的资产负债表 / 利润表 |
| 折算 | 按 §0.2 取"该年 12-31（或 `year` 末）"的汇率折算到报告币种 |
| 合并 | 各公司折算后**逐行相加**（按一级科目）；**不做内部交易抵消** |
| 一致性 | 单公司、单币种时结果与 `033` 报表**逐值一致** |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 币种 | `currencies` | `view` / `create` / `update` / `status` | `/api/currencies*`、`/currencies` |
| 汇率 | `exchangeRates` | `view` / `update`（含删除） | `/api/exchange-rates*`、`/currencies`（同页 tab） |
| 合并报表 | `consolidatedReports` | `view` | `/api/reports/consolidated`、`/consolidated-reports` |

### 0.6 菜单归属（在 `025` §0.2 表续行）

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 系统（`system`） | 币种与汇率（`currencies`） | `058` |
| 报表（`report`） | 合并报表（`consolidatedReport`） | `058` |

## 1. 总体设计

```
多币种（前端 /currencies、/consolidated-reports）
  → CurrenciesController / ExchangeRatesController / ConsolidatedReportsController
    → App.Core/Features/<Currencies|ExchangeRates|ConsolidatedReports>/<Action>/*RequestHandler
      → ICurrencyRepository / IExchangeRateRepository / IConsolidatedReportQueryRepository
        + ICompanyRepository（本位币，057）
        + IFinancialReportQueryRepository（033 各公司报表）
        → PostgreSQL（Currencies / ExchangeRates / Companies / Vouchers / VoucherEntries）

单据 / 凭证折算（横切）
  单据 Create* Handler
    → IExchangeRateResolver.ResolveAsync(from, to, businessDate)（取最新；缺 40199）
    → 金额列存本位币；落 CurrencyId + ExchangeRate 快照
    → 自动凭证透传 CurrencyId / ExchangeRate 至分录
```

核心原则：

- **本位币单一语义**：金额列只存本位币，原币靠汇率推导（避免双写漂移）。
- **快照冻结**：单据 / 分录冻结汇率，历史不随汇率变动重算。
- **合并只折算相加**：不做抵销（§5 范围外）；单公司单币种时与 `033` 报表逐值一致（可断言）。
- **单币种兼容**：仅 `CNY` 时汇率恒 1，行为与升级前一致。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/Currency.cs` 与表 `Currencies`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(3)` | NOT NULL，唯一索引 | `CNY` / `USD` |
| `Name` | `string` | `varchar(20)` | NOT NULL | 名称 |
| `Symbol` | `string?` | `varchar(5)` | NULL | 符号 |
| `DecimalPlaces` | `int` | `integer` | NOT NULL，默认 2，0–4 | 展示精度 |
| `Status` | `CurrencyStatus` | `smallint` | NOT NULL，默认 `Enabled` | 启停 |
| `IsPreset` | `bool` | `boolean` | NOT NULL，默认 false | `CNY` 预置不可删 |
| 审计 | | | | |

### 2.2 实体 `App.Core/Entities/ExchangeRate.cs` 与表 `ExchangeRates`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `FromCurrencyId` | `Guid` | `uuid` | NOT NULL，FK → `Currencies(Id)` | 源币 |
| `ToCurrencyId` | `Guid` | `uuid` | NOT NULL，FK → `Currencies(Id)` | 目标币 |
| `Rate` | `decimal` | `numeric(18,6)` | NOT NULL，> 0 | 1 `From` = `Rate` `To` |
| `EffectiveDate` | `DateOnly` | `date` | NOT NULL | 生效日期 |
| 审计 | | | | |

- **唯一索引** `(FromCurrencyId, ToCurrencyId, EffectiveDate)`。

### 2.3 扩展与常量

| 对象 | 变更 |
|---|---|
| `Company`（`057`） | 追加 `BaseCurrencyId`（FK，NOT NULL） |
| 单据（§0.3） | 追加 `CurrencyId` / `ExchangeRate` |
| `VoucherEntry` | 追加 `CurrencyId` / `ExchangeRate` |
| `CurrencyFieldConstraints` | `CodeLength = 3` / `NameMaxLength = 20` / `SymbolMaxLength = 5` / `DecimalPlacesMin = 0` / `DecimalPlacesMax = 4` |
| `ExchangeRateFieldConstraints` | `RatePrecision = 6` |

- 枚举：`CurrencyStatus`（`Enabled = 1` / `Disabled = 0`）。

### 2.4 迁移与种子

- 迁移 `AddErpMultiCurrency`：建 `Currencies` / `ExchangeRates`；`InsertData` 预置 `CNY`；受影响表加列（默认 = 公司本位币 / 汇率 1）并回填。
- `DatabaseInitializer` 幂等保证 `CNY` 存在且为各公司本位币默认值。

## 3. 后端设计

### 3.1 组件

| 类型 | 位置 | 说明 |
|---|---|---|
| `ICurrencyRepository` / `IExchangeRateRepository` | `App.Core/Abstractions/` | 币种 CRUD（含预置保护）/ 汇率 CRUD 与"取最新"查询 |
| `IExchangeRateResolver` | `App.Core/Abstractions/` | `ResolveAsync(from, to, date)`：同币种 1；取 ≤ date 最新；停用 `40198` / 缺 `40199` |
| `IConsolidatedReportQueryRepository` | `App.Core/Abstractions/` | 各公司报表 + 汇率 → 折算合并（只读） |

- 读模型：`CurrencyListItem` / `ExchangeRateListItem` / `ConsolidatedReport` / `ConsolidatedReportItem`。

### 3.2 错误码（`ErrorCode.cs`，从 `40198` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40198 | `CurrencyNotEnabled` | 币种已停用（不可用于折算 / 设置） |
| 40199 | `ExchangeRateMissing` | 缺少该币种该日期的汇率，无法折算 |
| 40200 | `BaseCurrencyImmutable` | 公司已有业务 / 账务，本位币不可修改 |

> 下一个可用业务码 → `40201`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/currencies` | GET/POST | `Currencies/GetCurrencies` / `CreateCurrency` | `PagedResult<..>` / `CurrencyDetailDto` | `currencies.view` / `create` / 40000 |
| `/api/currencies/{id:guid}` | PUT | `Currencies/UpdateCurrency` | `CurrencyDetailDto` | `currencies.update` / 40000 / 40400 |
| `/api/currencies/{id:guid}/status` | PUT | `Currencies/UpdateCurrencyStatus` | `CurrencyDetailDto` | `currencies.status` / 40198 / 40200 / 40400 |
| `/api/exchange-rates` | GET/POST | `ExchangeRates/GetExchangeRates` / `CreateExchangeRate` | `PagedResult<..>` / `..DetailDto` | `exchangeRates.view` / `update` / 40000 / 40198 |
| `/api/exchange-rates/{id:guid}` | DELETE | `ExchangeRates/DeleteExchangeRate` | `null` | `exchangeRates.update` / 40400 |
| `/api/companies/{id:guid}/base-currency`（`057` 扩展） | PUT | `Companies/UpdateBaseCurrency` | `CompanyDetailDto` | `companies.update` / 40198 / 40200 / 40400 |
| `/api/reports/consolidated` | GET | `ConsolidatedReports/GetConsolidatedReport` | `ConsolidatedReportDto` | `consolidatedReports.view` / 40000 / 40199 |

### 3.4 关键用例流程（Handler）

- **CreateExchangeRate**：`From` / `To` 存在且启用（`40198`）；`(From, To, EffectiveDate)` 唯一（`40000`）；落库。
- **UpdateCurrencyStatus**：停用 `CNY` 或某公司本位币 → `40198` / `40200`。
- **UpdateBaseCurrency**：该公司存在任何业务 / 账务数据 → `40200`；目标币存在且启用（`40198`）。
- **单据 Create*（改造）**：按业务日期 `ResolveAsync(币种 → 公司本位币, 单据日期)`（缺 `40199`）→ 金额列存本位币 → 落 `CurrencyId` / `ExchangeRate` 快照 → 自动凭证透传。
- **GetConsolidatedReport**：逐公司取 `033` 报表 → 按期末汇率折算（缺 `40199`）→ 逐行相加 → 返回（单公司单币种与 `033` 一致）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdateCurrencyRequest` | `code` 必填 3 且 `^[A-Z]{3}$`；`name` 必填 1–20；`symbol` ≤ 5；`decimalPlaces` 0–4 |
| `CreateExchangeRateRequest` | `fromCurrencyId` / `toCurrencyId` 必填且不同；`rate` > 0；`effectiveDate` 必填 |
| `GetExchangeRatesRequest` | 分页；可选 `from` / `to` / 日期区间 |
| `GetConsolidatedReportRequest` | `year` 2000–2999；`currencyId` 可空 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── currency.ts                    # 币种 + 汇率
│   └── consolidatedReport.ts
└── views/
    ├── CurrencyManagement/
    │   └── CurrenciesView.vue         # 币种列表 + 汇率 tab
    └── ReportManagement/
        └── ConsolidatedReportView.vue # 合并报表（资产负债表 / 利润表）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `currencies` | `currencies` | `CurrenciesView` |
| `consolidated-reports` | `consolidatedReport` | `ConsolidatedReportView` |

- 「系统」分组续行「币种与汇率」；「报表」分组续行「合并报表」。

### 4.3 页面交互

- **`CurrenciesView.vue`**：币种 tab（列表 + 抽屉表单 + 启停，`CNY` 只读）+ 汇率 tab（列表 + 新增 / 删除，按币种对与日期）。
- **`ConsolidatedReportView.vue`**：年度 + 报告币种选择；两张表（资产负债表 / 利润表）按一级科目列示；未换算提示。
- **单据表单（改造）**：增「币种」下拉（默认公司本位币）；金额输入按原币，旁边展示折算本位币（后端返回汇率）。

### 4.4 接口层

- `api/currency.ts`：币种 / 汇率 CRUD；`api/consolidatedReport.ts`：合并报表。
- 既有单据 `api/*.ts`：类型增 `currencyId` / `exchangeRate`（可空，兼容）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 金额列**只存本位币** | 原币推导 | 避免双写与原币 / 本位币不一致，改造面小 |
| 汇率**快照冻结** | 落 `ExchangeRate` | 历史单据不随汇率变动重算（会计稳定性） |
| 汇率**单一折算率** | 按生效日期取最新 | 多汇率类型（即期 / 平均 / 历史）复杂度高，收益不足 |
| 合并**只折算相加** | 不抵销 | 内部交易抵消需内部交易标识与抵消规则，另评 |
| 库存 / 资金**不重估** | 本位币记录 | 外币重估涉及汇兑损益与期末重估流程，另评 |
| 本位币**有业务后不可改** | `40200` | 改本位币等价于重记账套，风险极高 |
| 单币种**兼容** | 仅 `CNY` 时汇率 1 | 升级后单币种行为不变 |
| 预置 `CNY` **不可删** | `IsPreset` | 避免无本位币可用的空状态 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **汇率**：同日唯一；取最新 ≤ 业务日期；同币种 1；停用 `40198`；缺 `40199`。
- **折算**：单据本位币金额 = 原币 × 汇率（快照）；汇率变动不改历史。
- **本位币**：有数据后改 `40200`；停用本位币币种 `40198`。
- **凭证**：自动凭证透传 `CurrencyId` / `ExchangeRate`；手工凭证默认本位币。
- **合并报表**：单公司单币种与 `033` 报表逐值一致；两公司折算相加正确；缺汇率 `40199`。
- **回归**：`057` 隔离用例；仅 `CNY` 时既有用例全绿。
- **字段约束一致性**：常量与 EF 列长一致。

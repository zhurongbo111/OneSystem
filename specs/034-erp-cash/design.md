---
created: 2026-09-20
updated: 2026-09-23
---

# 设计规格：资金出纳（erp-cash）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；**小改造 `023`**（`Settlement` 增 `BankAccountId`），改造风格参照 `023` 自身的迁移。
> 权限机制复用 `028`；报表式只读聚合参照 `025`。

## 0. 约定正文（唯一事实源）

### 0.1 账户类型与结算方式匹配

| 枚举 | 取值 | 说明 |
|---|---|---|
| `BankAccountType` | `Cash = 1`（现金）/ `Bank = 2`（银行） | 资金账户类型 |
| `BankAccountStatus` | `Enabled = 1` / `Disabled = 0` | |

| 收付款结算方式（`023`） | 允许关联的账户类型 |
|---|---|
| `Cash`（现金） | `Cash` 账户 |
| `BankTransfer`（银行转账） | `Bank` 账户 |
| `Other`（其他） | 可空（不关联账户） |

- 不匹配 → `40162`（`BankAccountTypeMismatch`）。

### 0.2 余额与日记账口径

| 概念 | 规则 |
|---|---|
| 账户余额 | `初始余额 + Σ 收款（Receipt）− Σ 付款（Payment）`（只计未作废收付款单） |
| 期初余额（日记账） | `初始余额 + start 前全部收付款净额` |
| 期末余额 | `期初 + 区间收 − 区间付` |
| 流水方向 | 收款 = 收（`+`）、付款 = 付（`−`） |
| 数据来源 | 只读聚合 `Settlements`（`023`），**不单独落流水表** |

### 0.3 唯一性与删除保护

| 对象 | 字段 | 唯一范围 | 冲突错误码 |
|---|---|---|---|
| 资金账户 | `Code` | 全局 | `40160` |

| 动作 | 前置检查 | 错误码 |
|---|---|---|
| 删除资金账户 | 无收付款单引用 | `40161` |

### 0.4 菜单归属（在 `025` §0.2 表续行）

「财务（`finance`）」分组续行两子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 财务（`finance`） | 银行账户（`bankAccounts`）/ 资金日记账（`cashJournals`） | `034` |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 资金账户 | `bankAccounts` | `view` / `create` / `update` / `delete` / `status` | `/api/bank-accounts*`、`/bank-accounts` |
| 资金日记账 | `cashJournals` | `view` | `/api/cash-journals*`、`/cash-journals` |

## 1. 总体设计

```
资金出纳（前端 /bank-accounts、/cash-journals，域目录 CashManagement/）
  → BankAccountsController / CashJournalsController
    → App.Core/Features/<BankAccounts|CashJournals>/<Action>/*RequestHandler
      → IBankAccountRepository（+ 余额聚合）
        + ICashJournalQueryRepository（只读聚合 Settlements）
        → PostgreSQL（BankAccounts / Settlements）

收付款关联账户（023 改造）
  CreateSettlement → 校验账户存在 + 类型匹配（40162）→ 落库 BankAccountId
```

核心原则：**账户是主数据、流水是派生**——资金日记账不落流水表，由 `023` 收付款单按账户聚合；避免"两套流水不一致"。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；金额 `numeric(18,2)`；枚举 `smallint`。

### 2.1 实体 `App.Core/Entities/BankAccount.cs` 与表 `BankAccounts`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 账户编码 |
| `Name` | `string` | `varchar(50)` | NOT NULL | 账户名称 |
| `Type` | `BankAccountType` | `smallint` | NOT NULL | 现金 / 银行 |
| `BankName` | `string?` | `varchar(100)` | NULL | 开户行（银行账户） |
| `AccountNo` | `string?` | `varchar(30)` | NULL | 银行账号（银行账户） |
| `InitialBalance` | `decimal` | `numeric(18,2)` | NOT NULL，默认 `0` | 初始余额 |
| `Status` | `BankAccountStatus` | `smallint` | NOT NULL，默认 `Enabled` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.2 `023` 表改造：`Settlements` 增列

| 项 | 变化 |
|---|---|
| 列 | `Settlements` 增 `BankAccountId`（`uuid`，NULL，FK → `BankAccounts(Id)`，索引） |
| 实体 | `Settlement` 增 `BankAccountId`（`Guid?`） |
| 仓储 | `ISettlementRepository` 的 `GetPagedAsync` / `GetDetailAsync` 出参加账户名（读模型增 `BankAccountName`） |
| 接口 | `CreateSettlementRequest` 增可空 `bankAccountId`；`GetUnsettledOrders` 不受影响 |

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `BankAccountFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `BankNameMaxLength = 100` / `AccountNoMaxLength = 30` / `RemarkMaxLength = 200` |

### 2.4 迁移与种子

- 迁移：`dotnet ef migrations add AddErpCash -p src/App.Infrastructure -s src/App.Api`（建 `BankAccounts` + `Settlements.BankAccountId`）。
- 种子（`DatabaseInitializer` 幂等）：预置一个**现金账户**（`Code = CASH`，`Type = Cash`，`InitialBalance = 0`），保证现金结算可开箱使用。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IBankAccountRepository.GetPagedAsync(...)` / `GetByIdAsync` / `ExistsByCodeAsync` | |
| `IBankAccountRepository.GetBalancesAsync()` | 各账户余额（`初始余额 + Σ收 − Σ付`，只计未作废收付款） |
| `IBankAccountRepository.IsReferencedAsync(id)` | 删除保护（被收付款单引用） |
| `IBankAccountRepository.AddAsync` / `UpdateAsync` / `DeleteAsync` | |
| `ICashJournalQueryRepository.GetJournalAsync(bankAccountId, start, end)` | 日记账（期初 + 流水 + 期末） |

读模型：`BankAccountListItem`（含派生列 `Balance`）、`BankAccountBalanceItem`、`CashJournalEntryItem`（`Date` / `SettlementNo` / `Summary` / `Debit`（收）/ `Credit`（付）/ `Balance`）、`CashJournalResult`（`OpeningBalance` / `Entries` / `ClosingBalance`）；
`023` 收付款单增 `BankAccountId` 后，列表 / 详情联查资金账户名，故仓储出参由实体改为读模型 `SettlementListItem` / `SettlementDetail`（字段与实体 1:1 + 联查列 `BankAccountName`）。

> 账户下拉复用列表接口（`GET /api/bank-accounts`，启用 + 按类型过滤），故不设「取全部启用账户」的独立仓储方法。

### 3.2 错误码（`ErrorCode.cs`，从 `40160` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40160 | `BankAccountCodeExists` | 资金账户编码已存在 |
| 40161 | `BankAccountInUse` | 资金账户已被收付款单引用，禁止删除 |
| 40162 | `BankAccountTypeMismatch` | 结算方式与资金账户类型不匹配 |

> 下一个可用业务码 → `40163`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/bank-accounts` | GET | `BankAccounts/GetBankAccounts` | `PagedResult<BankAccountListItemDto>` | `bankAccounts.view` / 40000 |
| `/api/bank-accounts` | POST | `BankAccounts/CreateBankAccount` | `BankAccountDetailDto` | `bankAccounts.create` / 40000 / 40160 |
| `/api/bank-accounts/{id:guid}` | GET | `BankAccounts/GetBankAccountById` | `BankAccountDetailDto` | `bankAccounts.view` / 40400 |
| `/api/bank-accounts/{id:guid}` | PUT | `BankAccounts/UpdateBankAccount` | `BankAccountDetailDto` | `bankAccounts.update` / 40000 / 40160 / 40400 |
| `/api/bank-accounts/{id:guid}` | DELETE | `BankAccounts/DeleteBankAccount` | `null` | `bankAccounts.delete` / 40161 / 40400 |
| `/api/bank-accounts/{id:guid}/status` | PUT | `BankAccounts/UpdateBankAccountStatus` | `BankAccountDetailDto` | `bankAccounts.status` / 40400 |
| `/api/bank-accounts/summary` | GET | `BankAccounts/GetBankAccountSummary` | `IReadOnlyList<BankAccountBalanceItemDto>` | `bankAccounts.view` |
| `/api/cash-journals` | GET | `CashJournals/GetCashJournal` | `CashJournalDto` | `cashJournals.view` / 40000 |

### 3.4 `023` 改造

- `CreateSettlement`：`bankAccountId` 非空时 → 账户存在（`40400`）、启用、类型与 `method` 匹配（`40162`）；落库 `BankAccountId`。
- `VoidSettlement`：不变（余额聚合天然排除作废单）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateBankAccountRequest` / `UpdateBankAccountRequest` | `code` 必填 1–20；`name` 必填 1–50；`type` 枚举合法；`type = Bank` 时 `bankName` 必填 ≤ 100、`accountNo` ≤ 30；`type = Cash` 时二者忽略；`initialBalance` ≥ 0 |
| `GetBankAccountsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |
| `GetCashJournalRequest` | `bankAccountId` 必填 GUID；`start` / `end` 必填且 `start ≤ end` |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── bankAccount.ts             # 资金账户 + 资金日记账接口层
└── views/
    └── CashManagement/
        ├── BankAccountsView.vue       # 资金账户列表 + 余额总览
        ├── BankAccountFormDrawer.vue  # 账户新增 / 编辑抽屉
        └── CashJournalsView.vue       # 资金日记账
```

- `views/SettlementManagement/SettlementFormPage.vue`（`023`）追加「资金账户」选择（依赖结算方式联动过滤账户类型）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `bank-accounts` | `bankAccounts` | `BankAccountsView` |
| `cash-journals` | `cashJournals` | `CashJournalsView` |

- 「财务」分组续行两项；`meta.permission = 'bankAccounts.view'` / `'cashJournals.view'`。

### 4.3 页面交互

- **`BankAccountsView.vue`**：顶部余额总览（`a-statistic` 卡片：账户数 / 总余额）；列表（筛选：关键词 / 类型 / 状态）；列：编码、名称、类型（`a-tag`）、开户行、账号、当前余额、状态、操作列。
- **`BankAccountFormDrawer.vue`**：编码 / 名称 / 类型（`a-radio`，切换时控制开户行 / 账号显示）/ 开户行 / 账号 / 初始余额 / 状态 / 备注。
- **`CashJournalsView.vue`**：筛选（账户 `a-select`、日期范围）；期初余额展示；流水表格（日期、单据号、摘要、收、付、结余）；期末余额；空态友好。
- **`SettlementFormPage.vue`（`023` 改造）**：结算方式切换时，账户下拉按类型过滤（现金 / 银行 / 其他不显示账户）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 日记账不落流水表 | 由 `023` 收付款单聚合 | 收付款单已是资金流水来源，落两套必然不一致 |
| 账户类型与结算方式强校验 | `40162` | 防止"现金结算挂银行账户"的脏数据 |
| 账户余额含初始余额 | `InitialBalance` 列 | 上线时账户已有余额，需作为起点 |
| 预置现金账户 | 种子 | 保证 `Cash` 结算开箱可用 |
| 凭证不按账户细分 | 沿用 `033` 的 `Bank` 映射科目 | 一期核算到科目级；按账户核算属辅助核算演进 |

## 6. 单元测试设计

- **账户**：`CreateBankAccount` 成功 / 编码重复 `40160` / 银行账户缺开户行或账号 `40000`；`DeleteBankAccount` 被引用 `40161` / 成功；筛选分页。
- **余额与日记账**：`GetBalancesAsync` 口径（初始 + 收 − 付，排除作废）；日记账期初 / 期末与逐笔结余正确；空区间返回期末 = 期初。
- **类型匹配**：`CreateSettlement` 现金单挂银行账户 → `40162`；银行单挂现金账户 → `40162`；`Other` 不关联账户通过。
- **字段约束一致性**：`BankAccountFieldConstraints` 与 EF 列长一致。
- **清单守卫**：8 个端点纳入 `028` 既有守卫。

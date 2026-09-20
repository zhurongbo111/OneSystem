---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：财务主数据（erp-finance-master）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `009-user-management`（用户域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制**复用 `028`**；本规格只续行权限点。科目树实现参照 `030-erp-org-employee` 的部门树（`ParentId` 自引用 + 内存建树）。

## 0. 约定正文（唯一事实源）

### 0.1 科目类别与余额方向（枚举口径）

| 枚举 | 取值 | 说明 |
|---|---|---|
| `AccountCategory` | `Asset = 1` / `Liability = 2` / `Equity = 3` / `Cost = 4` / `ProfitLoss = 5` | 资产 / 负债 / 所有者权益 / 成本 / 损益 |
| `AccountDirection` | `Debit = 1` / `Credit = 2` | 借 / 贷（科目余额方向） |
| `AccountStatus` | `Enabled = 1` / `Disabled = 0` | 停用科目不可被新凭证引用（`033`） |
| `TaxRateStatus` | `Enabled = 1` / `Disabled = 0` | |

- **末级判定**：无子科目 = 末级（可记账）；为派生规则，不落列。
- **类别与方向的默认关系**（编辑表单默认值，可改）：资产 / 成本 → `Debit`；负债 / 权益 / 损益 → `Credit`（损益类实际按科目而定，用户可改）。

### 0.2 唯一性与删除保护

| 对象 | 字段 | 唯一范围 | 冲突错误码 |
|---|---|---|---|
| 科目 | `Code` | 全局 | `40149` |
| 税率 | `Code` | 全局 | `40151` |
| 税率 | `Name` | 全局 | `40152` |

| 动作 | 前置检查 | 错误码 |
|---|---|---|
| 删除科目 | 无子科目 **且** 未被凭证分录引用（`033` 落地后检查） | `40150` / `40153` |
| 删除 / 停用预置科目 | 预置科目不可删（`IsPreset = true` → 拒绝删除） | `40150` |

### 0.3 菜单归属（在 `025` §0.2 表续行）

新增顶级分组「财务（`finance`）」（顺序置于「资金」之后、「报表」之前）：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 财务（`finance`） | 会计科目（`accounts`）/ 税率（`taxRates`）/ 凭证（`vouchers`，`033`）/ 财务报表（`financialReports`，`033`）/ 银行账户（`bankAccounts`，`034`）/ 资金日记账（`cashJournals`，`034`） | `031` / `033` / `034` |

### 0.4 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 会计科目 | `accounts` | `view` / `create` / `update` / `delete` / `status` | `/api/accounts*`、`/accounts` |
| 税率 | `taxRates` | `view` / `create` / `update` / `delete` / `status` | `/api/tax-rates*`、`/tax-rates` |

## 1. 总体设计

```
财务主数据（前端 /accounts、/tax-rates，域目录 FinanceManagement/）
  → AccountsController / TaxRatesController
    → App.Core/Features/<Accounts|TaxRates>/<Action>/*RequestHandler
      → IAccountRepository / ITaxRateRepository（App.Core）→ EF Core 实现（App.Infrastructure）
        → PostgreSQL（Accounts / TaxRates）
```

核心原则：**科目是账簿的骨架**，本规格只维护骨架，不做记账（`033`）；类别 / 方向为凭证与报表的取数依据；预置科目保证开箱有标准科目。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；金额 / 比率 `numeric`；枚举 `smallint`。审计字段：`CreatedAt` / `UpdatedAt`（NOT NULL）+ `CreatedBy` / `UpdatedBy`（可空）。

### 2.1 实体 `App.Core/Entities/Account.cs` 与表 `Accounts`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 科目编码（如 `1001`、`100101`） |
| `Name` | `string` | `varchar(50)` | NOT NULL | 科目名称 |
| `Category` | `AccountCategory` | `smallint` | NOT NULL | 科目类别 |
| `Direction` | `AccountDirection` | `smallint` | NOT NULL | 余额方向 |
| `ParentId` | `Guid?` | `uuid` | NULL，FK → `Accounts(Id)`，索引 | 上级科目（`NULL` = 一级） |
| `SortOrder` | `int` | `integer` | NOT NULL，默认 `0` | 同级排序 |
| `IsPreset` | `bool` | `boolean` | NOT NULL，默认 `false` | 预置科目（不可删） |
| `Status` | `AccountStatus` | `smallint` | NOT NULL，默认 `Enabled` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |

### 2.2 实体 `App.Core/Entities/TaxRate.cs` 与表 `TaxRates`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 税率编码（如 `VAT13`） |
| `Name` | `string` | `varchar(50)` | NOT NULL，唯一索引 | 税率名称（如「增值税 13%」） |
| `Rate` | `decimal` | `numeric(9,4)` | NOT NULL，≥ 0，≤ 100 | 百分比（`13` 表示 13%） |
| `Status` | `TaxRateStatus` | `smallint` | NOT NULL，默认 `Enabled` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `AccountFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` / `SortOrderMax = 9999` |
| `TaxRateFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `RateMin = 0` / `RateMax = 100` / `RemarkMaxLength = 200` |

### 2.4 迁移与种子

- 迁移：`dotnet ef migrations add AddErpFinanceMaster -p src/App.Infrastructure -s src/App.Api`。
- 种子（`DatabaseInitializer` 幂等，`IsPreset = true`）：最小标准科目表——
  - 资产：`1001 库存现金`、`1002 银行存款`、`1122 应收账款`、`1405 库存商品`、`1403 原材料`；
  - 负债：`2202 应付账款`、`2221 应交税费`；
  - 权益：`4001 实收资本`、`4103 本年利润`、`4104 利润分配`；
  - 成本：`5001 生产成本`；
  - 损益：`6001 主营业务收入`、`6401 主营业务成本`、`6602 管理费用`、`6603 财务费用`。
  - 预置项已存在（按 `Code` 判定）则跳过，可重复执行。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IAccountRepository.GetAllAsync()` | 取全量科目（`AsNoTracking`），Handler 内存建树 |
| `IAccountRepository.GetByIdAsync` / `ExistsByCodeAsync(code, excludeId)` | 存在性 / 唯一性 |
| `IAccountRepository.HasChildrenAsync(id)` | 删除保护（有子科目） |
| `IAccountRepository.IsReferencedByVoucherAsync(id)` | 删除保护（被凭证引用，`033` 落地后启用；无凭证表时返回 `false`） |
| `IAccountRepository.AddAsync` / `UpdateAsync` / `DeleteAsync` | |
| `ITaxRateRepository.GetPagedAsync(...)` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` | |

读模型：`AccountTreeNode`（含 `Children`）、`TaxRateListItem`。

### 3.2 错误码（`ErrorCode.cs`，从 `40149` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40149 | `AccountCodeExists` | 科目编码已存在 |
| 40150 | `AccountInUse` | 科目有子科目或为预置科目，禁止删除 |
| 40151 | `TaxRateCodeExists` | 税率编码已存在 |
| 40152 | `TaxRateNameExists` | 税率名称已存在 |
| 40153 | `AccountReferencedByVoucher` | 科目已被凭证引用，禁止删除（`033` 落地后生效） |

> 下一个可用业务码 → `40154`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/accounts` | GET | `Accounts/GetAccounts` | `IReadOnlyList<AccountTreeNodeDto>` | `accounts.view` |
| `/api/accounts` | POST | `Accounts/CreateAccount` | `AccountDetailDto` | `accounts.create` / 40000 / 40149 / 40400 |
| `/api/accounts/{id:guid}` | GET | `Accounts/GetAccountById` | `AccountDetailDto` | `accounts.view` / 40400 |
| `/api/accounts/{id:guid}` | PUT | `Accounts/UpdateAccount` | `AccountDetailDto` | `accounts.update` / 40000 / 40149 / 40141（环） / 40400 |
| `/api/accounts/{id:guid}` | DELETE | `Accounts/DeleteAccount` | `null` | `accounts.delete` / 40150 / 40153 / 40400 |
| `/api/accounts/{id:guid}/status` | PUT | `Accounts/UpdateAccountStatus` | `AccountDetailDto` | `accounts.status` / 40400 |
| `/api/tax-rates` | GET | `TaxRates/GetTaxRates` | `PagedResult<TaxRateListItemDto>` | `taxRates.view` / 40000 |
| `/api/tax-rates` | POST | `TaxRates/CreateTaxRate` | `TaxRateDetailDto` | `taxRates.create` / 40000 / 40151 / 40152 |
| `/api/tax-rates/{id:guid}` | GET | `TaxRates/GetTaxRateById` | `TaxRateDetailDto` | `taxRates.view` / 40400 |
| `/api/tax-rates/{id:guid}` | PUT | `TaxRates/UpdateTaxRate` | `TaxRateDetailDto` | `taxRates.update` / 40000 / 40151 / 40152 / 40400 |
| `/api/tax-rates/{id:guid}` | DELETE | `TaxRates/DeleteTaxRate` | `null` | `taxRates.delete` / 40400 |
| `/api/tax-rates/{id:guid}/status` | PUT | `TaxRates/UpdateTaxRateStatus` | `TaxRateDetailDto` | `taxRates.status` / 40400 |

- 科目树的防环（`ParentId` 不得为自身或后代）复用 `030` 的 `40141`（`DepartmentCycle`）语义；本规格**新增通用码** `40141` 已被 `030` 占用——**本规格改用 `AccountCodeExists` 之外的环检查复用同一 `40141`**（跨域共用同一"树形环"码，避免撞码；在 `028`/`030` 注记）。

### 3.4 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateAccountRequest` / `UpdateAccountRequest` | `code` 必填 1–20；`name` 必填 1–50；`category` / `direction` 枚举合法；`parentId` 可空 GUID；`sortOrder` 0–9999；`remark` ≤ 200 |
| `GetTaxRatesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（对齐 `TaxRates.Name`） |
| `CreateTaxRateRequest` / `UpdateTaxRateRequest` | `code` 必填 1–20；`name` 必填 1–50；`rate` 0–100（≤ 4 位小数）；`remark` ≤ 200 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── account.ts                 # 科目接口层
│   └── taxRate.ts                 # 税率接口层
└── views/
    └── FinanceManagement/         # 财务主数据域
        ├── AccountsView.vue           # 科目树列表
        ├── AccountFormDrawer.vue      # 科目新增 / 编辑抽屉
        ├── TaxRatesView.vue           # 税率列表
        └── TaxRateFormDrawer.vue      # 税率新增 / 编辑抽屉
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `accounts` | `accounts` | `AccountsView` |
| `tax-rates` | `taxRates` | `TaxRatesView` |

- 「财务」分组新增两项；`meta.permission = 'accounts.view'` / `'taxRates.view'`。

### 4.3 页面交互

- **`AccountsView.vue`**：树形表格（同 `030` 部门树形态）；工具条「新增科目」/ 展开收起 / 刷新；列：科目编码、名称（树列）、类别（`a-tag`）、方向、状态、操作列（新增下级 / 编辑 / 启停 / 删除）。
- **`AccountFormDrawer.vue`**：编码 / 名称 / 类别（`a-select`）/ 方向（`a-radio`）/ 上级（`a-tree-select`，编辑排除自身及后代）/ 排序 / 状态 / 备注。
- **`TaxRatesView.vue`**：标准列表（筛选：关键词 / 状态）；列：编码、名称、税率（`%`）、状态、备注、操作列。
- **`TaxRateFormDrawer.vue`**：编码 / 名称 / 税率（`a-input-number`，精度 4）/ 状态 / 备注。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 科目用 `Code` 而非纯自增 | 手工维护编码（`1001` 等） | 会计习惯按编码组织；唯一约束保证无重复 |
| 末级派生不落列 | 查子科目判定 | 避免"末级标记"与"实际有无子科目"不一致 |
| 预置科目不可删 | `IsPreset` | 标准科目被删除会导致 `033` 默认映射失效；允许改名 / 停用 |
| 类别 + 方向分列 | 两个枚举 | 报表按类别汇总、余额按方向计算，二者独立 |
| 不落"期初余额" | 由 `033` 处理 | 期初属记账范畴，不属主数据 |

## 6. 单元测试设计

- **科目**：`CreateAccount` 成功 / 编码重复 `40149` / 上级不存在 `40400`；`UpdateAccount` 上级为自身 / 后代拒绝；`DeleteAccount` 有子科目 `40150` / 预置科目 `40150` / 被凭证引用 `40153` / 成功；`GetAccounts` 树组装与排序。
- **税率**：`CreateTaxRate` / `UpdateTaxRate` 编码 `40151`、名称 `40152`；`rate` 边界（`0` / `100` 通过，`-1` / `101` 拒绝）；`GetTaxRates` 筛选分页。
- **种子**：预置科目幂等（连续两次不重复）。
- **字段约束一致性**：`Accounts.Code` / `TaxRates.Rate` 的 EF 精度与常量一致。
- **清单守卫**：12 个端点全部标注合法权限点（纳入 `028` 既有守卫）。

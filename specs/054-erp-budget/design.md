---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：预算管理（erp-budget）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；执行数取数复用 `033-erp-general-ledger` §0.4 口径（只读），不改动任何写入路径。
> 科目见 `specs/031-erp-finance-master/design.md` §0；期间 / 凭证取数见 `specs/033-erp-general-ledger/design.md` §0。

## 0. 约定正文（唯一事实源）

### 0.1 预算对象与口径

| 项 | 规则 |
|---|---|
| 编制维度 | `会计科目 × 年 × 月`（月 1–12）；年度合计 = 12 个月之和（推导） |
| 科目范围 | 须为 `AccountCategory = 损益`、**末级**、**启用**（否则 `40191`） |
| 金额 | `≥ 0`，`numeric(18,2)`；未编制 = 0 |
| 可改条件 | 该年该月的**会计期间未结账**（`033` §0.1）；已结账 → `40154`（复用） |
| 全量覆盖 | `PUT /api/budgets` 为所选范围的**全量替换**（`AGENTS.md` §4.5） |

### 0.2 执行数口径（引用 `033` §0.4）

| 项 | 口径 |
|---|---|
| 实际发生额 | 该科目该期间的**净发生额**（`Posted` 且未作废分录；费用类取借方净额，收入类取贷方净额，按 `AccountDirection`）——与 `033` §0.4「科目余额表本期发生额」同口径 |
| 差异 | `预算 − 实际`（负数为超支） |
| 执行率 | `实际 ÷ 预算`（预算 0 时按 0 / 不展示） |

### 0.3 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 预算管理 | `budgets` | `view` / `update` | `/api/budgets*`、`/budgets` |

### 0.4 菜单归属（在 `025` §0.2 表续行）

「财务（`finance`）」分组续行子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 财务（`finance`） | 预算管理（`budgets`） | `054` |

## 1. 总体设计

```
预算管理（前端 /budgets，域目录 BudgetManagement/）
  → BudgetsController
    → App.Core/Features/Budgets/GetBudgets | SaveBudgets | GetBudgetExecution
      → IBudgetRepository（编制：读 / 全量替换）
        + IBudgetExecutionQueryRepository（只读：按科目期间取实际发生额，复用 033 口径）
        + IAccountingPeriodRepository（结账判断，033）
        + IAccountRepository（科目校验，031）
        → PostgreSQL（Budgets / VoucherEntries / Accounts / AccountingPeriods）
```

核心原则：

- **只看不拦**：预算不参与任何单据写入判定（无拦截，§5 范围外）。
- **执行数复用总账口径**：不新造取数逻辑，与 `033` §0.4 一致（避免"预算口径"与"账面口径"打架）。
- **编制粒度到月**：年度合计由月汇总推导，不落"年度行"。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/Budget.cs` 与表 `Budgets`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AccountId` | `Guid` | `uuid` | NOT NULL，FK → `Accounts(Id)`，索引 | 费用 / 损益科目 |
| `Year` | `int` | `integer` | NOT NULL | 年度 |
| `Month` | `int` | `integer` | NOT NULL，1–12 | 月份 |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 预算额 |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

- **唯一索引** `(AccountId, Year, Month)`。

### 2.2 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `BudgetFieldConstraints` | `YearMinValue = 2000` / `YearMaxValue = 2999` / `MonthMinValue = 1` / `MonthMaxValue = 12` / `AmountMaxValue = 999999999999.99` |

### 2.3 迁移与种子

- 迁移：`dotnet ef migrations add AddErpBudget -p src/App.Infrastructure -s src/App.Api`（建 1 表 + 唯一索引）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IBudgetRepository.GetByYearAsync(year, ...)` | 该年全部预算行（按科目 / 月份） |
| `IBudgetRepository.ReplaceAsync(rows, ...)` | 全量替换（所选范围；同事务） |
| `IBudgetExecutionQueryRepository.GetActualAsync(year, accountIds, ...)` | 该年逐科目逐月实际发生额（跨 `VoucherEntries` / `Vouchers`，口径同 `033` §0.4） |

- 读模型：`BudgetItem`（科目 + 年 + 月 + 金额）、`BudgetExecutionItem`（科目 + 月 + 预算 + 实际 + 差异 + 执行率）。

### 3.2 错误码（`ErrorCode.cs`，从 `40191` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40191 | `BudgetAccountInvalid` | 预算科目非法（非损益类 / 非末级 / 已停用） |

> 期间已结账复用 `033` 的 `40154`；下一个可用业务码 → `40192`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/budgets` | GET | `Budgets/GetBudgets` | `IReadOnlyList<BudgetItemDto>`（按科目含 12 月） | `budgets.view` / 40000 |
| `/api/budgets` | PUT | `Budgets/SaveBudgets` | `IReadOnlyList<BudgetItemDto>` | `budgets.update` / 40000 / 40154 / 40191 |
| `/api/budgets/execution` | GET | `Budgets/GetBudgetExecution` | `IReadOnlyList<BudgetExecutionItemDto>` | `budgets.view` / 40000 |

### 3.4 关键用例流程（Handler）

- **GetBudgets**：`year` → `GetByYearAsync` → 按科目聚合出 12 月数组（缺省 0）。
- **SaveBudgets**：逐行科目校验（§0.1 → `40191`）；逐行期间结账判断（`IsClosedAsync` → `40154`）；`ReplaceAsync`（同事务）。
- **GetBudgetExecution**：`GetByYearAsync` + `GetActualAsync` → 逐科目逐月计算差异 / 执行率（§0.2）→ 映射。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `SaveBudgetsRequest` | `items` 每项：`accountId` 必填 GUID、`year` 2000–2999、`month` 1–12、`amount` ≥ 0 且 ≤ 上界；`(accountId, year, month)` 去重 |
| `GetBudgetsRequest` / `GetBudgetExecutionRequest` | `year` 2000–2999 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── budget.ts
└── views/
    └── BudgetManagement/
        └── BudgetsView.vue        # 预算编制 + 执行对比（单页）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `budgets` | `budgets` | `BudgetsView` |

- 「财务」分组续行「预算管理」；`meta.permission = 'budgets.view'`。

### 4.3 页面交互

- **`BudgetsView.vue`**：年度选择；表格按科目行展开 12 月列（每格 `a-input-number :min="0" :precision="2"`，逐年可编辑）；年度合计列（行合计）；行尾「执行」按钮打开对比抽屉（12 月 预算 / 实际 / 差异 / 执行率，超支标红）；保存按钮 loading；已结账月份单元格禁用并提示。
- 科目行取自 `031` 损益类末级科目（`a-tree-select` 或科目 pick）。

### 4.4 接口层

- `api/budget.ts`：`getBudgets` / `saveBudgets` / `getBudgetExecution`；类型与后端 DTO camelCase 一一对应。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 编制**到月**、年度行推导 | `(科目, 年, 月)` | 月度预算是执行对比的最小粒度；年度合计不必双写 |
| **不拦截** | 只看不拦 | 拦截需改各单据写入路径，投入大且易误伤；一期先建立"可见性" |
| 执行数**复用总账口径** | `033` §0.4 | 避免预算口径与账面口径不一致 |
| 科目限**损益类末级** | §0.1 | 费用预算场景；资产负债类预算暂无诉求 |
| 全量覆盖保存 | `PUT` 全量替换 | 与 `AGENTS.md` §4.5 一致；月度单元格编辑天然整表提交 |
| 已结账月份禁改 | `40154` 复用 | 结账后账面已定，预算不应再变 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **编制**：保存 / 读取往返；科目非法 `40191`；已结账月份 `40154`；同事务全量替换。
- **执行**：实际发生额与 `033` §0.4 一致（含作废剔除）；差异 / 执行率（预算 0 → 0）；跨月归集。
- **校验**：`year` / `month` / `amount` 边界与去重。
- **字段约束一致性**：常量与 EF 列长一致。

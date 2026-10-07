---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：费用报销（erp-expense）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；凭证写入复用 `033-erp-general-ledger` 的 `VoucherFactory` / `VoucherWriter`（同事务、期间闸门、映射缺失回滚）。
> 科目（`031`）、资金账户（`034`）、期间 / 凭证模板与取数口径（`033` §0）不重复定义。

## 0. 约定正文（唯一事实源）

### 0.1 状态与流转

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `ExpenseStatus` | `Draft = 0` / `Approved = 1` / `Voided = 2` | 草稿 / 已审批 / 已作废 | `blue` / `green` / `gray` |

| 当前状态 | 可编辑 | 可作废 | 可审批 |
|---|---|---|---|
| `Draft` | 是 | 是 | 是 |
| `Approved` | 否 | 否（`40185`） | 否（`40186`） |
| `Voided` | 否 | 否（`40185`） | 否（`40186`） |

### 0.2 科目与账户校验

| 项 | 规则 | 错误码 |
|---|---|---|
| 费用科目 | 明细行科目须为 `AccountCategory = 损益`、**末级**、**启用** | `40187` |
| 支付账户 | 单据须指定 `BankAccountId`；存在 / 启用（`034` 既有） | `40400` / `034` 既有 |
| 贷方科目 | 按账户类型映射：`Cash` → 库存现金（映射键 `Cash`）；`Bank` → 银行存款（映射键 `Bank`） | `40158`（映射缺失，`033`） |

### 0.3 自动凭证模板（`SourceType = Expense`）

| 借方 | 贷方 | 说明 |
|---|---|---|
| 各费用科目（明细行金额） | 现金 / 银行存款（`Cash` / `Bank`，按支付账户类型）合计 | 借合计 = 贷合计 = `TotalAmount` |

- 摘要「费用报销 <单号>」；归属期间由 `VoucherDate`（= 审批当天）决定（`033` §0.1）。
- **期间已结账 / 映射缺失**：整单事务回滚（复用 `033` 既有 `40154` / `40158`）。
- `033` §0.1 `VoucherSourceType` 续行 `Expense`。

### 0.4 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 费用报销单 | `ExpenseNo` | 唯一；`EX + yyyyMMdd + 4 位序号` | — |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 费用报销 | `expenses` | `view` / `create` / `update` / `void` / `approve` | `/api/expenses*`、`/expenses` |

### 0.6 菜单归属（在 `025` §0.2 表续行）

「财务（`finance`）」分组续行子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 财务（`finance`） | 费用报销（`expenses`） | `052` |

## 1. 总体设计

```
费用报销（前端 /expenses，域目录 ExpenseManagement/）
  → ExpensesController
    → App.Core/Features/Expenses/<Action>/*RequestHandler
      → IExpenseRepository（主表 + 明细）
        + IAccountRepository（费用科目校验，031）
        + IBankAccountRepository（支付账户，034）
        + IAccountMappingRepository（Cash / Bank，033）
        + VoucherWriter（同事务生成 / 作废凭证，033）
        → PostgreSQL（Expenses / ExpenseItems / Vouchers / VoucherEntries）
```

核心原则：

- **审批即入账**：审批通过同事务生成凭证（与 `015` / `016` 等"创建即入账"不同——费用需确认后才入账）。
- **复用凭证通道**：不新写凭证逻辑，走 `033` 的 `VoucherFactory` / `VoucherWriter`（借贷平衡 / 末级科目 / 期间闸门统一）。
- **不可逆**：已审批不可编辑 / 作废（冲销走范围外路径），避免账实反复。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/Expense.cs` 与表 `Expenses`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ExpenseNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `EX + yyyyMMdd + 4` |
| `EmployeeId` | `Guid` | `uuid` | NOT NULL，FK → `Employees(Id)`，索引 | 报销人 |
| `EmployeeName` | `string` | `varchar(50)` | NOT NULL | 报销人姓名**快照** |
| `ExpenseDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 报销日期 |
| `BankAccountId` | `Guid` | `uuid` | NOT NULL，FK → `BankAccounts(Id)` | 支付账户 |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | Σ 明细金额（后端计算） |
| `Status` | `ExpenseStatus` | `smallint` | NOT NULL，默认 `Draft` | 见 §0.1 |
| `ApprovedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 审批时间 |
| `ApprovedBy` | `Guid?` | `uuid` | NULL | 审批人 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

明细（`ExpenseItem` / `ExpenseItems`）：

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ExpenseId` | `Guid` | `uuid` | NOT NULL，FK，索引 | |
| `AccountId` | `Guid` | `uuid` | NOT NULL，FK → `Accounts(Id)` | 费用科目 |
| `AccountCode` / `AccountName` | `string` | `varchar(20)` / `varchar(50)` | NOT NULL | 科目编码 / 名称**快照** |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 金额 |
| `Summary` | `string?` | `varchar(100)` | NULL | 摘要 |

### 2.2 字段约束（单一来源）

- **不新建常量类**：金额 / 备注列长复用既有（`OrderFieldConstraints.RemarkMaxLength` 等）；新增列长与 EF 配置一致，由 `FieldValidationConsistencyTests` 守护。

### 2.3 迁移与种子

- 迁移：`dotnet ef migrations add AddErpExpense -p src/App.Infrastructure -s src/App.Api`（建 2 表 + 索引；FK 不级联删除）。
- 无种子数据（费用科目由用户用 `031` 既有科目，如需预置可另评）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IExpenseRepository.GetPagedAsync(...)` | 列表（关键词 / 状态 / 日期区间，明细行数聚合） |
| `IExpenseRepository.GetByIdAsync` / `AddAsync` / `UpdateAsync`（明细全量替换） | |
| `IExpenseRepository.GenerateNoAsync(date, ...)` | 单号 `EX` |
| `IExpenseRepository.UpdateStatusAsync(id, status, approvedAt, approvedBy, ...)` | 审批 / 作废状态回写 |

- 读模型：`ExpenseListItem`（含报销人 / 账户名）、`ExpenseDetail`（主表 + 明细）。

### 3.2 错误码（`ErrorCode.cs`，从 `40185` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40185 | `ExpenseNotEditable` | 报销单非草稿，不可编辑 / 作废 |
| 40186 | `ExpenseCannotApprove` | 报销单不可审批（非草稿 / 已审批） |
| 40187 | `ExpenseAccountInvalid` | 费用科目非法（非损益类 / 非末级 / 已停用） |

> 下一个可用业务码 → `40188`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/expenses` | GET | `Expenses/GetExpenses` | `PagedResult<ExpenseListItemDto>` | `expenses.view` / 40000 |
| `/api/expenses` | POST | `Expenses/CreateExpense` | `ExpenseDetailDto` | `expenses.create` / 40000 / 40400 |
| `/api/expenses/{id:guid}` | GET | `Expenses/GetExpenseById` | `ExpenseDetailDto` | `expenses.view` / 40400 |
| `/api/expenses/{id:guid}` | PUT | `Expenses/UpdateExpense` | `ExpenseDetailDto` | `expenses.update` / 40000 / 40185 / 40400 |
| `/api/expenses/{id:guid}/approve` | PUT | `Expenses/ApproveExpense` | `ExpenseDetailDto` | `expenses.approve` / 40186 / 40187 / 40154 / 40158 / 40400 |
| `/api/expenses/{id:guid}/void` | PUT | `Expenses/VoidExpense` | `ExpenseDetailDto` | `expenses.void` / 40185 / 40400 |

### 3.4 关键用例流程（Handler）

- **CreateExpense**：报销人存在（`40400`）；支付账户存在 / 启用（`034`）；`TotalAmount` 后端计算；生成单号；落库（`Draft`）。
- **UpdateExpense**：`Status != Draft` → `40185`；明细全量替换；重算金额。
- **VoidExpense**：`Status != Draft` → `40185`；置 `Voided`。
- **ApproveExpense**：`Status != Draft` → `40186`；逐行校验费用科目（§0.2，`40187`）；按 §0.3 组装凭证 → 与状态回写**同一事务**（`VoucherWriter`；期间已结账 `40154` / 映射缺失 `40158` 整体回滚）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdateExpenseRequest` | `employeeId` 必填 GUID；`expenseDate` 必填；`bankAccountId` 必填 GUID；`items` 1–200 项，每项 `accountId` 必填、`amount` 0.01–9999999.99、`summary` ≤ 100；`remark` ≤ 200 |
| `GetExpensesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── expense.ts
└── views/
    └── ExpenseManagement/
        ├── ExpensesView.vue        # 列表
        ├── ExpenseFormPage.vue     # 登记 / 编辑（共用）
        └── ExpenseDetailView.vue   # 详情（审批 / 作废）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `expenses` | `expenses` | `ExpensesView` |
| `expenses/new` | `expenseCreate` | `ExpenseFormPage` |
| `expenses/detail/:id` | `expenseDetail` | `ExpenseDetailView` |

- 「财务」分组续行「费用报销」；`meta.permission = 'expenses.view'`。

### 4.3 页面交互

- **列表**：筛选（关键词 / 状态 / 日期区间）；列：单号、报销人、报销日期、支付账户、金额、状态（`a-tag`）、审批时间、操作列（查看 / 编辑 / 审批 / 作废，按状态显示）。
- **表单页**：报销人（员工下拉）/ 报销日期 / 支付账户（下拉，按类型展示）/ 明细子表（科目 `a-tree-select`（仅损益类末级启用）+ 金额 + 摘要）/ 备注；明细合计实时显示。
- **详情页**：基本信息 + 明细只读 + 「审批」（草稿时）+ 「作废」；审批后只读并展示审批时间 / 人。

### 4.4 接口层

- `api/expense.ts`：列表 / 新建 / 详情 / 编辑 / 审批 / 作废；`EXPENSE_STATUS_META`（值 → 文案 / 颜色）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 审批**即时入账** | 审批 = 生成凭证 | 一期无"应付员工款"环节，简化；如需两段（确认 + 付款）另评 |
| 复用 `033` 凭证通道 | `VoucherFactory` / `VoucherWriter` | 借贷平衡 / 期间闸门 / 映射统一，避免第二套凭证逻辑 |
| 费用科目由明细指定 | 用户选损益类科目 | 复用 `031` 科目树，不新增费用类型字典 |
| 已审批不可逆 | 无作废 / 无编辑 | 入账后改动破坏报表；冲销另立（§5 范围外） |
| 单级审批（不接 `042`） | `expenses.approve` 权限 | 接 `042` 需扩展其单据类型集合，本期避免 |
| 支付账户**必填** | 关联 `034` | 一期即入账需明确贷方科目（现金 / 银行） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **创建**：合计后端计算；报销人 / 账户存在性。
- **编辑 / 作废**：草稿可改；非草稿 `40185`。
- **审批**：非草稿 `40186`；费用科目非损益 / 非末级 / 停用 `40187`；凭证分录（借各费用科目 / 贷 现金或银行）与借贷平衡；期间已结账 `40154` 整体回滚；映射缺失 `40158`。
- **列表**：筛选与明细行数聚合。
- **字段约束一致性**：常量与 EF 列长一致。

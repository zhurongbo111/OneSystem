---
created: 2026-09-20
updated: 2026-09-22
---

# 设计规格：总账（erp-general-ledger）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；**改造既有单据写入路径**（同事务追加凭证），改造风格参照 `026-erp-cost`（同事务追加成本）。
> 权限机制复用 `028`；科目树 / 建树参照 `030` / `031`；报表组织参照 `025`（只读聚合）。

## 0. 约定正文（唯一事实源）

### 0.1 期间、凭证与状态口径

| 概念 | 规则 |
|---|---|
| 会计期间 | 按「年-月」唯一；状态 `Open = 0` / `Closed = 1`；凭证归属由 `VoucherDate` 的年月决定 |
| 期间控制 | 期间不存在 → `40159`；期间已结账 → `40154`（禁止新增 / 作废凭证） |
| 凭证状态 | `Posted = 1`（已过账，默认）/ `Voided = 0`（已作废）；**只有 `Posted` 计入余额** |
| 借贷平衡 | Σ 借方 = Σ 贷方（`decimal`，`numeric(18,2)`）；手工与自动凭证共用同一校验 |
| 分录科目 | 必须为**末级**（无子科目）且**启用**，否则 `40157` |
| 凭证号 | `记-YYYYMM-0001`（按期间顺序），后端生成，唯一 |
| 来源 | `SourceType`：`Manual` / `PurchaseInbound` / `SalesOutbound` / `PurchaseReturn` / `SalesReturn` / `Receipt` / `Payment` / `CostCarry`（销售成本结转）；`SourceId` / `SourceNo` 记录来源单据 |

### 0.2 自动凭证模板（按来源，金额均为含税总额）

| 来源 | 借方 | 贷方 | 说明 |
|---|---|---|---|
| 采购入库 | 存货科目（`Inventory`）`TotalAmount` | 应付账款（`Payable`）`TotalAmount` | |
| 销售出库 | 应收账款（`Receivable`）`TotalAmount` | 主营业务收入（`Revenue`）`TotalAmount` | 收入确认 |
| 销售出库（成本结转） | 主营业务成本（`Cost`）`Σ 流水 TotalCost` | 存货科目（`Inventory`）`Σ 流水 TotalCost` | 与收入同一凭证或独立 `CostCarry` 凭证，见 §5 |
| 采购退货 | 应付账款（`Payable`）`TotalAmount` | 存货科目（`Inventory`）`TotalAmount` | 红字冲减 |
| 销售退货 | 主营业务收入（`Revenue`）`TotalAmount` | 应收账款（`Receivable`）`TotalAmount` | 收入冲减 |
| 销售退货（成本转回） | 存货科目（`Inventory`）`Σ TotalCost` | 主营业务成本（`Cost`）`Σ TotalCost` | 成本转回 |
| 收款 | 现金 / 银行存款（`Cash` / `Bank`）`TotalAmount` | 应收账款（`Receivable`）`TotalAmount` | 按 `SettlementMethod` 选科目 |
| 付款 | 应付账款（`Payable`）`TotalAmount` | 现金 / 银行存款（`Cash` / `Bank`）`TotalAmount` | 同上 |

- **收付款选科目**：`SettlementMethod.Cash` → `Cash` 科目；`BankTransfer` / `Other` → `Bank` 科目。
- **作废**：来源单据作废 → 其自动凭证置 `Voided`（不生成红字凭证）；余额随之回退。

### 0.3 科目映射键（`AccountMappings`）

| 键 | 含义 | 预置指向（`031` 预置科目） |
|---|---|---|
| `Inventory` | 存货（库存商品） | `1405 库存商品` |
| `Receivable` | 应收账款 | `1122 应收账款` |
| `Payable` | 应付账款 | `2202 应付账款` |
| `Revenue` | 主营业务收入 | `6001 主营业务收入` |
| `Cost` | 主营业务成本 | `6401 主营业务成本` |
| `Cash` | 库存现金 | `1001 库存现金` |
| `Bank` | 银行存款 | `1002 银行存款` |
| `Profit` | 本年利润 | `4103 本年利润` |

- 缺失映射 → 自动凭证拒绝生成（`40158`），并使来源单据事务回滚。

### 0.4 余额与报表取数口径

| 报表 | 口径 |
|---|---|
| 科目余额表 | 期初 = 该科目在 `start` 前全部 `Posted` 分录的净额（按方向）；本期借 / 贷 = 期间内发生额；期末 = 期初 + 借 − 贷（资产 / 成本类）或 期初 + 贷 − 借（负债 / 权益 / 损益类，按 `Direction`） |
| 资产负债表 | 资产类科目余额合计 = 负债类 + 权益类 + （损益类净额计入「本年利润」行）；**恒等式**由单测守护 |
| 利润表 | 损益类科目本期发生额：收入（贷方净额）− 成本费用（借方净额） |
| 作废凭证 | 不计入任何取数 |

- 报表按**一级科目**列示（末级余额向上汇总），不做可配置报表项目映射（§5）。

## 1. 总体设计

```
会计期间 / 凭证（前端 /accounting-periods、/vouchers，域目录 GeneralLedgerManagement/）
  → AccountingPeriodsController / VouchersController / AccountMappingsController / FinancialReportsController
    → App.Core/Features/<AccountingPeriods|Vouchers|AccountMappings|FinancialReports>/<Action>/*RequestHandler
      → IVoucherRepository / IAccountingPeriodRepository / IAccountMappingRepository / IAccountRepository(038)
        + IFinancialReportQueryRepository（只读聚合）
        → PostgreSQL（AccountingPeriods / Vouchers / VoucherEntries / AccountMappings / Accounts）

自动凭证（改造既有写入路径）
  单据 Create* / Void* Handler（015/016/021/022/023）
    → 同一 IUnitOfWork 事务内：既有单据 / 库存 / 成本写入 + IVoucherRepository.AppendAsync（按 §0.2 模板）
    → 失败整体回滚（含映射缺失 40158）
```

核心原则：

- **凭证与单据同事务**：不出现"单据生效但无凭证"（同 `026` 的成本一致性原则）。
- **模板驱动**：自动分录由 §0.2 模板 + §0.3 映射决定，不在各单据 Handler 内硬编码科目。
- **单一平衡校验**：手工与自动凭证共用同一借贷平衡 / 科目合法性校验。
- **报表只读聚合**：报表不落物化表，按凭证分录现算（单组织单月量级）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；金额 `numeric(18,2)`；枚举 `smallint`。

### 2.1 实体 `App.Core/Entities/AccountingPeriod.cs` 与表 `AccountingPeriods`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Year` | `int` | `integer` | NOT NULL | 年 |
| `Month` | `int` | `integer` | NOT NULL | 月（1–12） |
| `Status` | `PeriodStatus` | `smallint` | NOT NULL，默认 `Open` | `Open = 0` / `Closed = 1` |
| `ClosedAt` / `ClosedBy` | `DateTimeOffset?` / `Guid?` | `timestamptz` / `uuid` | NULL | 结账信息 |

- 唯一索引 `(Year, Month)`。

### 2.2 实体 `App.Core/Entities/Voucher.cs` 与表 `Vouchers`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `VoucherNo` | `string` | `varchar(30)` | NOT NULL，唯一索引 | `记-YYYYMM-0001` |
| `VoucherDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 记账日期 |
| `PeriodId` | `Guid` | `uuid` | NOT NULL，FK → `AccountingPeriods(Id)` | 归属期间 |
| `Summary` | `string` | `varchar(200)` | NOT NULL | 摘要 |
| `SourceType` | `VoucherSourceType` | `smallint` | NOT NULL | 来源类型（§0.1） |
| `SourceId` | `Guid?` | `uuid` | NULL，索引 | 来源单据 id |
| `SourceNo` | `string?` | `varchar(20)` | NULL | 来源单据号快照 |
| `TotalDebit` | `decimal` | `numeric(18,2)` | NOT NULL | Σ 借方（= `TotalCredit`） |
| `TotalCredit` | `decimal` | `numeric(18,2)` | NOT NULL | Σ 贷方 |
| `Status` | `VoucherStatus` | `smallint` | NOT NULL，默认 `Posted` | |
| `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` | | | | 审计 |

### 2.3 实体 `App.Core/Entities/VoucherEntry.cs` 与表 `VoucherEntries`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `VoucherId` | `Guid` | `uuid` | NOT NULL，FK → `Vouchers(Id)`，索引 | |
| `LineNo` | `int` | `integer` | NOT NULL | 行号（1 起） |
| `AccountId` | `Guid` | `uuid` | NOT NULL，FK → `Accounts(Id)`，索引 | 末级科目 |
| `AccountCode` / `AccountName` | `string` | `varchar(20)` / `varchar(50)` | NOT NULL | 科目快照 |
| `Summary` | `string?` | `varchar(200)` | NULL | 行摘要 |
| `Debit` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0 | 借方 |
| `Credit` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0 | 贷方 |

- 约束：每行 `Debit` 与 `Credit` **恰有一个 > 0**（另一为 0）。

### 2.4 实体 `App.Core/Entities/AccountMapping.cs` 与表 `AccountMappings`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Key` | `string` | `varchar(30)` | NOT NULL，唯一索引 | 映射键（§0.3） |
| `AccountId` | `Guid` | `uuid` | NOT NULL，FK → `Accounts(Id)` | 目标科目（须末级启用） |
| 审计 | | | | |

### 2.5 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `VoucherFieldConstraints` | `NoMaxLength = 30` / `SummaryMaxLength = 200` / `EntrySummaryMaxLength = 200` / `EntriesMaxCount = 200` / `PerEntryMaxAmount = 9999999999999999.99`（`numeric(18,2)` 上界） |
| `AccountMappingFieldConstraints` | `KeyMaxLength = 30` |

### 2.6 迁移与种子

- 迁移：`dotnet ef migrations add AddErpGeneralLedger -p src/App.Infrastructure -s src/App.Api`（建 4 表 + 索引）。
- 种子（`DatabaseInitializer` 幂等）：
  1. 预置 `AccountMappings` 8 个键 → `031` 预置科目（按 `Code` 查）；
  2. 预置当年 1–12 月 `AccountingPeriods`（`Open`）。
- **不补录历史单据凭证**（§5 范围外）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IAccountingPeriodRepository.GetByYearMonthAsync(year, month)` / `GetAllAsync(year?)` / `SetStatusAsync(id, status, ...)` | |
| `IVoucherRepository.AppendAsync(Voucher, IReadOnlyList<VoucherEntry>, ...)` | 追加凭证 + 分录（同事务） |
| `IVoucherRepository.GetPagedAsync(...)` / `GetDetailAsync(id)` | 列表 / 详情（含分录） |
| `IVoucherRepository.VoidAsync(id, ...)` / `VoidBySourceAsync(sourceType, sourceId, ...)` | 作废（手工 / 随单据） |
| `IVoucherRepository.GenerateNoAsync(period, ...)` | 生成凭证号 |
| `IVoucherRepository.GetMaxLineNoAsync`（可选） | 分录行号 |
| `IAccountMappingRepository.GetAllAsync()` / `GetByKeyAsync(key)` / `UpsertAsync(...)` | 映射读取（自动凭证用）/ 维护 |
| `IFinancialReportQueryRepository.GetAccountBalancesAsync(period)` / `GetBalanceSheetAsync(period)` / `GetIncomeStatementAsync(period)` | 只读聚合（参照 `025` 的 `IReportQueryRepository` 组织） |

读模型：`AccountBalanceItem`（`AccountId` / `Code` / `Name` / `Category` / `Direction` / `OpeningBalance` / `PeriodDebit` / `PeriodCredit` / `ClosingBalance`）、`BalanceSheetItem`、`IncomeStatementItem`、`VoucherListItem`、`VoucherDetail`。

### 3.2 共享服务（`App.Core`）

- `VoucherFactory`（或 `IVoucherBuilder`，`App.Core/Finance/`）：入参「来源类型 + 金额 + 日期 + 单据号 + 映射表」→ 产出 `Voucher` + `VoucherEntry` 集合（按 §0.2 模板）；映射缺失 → `BusinessException(40158)`。
  - 定位：**无状态技术组件**（非业务编排），不注入仓储；映射由调用方（单据 Handler）经 `IAccountMappingRepository` 取好后传入（同事务）。
- `VoucherBalanceValidator`：校验借贷平衡（共用）。

### 3.3 错误码（`ErrorCode.cs`，从 `40154` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40154 | `PeriodClosed` | 会计期间已结账，禁止记账 |
| 40155 | `VoucherUnbalanced` | 凭证借贷不平衡 |
| 40156 | `VoucherNoEntries` | 凭证无分录 |
| 40157 | `VoucherAccountInvalid` | 分录科目非末级或已停用 |
| 40158 | `AccountMappingMissing` | 自动凭证所需科目映射缺失 |
| 40159 | `PeriodNotOpened` | 记账日期所在期间不存在 |

> 下一个可用业务码 → `40160`（`ROADMAP` §6 顶部同步）。

### 3.4 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/accounting-periods` | GET | `AccountingPeriods/GetPeriods` | `IReadOnlyList<PeriodDto>` | `vouchers.view` |
| `/api/accounting-periods/{id}/close` | PUT | `AccountingPeriods/ClosePeriod` | `PeriodDto` | `vouchers.close` / 40400 |
| `/api/accounting-periods/{id}/reverse` | PUT | `AccountingPeriods/ReversePeriod` | `PeriodDto` | `vouchers.close` / 40400 |
| `/api/vouchers` | GET | `Vouchers/GetVouchers` | `PagedResult<VoucherListItemDto>` | `vouchers.view` / 40000 |
| `/api/vouchers` | POST | `Vouchers/CreateVoucher` | `VoucherDetailDto` | `vouchers.create` / 40000 / 40154 / 40155 / 40156 / 40157 / 40159 |
| `/api/vouchers/{id:guid}` | GET | `Vouchers/GetVoucherById` | `VoucherDetailDto` | `vouchers.view` / 40400 |
| `/api/vouchers/{id:guid}/void` | PUT | `Vouchers/VoidVoucher` | `VoucherDetailDto` | `vouchers.void` / 40154 / 40400 |
| `/api/account-mappings` | GET | `AccountMappings/GetAccountMappings` | `IReadOnlyList<AccountMappingDto>` | `vouchers.view` |
| `/api/account-mappings` | PUT | `AccountMappings/UpdateAccountMappings` | `IReadOnlyList<AccountMappingDto>` | `vouchers.updateMapping` / 40000 / 40157 |
| `/api/reports/account-balance` | GET | `FinancialReports/GetAccountBalance` | `IReadOnlyList<AccountBalanceItemDto>` | `financialReports.view` / 40000 |
| `/api/reports/balance-sheet` | GET | `FinancialReports/GetBalanceSheet` | `BalanceSheetDto` | `financialReports.view` |
| `/api/reports/income-statement` | GET | `FinancialReports/GetIncomeStatement` | `IncomeStatementDto` | `financialReports.view` |

- 权限点续行：`vouchers`（`view` / `create` / `void` / `close` / `updateMapping`）、`financialReports`（`view`）。

### 3.5 既有单据写入路径改造（自动凭证）

在 `015`（采购入库）、`016`（销售出库）、`021`（采购退货）、`022`（销售退货）、`023`（收付款）的 **`Create*` / `Void*`** Handler 内、既有 `IUnitOfWork` 事务中追加：

1. 取 `IAccountMappingRepository.GetAllAsync()` → 构建映射字典；
2. `VoucherFactory.Build(sourceType, amount, date, sourceNo, mappings)`（销售出库/退货额外取 `026` 流水成本做成本结转分录）；
3. `IVoucherRepository.AppendAsync(voucher, entries)`；
4. 单据作废时改调 `VoidBySourceAsync(sourceType, sourceId)`。

- **失败整体回滚**（映射缺失 `40158` 会阻断单据创建）——因此在 `033` 上线迁移中**必须**预置映射（§2.6）。
- 改造范围与 `026` 同级：每处改动为"同事务追加一次写入"，不改数量 / 金额语义。

### 3.6 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateVoucherRequest` | `voucherDate` 必填；`summary` 必填 1–200；`entries` 1–200 条；每条 `accountId` 必填 GUID、`debit` / `credit` ≥ 0 且**恰一 > 0**、`summary` ≤ 200；**借贷平衡**在 Handler 校验（`40155`） |
| `GetVouchersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 30（对齐 `VoucherNo`）；期间 / 来源类型可选 |
| `UpdateAccountMappingsRequest` | 8 个键齐全；`accountId` 必填 GUID（末级启用校验在 Handler） |
| `GetAccountBalanceRequest` / 报表请求 | `year` / `month` 必填合法 |

### 3.7 Swagger

- 不分组（同既有约定）；报表接口按 `003` / `025` 约定标注。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── voucher.ts                 # 凭证 + 期间 + 科目映射接口层
│   └── financialReport.ts         # 科目余额表 / 资产负债表 / 利润表
└── views/
    └── GeneralLedgerManagement/
        ├── VouchersView.vue           # 凭证列表
        ├── VoucherFormPage.vue        # 手工凭证录入（主表 + 分录子表）
        ├── VoucherDetailView.vue      # 凭证详情（含分录）
        ├── AccountMappingsView.vue    # 科目映射配置
        └── FinancialReportsView.vue   # 财务报表（科目余额表 / 资产负债表 / 利润表 tab）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `vouchers` | `vouchers` | `VouchersView` |
| `vouchers/new` | `voucherCreate` | `VoucherFormPage` |
| `vouchers/detail/:id` | `voucherDetail` | `VoucherDetailView` |
| `financial-reports` | `financialReports` | `FinancialReportsView` |

- 「财务」分组新增「凭证」「财务报表」；「科目映射」作为凭证页工具条入口（`/vouchers` 页顶部「科目映射」按钮打开 `AccountMappingsView` 抽屉或独立页，实现时择一，默认抽屉）。
- `meta.permission = 'vouchers.view'` / `'financialReports.view'`。

### 4.3 页面交互

- **`VouchersView.vue`**：筛选（期间 `a-select` 年-月、来源类型、关键词）；列：凭证号、日期、摘要、来源单据号、借方合计、贷方合计、状态（`a-tag`）、操作列（查看 / 作废 popconfirm）；工具条「手工凭证」+「科目映射」+ 刷新。
- **`VoucherFormPage.vue`**：主表（日期 / 摘要）+ 分录子表（可增删行：科目 `a-tree-select`（仅末级）/ 摘要 / 借 / 贷）；底部实时借贷合计与**差额提示**；提交前 `formRef.validate()`，不平衡阻止提交。
- **`VoucherDetailView.vue`**：主表信息 + 分录表格 + 作废按钮；作废凭证置灰。
- **`AccountMappingsView.vue`**：8 个键 → 科目 `a-tree-select`（末级）；保存全量覆盖。
- **`FinancialReportsView.vue`**：期间选择 + 三 tab（科目余额表 / 资产负债表 / 利润表）；表格 + 合计行；导出复用 `027`（可选，一期不做导出的报表留 `:disabled` 占位）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 自动凭证与单据同事务 | 单据 Handler 内追加 | 保证"单据生效 ⇔ 有凭证"；与 `026` 成本一致性同一思路 |
| 模板 + 映射 而非硬编码科目 | `AccountMappings` 配置 | 科目可换（预置只是默认）；改科目不动代码 |
| 作废用 `Voided` 而非红字凭证 | 凭证状态位 | 一期简化；红字凭证（保留可审计的冲销分录）作为演进 |
| 成本结转并入销售凭证 | 同凭证多分录 | 买卖两张凭证的科目余额等价，同凭证更省 |
| 报表按一级科目列示 | 不做报表项目映射 | 一期在"可读"与"可配置"间取前者；映射表作为演进 |
| 历史单据不补录 | 从上线日起记账 | 补录需要重放全部历史单据，成本与风险高；标注范围外 |
| 期间预置 12 个月 | 种子 | 避免每月手工开账 |

## 6. 单元测试设计

- **凭证模板（`VoucherFactory`）**：6 类来源 + 成本结转的分录正确（科目 / 借贷 / 金额）；收付款按 `SettlementMethod` 选现金 / 银行科目；映射缺失 `40158`。
- **手工凭证**：平衡通过 / 不平衡 `40155` / 无分录 `40156` / 科目非末级或停用 `40157` / 期间已结账 `40154` / 期间不存在 `40159`。
- **期间**：结账 / 反结账；结账后禁记账。
- **自动凭证改造**：`Create*` 成功后凭证存在且平衡；`Void*` 后凭证 `Voided`；映射缺失使单据创建失败并回滚（假实现断言无单据落库）。
- **报表**：科目余额表口径（期初 / 发生 / 期末）；资产负债表恒等式（资产 = 负债 + 权益 + 本年利润）；利润表与 `026` 毛利一致（同一数据集对比）。
- **字段约束一致性**：`Vouchers.VoucherNo` 列长 == 常量等。
- **清单守卫**：12 个端点纳入 `028` 既有守卫。

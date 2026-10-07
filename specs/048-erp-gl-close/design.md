---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：期初建账与期末结转（erp-gl-close）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格扩展 `033-erp-general-ledger`（新增 1 张期初表 + 3 个用例 + 修订结账闸门与报表取数）。
> 期间 / 凭证 / 分录 / 报表取数口径见 `specs/033-erp-general-ledger/design.md` §0；科目与预置科目见 `specs/031-erp-finance-master/design.md` §2.4。

## 0. 约定正文（唯一事实源）

### 0.1 期初建账口径

| 项 | 规则 |
|---|---|
| 录入粒度 | **末级科目**（同 `033` §0.1）；每条 = `科目 + 方向（借 / 贷）+ 金额` |
| 平衡校验 | **Σ 借方金额 = Σ 贷方金额**（`decimal`，`numeric(18,2)`）；不平 → `40000` |
| 建账窗口 | 仅当**账套无任何凭证**（`Vouchers` 为空，含已作废）时允许提交；已有凭证 → `40176` |
| 期初取数（**修订 `033` §0.4**） | 科目余额表「期初」= **期初余额表净额 + `start` 前全部 `Posted` 分录净额**（按 `Direction`） |
| 全量覆盖 | `PUT /api/opening-balances` 为**全量替换**（缺省科目 = 清空，`AGENTS.md` §4.5） |

### 0.2 期末损益结转模板

按期间取**损益类科目**（`AccountCategory` = 收入 / 成本费用）的期间净发生额（`Posted`、未作废分录）：

| 科目类别 | 净额方向 | 结转分录 |
|---|---|---|
| 收入类 | 贷方净额 `R > 0` | 借 该收入科目 `R` / 贷 本年利润 `R` |
| 成本费用类 | 借方净额 `E > 0` | 借 本年利润 `E` / 贷 该成本费用科目 `E` |

- 合并分录后借贷天然平衡（借合计 = 贷合计 = `ΣR + ΣE`）。
- 凭证：`SourceType = ProfitCarry`、`SourceNo = 结转-YYYYMM`、摘要「期末损益结转 YYYY-MM」、归属 = 该期间。
- **幂等**：同期间已存在**未作废** `ProfitCarry` 凭证 → `40177`；该期间损益类净额全为 0 → `40178`（无需结转）。
- 目标科目：本年利润（映射键 `Profit` → `4103 本年利润`，`033` §0.3 已预置）；映射缺失 → `40158`（`033` 既有）。

### 0.3 结账闸门（修订 `033` §0.1）

- 期间结账（`vouchers.close`）前须存在该期间**未作废的 `ProfitCarry` 凭证**；否则 → `40179`。

### 0.4 年末结转口径

| 项 | 规则 |
|---|---|
| 前置 | 该年 **1–11 月期间全部 `Closed`**、**12 月期间存在且未 `Closed`**；不满足 → `40180`（12 月期间不存在 → `40159` 复用） |
| 结转对象 | 「本年利润」科目（映射键 `Profit`）截至 12 月的 `Posted` 余额；余额为 0 → `40180` |
| 结转分录 | 借 本年利润（若有贷方余额）/ 贷 利润分配（`RetainedEarnings`）；方向按余额正负决定 |
| 凭证 | `SourceType = YearCarry`、`SourceNo = 年结-YYYY`、摘要「年末结转 YYYY」、归属 = 该年 12 月期间 |
| 幂等 | 同年度已存在未作废 `YearCarry` 凭证 → `40180` |

### 0.5 科目与映射键续行

- `031` §2.4 预置科目**续行**：`4104 利润分配`（`AccountCategory = 权益`，`IsPreset = true`，末级可用）。
- `033` §0.3 映射键**续行**：`RetainedEarnings`（利润分配 → 预置 `4104 利润分配`）。
- `033` §0.1 `VoucherSourceType` **续行**：`ProfitCarry` / `YearCarry`（追加枚举值，无迁移）。

### 0.6 权限点与菜单

- **无新增权限点**：期初建账复用 `vouchers.create`，结转 / 年结复用 `vouchers.close`（`028` §0.2）。
- **无新增菜单 / 路由**：入口挂在既有「财务 → 总账」页（§4.2）。

## 1. 总体设计

```
期初建账（前端：科目余额表页工具条 → 期初余额抽屉）
  → OpeningBalancesController
    → Features/OpeningBalances/GetOpeningBalances | SaveOpeningBalances
      → IOpeningBalanceRepository → PostgreSQL（OpeningBalances）

期末结转 / 年结（前端：凭证列表 → 期间管理抽屉）
  → AccountingPeriodsController（改造）
    → Features/AccountingPeriods/CarryProfit | CarryYear | CloseAccountingPeriod(改造)
      → IProfitCarryQueryRepository（只读：损益类期间净额 / 本年利润余额）
      → IVoucherRepository.AppendAsync（ProfitCarry / YearCarry，同事务）
      → IAccountMappingRepository（Profit / RetainedEarnings）

报表取数（改造 033）
  → FinancialReports/GetAccountBalance | GetBalanceSheet
    → 期初 = IOpeningBalanceRepository 净额 + 既有「start 前分录净额」（§0.1）
```

核心原则：

- **建账不生成凭证**：期初余额独立成表，避免"期初凭证归属哪个期间"的循环依赖（§5）。
- **结转是普通凭证**：复用 `033` 的借贷平衡 / 末级科目 / 期间闸门校验，不另立写入路径。
- **闸门单点**：结账前"必须已结转"由 `CloseAccountingPeriod` 单点校验。
- **可测性**：报表基准日 / 期间由入参传入（沿用 `033` 惯例）。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/OpeningBalance.cs` 与表 `OpeningBalances`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AccountId` | `Guid` | `uuid` | NOT NULL，**唯一索引**，FK → `Accounts(Id)` | 末级科目 |
| `Direction` | `AccountDirection` | `smallint` | NOT NULL | 借 / 贷（复用 `031`） |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 期初金额 |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

### 2.2 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `OpeningBalanceFieldConstraints` | `AmountMaxValue = 999999999999.99`（`numeric(18,2)` 上界，单一来源） |

### 2.3 迁移与种子

- 迁移：`dotnet ef migrations add AddErpGlClose -p src/App.Infrastructure -s src/App.Api`（建 1 表 + 唯一索引；**另** `DatabaseInitializer` 幂等追加预置科目 `4104 利润分配`与映射键 `RetainedEarnings`）。
- 无数据回填（新表）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IOpeningBalanceRepository.GetAllAsync(...)` | 全部期初余额（联查科目标识） |
| `IOpeningBalanceRepository.ReplaceAllAsync(rows, ...)` | 全量替换（同事务） |
| `IVoucherRepository.ExistsPostedAsync(...)`（追加） | 是否有任何凭证（建账窗口判据） |
| `IVoucherRepository.ExistsBySourceAsync(period, sourceType, ...)`（追加） | 未作废来源凭证存在性（结转幂等 / 结账闸门） |
| `IProfitCarryQueryRepository.GetPeriodProfitNetAsync(periodId, ...)` | 损益类科目期间净额（收入 / 成本费用分列） |
| `IProfitCarryQueryRepository.GetProfitBalanceAsync(asOfPeriodId, ...)` | 本年利润科目截至某期间的 `Posted` 余额（含方向） |

- 读模型：`OpeningBalanceItem`（科目编码 / 名称 / 方向 / 金额）、`ProfitCarryEntryItem`（科目 + 净额）、`ProfitBalanceItem`（方向 + 余额）——均 `sealed record`，`App.Core/Abstractions/`。

### 3.2 错误码（`ErrorCode.cs`，从 `40176` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40176 | `OpeningWindowClosed` | 账套已有凭证，期初建账窗口已关闭 |
| 40177 | `ProfitCarryDuplicate` | 该期间损益已结转 |
| 40178 | `ProfitCarryEmpty` | 该期间无损益发生额，无需结转 |
| 40179 | `PeriodProfitNotCarried` | 期间结账前须先结转损益 |
| 40180 | `YearCarryInvalid` | 年末结转前置不满足（期间未就绪 / 本年利润为 0 / 已结转） |

> 下一个可用业务码 → `40181`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/opening-balances` | GET | `OpeningBalances/GetOpeningBalances` | `OpeningBalancesDto`（明细 + 借 / 贷合计） | `vouchers.create` |
| `/api/opening-balances` | PUT | `OpeningBalances/SaveOpeningBalances` | `OpeningBalancesDto` | `vouchers.create` / 40000 / 40176 |
| `/api/accounting-periods/{id:guid}/carry-profit` | POST | `AccountingPeriods/CarryProfit` | `VoucherDetailDto` | `vouchers.close` / 40158 / 40177 / 40178 / 40400 |
| `/api/accounting-periods/{year:int}/carry-year` | POST | `AccountingPeriods/CarryYear` | `VoucherDetailDto` | `vouchers.close` / 40158 / 40159 / 40180 |
| `/api/accounting-periods/{id:guid}/close`（改造） | PUT | `AccountingPeriods/CloseAccountingPeriod` | `AccountingPeriodDto` | `vouchers.close` / 40179 / 40400 |

### 3.4 关键用例流程（Handler）

- **SaveOpeningBalances**：`ExistsPostedAsync`（含已作废）→ `40176`；逐科目校验为末级 + 启用（`40157` 复用）；Σ借 = Σ贷（`40000`）；`ReplaceAllAsync`（同事务）。
- **CarryProfit**：取期间（`40400`）→ 已存在未作废 `ProfitCarry` → `40177`；取损益类净额，全 0 → `40178`；按 §0.2 组装分录 → `VoucherFactory` / `VoucherWriter` 复用（`033` §3.2）写入 → 返回凭证。
- **CloseAccountingPeriod（改造）**：原校验之上增「存在未作废 `ProfitCarry`」→ 否则 `40179`。
- **CarryYear**：校验 1–11 月 `Closed` + 12 月 `Open`（`40180`；12 月不存在 `40159`）→ 已存在未作废 `YearCarry` → `40180`；取本年利润余额，为 0 → `40180`；按 §0.4 组装分录写入 12 月期间。

### 3.5 报表取数改造（`033` §0.4 修订）

- `FinancialReports/GetAccountBalance` / `GetBalanceSheet`：期初项由「`start` 前分录净额」改为「**期初余额表净额 + `start` 前分录净额**」（同方向相加；无期初记录科目仅计分录净额）。

### 3.6 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `SaveOpeningBalancesRequest` | `items` 每项：`accountId` 必填 GUID、`direction` 枚举合法、`amount` > 0 且 ≤ 上界；`items` 科目去重 |
| `CarryProfitRequest` | 无请求体（路径期间 id） |
| `CarryYearRequest` | `year` 四位整数（1900–2999） |

## 4. 前端设计

### 4.1 目录（复用 `033` 的 `GeneralLedgerManagement/`）

```
src/
├── api/
│   └── voucher.ts                 # 增期初余额 / 损益结转 / 年末结转接口与类型
└── views/
    └── GeneralLedgerManagement/
        ├── OpeningBalanceDrawer.vue        # 期初余额录入（新增）
        ├── PeriodManagementDrawer.vue      # 期间管理：增「结转损益 / 年末结转」（改造）
        └── FinancialReportsView.vue        # 科目余额表工具条「期初建账」（改造）
```

### 4.2 页面交互

- **`OpeningBalanceDrawer.vue`**：按末级科目列表录入方向 + 金额（`a-input-number :precision="2"`）；底部实时显示借 / 贷合计与差额（不平衡时禁用保存）；账套已有凭证时只读并提示。
- **`PeriodManagementDrawer.vue`**：每期间行增「结转损益」按钮（已结转置灰）；年度区增「年末结转」按钮（前置不满足置灰并给出原因）。
- **`FinancialReportsView.vue`**：科目余额表工具条增「期初建账」入口（打开抽屉）；期初列口径已由后端调整，前端不重算。
- 所有写操作按钮遵循 `specs/010-button-loading/design.md` §0。

### 4.3 接口层

- `api/voucher.ts`：`getOpeningBalances` / `saveOpeningBalances` / `carryProfit` / `carryYear`；类型与后端 DTO camelCase 一一对应。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 期初余额**独立成表**而非凭证 | `OpeningBalances` | 避免"期初凭证归属哪个期间 / 是否受结账闸门约束"的循环依赖；报表期初取数改为两段相加（§0.1） |
| 建账窗口一次性 | 有凭证即 `40176` | 期初余额是时点快照，账套开始记账后再改会破坏报表连续性 |
| 结转**不自动**、由人触发 | 显式动作 | 结转涉及科目判断，自动结转易在期间未备妥时误生成；结账闸门强制"先结转" |
| 结账闸门用「存在 `ProfitCarry` 凭证」判据 | 而非余额为 0 | 余额为 0 可能是"本无损益"也可能是"已结转"，凭证存在性判据唯一且可追溯 |
| 年结凭证记在 12 月 | 而非次年 1 月 | 年末结转属当年账务（先年结、再结 12 月），避免跨年污染 |
| 利润分配**续行预置科目** | 追加 `4104` | 年结需目标科目；复用 `031` 种子机制，不硬编码 |
| 无新增权限点 | 复用 `vouchers.create` / `close` | 期初与结转同属"记账 / 结账"动作，细分权限收益不足 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **期初建账**：平衡校验通过 / 不平 `40000`；有凭证 `40176`；科目非末级 / 停用 `40157`；全量替换语义。
- **损益结转**：分录方向与金额正确（收入 / 费用分列）；借贷平衡；重复 `40177`；净额全 0 `40178`；映射缺失 `40158`。
- **结账闸门**：未结转 `40179`；结转后放行（`033` 结账用例回归）。
- **年末结转**：1–11 月未全结 `40180`；12 月不存在 `40159`；本年利润 0 `40180`；正常结转后本年利润余额为 0、利润分配增加；重复 `40180`。
- **报表**：期初 = 期初余额表 + `start` 前分录（科目余额表 / 资产负债表）；资产负债表恒等式回归。

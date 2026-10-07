---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：固定资产（erp-fixed-asset）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；凭证写入复用 `033-erp-general-ledger` 的 `VoucherFactory` / `VoucherWriter`。
> 科目映射与凭证模板见 `specs/033-erp-general-ledger/design.md` §0.1 / §0.3；预置科目见 `specs/031-erp-finance-master/design.md` §2.4。

## 0. 约定正文（唯一事实源）

### 0.1 折旧口径（直线法 / 年限平均）

| 项 | 规则 |
|---|---|
| 可折旧总额 | `原值 − 残值`（残值 ≥ 0 且 < 原值） |
| 月折旧额 | `round((原值 − 残值) / 预计使用月数, 2)`（分位四舍五入） |
| 末月补差 | 最后一次计提取 `可折旧总额 − 累计折旧`（确保累计不超可折旧总额） |
| 计提条件 | 资产**在用**且 `累计折旧 < 可折旧总额` |
| 起提 | 取得日期**次月**起计提（当月不提，简化） |
| 幂等 | 同一资产同一期间只提一次（资产记 `LastDepreciationPeriod`） |

### 0.2 状态与流转

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `FixedAssetStatus` | `InUse = 0` / `Disposed = 1` | 在用 / 已处置 | `green` / `gray` |

| 操作 | 允许条件 | 违例 |
|---|---|---|
| 编辑 | 在用（`Disposed` 不可编辑） | `40190` |
| 计提折旧 | 在用且未提完 | `40189` |
| 处置 | 在用 | `40190` |

### 0.3 折旧凭证模板（`SourceType = Depreciation`）

一次计提生成**一张**凭证（按期间）：

| 借方 | 贷方 | 说明 |
|---|---|---|
| 各资产折旧费用科目（按科目汇总） | 累计折旧（映射键 `AccumDepreciation`）合计 | 借合计 = 贷合计 = 本期计提总额 |

- 摘要「计提折旧 YYYY-MM」；归属期间 = 计提入参期间（`033` §0.1，期间已结账 → `40154`，映射缺失 → `40158`，整体回滚）。
- `033` §0.1 `VoucherSourceType` 续行 `Depreciation`。

### 0.4 科目与映射续行

- `031` §2.4 预置科目（缺则**幂等追加**，已存在则不动）：`1601 固定资产`（资产）/ `1602 累计折旧`（资产，贷方备抵）/ `6602 管理费用`（损益）。
- `033` §0.3 映射键续行：`FixedAsset`（→ `1601`）/ `AccumDepreciation`（→ `1602`）/ `DepreciationExpense`（→ `6602`，默认折旧费用科目，资产可覆盖）。

### 0.5 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 固定资产 | `AssetNo` | 唯一；`FA + yyyyMMdd + 4 位序号` | — |

### 0.6 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 固定资产 | `fixedAssets` | `view` / `create` / `update` / `depreciate` / `dispose` | `/api/fixed-assets*`、`/fixed-assets` |

### 0.7 菜单归属（在 `025` §0.2 表续行）

「财务（`finance`）」分组续行子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 财务（`finance`） | 固定资产（`fixedAssets`） | `053` |

## 1. 总体设计

```
固定资产（前端 /fixed-assets，域目录 FixedAssetManagement/）
  → FixedAssetsController
    → App.Core/Features/FixedAssets/<Action>/*RequestHandler
      → IFixedAssetRepository（台账 + 计提状态回写）
        + IAccountRepository（费用科目校验，031）
        + IAccountMappingRepository（AccumDepreciation，033）
        + VoucherWriter（同事务生成折旧凭证，033）
        → PostgreSQL（FixedAssets / Vouchers / VoucherEntries）
```

核心原则：

- **计提是期间动作**：一次调用对全部可提资产计提并生成一张汇总凭证。
- **复用凭证通道**：折旧凭证走 `033` 的写入器（平衡 / 期间闸门统一）。
- **幂等靠期间标记**：资产记 `LastDepreciationPeriod`，避免重复计提（单线程月度动作，无需额外台账表）。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/FixedAsset.cs` 与表 `FixedAssets`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AssetNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `FA + yyyyMMdd + 4` |
| `Name` | `string` | `varchar(50)` | NOT NULL | 资产名称 |
| `AcquiredDate` | `DateOnly` | `date` | NOT NULL | 取得日期（次月起提） |
| `OriginalCost` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 原值 |
| `ResidualValue` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，< `OriginalCost` | 残值 |
| `UsefulMonths` | `int` | `integer` | NOT NULL，≥ 1 | 预计使用月数 |
| `ExpenseAccountId` | `Guid` | `uuid` | NOT NULL，FK → `Accounts(Id)` | 折旧费用科目（损益类，可覆盖默认） |
| `AccumulatedDepreciation` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0 | 累计折旧 |
| `LastDepreciationPeriod` | `string?` | `varchar(6)` | NULL | 上次计提期间（`YYYYMM`，幂等标记） |
| `Status` | `FixedAssetStatus` | `smallint` | NOT NULL，默认 `InUse` | 见 §0.2 |
| `DisposedDate` | `DateOnly?` | `date` | NULL | 处置日期 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

- **净值 = `OriginalCost − AccumulatedDepreciation`（推导，不落列）**。

### 2.2 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `FixedAssetFieldConstraints` | `NoMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` / `UsefulMonthsMinValue = 1` / `UsefulMonthsMaxValue = 600` / `CostMaxValue = 999999999999.99` |

### 2.3 迁移与种子

- 迁移：`dotnet ef migrations add AddErpFixedAsset -p src/App.Infrastructure -s src/App.Api`（建 1 表 + 索引）。
- 种子：`DatabaseInitializer` 幂等追加预置科目 `1601` / `1602` / `6602` 与映射键 `FixedAsset` / `AccumDepreciation` / `DepreciationExpense`。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IFixedAssetRepository.GetPagedAsync(...)` | 列表（关键词 / 状态，`AcquiredDate` 倒序） |
| `IFixedAssetRepository.GetByIdAsync` / `AddAsync` / `UpdateAsync` | |
| `IFixedAssetRepository.GenerateNoAsync(date, ...)` | 单号 `FA` |
| `IFixedAssetRepository.GetDepreciableAsync(period, ...)` | 取可计提资产（在用且未提完） |
| `IFixedAssetRepository.UpdateDepreciationAsync(assetId, addedAmount, period, ...)` | 累计折旧 + 期间标记回写 |

- 读模型：`FixedAssetListItem`（含净值推导）、`FixedAssetDetail`。

### 3.2 错误码（`ErrorCode.cs`，从 `40188` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40188 | `DepreciationPeriodExists` | 该期间已计提折旧 |
| 40189 | `AssetNotDepreciable` | 资产不可计提（已提完 / 已处置） |
| 40190 | `AssetDisposalInvalid` | 资产不可处置 / 不可编辑（已处置） |

> 下一个可用业务码 → `40191`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/fixed-assets` | GET | `FixedAssets/GetFixedAssets` | `PagedResult<..ListItemDto>` | `fixedAssets.view` / 40000 |
| `/api/fixed-assets` | POST | `FixedAssets/CreateFixedAsset` | `..DetailDto` | `fixedAssets.create` / 40000 / 40400 |
| `/api/fixed-assets/{id:guid}` | GET | `FixedAssets/GetFixedAssetById` | `..DetailDto` | `fixedAssets.view` / 40400 |
| `/api/fixed-assets/{id:guid}` | PUT | `FixedAssets/UpdateFixedAsset` | `..DetailDto` | `fixedAssets.update` / 40000 / 40190 / 40400 |
| `/api/fixed-assets/depreciate` | POST | `FixedAssets/DepreciateFixedAssets` | `{ period, totalAmount, voucherNo }` | `fixedAssets.depreciate` / 40188 / 40189 / 40154 / 40158 |
| `/api/fixed-assets/{id:guid}/dispose` | PUT | `FixedAssets/DisposeFixedAsset` | `..DetailDto` | `fixedAssets.dispose` / 40190 / 40400 |

### 3.4 关键用例流程（Handler）

- **CreateFixedAsset**：费用科目末级 + 损益类 + 启用（否则 `40000` / `40157` 复用）；生成单号；落库。
- **UpdateFixedAsset**：`Status == Disposed` → `40190`；更新（**不改累计折旧**）。
- **DepreciateFixedAssets**：入参期间已计提（存在该期间未作废 `Depreciation` 凭证）→ `40188`；取可提资产，为空 → `40189`；逐资产按 §0.1 计算（末月补差）→ 按费用科目汇总借方 → 同事务：`VoucherWriter` 写凭证 + 逐资产 `UpdateDepreciationAsync`。
- **DisposeFixedAsset**：`Status == Disposed` → `40190`；置 `Disposed` + `DisposedDate`。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdateFixedAssetRequest` | `name` 必填 1–50；`acquiredDate` 必填；`originalCost` > 0 且 ≤ 上界；`residualValue` ≥ 0 且 < `originalCost`；`usefulMonths` 1–600；`expenseAccountId` 必填 GUID；`remark` ≤ 200 |
| `DepreciateFixedAssetsRequest` | `period` 必填 `YYYY-MM` 且合法 |
| `GetFixedAssetsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── fixedAsset.ts
└── views/
    └── FixedAssetManagement/
        ├── FixedAssetsView.vue        # 列表（含「计提折旧」工具条按钮 + 期间选择）
        └── FixedAssetFormDrawer.vue   # 登记 / 编辑抽屉（含处置）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `fixed-assets` | `fixedAssets` | `FixedAssetsView` |

- 「财务」分组续行「固定资产」；`meta.permission = 'fixedAssets.view'`。

### 4.3 页面交互

- **列表**：筛选（关键词 / 状态）；列：编码、名称、取得日期、原值、累计折旧、净值（推导）、状态（`a-tag`）、操作列（编辑 / 处置）。工具条增「计提折旧」按钮（弹期间选择，默认上月末）。
- **抽屉**：名称 / 取得日期 / 原值 / 残值 / 预计使用月数 / 折旧费用科目（科目树仅损益类末级）/ 备注；编辑态不含累计折旧；已处置只读。

### 4.4 接口层

- `api/fixedAsset.ts`：列表 / 新建 / 详情 / 编辑 / 计提 / 处置；`FIXED_ASSET_STATUS_META`（值 → 文案 / 颜色）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 只做**直线法** | 年限平均 | 覆盖绝大多数中小场景；加速折旧法收益不足 |
| 计提**次月**起 | `AcquiredDate` 次月 | 与"当月新增不提"的通行做法一致，免去按天折算 |
| 一次计提**一张汇总凭证** | 按费用科目汇总 | 减少凭证条数；期间动作一次入账 |
| 幂等用**期间标记** | `LastDepreciationPeriod` | 单线程月度动作，无需额外台账表 |
| 取得**不生成凭证** | 走采购 / 人工凭证 | 资产取得路径多（采购 / 自建 / 捐赠），一期不做 |
| 处置**不生成清理分录** | 只改状态 | 清理涉及清理科目与损益，另评 |
| 预置科目**幂等追加** | `1601` / `1602` / `6602` | 复用 `031` 种子机制；已存在不覆盖 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **折旧计算**：月折旧四舍五入；末月补差；`累计 ≤ 可折旧总额`；取得当月不提、次月起提。
- **计提**：正常生成凭证 + 回写；期间重复 `40188`；无可提 `40189`；期间已结账 `40154` 回滚；映射缺失 `40158`。
- **处置**：处置后不参与计提；重复处置 / 已处置编辑 `40190`。
- **列表 / 详情**：净值推导。
- **字段约束一致性**：常量与 EF 列长一致。

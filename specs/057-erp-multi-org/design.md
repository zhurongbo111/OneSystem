---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：多公司 / 多组织（erp-multi-org）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格为**横向改造**（新增公司域 + 全业务 / 账务表加 `CompanyId` + 查询隔离），改造面广但每处改动小，以 `028`（横向改造）与 `038`（默认实体 + 回填范式）为参照。
> 权限 / 数据范围机制复用 `028` / `046`；期间 / 凭证见 `specs/033-erp-general-ledger/design.md` §0。

## 0. 约定正文（唯一事实源）

### 0.1 共享 / 隔离边界（唯一判据）

| 类别 | 处置 | 理由 |
|---|---|---|
| **主数据**：商品 / 分类 / 往来 / 仓库定义 / 会计科目 / 税率 / 价格政策 | **共享**（不加 `CompanyId`） | 一套主数据多主体复用，避免重复维护；科目表共享便于 058 合并报表统一口径 |
| **组织**：部门 / 岗位 / 员工 | **共享** | 组织是人力域（`030`）主体；如需按公司分立部门另评 |
| **业务单据**：请购 / 采购订单 / 入库 / 退货 / 报价 / 销售订单 / 出库 / 退货 / 结算 / 发票 / 调拨 / 盘点 | **隔离** | 业务归属法人主体 |
| **库存**：库存台账 / 库存流水 | **隔离** | 各主体独立库存（组织级 = 本公司各仓合计，`038` §0.2 口径在公司内适用） |
| **账务**：会计期间 / 凭证 / 期初余额 / 预算 / 固定资产 / 费用报销 | **隔离**（各公司独立账套） | "各做各的账" |
| **资金**：资金账户 | **隔离** | 各主体独立银行账户 |
| **流程**：审批（`042`）/ 操作日志（`029`） | **隔离**（加 `CompanyId` 便于过滤） | 审批与留痕应归属公司 |
| **站内消息**（`041`） | **共享**（按接收人，不加 `CompanyId`） | 消息面向用户，非公司数据 |

### 0.2 受影响表分组（唯一事实源）

> 落地判据：**下表列出的实体必须含 `CompanyId`（NOT NULL，FK）**，由隔离守卫测试断言；`AppDbContext` 为最终清单来源。

| 分组 | 实体（追加 `CompanyId`） |
|---|---|
| 采购 | `PurchaseRequisition` / `PurchaseOrder` / `PurchaseReceipt` / `PurchaseReturn` |
| 销售 | `Quotation` / `SalesOrder` / `SalesShipment` / `SalesReturn` |
| 结算与发票 | `Settlement` / `Invoice` |
| 库存 | `Inventory` / `StockMovement` / `StockTake` / `Transfer` / `Batch` |
| 账务 | `AccountingPeriod` / `Voucher` / `OpeningBalance` / `Budget` / `FixedAsset` / `Expense` |
| 资金 | `BankAccount` |
| 流程 | `Approval` |

- **明细表**（`*Items`）**不加** `CompanyId`：归属由主表保证（避免冗余列与不一致）。
- **唯一键修订**：受影响的唯一索引前置 `CompanyId`——如 `AccountingPeriods` 唯一键由 `(Year, Month)` 改为 `(CompanyId, Year, Month)`；`Inventory` 由 `(ProductId, WarehouseId, BatchId)` 改为 `(CompanyId, ProductId, WarehouseId, BatchId)`；`Approvals` 由 `(OrderType, OrderId)` 改为 `(CompanyId, OrderType, OrderId)`。
- **单号**（`ROADMAP` §6.7）：**全局唯一不变**（不按公司分段），生成机制不变。

### 0.3 当前公司解析

| 项 | 规则 |
|---|---|
| 来源 | 请求头 `X-Company-Id`（前端切换器）；未带 → 用户**默认公司** |
| 校验 | 用户可访问该公司（`UserCompany` 或 `SuperAdmin`）；否则 `40195` |
| 缓存 | Scoped + 单请求缓存（同 `028` `IPermissionResolver` / `046` `IDataScopeProvider`） |
| 与数据范围 | `046` 数据范围在**当前公司内**再按部门 / 本人；公司层优先 |

### 0.4 默认公司与回填

- 迁移内插入**固定 GUID 默认公司**（`11111111-1111-1111-1111-111111111111`，编码 `DEFAULT`），所有既有数据回填其 `CompanyId`（同 `038` 默认仓范式）。
- 默认公司**不可停用 / 不可删除**（`40197`）；有数据的公司不可删除（`40196`）。

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 公司 | `companies` | `view` / `create` / `update` / `status` | `/api/companies*`、`/companies` |

### 0.6 菜单归属（在 `025` §0.2 表续行）

「系统（`system`）」分组续行子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 系统（`system`） | 公司管理（`companies`） | `057` |

## 1. 总体设计

```
公司管理（前端 /companies，域目录 CompanyManagement/）
  → CompaniesController
    → App.Core/Features/Companies/<Action>/*RequestHandler
      → ICompanyRepository / IUserCompanyRepository → PostgreSQL（Companies / UserCompanies）

当前公司（横切，全部业务 / 账务请求）
  HTTP 请求 → 认证（JWT）→ 权限（028）
    → ICurrentCompany.CompanyId()（请求头 X-Company-Id / 用户默认公司；无权 40195）
    → 数据范围（046，在当公司内）
    → 各仓储查询 / 写入带 CompanyId 过滤 / 赋值

前端
  顶栏公司切换器（AppLayout）→ 当前公司存 Pinia → request.ts 注入 X-Company-Id
    → 切换后重取列表 / 报表
```

核心原则：

- **默认无感**：升级后（默认公司）行为与单主体完全一致，隔离对既有功能透明。
- **单一解析入口**：当前公司只在 `ICurrentCompany` 解析；Handler / 仓储不各自读请求头。
- **隔离可断言**：受影响的每个实体必须含 `CompanyId`，由守卫测试守护（防漏改）。
- **主数据共享**：减少改造面（不改 `012` / `013` / `031` …），并让 `058` 合并报表口径统一。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/Company.cs` 与表 `Companies`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 公司编码 |
| `Name` | `string` | `varchar(50)` | NOT NULL | 公司名称 |
| `IsDefault` | `bool` | `boolean` | NOT NULL，默认 false | 默认公司（唯一） |
| `Status` | `CompanyStatus` | `smallint` | NOT NULL，默认 `Enabled` | 启用 / 停用 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.2 实体 `App.Core/Entities/UserCompany.cs` 与表 `UserCompanies`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `UserId` | `Guid` | `uuid` | NOT NULL，FK → `Users(Id)`，索引 | |
| `CompanyId` | `Guid` | `uuid` | NOT NULL，FK → `Companies(Id)`，索引 | |
| `IsDefault` | `bool` | `boolean` | NOT NULL，默认 false | 用户默认公司（每人至多一个） |

- 唯一索引 `(UserId, CompanyId)`；用户默认公司唯一由应用层保证（`SuperAdmin` 可访问全部公司，无需逐条 `UserCompanies`）。

### 2.3 枚举与常量

| 枚举 / 常量 | 取值 |
|---|---|
| `CompanyStatus` | `Enabled = 1` / `Disabled = 0` |
| `CompanyFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` / `DefaultCompanyId`（固定 GUID 常量） |

### 2.4 迁移（分步）

1. `AddErpMultiOrg`：建 `Companies` / `UserCompanies`；`InsertData` 插入固定 GUID 默认公司。
2. 受影响表（§0.2）逐表 `AddColumn CompanyId`（先可空）→ `UPDATE` 回填默认公司 → `AlterColumn` NOT NULL + 建 FK + 索引。
3. 唯一键重建（§0.2）。
4. 全部既有单测 / e2e 以默认公司通过（回归门槛）。

## 3. 后端设计

### 3.1 组件

| 类型 | 位置 | 说明 |
|---|---|---|
| `ICurrentCompany` / `CurrentCompany` | `App.Core/Abstractions/` + `App.Infrastructure/Auth/` | `Guid CompanyId()`（请求头 → 校验可见性 → 缓存；无权 `40195`） |
| `ICompanyRepository` / `IUserCompanyRepository` | `App.Core/Abstractions/` | 公司 CRUD / 用户公司集合 |
| 隔离过滤 | 各仓储 | 查询追加 `CompanyId == current`；写入赋值 `CompanyId` |

- 读模型：`CompanyListItem` / `CompanyDetail` / `CompanyPickItem`。

### 3.2 错误码（`ErrorCode.cs`，从 `40195` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40195 | `CompanyNotAccessible` | 用户无权访问该公司 |
| 40196 | `CompanyHasData` | 公司存在业务 / 账务数据，禁止删除 |
| 40197 | `CompanyDefaultImmutable` | 默认公司不可停用 / 删除 |

> 下一个可用业务码 → `40198`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/companies` | GET | `Companies/GetCompanies` | `PagedResult<CompanyListItemDto>` | `companies.view` / 40000 |
| `/api/companies` | POST | `Companies/CreateCompany` | `CompanyDetailDto` | `companies.create` / 40000 |
| `/api/companies/{id:guid}` | GET/PUT | `Companies/GetCompanyById` / `UpdateCompany` | `CompanyDetailDto` | `companies.view` / `update` / 40000 / 40197 / 40400 |
| `/api/companies/{id:guid}/status` | PUT | `Companies/UpdateCompanyStatus` | `CompanyDetailDto` | `companies.status` / 40197 / 40400 |
| `/api/companies/{id:guid}` | DELETE | `Companies/DeleteCompany` | `null` | `companies.update` / 40196 / 40197 / 40400 |
| `/api/companies/picks` | GET | `Companies/GetCompanyPicks` | `IReadOnlyList<CompanyPickItemDto>` | `companies.view`（用户可访问公司，供切换器） |

### 3.4 关键用例流程（Handler）

- **CurrentCompany 解析**：请求头 `X-Company-Id` 存在 → 校验公司启用且用户可访问（`40195`）→ 返回；否则取 `UserCompanies.IsDefault`；`SuperAdmin` 可访问全部。
- **CreateCompany**：编码唯一；落库（非默认）。
- **DeleteCompany**：`IsDefault` → `40197`；任一受影响表存在该公司数据 → `40196`；否则删除。
- **所有业务 / 账务 Handler（改造）**：写入时赋 `CompanyId = ICurrentCompany.CompanyId()`；读取时仓储按当前公司过滤。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdateCompanyRequest` | `code` 必填 2–20、`CodePattern`；`name` 必填 1–50；`remark` ≤ 200 |
| `GetCompaniesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── company.ts                     # 公司 CRUD + picks
├── stores/
│   └── company.ts                     # 当前公司（Pinia）
├── components/
│   └── AppLayout.vue                  # 顶栏公司切换器（改造）
└── views/
    └── CompanyManagement/
        └── CompaniesView.vue          # 公司管理页
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `companies` | `companies` | `CompaniesView` |

- 「系统」分组续行「公司管理」；`meta.permission = 'companies.view'`。
- 顶栏（`AppLayout`）增公司切换 `a-select`（选项来自 `/api/companies/picks`）。

### 4.3 页面交互

- **`CompaniesView.vue`**：列表（编码 / 名称 / 默认标记 / 状态 / 操作列）；抽屉表单；默认公司行停用 / 删除置灰。
- **顶栏切换器**：切换 → 更新 `stores/company.ts` → `request.ts` 注入 `X-Company-Id` → 刷新当前页数据；无权公司不在选项中。
- 所有列表 / 报表**无感**（隔离在后端）。

### 4.4 接口层

- `api/company.ts`：公司 CRUD + `getCompanyPicks`。
- `api/request.ts`（改造）：从 `stores/company.ts` 取当前公司注入请求头。
- `api/user.ts`（改造）：用户表单增「可访问公司」多选（`UserCompanies`）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| **单库多租**（`CompanyId` 隔离） | 非多库 | 部署 / 备份 / 升级简单；单库数据量在目标规模内可控 |
| 主数据**共享** | 不加 `CompanyId` | 显著缩小改造面；合并报表（`058`）口径统一 |
| 明细表**不加** `CompanyId` | 由主表保证 | 避免冗余列与两者不一致 |
| **单号全局唯一** | 不按公司分段 | 生成机制不变；避免改造 `015` 单号生成 |
| 当前公司走**请求头** | 非 URL 段 | 一处注入、全局生效；避免改造所有路由 |
| 默认公司**固定 GUID + 回填** | 同 `038` 默认仓 | 升级无感、迁移可复现 |
| 隔离**守卫测试** | 断言实体含 `CompanyId` | 横向改造漏改风险高，用测试强制 |
| 用户可访问公司**管理员维护** | 非按公司配角色 | 本期只需"能进哪家公司"；公司级角色差异另评 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **迁移回归**：默认公司回填后，全部既有业务 / 账务用例通过（单主体行为不变）。
- **当前公司**：请求头解析、默认公司、无权 `40195`、`SuperAdmin` 全量。
- **隔离**：单据 / 库存 / 账务 / 资金跨公司不可见；写入赋 `CompanyId`；明细不重复赋。
- **唯一键**：`(CompanyId, …)` 生效（同公司内唯一、跨公司可重复）。
- **公司域**：编码唯一；默认公司保护 `40197`；有数据删除 `40196`。
- **守卫**：§0.2 全部实体含 `CompanyId`（反射断言）。

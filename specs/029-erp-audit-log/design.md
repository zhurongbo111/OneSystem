---
created: 2026-09-17
updated: 2026-09-22
---

# 设计规格：业务操作审计日志（erp-audit-log）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格为**横向能力**（新增 1 张结构表 + 2 个只读接口 + 各域写路径追加日志）。
> 日志写入遵循「与业务同事务、失败不留痕、显式调用」三原则（见 §5 决策）。

## 0. 审计约定（唯一事实源）

### 0.1 覆盖范围（资源类型 × 动作）

> `2026-09-17` 版覆盖 `012`–`028` 全部写路径；后续规格（`030`–`045`）在实现时在本表续行（`tasks.md` 有「范围表守卫」验收项）。

| 资源（`AuditResource`） | 动作 | 用例 / 触发点 | 摘要模板（示例） |
|---|---|---|---|
| 商品 `Product` | 创建 / 更新 / 启停 | `Products/CreateProduct` / `UpdateProduct` / `UpdateProductStatus` | 「新增商品 编码 A001 名称 螺丝」/「修改商品 A001」/「停用商品 A001」 |
| 分类 `Category` | 创建 / 更新 / 删除 | `Categories/CreateCategory` / `UpdateCategory` / `DeleteCategory` | 「新增分类 五金」/「删除分类 五金」 |
| 往来单位 `Partner` | 创建 / 更新 / 启停 | `Partners/*` | 「新增往来单位 甲供应商（供应商）」/「停用往来单位 甲供应商」 |
| 仓库（`038`） `Warehouse` | 创建 / 更新 / 启停 / 设默认 | `Warehouses/*` | 「新增仓库 上海仓（SH）」/「设置默认仓库 上海仓」 |
| 客户价格（`036`） `PartnerPrice` | 创建 / 更新 / 删除 | `PartnerPrices/*` | 「设置客户价 甲客户 / A001 = 8.80」 |
| 用户 `User` | 创建 / 更新 / 启停 / 重置密码 / 角色变更 | `Users/*`（`028` 的 `roleIds`） | 「新增用户 zhangsan（张三）」/「重置用户 zhangsan 密码」/「调整用户 zhangsan 角色：Staff → 仓管,财务」 |
| 角色 `Role` | 创建 / 更新 / 删除 | `Roles/*` | 「新增角色 仓管（12 项权限）」/「修改角色 仓管 权限：+库存盘点.创建」/「删除角色 仓管」 |
| 采购订单 `PurchaseOrder` | 创建 / 更新 / 作废 / 关闭 | `PurchaseOrders/CreatePurchaseOrder` / `UpdatePurchaseOrder` / `VoidPurchaseOrder` / `ClosePurchaseOrder` | 「创建采购订单 PO…（供应商 甲，3 行，金额 1200.00）」/「关闭采购订单 PO…」 |
| 销售订单 `SalesOrder` | 创建 / 更新 / 作废 / 关闭 | `SalesOrders/CreateSalesOrder` / `UpdateSalesOrder` / `VoidSalesOrder` / `CloseSalesOrder` | 「创建销售订单 SO…（客户 乙，2 行，金额 500.00）」/「作废销售订单 SO…」 |
| 采购入库单 `PurchaseReceipt` | 创建 / 作废 | `Purchases/CreatePurchaseOrder` / `VoidPurchaseOrder` | 「开具采购入库单 GR202609170001（供应商 甲，金额 1000.00）」/「作废采购入库单 GR202609170001」 |
| 销售出库单 `SalesShipment` | 创建 / 作废 | `Sales/*` | 「开具销售出库单 GI202609170001（客户 乙，金额 500.00）」/「作废销售出库单 GI…」 |
| 采购退货单 `PurchaseReturn` | 创建 / 作废（结算态变更由「收付款单」记录） | `PurchaseReturns/*` | 「创建采购退货单 PR…（供应商 甲，金额 300.00）」/「作废采购退货单 PR…」 |
| 销售退货单 `SalesReturn` | 创建 / 作废（结算态变更由「收付款单」记录） | `SalesReturns/*` | 同采购退货（客户维度） |
| 收付款单 `Settlement` | 创建（核销）/ 作废 | `Settlements/CreateSettlement` / `VoidSettlement` | 「登记收款单 RC…（甲客户，金额 400.00，核销 GI…）」/「作废收款单 RC…」 |
| 库存盘点单 `StockTake` | 创建（期初 / 盘点，动作 `Adjust`） | `StockTakes/CreateStockTake` | 「期初建账 ST…（3 行，差异 2 行，涉及 螺丝、螺母）」/「库存盘点 ST…（差异 2 行）」 |
| 调拨单（`039`） `Transfer` | 创建 / 作废 | `Transfers/*` | 「开具调拨单 TR…（上海仓 → 北京仓）」 |
| 发票（`032`） `Invoice` | 创建 / 作废 | `Invoices/CreateInvoice` / `VoidInvoice` | 「登记销项发票 12345678（乙客户，金额 1017.00，关联 GI…）」/「作废销项发票 12345678（乙客户、金额 1017.00）」 |
| 成本重算 `Cost` | 重算（`Recalculate`，无明确业务对象 → `ResourceId` 为空） | `Costs/RecalculateCosts` | 「成本重算 2026-09-01 ~ 2026-09-30：流水 320 条、12 个商品、缺价 2 条」 |
| 部门 / 岗位 / 员工（`030`） `Department` / `Position` / `Employee` | 创建 / 更新 / 删除 / 启停 | `Departments/*`、`Positions/*`、`Employees/*` | 「新增部门 财务部（FIN）」/「新增岗位 出纳（P010）」/「新增员工 张三（E0001）」 |
| 会计科目 / 税率（`031`） `Account` / `TaxRate` | 创建 / 更新 / 删除 / 启停 | `Accounts/*`、`TaxRates/*` | 「新增科目 库存现金（1001）」/「新增税率 增值税 13%（VAT13）」 |
| 单据审批（`042`） `Approval` | 通过 / 驳回 | `Approvals/*` | 「审批通过 采购入库单 GR…（金额 12000.00）」 |
| 记账凭证 / 会计期间（`033`） `Voucher` / `AccountingPeriod` | 创建（录入手工凭证）/ 作废 / 结账 / 反结账 / 映射维护（`Update`） | `Vouchers/CreateVoucher` / `VoidVoucher`、`AccountingPeriods/ClosePeriod` / `ReversePeriod`、`UpdateAccountMappings` | 「记账凭证 记-202609-0001（摘要，借贷合计 20.00）」/「作废记账凭证 记-…」/「结账期间 2026-09」/「反结账期间 2026-09」/「维护科目映射 8 项」 |

- **范围外动作**：登录（`009` 已有）、查询 / 打印 / 导出（读操作）、密码哈希值本身、任何系统内部任务（如预警扫描生成站内信——属系统动作，`041` 自记）。

### 0.2 摘要与字段规则

- **摘要（`Summary`）**：中文、单行、≤ 200 字符，模板见 §0.1；含**业务标识**（编码 / 名称 / 单号 / 金额）便于人读；金额保留 2 位。
- **字段级变更（`Changes`）**：JSON 数组，每项 `{ field, label, before, after }`：
  - `field`：实体字段名（camelCase）；`label`：中文字段名，**取该域页面（列表 / 详情 / 表单）的既有中文文案**（各域 `design.md` §4.4 为准），本规格不复制字段标签清单；
  - **只记真正变化的字段**（前后值相同不记录）；新增 / 删除类动作 `before` / `after` 允许为 `null`；
  - 集合类字段（角色、权限点、单据明细）记 `before` / `after` 的**集合快照文本**（如 `Staff` → `仓管,财务`；权限点用「+ / -」差异文本），不逐项展开明细行。
- **敏感字段白名单（永不记录）**：`password`、`newPassword`、`passwordHash`、任何 `token` / `secret` / `key` 字段；实现上由 `AuditChangeBuilder` 的字段名黑名单统一拦截（不依赖各域自觉）。
- **不入库的字段**：`UpdatedAt` / `UpdatedBy`（审计噪音）、自增序号类展示字段。
- **金额**统一 `ToString("0.00")`（摘要与差异一致，保证详情页呈现 `10.00 → 8.80`）、**数量**统一 `ToString("0.##")`、**税率百分比**统一 `ToString("0.####")`（如 `13` / `13.5`）；日期 `yyyy-MM-dd`；枚举输出**中文文案**（统一取 `App.Core/Audit/AuditText.cs`，如结算状态「未结算 / 已结算」、订单状态「待收货 / 已完成收货」）。

### 0.3 动作枚举（`AuditAction`）

`Create = 0` / `Update = 1` / `Delete = 2` / `StatusChange = 3` / `Void = 4` / `Close = 5` / `Settle = 6` / `Approve = 7` / `Adjust = 8`（盘点调整）/ `Recalculate = 9`（成本重算）。

- 前端下拉选此项；文案（中文）在 `design.md` §4.4 定义一次（前端常量映射），后端只回枚举值。

## 1. 总体设计

```
写入（既有写用例改造，无新增写接口）
  各域 RequestHandler
    → IUnitOfWork 事务内：业务写（既有）
                          + IAuditLogger.RecordAsync(AuditEntry)（新增，同一事务）
    → PostgreSQL（AuditLogs + 业务表，同一事务）

查询（前端 /audit-logs）
  → AuditLogsController
    → App.Core/Features/AuditLogs/GetAuditLogs | GetAuditLogById
      → IAuditLogRepository → PostgreSQL（AuditLogs）
```

核心原则：

- **显式写入，不拦截 EF**：审计由 Handler 显式调用（在业务写之后、`CommitAsync` 之前），摘要与字段差异由业务上下文提供，语义准确可读；不用 `SaveChangesInterceptor` 自动推断（无法产出可读摘要与业务标识）。
- **同事务、失败不留痕**：日志与业务同事务；任一失败整体回滚，日志自然不落库（与 `019` 流水同样的约束方式）。
- **纯追加、无改删**：表无 `UpdatedAt` / `UpdatedBy`，不提供更新 / 删除接口。
- **不改变业务语义**：日志写入是**附加动作**，不得影响任何既有判定（如「商品停用」不因日志失败而变成另一套错误码；日志失败即事务回滚，业务一起失败——这是刻意的严格口径，见 §5）。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/AuditLog.cs` 与表 `AuditLogs`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `UserId` | `Guid?` | `uuid` | NULL | 操作人 id（无 HTTP 上下文时为空，如系统动作） |
| `Username` | `string?` | `varchar(50)` | NULL | 操作人登录名**快照** |
| `DisplayName` | `string?` | `varchar(50)` | NULL | 操作人显示名**快照** |
| `Resource` | `AuditResource` | `smallint` | NOT NULL | 资源类型（§0.1） |
| `Action` | `AuditAction` | `smallint` | NOT NULL | 动作（§0.3） |
| `ResourceId` | `Guid?` | `uuid` | NULL | 业务对象 id（如单据 id / 商品 id） |
| `ResourceNo` | `string?` | `varchar(50)` | NULL | 业务标识**快照**（单号 / 编码 / 用户名 / 角色名） |
| `Summary` | `string` | `varchar(200)` | NOT NULL | 中文摘要（§0.2） |
| `Changes` | `string?` | `jsonb` | NULL | 字段级差异 JSON 数组（无差异时为空） |
| `CreatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 操作时间 |

- 索引：`CreatedAt DESC`（默认排序）、`(Resource, ResourceId)`（按对象追溯）、`UserId`（按人追溯）、`ResourceNo`（按业务标识查询）。
- 枚举：`AuditResource`（§0.1 的资源列）、`AuditAction`（§0.3），放 `App.Core/Entities/`；`ResourceNo` 长度 50 覆盖最短的编码 / 单号（`Products.Code` 32 / `OrderNo` 20 / `Username` 50 / 角色名 20）。
- 无软删除、无审计字段（纯追加）；外键不建（`UserId` 不设 FK：操作人可能被停用，日志必须自包含；`ResourceId` 跨多表无法建 FK）。

### 2.2 EF Core 与迁移

- `AppDbContext` 新增 `DbSet<AuditLog> AuditLogs`；配置 `Persistence/Configurations/AuditLogConfiguration.cs`（列长 / 必填 / `jsonb` 列类型 / 索引）。
- 迁移：`dotnet ef migrations add AddErpAuditLog -p src/App.Infrastructure -s src/App.Api`（增量迁移，单表）。
- 无种子、不回填历史（历史操作无痕，无法重建）。

### 2.3 字段约束单一来源

- 新增 `App.Core/Entities/AuditLogFieldConstraints.cs`：`SummaryMaxLength = 200`、`ResourceNoMaxLength = 50`、`UsernameMaxLength = 50`（与 `UserFieldConstraints.UsernameMaxLength` 同源引用）、`DisplayNameMaxLength = 50`（同源引用）、`ChangesMaxLength = 8000`（JSON 文本上限，超出则截断并在摘要追加「（变更内容过长已截断）」，见 §5）。
- 查询 `keyword` 上限 50（对齐 `ResourceNo` 列长，后端规则 §5.3 ③）。

## 3. 后端设计

### 3.1 写入抽象

| 类型 | 位置 | 说明 |
|---|---|---|
| `AuditEntry` | `App.Core/Audit/` | 写入模型：`Resource` / `Action` / `ResourceId` / `ResourceNo` / `Summary` / `Changes?`（由 `AuditChangeBuilder` 产出）；操作人与时间由 `IAuditLogger` 实现自行填充（经 `ICurrentUser`） |
| `AuditChangeBuilder` | `App.Core/Audit/` | 链式构建：`Add(field, label, before, after)`（值相同不记录）、`Build()` → JSON 文本（超长从尾部丢弃并置 `Truncated`）；敏感字段黑名单（见 §0.2）**内置在 `Add` 中**兜底拦截（对外另暴露 `IsSensitive(field)` 便于单测守护），确保不遗漏不依赖各域自觉 |
| `IAuditLogger` | `App.Core/Abstractions/` | `Task RecordAsync(AuditEntry entry, CancellationToken ct)`；实现 `App.Infrastructure/Audit/AuditLogger.cs`（`Add` + `SaveChangesAsync`，随调用方事务提交；`CreatedAt` 由调用方传入或实现内取 `DateTimeOffset.UtcNow`，规格要求**由调用方传 `utcNow`** 以保持单测可注入） |
| 摘要构造辅助 | `App.Core/Audit/AuditSummary.cs` | 静态格式化方法（金额 / 数量 / 日期 / 枚举文案），供各域拼摘要，避免格式散落 |

- 实现注册：`AddInfrastructure` 注册 `IAuditLogger → AuditLogger`（`Scoped`，依赖 `AppDbContext` 与 `ICurrentUser`）。
- **调用约定（强制）**：在各 Handler 的**业务写完成之后、`CommitAsync` 之前**调用 `RecordAsync`；`utcNow` 沿用用例既有的时间参数；不要新增事务边界。

### 3.2 错误码

> **无新增错误码**。日志写入失败即抛异常 → 事务回滚 → 由全局异常中间件按 `50000` 处理（与「宁可失败也不留半截数据」的口径一致）。查询侧 `40000` / `40400` 复用全局。

### 3.3 仓储接口（新增，`App.Core/Abstractions/IAuditLogRepository.cs`）

| 方法 | 说明 |
|---|---|
| `Task AddAsync(AuditLog log, CancellationToken ct)` | 追加一条日志（调用方事务内） |
| `Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPagedAsync(string? keyword, AuditResource? resource, AuditAction? action, Guid? userId, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, CancellationToken ct)` | 查询：`keyword` 匹配 `ResourceNo` / `Username` / `DisplayName`；`CreatedAt` 闭区间；`CreatedAt DESC`；`AsNoTracking`（`Changes` 列在列表查询中**不加载**——用投影排除，避免大字段拖慢列表） |
| `Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct)` | 详情（含 `Changes`） |

### 3.4 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/audit-logs` | GET | `AuditLogs/GetAuditLogs` | `PagedResult<AuditLogListItemDto>` | `auditLogs.view` / 40000 |
| `/api/audit-logs/{id:guid}` | GET | `AuditLogs/GetAuditLogById` | `AuditLogDetailDto`（含 `changes` 数组） | `auditLogs.view` / 40400 |

- 出参：`AuditLogListItemDto { id, username, displayName, resource, action, resourceNo, summary, createdAt }`；`AuditLogDetailDto` 追加 `resourceId` 与 `changes: { field, label, before, after }[]`（后端把 `jsonb` 文本反序列化为数组返回，前端不再解析字符串）。
- `AuditLogsDtoMapper`（`Features/AuditLogs/`）负责映射与 `Changes` 反序列化（失败时返回空数组并记 warning 日志，不让坏数据打断查询）。

### 3.5 校验规则（FluentValidation，仅格式层）

| 请求 | 规则 |
|---|---|
| `GetAuditLogsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（`AuditLogFieldConstraints.KeywordMaxLength`）；`resource` / `action` 可空合法值；`userId` 可空；`start` / `end` 可空且 `start <= end` |

### 3.6 写入接入点（改造既有用例）

| 域 | 用例 | 动作 | 记录内容要点 |
|---|---|---|---|
| 商品 | `CreateProduct` / `UpdateProduct` / `UpdateProductStatus` | `Create` / `Update` / `StatusChange` | 更新记名称 / 分类 / 单位 / 采购价 / 销售价 / 安全库存 / 备注的前后值 |
| 分类 | `CreateCategory` / `UpdateCategory` / `DeleteCategory` | `Create` / `Update` / `Delete` | 名称前后值 |
| 往来单位 | `CreatePartner` / `UpdatePartner` / `UpdatePartnerStatus` | 同上 | 类型 / 联系人 / 电话 / 地址 / 备注 / 账期天数 / 信用额度（`036` 续行） |
| 客户价格（`036`） | `CreatePartnerPrice` / `UpdatePartnerPrice` / `DeletePartnerPrice` | `Create` / `Update` / `Delete` | 客户 / 商品 / 协议单价 / 备注的前后值（删除只记资源标识） |
| 用户 | `CreateUser` / `UpdateUser` / `UpdateUserStatus` / `ResetPassword` | `Create` / `Update` / `StatusChange` | 显示名 / 邮箱 / 手机号 / 状态 / 角色集合；**重置密码只记「已重置」不记内容** |
| 角色 | `CreateRole` / `UpdateRole` / `DeleteRole` | `Create` / `Update` / `Delete` | 名称 / 备注 / 权限点增删差异文本 |
| 单据（采购 / 销售 / 退货） | `Create*` / `Void*` / `Update*Settlement` | `Create` / `Void` / `Settle` | 单号 / 往来 / 金额 / 明细行数 / 状态前后值 |
| 收付款 | `CreateSettlement` / `VoidSettlement` | `Create` / `Void` | 单号 / 往来 / 金额 / 核销单据号列表 |
| 盘点 | `CreateStockTake` | `Adjust` | 单号 / 类型 / 明细行数 / 差异行数 |
| 成本重算 | `RecalculateCosts` | `Recalculate` | 流水条数 / 缺价条数 |

- 每处接入仅追加 ~3–6 行（构造 `AuditEntry` + `RecordAsync`），不改业务判定与错误码。
- **明细行变更**（单据明细）不逐行记录：记明细行数与总金额（决策见 §5）。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── auditLog.ts                        # 操作日志接口层 + 资源 / 动作文案映射常量
└── views/
    └── AuditLogManagement/
        ├── AuditLogsView.vue              # 操作日志列表
        └── AuditLogDetailDrawer.vue       # 详情抽屉（字段级差异表格）
```

- 资源 / 动作的中文文案与 `a-tag` 颜色映射为**前端常量**（放 `api/auditLog.ts` 或 `src/utils/`，全项目唯一来源），与后端枚举一一对应（后端返回枚举值，`design.md` 不重复）。

### 4.2 接口层

- `src/api/auditLog.ts`：`getAuditLogs` / `getAuditLogById` + 类型（`AuditLogListItem` 含 `resource` / `action` 数值）+ 资源 / 动作文案映射常量。
- 日期范围参数转 UTC ISO（同既有约定）。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `audit-logs` | `auditLogs` | `AuditLogsView` |

- `AppLayout.vue`「系统」分组追加「操作日志」（`025` §0.2 总表已预留）；菜单项 `permission: 'auditLogs.view'`（`028` 约定）。

### 4.4 页面交互

**操作日志列表 `AuditLogsView.vue`**（只读，参照 `specs/006-list-showcase/design.md` §0）：

- 筛选行：关键词（资源号 / 操作人）+ 资源类型下拉 + 动作下拉 + 操作人（`a-select`，可空，数据源可复用用户列表）+ 时间范围 + 搜索 / 重置。
- 操作行：仅刷新、列设置（无新增 / 编辑 / 删除：日志不可改）。
- 表格列：序号、操作时间、操作人（`displayName（username）`）、资源类型（`a-tag`）、动作（`a-tag`）、业务标识（`resourceNo`，空显示 `-`）、摘要（`ellipsis` + `tooltip`）、操作列（1 个「详情」`IconEye`）；服务端分页（`pageSize` 默认 20）。

**详情抽屉 `AuditLogDetailDrawer.vue`**（宽 640，只读）：

- 头部：摘要 + 操作时间 / 操作人 / 资源类型 / 动作 / 业务标识（`a-descriptions`）。
- 主体：字段级差异表格（列：字段、变更前、变更后；`before` 为空显示 `-`；`after` 为空（清空）显示 `-` 并标灰）；无差异时显示 `a-empty`「本次操作无字段变更」。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 打开详情抽屉 | `detailLoading` | 抽屉内容区 `a-spin` |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 显式记录而非 EF 拦截器 | Handler 内调用 `IAuditLogger` | 拦截器只能拿到实体差异，产不出「作废采购入库单 GR…（供应商 甲，金额 1000.00）」这类业务摘要；显式调用可读、可控、可单测 |
| 与业务同事务 | 复用调用方 `IUnitOfWork` | 「业务成功但日志丢失」会产生审计黑洞；「业务失败但有日志」产生噪音；同事务两个问题一起解决 |
| 日志失败即业务失败 | 抛异常回滚 | 审计是合规能力，宁可业务重试也不留无痕操作；代价是极端情况（如日志表异常）会阻断业务，可通过数据库权限与监控缓解 |
| 字段级差异存 `jsonb` | `Changes` 列 | 结构可变（不同资源字段不同），JSON 最合适；`jsonb` 可查询且 PG 原生；列表查询用投影排除该列避免拖慢 |
| 明细行不逐行记录 | 只记行数与金额 | 明细可能上百行，逐行记录使日志膨胀且难读；需要明细追溯时看单据本身（单据不可编辑 + 可作废，明细不会悄悄变） |
| 资源类型用枚举 | `AuditResource` | 可被索引与筛选；不用自由字符串（易漂移、无法做下拉） |
| 敏感字段黑名单在构建器统一拦截 | `AuditChangeBuilder` | 不能靠各域自觉（一次漏写就是密码明文入库）；集中拦截 + 单测守护 |
| 摘要长度与 JSON 长度有上限 | 200 / 8000，超出截断 | 防异常数据把日志表撑爆；截断时明确标注，避免误读为完整内容 |
| 不建 FK | `UserId` / `ResourceId` 无外键 | 日志必须自包含且永不被级联影响；`ResourceId` 跨多表无法建 FK |
| 不做读操作审计 | 只记写 | 读审计会产生数量级更大的数据（价值低），属合规特例需求 |
| 不做防篡改（哈希链） | 靠「无改删接口 + 库权限」 | MVP 阶段成本收益不匹配；如需要可由数据库侧只授予 INSERT / SELECT 权限（部署约定） |
| 无独立权限点以外的控制 | 复用 `auditLogs.view` | 日志只读，一个查看点足够 |
| 不回填历史 | 迁移不带数据 | 历史操作无法重建（谁在何时改了什么已丢失），伪造不如留空 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储与 `IAuditLogger`（或使用记录型假实现断言参数）；时间用固定 `DateTimeOffset`。

- **`AuditChangeBuilder`**：只记变化项；前后相同不记录；敏感字段（`password` / `newPassword` / `passwordHash` / `token` / `secret`）被拦截；集合类快照文本；JSON 结构可被反序列化回数组；超长截断标注。
- **各域接入点**（按 §3.6 逐域至少一例）：成功路径断言 `RecordAsync` 被调用一次且参数正确（资源 / 动作 / 业务标识 / 摘要 / 变化字段）；`UpdateXxxSettlement`、`VoidXxx` 等状态类动作摘要含前后状态。
- **失败路径不写日志**：`40101`（重复编码）、`40103`（库存不足）、`40104`（已作废）、`40119`（角色重名）等场景断言 `RecordAsync` **未被调用**。
- **事务回滚**：`Commit` 抛异常 → `RollbackAsync` 被调用（日志随业务一起回滚）。
- **`GetAuditLogs`**：筛选传参组合（`keyword` / `resource` / `action` / `userId` / 时间范围）、分页映射、列表不加载 `Changes`（投影断言）。
- **`GetAuditLogById`**：`Changes` 反序列化为数组；`Changes` 为 `null` → 空数组；不存在 → `40400`；坏 JSON 不抛异常（返回空数组 + warning）。
- **范围表守卫**（集成测试）：遍历 §0.1 范围表中登记的「用例 → 动作」组合，断言对应 Handler 内存在 `IAuditLogger` 调用（可用反射 / 源码扫描或对每个用例的参数化测试覆盖）——新增写用例漏接日志即失败。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`AuditLogs.Summary` 列长 200 == 常量；`ResourceNo` 50 == 常量且 ≥ `Products.Code`(32) / `OrderNo`(20)；`keyword` 50 通过 / 51 拒绝。

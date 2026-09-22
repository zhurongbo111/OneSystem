# CONTEXT.md — 项目上下文摘要

> 本文件是项目结构的事实快照，用于替代"实现前全仓代码探索"（读取规则见 `AGENTS.md` §2.4）。
> 维护要求：项目结构（新增项目 / 模块 / 实体 / 功能 / 关键文件位置 / 命令 / 端口）发生变化时，须同步更新本文件并随当次变更一起提交。
> 定位：本文件是导航地图，不是事实源——与本文件不一致时以实际代码与 `specs/` 为准，并顺手修正本文件。
> 书写原则：只记**规律与位置**（哪类文件放在哪、怎么命名、去哪里看），**不逐一罗列可从目录枚举的清单**；需要具体清单时用目录列表获取（见 §2 / §3 / §6）。

## 1. 技术栈

栈、版本与各端库选型见 `AGENTS.md` §1 与两端规则的「技术栈」小节；本文件只记录**结构与位置**。

## 2. 后端结构（backend/）

```
backend/
├── App.sln / .editorconfig
├── src/
│   ├── App.Api/             # Program.cs；Authentication/、Authorization/（权限特性与过滤器）、Controllers/、Http/、Middleware/、Swagger/、appsettings*.json、nlog.config
│   ├── App.Core/            # DependencyInjection.cs（AddCore）；Abstractions/、Auth/、Entities/、Errors/、Exports/、Mediation/、Features/
│   └── App.Infrastructure/  # DependencyInjection.cs（AddInfrastructure）、AppDbContext.cs、Migrations/、Persistence/、Repositories/、Exports/
└── tests/App.Tests/         # 每 Handler 一个测试文件 + ApiIntegration / FieldValidationConsistency / TestSupport + 横切守卫（ApiPermissionMatrix：全部 Controller 动作权限标注守卫）
```

**命名规律**（据此定位，不逐一列举）：

- `App.Core/Abstractions/`：仓储（`I<实体>Repository`）、`IUnitOfWork`、中介（`IMediator` / `IRequest` / `IRequestHandler`）、`ICurrentUser`(Extensions)、`IClientInfo`、`IExcelExporter`、审计写入 `IAuditLogger` 与 `IAuditLogRepository` 等接口，以及跨用例**读模型**（命名 `<实体><用途>`，创建判据见后端规则 §4.3；`030` 组织人事读模型 `DepartmentTreeNode` / `PositionListItem` / `PositionPickItem` / `EmployeeListItem` / `EmployeeDetail` / `EmployeePickUserItem`，`031` 财务主数据读模型 `AccountTreeNode` / `TaxRateListItem`，`032` 发票读模型 `InvoicableOrderItem` / `InvoiceListItem`，`033` 总账读模型 `AccountBalanceItem` / `BalanceSheet` / `BalanceSheetItem` / `IncomeStatementItem`）；完整清单用目录列表获取。
- `App.Core/Entities/`：每实体一个 `<实体>.cs` + `<实体>FieldConstraints.cs`（字段约束常量）；枚举 `UserStatus` / `ProductStatus` / `PartnerType` / `PartnerStatus` / `OrderStatus` / `StockMovementType`（10 值）/ `StockTakeType`（020，期初建账 / 库存盘点）/ `SettlementType` / `SettlementMethod` / `SettlementOrderType` / `SettlementState`（023，结算推导态）/ `OrderFlowStatus`（024，订单流转状态：待收货 / 部分收货 / 已完成 / 已关闭 / 已作废）/ `AuditResource` / `AuditAction`（029，操作日志的资源类型 / 动作；实体 `AuditLog` 为**纯追加表**，无 `UpdatedAt` / `UpdatedBy`）/ `DepartmentStatus` / `PositionStatus` / `EmployeeStatus` / `Gender`（030，组织主数据与员工档案；实体 `Department`（`ParentId` 自引用树）/ `Position` / `Employee`）；`AccountCategory`（5 值）/ `AccountDirection` / `AccountStatus` / `TaxRateStatus`（031，财务主数据；实体 `Account`（`ParentId` 自引用树 + `IsPreset` 预置不可删）/ `TaxRate`）；`InvoiceType`（032，发票登记；实体 `Invoice` / `InvoiceItem`，关联单据类型复用 023 的 `SettlementOrderType`）；`PeriodStatus` / `VoucherSourceType` / `VoucherStatus`（033，总账；实体 `AccountingPeriod`（按年月唯一）/ `Voucher` / `VoucherEntry`（凭证主子表，金额快照到分录）/ `AccountMapping`（按 `Key` 唯一，指向 `031` 的 `Account`））。
- 其他 Core 类型：`Auth/`（`JwtOptions`、`PasswordHasher`、`TokenService`）、`Errors/`（`BusinessException`、`ErrorCode`、`OrderNoConflictException`）、`Mediation/Mediator`（分发前统一跑 Validator）、`Audit/`（029：`AuditEntry` / `AuditChangeBuilder`（只记变化 + 敏感字段黑名单 + 超长截断）/ `AuditSummary`（金额 / 数量 / 日期 / 集合格式化）/ `AuditText`（枚举中文文案））。
- `App.Infrastructure/Repositories/` 每实体一个 `<实体>Repository.cs`；`Persistence/Configurations/` 每实体一个 `<实体>Configuration.cs`；`Persistence/` 另有 `UnitOfWork`、`DatabaseInitializer`。
- `AppDbContext`：DbSet 与实体一一对应，单据明细表为 `<单据>Items` 独立 DbSet（八张）；**完整清单以 `AppDbContext` 为准**（用目录 / 文件查看获取）。
- **共享出参与映射**：各功能在 `Features/<Feature>/` 下放跨用例共享 DTO 与 `<Feature>DtoMapper`（正向映射，方法名 `To` + 目标 DTO 类型名），约定见 `rules/backend/RULE.mdc` §4.3。
- **当前用户与审计**：id 解析入口 `Abstractions/ICurrentUserExtensions.UserId()`；审计字段由 Handler 经 `ICurrentUser` 传入、仓储不感知当前用户（约定见 `rules/backend/RULE.mdc` §4.1）。
- **共享工具**：`App.Core/SequentialGuidGenerator.cs`（顺序 GUID 生成器，采购 / 销售单据共用，命名空间 `App.Core`）、`App.Core/SettlementStateCalculator.cs`（单据结算状态 / 未结金额推导，四类单据 DTO 映射共用）；**已核销单据禁止作废**由四类单据 `Void*` Handler 校验（`ErrorCode.OrderSettledCannotVoid = 40120`，判据见 `specs/023-erp-settlement/design.md` §0）；**收付款单不限制往来档案类型**（收款可对供应商收回退货退款），**往来对账应收 / 应付按四表未结金额（`TotalAmount − SettledAmount`）归集**、不引用收付款单类型，业务类型由 `SettlementType` + `SettlementItem.OrderType` 派生（同节）。
- **导出能力（erp-export）**：表格模型与守卫类型集中在 `App.Core/Exports/`（导出上限单一来源 `ExportFieldConstraints.MaxRows = 50000`），抽象 `Abstractions/IExcelExporter.cs`、实现 `App.Infrastructure/Exports/ClosedXmlExcelExporter.cs`（依赖 `ClosedXML`，注册于 `AddInfrastructure`）；各域导出用例为 `Features/<Feature>/Export<X>`，端点为各域 Controller 追加的 `GET .../export`（契约例外：成功返回二进制流，见 `specs/027-erp-export/design.md` §0.1）。
- **导出用批量查询**：各仓储提供 `Get*ByIdsAsync`（创建人显示名、单据明细商品编码等），按 id 集合一次取数、避免逐单 N+1。

**已实现的 Features**（`App.Core/Features/`，每用例一个 `<Action>/` 目录、四件套）：

完整清单用目录列表获取；命名规律为 Feature 用资源名（可数用复数，不可数 / 集合概念保留单数，如 `Products` / `Inventory`）、`<Action>` 用动词短语，用例既有形态照下方「基准参照」与同域已有 `<Action>/` 一比一组织。`Reports` 各用例为只读聚合（经 `IReportQueryRepository` 跨表，不新增写路径）。

**成本能力（erp-cost，`026`）**：成本写入见各单据 `Create*/Void*` Handler，重算入口 `Costs/RecalculateCosts`（并发拒绝 `40118`），报表 `Reports/GetCostProfitReport`；仓储方法、读模型与错误码明细见 `specs/026-erp-cost/design.md`。

**权限能力（erp-rbac，`028`）**（后端 + 前端已交付，e2e 待环境跑通）：

- 权限点常量 `App.Core/Auth/Permissions.cs`（`PermissionKeys` / `Permissions.All` / 分组元数据），**唯一事实源** `specs/028-erp-rbac/design.md` §0.2；内置角色常量 `App.Core/BuiltinRoles.cs`（`SuperAdmin` / `Staff`）。
- 实体 `Role` / `RolePermission` / `UserRole` + `Entities/RoleFieldConstraints.cs`；解析 `Abstractions/IPermissionResolver` → `App.Infrastructure/Auth/PermissionResolver`（SuperAdmin 全量、多角色并集、单请求缓存）。
- 强制校验 `App.Api/Authorization/`：`RequirePermissionAttribute` / `SkipPermissionCheckAttribute` / `PermissionAuthorizationFilter`——**默认拒绝**（未标注权限点的动作与无权限均返回 `40300`，HTTP 200；白名单见 `specs/028-erp-rbac/design.md` §0.4）。
- 用例：`Features/Roles/`（CRUD 5 个）、`Features/Permissions/GetPermissions`、`Features/Users/GetMyPermissions`；业务码 `40173`–`40175`，下一个可用 `40176`（见 `specs/ROADMAP.md` §6）。

**审计能力（erp-audit-log，`029`）**（后端 + 前端已交付）：

- 写入：各域写用例经 `IAuditLogger`（`Abstractions/` 接口 + `App.Infrastructure/Audit/AuditLogger.cs`）在「业务写之后、`CommitAsync` 之前」记一条 `AuditLog`（与业务同事务，失败一起回滚）；接入点清单唯一来源 `specs/029-erp-audit-log/design.md` §0.1，守卫测试 `tests/App.Tests/AuditLogScopeGuardTests.cs`（新增写用例漏接日志即失败）。
- 查询：`Features/AuditLogs/GetAuditLogs`（分页 + 关键词 / 资源 / 动作 / 操作人 / 时间筛选，列表投影**排除** `Changes` 大字段）与 `GetAuditLogById`（含字段级差异数组），端点 `AuditLogsController`，权限点 `auditLogs.view`；无改删接口（纯追加）。

**组织人事（erp-org-employee，`030`）**（后端 + 前端已交付）：

- 实体 `Department`（`ParentId` 自引用树，防环在 Handler 沿父链上溯）/ `Position` / `Employee`（`UserId` 可空、非空时唯一 → 绑定一个系统账号；**员工 ≠ 账号**）+ 3 个字段约束常量类；仓储 `IDepartmentRepository` / `IPositionRepository` / `IEmployeeRepository`（`GetEmployeeCountsAsync` 为树「在职人数」，`CountEmployeesAsync` 含离职、供删除保护）。
- 用例：`Features/Departments/`（树 + CRUD + 启停，6）、`Features/Positions/`（CRUD + 启停 + picks，7）、`Features/Employees/`（列表 / CRUD / 在职离职 / available-users / 导出，7）；端点 `DepartmentsController` / `PositionsController` / `EmployeesController`；权限点 `departments.*` / `positions.*` / `employees.*`；错误码 `40138`–`40148`（区间内下一个可用 `40149`，见 `specs/ROADMAP.md` §6）。
- 员工导出复用 `027`（`IExcelExporter`，列 = 列表列 + 创建人）；全部写用例已接入操作日志（`029` §0.1 续行）；前端域 `OrgManagement/`。

**财务主数据（erp-finance-master，`031`）**（后端 + 前端已交付）：

- 实体 `Account`（`ParentId` 自引用树 + `IsPreset` 预置科目不可删）/ `TaxRate`（`Rate` 为百分比数值，`numeric(9,4)`）+ 2 个字段约束常量类；仓储 `IAccountRepository`（`GetAllAsync` 取全量、`HasChildrenAsync` / `IsReferencedByVoucherAsync` 供删除保护，后者待 `033` 落地后生效）/ `ITaxRateRepository`。
- 用例：`Features/Accounts/`（树 + CRUD + 启停，6）、`Features/TaxRates/`（列表 + CRUD + 启停，6）；端点 `AccountsController` / `TaxRatesController`；权限点 `accounts.*` / `taxRates.*`；错误码 `40149`–`40153`（该区间内下一个可用 `40154`，见 `specs/ROADMAP.md` §6）；科目树防环复用 `030` 的 `40141`。
- 预置标准科目由 `DatabaseInitializer` 幂等种子写入（`IsPreset = true`，15 条，见 `specs/031-erp-finance-master/design.md` §2.4）；全部写用例已接入操作日志（`029` §0.1 续行）；前端域 `FinanceManagement/`。

**发票登记（erp-invoice，`032`）**（后端 + 前端已交付）：

- 实体 `Invoice`（发票号唯一 + 往来名称快照 + 税额 / 价税合计后端重算）/ `InvoiceItem`（单据号 / 日期 / 总额快照，`(OrderType, OrderId)` 复合索引服务已开票金额聚合）；关联单据类型复用 `SettlementOrderType`（语义已泛化为「可关联单据类型」）。
- 仓储 `IInvoiceRepository`（分页含关联单据号 EXISTS 检索与「关联单据」摘要拼接 / 详情 / 唯一性 / 新增 / 作废 / 批量取明细）与 `IInvoiceQueryRepository`（可开票候选 + 已开票金额聚合，跨四表只读）；**已开票金额按聚合推导、不落单据列**，作废即自动释放。
- 用例：`Features/Invoices/`（列表 + 登记 + 详情 + 作废 + 可开票候选 + 导出）；端点 `InvoicesController`（含 `GET /api/invoices/export` 文件流契约例外）；权限点 `invoices.view/create/void/export`；错误码 `40132`–`40135`（`40104` / `40108` / `40109` / `40110` / `40400` 复用）。
- 导出已登记 `027` §0.1 范围表（发票 + 关联明细两个工作表）；写用例已接入操作日志（`029` §0.1 续行）；前端域 `InvoiceManagement/`。

**总账（erp-general-ledger，`033`）**（后端 + 前端已交付）：

- 实体 `AccountingPeriod`（年月唯一 + 结账状态）/ `Voucher` / `VoucherEntry`（分录快照科目编码 / 名称）/ `AccountMapping`（8 个业务科目映射键 → `031` 的 `Account`） + 4 个字段约束常量类。
- 仓储 `IVoucherRepository`（分页 / 详情 / 主子表同事务新增 / 作废 / 按来源作废 / 单号生成）/ `IAccountingPeriodRepository`（含按年月幂等取当期）/ `IAccountMappingRepository` 与只读 `IFinancialReportQueryRepository`（三大报表跨 `Vouchers` / `VoucherEntries` / `Accounts` 取数）；`031` 的 `IsReferencedByVoucherAsync` 自本域落地后生效。
- 用例：`Features/Vouchers/`、`Features/AccountingPeriods/`、`Features/AccountMappings/`、`Features/FinancialReports/`（共享静态 `App.Core/Finance/VoucherFactory` 构建自动凭证、`VoucherWriter` 负责写入 / 作废）；端点 `VouchersController` / `AccountingPeriodsController` / `AccountMappingsController` / `FinancialReportsController`；权限点 `vouchers.view/create/void/close/updateMapping`、`financialReports.view`；错误码 `40154`–`40163`（区间内下一个可用 `40164`，见 `specs/ROADMAP.md` §6）。
- 横向改造：`015` / `016` / `021` / `022` / `023` 的创建用例同事务生成自动凭证、作废用例同事务作废（期间已结账或映射缺失则整单失败回滚）；勾稽关系见 `specs/033-erp-general-ledger/design.md` §2.4。
- 期间与映射由 `DatabaseInitializer` 幂等种子（当年 12 个月 + 8 个映射键）；写用例已接入操作日志（`029` §0.1 续行）；前端域 `GeneralLedgerManagement/`。

**基准参照**：

- 后端用例脚手架：`Features/Auth/Login`（四件套）、`Features/Users/GetCurrentUser`（无参用例形态）；分发与全局校验见 `Core/Mediation/Mediator.cs`。
- 字段约束单一来源：`Entities/UserFieldConstraints.cs` + `Configurations/UserConfiguration.cs` + 各 Validator，一致性由 `tests/App.Tests/FieldValidationConsistencyTests.cs` 守护。

**改动面 → 必读 / 必改文件**（按改动面选读，**不预先全读**；读取规则见后端规则 §3.1）：

| 改动面 | 文件 |
|---|---|
| 改字段 | `Entities/<实体>.cs` + `Entities/<实体>FieldConstraints.cs` |
| 改列 / 索引 | `Persistence/Configurations/<实体>Configuration.cs` |
| 新增查询 | `Abstractions/I<实体>Repository.cs` + `Repositories/<实体>Repository.cs`（有联查字段时加 `Abstractions/` 下对应读模型） |
| 组织新用例 | 同 `Feature` 目录下已有的 `<Action>/`（一比一照结构组织） |
| 新增用例 / 仓储 | `Core/DependencyInjection.cs`（新增仓储再加 `Infrastructure/DependencyInjection.cs`） |
| 改字段约束 | 追加 `tests/App.Tests/FieldValidationConsistencyTests.cs`（或同域 `<实体>FieldConsistencyTests.cs`） |
| 测试支撑 | `tests/App.Tests/TestSupport.cs` + 同域既有测试 + 需假实现时的 `*TestDoubles.cs` |

## 3. 前端结构（frontend/）

```
frontend/
├── index.html / vite.config.ts / playwright.config.ts / eslint.config.js
├── e2e/        # 每功能域一个或多个 <域名>.spec.ts（kebab-case 功能短名，命名判据见前端规则 §10）+ helpers/（菜单点击 / 表格搜索 / 重试点击 / 登录 loginAs / 消息断言 expectMessage，判据见前端规则 §10.1）+ global-setup.ts（冷启动预热，见前端规则 §10）；权限用例见 `rbac.spec.ts`（最小权限角色经接口构造 fixture）
└── src/
    ├── main.ts / App.vue / env.d.ts
    ├── api/         # request.ts（统一解包 / 40100 处置 / downloadBlob 文件下载与契约例外分流；`40300` 与 `40000` 同处置：统一 `Message.error`）+ 按业务域拆分 <entity>.ts（`030` 起组织人事拆 department.ts / position.ts / employee.ts，`031` 财务主数据拆 account.ts / taxRate.ts，`032` 发票拆 invoice.ts）+ voucher.ts / financialReport.ts（033，凭证 / 期间 / 科目映射 / 三大报表）+ export.ts（12 个列表导出）+ role.ts（角色 CRUD 与权限点分组清单，中文名由后端返回）+ auditLog.ts（操作日志查询 + 资源 / 动作文案与着色常量，`029`）
    ├── components/  # AppLayout.vue（侧边菜单多顶级分组：示例页面 / 基础档案 / 采购 / 销售 / 库存 / 资金（含发票登记）/ 财务 / 报表 / 系统；子菜单默认折叠、仅当前分组自动展开；菜单项按 `MENU_PERMISSIONS` 权限过滤，分组内无可见子项则整组隐藏，`028`）、SettlementRecords.vue（四类单据详情「收付款明细」只读反查，`023`）
    ├── composables/ # useOrderStore.ts（演示用）
    ├── router/ stores/ utils/   # index.ts（路由懒加载；`ROUTE_PERMISSIONS` 集中登记「路由名 → 权限点」并注入 `meta.permission`，守卫未登录跳登录页、无权限跳 `/403`；另含 6 条顶层 `print/...` 打印路由与顶层 `/403`，均不进 AppLayout）/ auth.ts（Pinia：token / user / permissions + `hasPermission` / `hasAnyPermission` / `fetchPermissions`）/ datetime.ts / settlement.ts（结算状态文案与颜色）
    └── views/       # 按功能域分目录（域内文件平铺，不套子目录）；无功能域归属的 `ForbiddenView.vue`（顶层 `/403`）平铺在 views/ 根
```

**功能域目录**（`views/`，与后端 `Features/<Feature>`、路由前缀、e2e spec 四者对齐，约定见前端规则 §4.1）：

- 规律：一个域一个 `<Domain>/`，域内文件平铺不套子目录；列表页用复数域名，表单 / 详情页用单数实体名（如 `PurchasesView` / `PurchaseFormPage` / `PurchaseDetailView`）；完整清单用目录列表获取。
- 无功能域归属的独立页平铺在 `views/` 根：`LoginView.vue` / `HomeView.vue`（菜单归属见 `components/AppLayout.vue`）。
- 接口文件按业务域命名 `api/<entity>.ts`（归属判定见前端规则 §3）；各单据打印视图统一为顶层 `print/<资源路径>/:id` 路由、不进 `AppLayout`（共 6 条，清单见上方目录树 `router/` 注释）。
- 目录枚举不出来的关联：
  - `Showcase/` 为示例页集合，其 `OrderFormDrawer.vue` 为域内共享表单；
  - `InventoryManagement/` 与 `StockMovementManagement/`（库存流水）均为只读页，前者操作列「流水」下钻后者；
  - `PurchaseManagement/`（采购入库）与 `SalesManagement/`（销售出库）开单页可关联上游订单，见 `specs/024-erp-order-flow/design.md`；
  - `SettlementManagement/` 含往来对账页；`ReportManagement/` 含成本毛利报表（库存余额表与库存流水页也含成本列）；报表 API 集中 `api/report.ts`，成本重算在 `api/cost.ts`。
  - `RoleManagement/` 为角色权限域（`028`）：列表 + 抽屉表单（权限树勾选），无独立详情页；权限点中文名由 `GET /api/permissions` 返回，前端不硬编码；无权限页 `ForbiddenView.vue` 不在任何功能域内。
  - `AuditLogManagement/` 为操作日志域（`029`）：只读列表 + 详情抽屉（字段级差异表），无新建 / 编辑 / 删除入口（日志不可改）。
  - `OrgManagement/` 为组织人事域（`030`）：部门树（树形表格 + 抽屉表单）/ 岗位列表 / 员工列表三页平铺在同一域目录；员工抽屉内可选部门（`a-tree-select`，仅启用）/ 岗位（`getPositionPicks`）/ 关联账号（`getAvailableUsers`）。
  - `FinanceManagement/` 为财务主数据域（`031`）：会计科目（树形表格，名称列含「预置」标记 + 抽屉表单）/ 税率列表（抽屉表单）两页平铺在同一域目录。
  - `InvoiceManagement/` 为发票域（`032`）：列表 / 新建（独立页，含可开票单据子表格 + 金额一致性提示）/ 详情（含关联单据只读表 + 作废）三页平铺在同一域目录。
  - `GeneralLedgerManagement/` 为总账域（`033`）：凭证列表（工具条「手工凭证 / 科目映射 / 期间管理」抽屉入口）/ 手工凭证页（独立页，分录子表 + 借贷差额提示）/ 凭证详情（主表 + 分录表）/ 财务报表（三 tab：科目余额表 / 资产负债表 / 利润表）平铺在同一域目录；期间结账与科目映射均以抽屉承载。

**图标选型**：业务图标（侧边菜单、列表工具条、操作列）统一 Tabler（`@tabler/icons-vue`）；仅「图标」示例页为演示保留三套并存；优先级见前端规则 §4.7。

**基准参照**：

- 列表页标准实现 `views/Showcase/ListShowcaseView.vue`（正文见 `specs/006-list-showcase/design.md` §0），新增列表页复制其结构再替换业务字段。
- 表单 / 详情参照 `views/Showcase/` 的 `FormShowcaseView.vue`、`OrderFormDrawer.vue`、`FormPageFormView.vue`、`FormDetailView.vue`；接口层写法读 `api/request.ts` + 本次要用的 `api/<entity>.ts`；仅新增页面 / 菜单项时读 `router/index.ts`、`components/AppLayout.vue`。
- 页面命名 / 目录归属见前端规则 §4.1；各交互约定（列表页 / 操作列 / 按钮 loading / 组合式分区 / 图标 / 表单详情）的规格 §0 正文位置见前端规则 §2.1 第 3 项。

## 4. 常用命令（Windows PowerShell）

| 动作 | 命令 |
|---|---|
| 后端启动（dev，端口 5080） | `cd backend; dotnet run --project src/App.Api`（dev 连接串取自 `appsettings.Development.json`） |
| 后端单测 | `cd backend; dotnet test` |
| 前端启动（dev，端口 5173） | `cd frontend; npm run dev` |
| 前端 e2e（先起后端 5080 + 前端 5173） | `cd frontend; npm run test:e2e` |
| 前端 e2e 一键（每轮独立库，跑完自动删库） | `cd frontend; npm run e2e:run`（脚本编排见 `specs/002-frontend-e2e/design.md` §6） |
| 前端类型检查 | `cd frontend; npm run type-check` |
| 前端 lint | `cd frontend; npm run lint` |
| 前端构建 | `cd frontend; npm run build` |

> 端口与「先起前后端再跑 e2e」的约定以本表为准，其他文件只写指针（`AGENTS.md` §2.4）。

## 5. 环境

| 项 | 值 |
|---|---|
| 数据库 | PostgreSQL，localhost:5432，库 `app`；dev 连接串明文存于 `appsettings.Development.json`（仅本地开发库，例外见 `AGENTS.md` §7） |
| e2e 数据库 | 每轮 `app_e2e_<时间戳>`：由 `npm run e2e:run` 自动创建与删除，连接串由脚本从开发连接串替换 `Database` 段得到（见 `specs/002-frontend-e2e/design.md` §6） |
| 前端 dev API | `VITE_API_BASE_URL=/api`，Vite proxy 转发 `/api` → `http://localhost:5080` |
| 敏感配置 | prod 只从环境变量读取：数据库连接串 `ConnectionStrings__Default`、JWT 密钥 `JWT__SECRET`（dev 缺失时用随机兜底密钥）、OTel 端点；dev 允许连接串明文存配置文件；代码内一律禁止硬编码 |

## 6. 现有功能规格（specs/）

`specs/` 下目录名为 `<三位序号>-<功能名>`，序号 = **既定实现顺序**（规则见 `AGENTS.md` §2.1 / §2.5），**按名称排序即实现顺序**；**完整清单用目录列表获取**，功能名指代不含序号。范围规律：工程与前端交互模式为 `001`–`011`、`018`，业务为 `009`，ERP 为 `012`–`033`（已实现）与 `034`–`045`（已起草未实现，**无待起草模块**）。`028` 为**横向改造**（角色域 + 为 `012`–`027` 全部动作补权限点），权限点清单唯一来源 `specs/028-erp-rbac/design.md` §0.2、白名单 §0.4；各域 `design.md` 均带「演进（erp-rbac）」指针注记。`029` 为**横向能力**（为 `012`–`028` 各写路径追加操作日志 + 2 个只读查询接口 + 只读日志页），覆盖范围表唯一来源 `specs/029-erp-audit-log/design.md` §0.1；受影响各域 `design.md` 带「演进（erp-audit-log）」指针注记。

`specs/ROADMAP.md` 是 ERP **全域**（内核 + 外围系统）的**路线索引**（单文件，非 spec 目录、无三件套）：记录模块边界（§1）、模块地图（§2）、覆盖矩阵（§3）、阶段路线 P1–P6（§4.2），并写明跨功能前置决策（多仓 / 结算 / 权限 / 组织等）。接续 ERP 功能前先读它，再进具体规格。

**e2e 运行**（判据见 `specs/002-frontend-e2e/design.md` §6）：一律 `npm run e2e:run`（每轮独立临时库，跑完自动删库）；迭代单文件用 `npm run e2e:run -- -Spec e2e/<功能>.spec.ts`、按标题过滤用 `-Grep <关键字>`（均仍走临时库）。**禁止**手工起后端 / 前端连开发库跑用例——用例数据会落进开发库，清理只能重建开发库。

**交互约定「改动类型 → 规格 §0 正文」对照表**（前端规则 §4.5 / §4.6 / §4.7 / §5 / §5.2 / §5.5 只留判据，正文在下列 §0；**新增交互约定只更新本表，不改规则**）：

| 改动类型 | 正文位置 |
|---|---|
| 列表页 | `specs/006-list-showcase/design.md` §0 |
| 操作列 | `specs/011-action-column/design.md` §0 |
| 按钮 loading | `specs/010-button-loading/design.md` §0 |
| 组合式分区 | `specs/008-composable-style/design.md` §0 |
| 图标 | `specs/018-icon-showcase/design.md` §0 |
| 表单 / 详情 | `specs/007-form-detail-showcase/design.md` §0 |

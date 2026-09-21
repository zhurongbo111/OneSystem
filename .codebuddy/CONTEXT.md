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
│   ├── App.Api/             # Program.cs；Authentication/、Controllers/、Http/、Middleware/、Swagger/、appsettings*.json、nlog.config
│   ├── App.Core/            # DependencyInjection.cs（AddCore）；Abstractions/、Auth/、Entities/、Errors/、Exports/、Mediation/、Features/
│   └── App.Infrastructure/  # DependencyInjection.cs（AddInfrastructure）、AppDbContext.cs、Migrations/、Persistence/、Repositories/、Exports/
└── tests/App.Tests/         # 每 Handler 一个测试文件 + ApiIntegration / FieldValidationConsistency / TestSupport
```

**命名规律**（据此定位，不逐一列举）：

- `App.Core/Abstractions/`：仓储（`I<实体>Repository`）、`IUnitOfWork`、中介（`IMediator` / `IRequest` / `IRequestHandler`）、`ICurrentUser`(Extensions)、`IClientInfo`、`IExcelExporter` 等接口，以及跨用例**读模型**（命名 `<实体><用途>`，创建判据见后端规则 §4.3）；完整清单用目录列表获取。
- `App.Core/Entities/`：每实体一个 `<实体>.cs` + `<实体>FieldConstraints.cs`（字段约束常量）；枚举 `UserStatus` / `ProductStatus` / `PartnerType` / `PartnerStatus` / `OrderStatus` / `StockMovementType`（10 值）/ `StockTakeType`（020，期初建账 / 库存盘点）/ `SettlementType` / `SettlementMethod` / `SettlementOrderType` / `SettlementState`（023，结算推导态）/ `OrderFlowStatus`（024，订单流转状态：待收货 / 部分收货 / 已完成 / 已关闭 / 已作废）。
- 其他 Core 类型：`Auth/`（`JwtOptions`、`PasswordHasher`、`TokenService`）、`Errors/`（`BusinessException`、`ErrorCode`、`OrderNoConflictException`）、`Mediation/Mediator`（分发前统一跑 Validator）。
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
├── e2e/        # 每功能域一个或多个 <域名>.spec.ts（kebab-case 功能短名，命名判据见前端规则 §10）+ helpers/（菜单点击 / 表格搜索 / 重试点击 / 登录 loginAs / 消息断言 expectMessage，判据见前端规则 §10.1）+ global-setup.ts（冷启动预热，见前端规则 §10）
└── src/
    ├── main.ts / App.vue / env.d.ts
    ├── api/         # request.ts（统一解包 / 40100 处置 / downloadBlob 文件下载与契约例外分流）+ 按业务域拆分 <entity>.ts + export.ts（10 个列表导出）
    ├── components/  # AppLayout.vue（侧边菜单：「示例页面」「进销存」两组，子菜单默认折叠、仅当前分组自动展开；`025` 落地后改为多顶级分组，目标结构见 `specs/025-erp-report/design.md` §0.2）、SettlementRecords.vue（四类单据详情「收付款明细」只读反查，`023`）
    ├── composables/ # useOrderStore.ts（演示用）
    ├── router/ stores/ utils/   # index.ts（路由懒加载；另含 6 条顶层 `print/...` 打印路由，不进 AppLayout）/ auth.ts（Pinia）/ datetime.ts / settlement.ts（结算状态文案与颜色）
    └── views/       # 按功能域分目录（域内文件平铺，不套子目录）
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

`specs/` 下目录名为 `<三位序号>-<功能名>`，序号 = **既定实现顺序**（规则见 `AGENTS.md` §2.1 / §2.5），**按名称排序即实现顺序**；**完整清单用目录列表获取**，功能名指代不含序号。范围规律：工程与前端交互模式为 `001`–`011`、`018`，业务为 `009`，ERP 为 `012`–`027`（已实现）与 `028`–`045`（已起草未实现，**无待起草模块**）。

`specs/ROADMAP.md` 是 ERP **全域**（内核 + 外围系统）的**路线索引**（单文件，非 spec 目录、无三件套）：记录模块边界（§1）、模块地图（§2）、覆盖矩阵（§3）、阶段路线 P1–P6（§4.2），并写明跨功能前置决策（多仓 / 结算 / 权限 / 组织等）。接续 ERP 功能前先读它，再进具体规格。

**交互约定「改动类型 → 规格 §0 正文」对照表**（前端规则 §4.5 / §4.6 / §4.7 / §5 / §5.2 / §5.5 只留判据，正文在下列 §0；**新增交互约定只更新本表，不改规则**）：

| 改动类型 | 正文位置 |
|---|---|
| 列表页 | `specs/006-list-showcase/design.md` §0 |
| 操作列 | `specs/011-action-column/design.md` §0 |
| 按钮 loading | `specs/010-button-loading/design.md` §0 |
| 组合式分区 | `specs/008-composable-style/design.md` §0 |
| 图标 | `specs/018-icon-showcase/design.md` §0 |
| 表单 / 详情 | `specs/007-form-detail-showcase/design.md` §0 |

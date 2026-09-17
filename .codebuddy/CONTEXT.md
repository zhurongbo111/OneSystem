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
│   ├── App.Core/            # DependencyInjection.cs（AddCore）；Abstractions/、Auth/、Entities/、Errors/、Mediation/、Features/
│   └── App.Infrastructure/  # DependencyInjection.cs（AddInfrastructure）、AppDbContext.cs、Migrations/、Persistence/、Repositories/
└── tests/App.Tests/         # 每 Handler 一个测试文件 + ApiIntegration / FieldValidationConsistency / TestSupport
```

**命名规律**（据此定位，不逐一列举）：

- `App.Core/Abstractions/`：`I<实体>Repository`、`IUnitOfWork`、`IMediator` / `IRequest` / `IRequestHandler`、`ICurrentUser`(Extensions)、`IClientInfo`，以及跨用例读模型（`ProductListItem` / `ProductDetail` / `ProductPickItem` / `InventoryItem` / `StockMovementItem`）。
- `App.Core/Entities/`：每实体一个 `<实体>.cs` + `<实体>FieldConstraints.cs`（字段约束常量）；枚举 `UserStatus` / `ProductStatus` / `PartnerType` / `PartnerStatus` / `OrderStatus` / `OrderSettlementStatus` / `StockMovementType`（8 值，取值 9–10 由 022 追加）/ `StockTakeType`（020，期初建账 / 库存盘点）。
- 其他 Core 类型：`Auth/`（`JwtOptions`、`PasswordHasher`、`TokenService`）、`Errors/`（`BusinessException`、`ErrorCode`、`OrderNoConflictException`）、`Mediation/Mediator`（分发前统一跑 Validator）。
- `App.Infrastructure/Repositories/` 每实体一个 `<实体>Repository.cs`；`Persistence/Configurations/` 每实体一个 `<实体>Configuration.cs`；`Persistence/` 另有 `UnitOfWork`、`DatabaseInitializer`。
- `AppDbContext` 含 15 个 DbSet（与实体一一对应，另有 PurchaseOrderItems / SalesOrderItems / StockTakeItems / PurchaseReturnItems 四张明细表）。
- **共享出参与映射**：各功能在 `Features/<Feature>/` 下放跨用例共享 DTO 与 `<Feature>DtoMapper`（正向映射，方法名 `To` + 目标 DTO 类型名），约定见 `rules/backend/RULE.mdc` §4.3。
- **当前用户与审计**：id 解析入口 `Abstractions/ICurrentUserExtensions.UserId()`；审计字段由 Handler 经 `ICurrentUser` 传入、仓储不感知当前用户（约定见 `rules/backend/RULE.mdc` §4.1）。
- **共享工具**：`App.Core/SequentialGuidGenerator.cs`（顺序 GUID 生成器，采购 / 销售单据共用，命名空间 `App.Core`）。

**已实现的 Features**（`App.Core/Features/`，每用例四件套）：

| Feature | 用例 |
|---|---|
| Auth | Login |
| LoginLogs | GetLoginLogs |
| Users | CreateUser、GetUsers、GetUserById、GetCurrentUser（空 Request 无 Validator）、UpdateUser、UpdateUserStatus、ResetPassword |
| Products | GetProducts、GetProductById、CreateProduct、UpdateProduct、UpdateProductStatus、GetProductPickList |
| Categories | GetCategories、GetCategoriesPaged、CreateCategory、UpdateCategory、DeleteCategory |
| Partners | GetPartners、CreatePartner、GetPartnerById、UpdatePartner、UpdatePartnerStatus |
| Inventory | GetInventory |
| StockMovements | GetStockMovements |
| Purchases | CreatePurchaseOrder、GetPurchaseOrders、GetPurchaseOrderById、VoidPurchaseOrder、UpdatePurchaseOrderSettlement |
| PurchaseReturns | CreatePurchaseReturn、GetPurchaseReturns、GetPurchaseReturnById、VoidPurchaseReturn、UpdatePurchaseReturnSettlement |
| Sales | CreateSalesOrder、GetSalesOrders、GetSalesOrderById、VoidSalesOrder、UpdateSalesOrderSettlement |
| StockTakes | CreateStockTake、GetStockTakes、GetStockTakeById、GetStockTakePickProducts |

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
├── e2e/        # 每功能域一个 <域名>.spec.ts（kebab-case）+ helpers/（如 clickMenuItem：点子菜单项时先展开所属分组）
└── src/
    ├── main.ts / App.vue / env.d.ts
    ├── api/         # request.ts（统一解包 / 40100 处置）+ 按业务域拆分 <entity>.ts
    ├── components/  # AppLayout.vue（侧边菜单：「示例页面」「进销存」两组，子菜单默认折叠、仅当前分组自动展开）
    ├── composables/ # useOrderStore.ts（演示用）
    ├── router/ stores/ utils/   # index.ts（路由懒加载）/ auth.ts（Pinia）/ datetime.ts
    └── views/       # 按功能域分目录（域内文件平铺，不套子目录）
```

**功能域目录**（`views/`，与后端 `Features/<Feature>`、路由前缀、e2e spec 四者对齐，约定见前端规则 §4.1）：

- `Showcase/` — 示例页：ComponentShowcaseView / ListShowcaseView / FormShowcaseView / FormPageFormView / FormDetailView + 共享 `OrderFormDrawer.vue`
- `UserManagement/` — UsersView + UserDetailView + UserFormDrawer
- `LoginLogManagement/` — LoginLogsView
- `ProductManagement/` — ProductsView + ProductFormDrawer
- `CategoryManagement/` — CategoriesView + CategoryFormDrawer
- `PartnerManagement/` — PartnersView + PartnerFormDrawer
- `InventoryManagement/` — InventoryView（只读；操作列含「流水」下钻到 StockMovementManagement）
- `StockMovementManagement/` — StockMovementsView（只读；API 在 `api/stockMovement.ts`）
- `PurchaseManagement/` — PurchasesView + PurchaseFormPage + PurchaseDetailView
- `PurchaseReturnManagement/` — PurchaseReturnsView + PurchaseReturnFormPage + PurchaseReturnDetailView（API 在 `api/purchaseReturn.ts`）
- `SalesManagement/` — SalesView + SaleFormPage + SaleDetailView
- `StockTakeManagement/` — StockTakesView + StockTakeFormPage + StockTakeDetailView（API 在 `api/stockTake.ts`）
- 无功能域归属的独立页平铺在 `views/` 根：`LoginView.vue` / `HomeView.vue`（菜单归属见 `components/AppLayout.vue`）。

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
| 前端类型检查 | `cd frontend; npm run type-check` |
| 前端 lint | `cd frontend; npm run lint` |
| 前端构建 | `cd frontend; npm run build` |

> 端口与「先起前后端再跑 e2e」的约定以本表为准，其他文件只写指针（`AGENTS.md` §2.4）。

## 5. 环境

| 项 | 值 |
|---|---|
| 数据库 | PostgreSQL，localhost:5432，库 `app`；dev 连接串明文存于 `appsettings.Development.json`（仅本地开发库，例外见 `AGENTS.md` §7） |
| 前端 dev API | `VITE_API_BASE_URL=/api`，Vite proxy 转发 `/api` → `http://localhost:5080` |
| 敏感配置 | prod 只从环境变量读取：数据库连接串 `ConnectionStrings__Default`、JWT 密钥 `JWT__SECRET`（dev 缺失时用随机兜底密钥）、OTel 端点；dev 允许连接串明文存配置文件；代码内一律禁止硬编码 |

## 6. 现有功能规格（specs/）

`specs/` 下目录名为 `<三位序号>-<功能名>`，序号 = 创建顺序（规则见 `AGENTS.md` §2.1 / §2.5），**按名称排序即创建时间正序**；**完整清单用目录列表获取**，功能名指代不含序号。分类如下：

- 工程 / 脚手架：`001-project-scaffold`、`003-api-swagger`
- 前端交互模式：`002-frontend-e2e`、`004-frontend-component-showcase`、`005-app-layout`、`006-list-showcase`、`007-form-detail-showcase`、`008-composable-style`、`010-button-loading`、`011-action-column`、`018-icon-showcase`
- 业务：`009-user-management`
- ERP（均已实现）：`012-erp-product`、`013-erp-partner`、`014-erp-inventory-query`、`015-erp-purchase`、`016-erp-sale`、`017-erp-category`、`019-erp-stock-movement`、`020-erp-stock-take`、`021-erp-purchase-return`
- ERP 扩展路线（规划中，**未实现**）：批次二 `022-erp-sale-return`、`023-erp-settlement`、`024-erp-order-flow`（均已起草规格；`024` 含待用户确认的命名决策）；批次三及以后待起草，见 `specs/ROADMAP.md`

`specs/ROADMAP.md` 是 ERP 功能组的**路线索引**（单文件，非 spec 目录、无三件套）：记录批次、序号、依赖与状态，并写明跨功能前置决策（多仓 / 结算 / 权限等）。接续 ERP 功能前先读它，再进具体规格。

**交互约定「改动类型 → 规格 §0 正文」对照表**（前端规则 §4.5 / §4.6 / §4.7 / §5 / §5.2 / §5.5 只留判据，正文在下列 §0；**新增交互约定只更新本表，不改规则**）：

| 改动类型 | 正文位置 |
|---|---|
| 列表页 | `specs/006-list-showcase/design.md` §0 |
| 操作列 | `specs/011-action-column/design.md` §0 |
| 按钮 loading | `specs/010-button-loading/design.md` §0 |
| 组合式分区 | `specs/008-composable-style/design.md` §0 |
| 图标 | `specs/018-icon-showcase/design.md` §0 |
| 表单 / 详情 | `specs/007-form-detail-showcase/design.md` §0 |

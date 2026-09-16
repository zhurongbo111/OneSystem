# CONTEXT.md — 项目上下文摘要

> 本文件是项目结构的事实快照，用于替代"实现前全仓代码探索"（读取规则见 `AGENTS.md` §2.4）。
> 维护要求：项目结构（新增项目 / 模块 / 实体 / 功能 / 关键文件位置 / 命令 / 端口）发生变化时，须同步更新本文件并随当次变更一起提交。
> 定位：本文件是导航地图，不是事实源——与本文件不一致时以实际代码与 `specs/` 为准，并顺手修正本文件。

## 1. 技术栈速览

| 端 | 技术 |
|---|---|
| 前端 | Vue 3 + TypeScript（`<script setup>` 组合式 API）+ Arco Design Vue + Vite + Pinia + vue-router + Axios |
| 后端 | .NET 8 + ASP.NET Core + EF Core（Npgsql）+ PostgreSQL + FluentValidation + NLog + OpenTelemetry |
| 测试 | 后端 xUnit（`backend/tests/App.Tests`）；前端 Playwright e2e（`frontend/e2e/`，直连 dev 后端、不打 mock） |

## 2. 后端结构（backend/）

```
backend/
├── App.sln
├── src/
│   ├── App.Api/          # 入口与 Web 层
│   │   ├── Program.cs
│   │   ├── Authentication/   # CurrentUserAccessor、JwtBearerExtensions
│   │   ├── Controllers/      # Auth / Health / LoginLogs / Users / Products / Categories / Partners / Inventory / PurchaseOrders / SalesOrders
│   │   ├── Http/             # ClientInfoAccessor
│   │   ├── Middleware/       # GlobalExceptionMiddleware
│   │   ├── Swagger/          # SwaggerSecurityOperationFilter
│   │   └── appsettings*.json / nlog.config
│   ├── App.Core/         # 业务核心（禁止反向依赖）
│   │   ├── DependencyInjection.cs     # AddCore：注册 Mediator、Handler、Validator
│   │   ├── Abstractions/              # IMediator、IRequest、IRequestHandler、IUnitOfWork、ICurrentUser、ICurrentUserExtensions、IClientInfo、IUserRepository、IUserLoginLogRepository、ICategoryRepository、IProductRepository、IInventoryRepository、IPartnerRepository、IPurchaseOrderRepository、ISalesOrderRepository、ProductListItem、ProductDetail、ProductPickItem、InventoryItem
│   │   ├── Auth/                      # JwtOptions、PasswordHasher、TokenService
│   │   ├── Entities/                  # User、UserLoginLog、UserStatus、UserFieldConstraints、Product、Category、Inventory、ProductStatus、ProductFieldConstraints、CategoryFieldConstraints、Partner、PartnerType、PartnerStatus、PartnerFieldConstraints、PurchaseOrder、PurchaseOrderItem、SalesOrder、SalesOrderItem、OrderStatus、OrderSettlementStatus、OrderFieldConstraints
│   │   ├── Errors/                    # BusinessException、ErrorCode、OrderNoConflictException
│   │   ├── Mediation/                 # Mediator（自研简化 MediatR，Send 前统一跑 Validator）
│   │   └── Features/                  # 每用例四件套：Request / RequestValidator / RequestHandler / Response
│   └── App.Infrastructure/
│       ├── DependencyInjection.cs     # AddInfrastructure：IUnitOfWork、仓储实现
│       ├── AppDbContext.cs            # 含 Categories / Products / Inventory / Partners / PurchaseOrders / PurchaseOrderItems / SalesOrders / SalesOrderItems 八个 DbSet
│       ├── Migrations/
│       ├── Persistence/               # UnitOfWork、DatabaseInitializer、Configurations/
│       └── Repositories/              # UserRepository、UserLoginLogRepository、CategoryRepository、ProductRepository、InventoryRepository、PartnerRepository、PurchaseOrderRepository、SalesOrderRepository
└── tests/App.Tests/      # 每 Handler 一个测试文件 + ApiIntegration / FieldValidationConsistency / TestSupport 等
```

**已实现的 Features**（`App.Core/Features/`）：

| Feature | 用例 |
|---|---|
| Auth | Login |
| LoginLogs | GetLoginLogs |
| Users | CreateUser、GetUsers、GetUserById、GetCurrentUser（空 Request 无 Validator）、UpdateUser、UpdateUserStatus、ResetPassword |
| Products | GetProducts、GetProductById、CreateProduct、UpdateProduct、UpdateProductStatus、GetProductPickList |
| Categories | GetCategories、GetCategoriesPaged、CreateCategory、UpdateCategory、DeleteCategory |
| Partners | GetPartners、CreatePartner、GetPartnerById、UpdatePartner、UpdatePartnerStatus |
| Inventory | GetInventory |
| Purchases | CreatePurchaseOrder、GetPurchaseOrders、GetPurchaseOrderById、VoidPurchaseOrder、UpdatePurchaseOrderSettlement |
| Sales | CreateSalesOrder、GetSalesOrders、GetSalesOrderById、VoidSalesOrder、UpdateSalesOrderSettlement |

共享出参：`Features/Users/UserDto`、`UserListItemDto`、`UserDetailDto`、`UserDtoMapper`、`UserInputNormalizer`；`Features/LoginLogs/LoginLogListItemDto`、`LoginLogDtoMapper`；`Features/Products/ProductDto`、`ProductPickDto`、`ProductDtoMapper`；`Features/Categories/CategoryDto`、`CategoryDtoMapper`；`Features/Partners/PartnerDto`、`PartnerDtoMapper`；`Features/Inventory/InventoryItemDto`、`InventoryDtoMapper`；`Features/Purchases/PurchaseOrderDto`、`PurchaseDtoMapper`；`Features/Sales/SalesOrderDto`、`SalesDtoMapper`。

**当前用户与审计字段**：id 解析统一用 `Abstractions/ICurrentUserExtensions.UserId()` 扩展方法（claims 缺失 / 非法返回 `null`），禁止内联 `Guid.TryParse` 或私有解析方法。审计字段（CreatedBy / UpdatedBy）一律由 Handler 经 `ICurrentUser` 获取后随实体 / `operatorId` 参数传入，仓储不注入 `ICurrentUser`、不感知当前用户。

**DTO 映射**：每个功能在 `Features/<Feature>/` 下放 `internal static class <Feature>DtoMapper` 集中正向映射（实体 / 读模型 → 出参 DTO），方法名 `To` + 目标 DTO 类型名（如 `UserDtoMapper.ToUserDetailDto`、`ProductDtoMapper.ToProductDto`）；Handler 内禁止内联 `new XxxDto` / 私有 `ToDto` 重复拼装；反向（入参 Request → 实体）当前为各用例 Handler 内联拼装，若抽取工厂方法须用 `From` + 源类型名区分方向。约定见 `rules/backend/RULE.mdc` §4.3。

**共享工具**：`App.Core/SequentialGuidGenerator.cs`（顺序 GUID 生成器，采购 / 销售单据共用，命名空间 `App.Core`）。

**基准参照**：
- 后端用例脚手架：`Features/Auth/Login`（四件套）、`Features/Users/GetCurrentUser`（无参用例形态）。
- 字段约束单一来源：`Entities/UserFieldConstraints.cs` + `Configurations/UserConfiguration.cs` + 各 Validator，一致性由 `tests/App.Tests/FieldValidationConsistencyTests.cs` 守护。

## 3. 前端结构（frontend/）

```
frontend/
├── index.html / vite.config.ts / playwright.config.ts / eslint.config.js
├── src/
│   ├── main.ts / App.vue / env.d.ts
│   ├── api/          # request.ts（Axios 统一解包/40100 处置）、auth.ts、user.ts、loginLog.ts、product.ts、partner.ts、inventory.ts、purchase.ts、sale.ts
│   ├── components/   # AppLayout.vue（侧边栏含「示例页面」「进销存」子菜单：商品管理、分类管理、往来单位、库存查询、采购管理、销售开单；子菜单默认折叠，仅当前路由所属分组自动展开）
│   ├── composables/  # useOrderStore.ts（演示用）
│   ├── router/       # index.ts（按功能路由懒加载）
│   ├── stores/       # auth.ts（Pinia）
│   ├── utils/        # datetime.ts
│   └── views/        # 按功能域目录组织（PascalCase，域内文件平铺）
│       ├── LoginView.vue / HomeView.vue   # 无功能域归属的独立页
│       ├── Showcase/            # 演示页：ComponentShowcaseView / ListShowcaseView / FormShowcaseView / FormPageFormView / FormDetailView + 共享 OrderFormDrawer.vue
│       ├── UserManagement/      # UsersView + UserDetailView + UserFormDrawer
│       ├── LoginLogManagement/  # LoginLogsView
│       ├── ProductManagement/   # ProductsView + ProductFormDrawer（进销存/商品管理）
│       ├── CategoryManagement/  # CategoriesView + CategoryFormDrawer（进销存/分类管理，搜索分页 + 编辑抽屉）
│       ├── PartnerManagement/   # PartnersView + PartnerFormDrawer（进销存/往来单位）
│       ├── InventoryManagement/ # InventoryView（进销存/库存查询，只读）
│       ├── PurchaseManagement/  # PurchasesView + PurchaseFormPage + PurchaseDetailView（进销存/采购管理）
│       └── SalesManagement/     # SalesView + SaleFormPage + SaleDetailView（进销存/销售开单）
└── e2e/              # app-layout / component-showcase / form-showcase / list-showcase / login-log / login / user-management / product-management / category-management / partner-management / inventory-management / purchase / sale 各一个 spec.ts；helpers/menu.ts（clickMenuItem：点击侧边菜单项，子菜单折叠时先展开所属分组）
```

**图标选型**：业务图标默认 Tabler（`@tabler/icons-vue`）→ 回退 Lucide（`@lucide/vue`）→ 兜底 Arco 自带 `Icon*`；细则见前端规则 §4.7。业务代码（侧边菜单、列表工具条、操作列）已全部迁移为 Tabler，仅「图标」示例页（`/components` 图标 tab）为演示保留三套并存；Tabler 默认 24px 由 `App.vue` 全局样式在 Arco 按钮 / 下拉项内收敛到 1em、侧边菜单收敛到 18px（stroke 2.5）。

**基准参照**：
- 列表页标准实现：`src/views/Showcase/ListShowcaseView.vue`（规格 `specs/list-showcase/`），新增列表页复制其结构再替换业务字段。
- 页面命名 / 目录归属约定见前端规则 §4.1。
- 表单/详情页形态：`specs/form-detail-showcase/`；操作列：`specs/action-column/`；按钮 loading：`specs/button-loading/`；组合式分区：`specs/composable-style/`。

## 4. 常用命令（Windows PowerShell）

| 动作 | 命令 |
|---|---|
| 后端启动（dev，端口 5080） | `cd backend; dotnet run --project src/App.Api` |
| 后端单测 | `cd backend; dotnet test` |
| 前端启动（dev，端口 5173） | `cd frontend; npm run dev` |
| 前端 e2e（先起后端 5080 + 前端 5173） | `cd frontend; npm run test:e2e` |
| 前端类型检查 | `cd frontend; npm run type-check` |
| 前端 lint | `cd frontend; npm run lint` |
| 前端构建 | `cd frontend; npm run build` |

## 5. 环境

| 项 | 值 |
|---|---|
| 数据库 | PostgreSQL，localhost:5432，库 `app`，admin/admin123（见 `appsettings.Development.json`） |
| 前端 dev API | `VITE_API_BASE_URL=/api`，Vite proxy 转发 `/api` → `http://localhost:5080` |
| 敏感配置 | 只从环境变量读取（数据库连接串、JWT 密钥、OTel 端点），禁止入库/硬编码 |

## 6. 现有功能规格（specs/）

- 工程/脚手架：`project-scaffold`、`api-swagger`
- 前端交互模式：`app-layout`、`list-showcase`、`action-column`、`button-loading`、`composable-style`、`form-detail-showcase`、`frontend-component-showcase`（`/components` 组件示例页，含 Arco 组件 + Arco/Tabler/Lucide 三套图标「图标」tab）、`frontend-e2e`、`icon-showcase`（Arco + Tabler + Lucide 图标示例，复用组件示例页「图标」tab，三套以分组标题分隔）
- 业务：`user-management`
- ERP：`erp-product`（已完成）、`erp-partner`（已完成）、`erp-inventory-query`（已完成）、`erp-purchase`（已完成）、`erp-sale`（已完成）、`erp-category`（已完成，分类管理独立页：搜索分页 + 编辑抽屉 + 后端分页查询接口）

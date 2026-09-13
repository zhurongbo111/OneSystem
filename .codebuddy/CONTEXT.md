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
│   │   ├── Controllers/      # Auth / Health / LoginLogs / Users / Products / Categories
│   │   ├── Http/             # ClientInfoAccessor
│   │   ├── Middleware/       # GlobalExceptionMiddleware
│   │   ├── Swagger/          # SwaggerSecurityOperationFilter
│   │   └── appsettings*.json / nlog.config
│   ├── App.Core/         # 业务核心（禁止反向依赖）
│   │   ├── DependencyInjection.cs     # AddCore：注册 Mediator、Handler、Validator
│   │   ├── Abstractions/              # IMediator、IRequest、IRequestHandler、IUnitOfWork、ICurrentUser、IClientInfo、IUserRepository、IUserLoginLogRepository、ICategoryRepository、IProductRepository、IInventoryRepository、ProductListItem、ProductDetail、ProductPickItem
│   │   ├── Auth/                      # JwtOptions、PasswordHasher、TokenService
│   │   ├── Entities/                  # User、UserLoginLog、UserStatus、UserFieldConstraints、Product、Category、Inventory、ProductStatus、ProductFieldConstraints、CategoryFieldConstraints
│   │   ├── Errors/                    # BusinessException、ErrorCode
│   │   ├── Mediation/                 # Mediator（自研简化 MediatR，Send 前统一跑 Validator）
│   │   └── Features/                  # 每用例四件套：Request / RequestValidator / RequestHandler / Response
│   └── App.Infrastructure/
│       ├── DependencyInjection.cs     # AddInfrastructure：IUnitOfWork、仓储实现
│       ├── AppDbContext.cs            # 含 Categories / Products / Inventory 三个 DbSet
│       ├── Migrations/
│       ├── Persistence/               # UnitOfWork、DatabaseInitializer、Configurations/
│       └── Repositories/              # UserRepository、UserLoginLogRepository、CategoryRepository、ProductRepository、InventoryRepository
└── tests/App.Tests/      # 每 Handler 一个测试文件 + ApiIntegration / FieldValidationConsistency / TestSupport 等
```

**已实现的 Features**（`App.Core/Features/`）：

| Feature | 用例 |
|---|---|
| Auth | Login |
| LoginLogs | GetLoginLogs |
| Users | CreateUser、GetUsers、GetUserById、GetCurrentUser（空 Request 无 Validator）、UpdateUser、UpdateUserStatus、ResetPassword |
| Products | GetProducts、GetProductById、CreateProduct、UpdateProduct、UpdateProductStatus、GetProductPickList |
| Categories | GetCategories、CreateCategory、UpdateCategory、DeleteCategory |

共享出参：`Features/Users/UserDto`、`UserListItemDto`、`UserDetailDto`、`UserDtoMapper`、`UserInputNormalizer`；`Features/LoginLogs/LoginLogListItemDto`；`Features/Products/ProductDto`、`ProductPickDto`、`ProductInputNormalizer`；`Features/Categories/CategoryDto`。

**基准参照**：
- 后端用例脚手架：`Features/Auth/Login`（四件套）、`Features/Users/GetCurrentUser`（无参用例形态）。
- 字段约束单一来源：`Entities/UserFieldConstraints.cs` + `Configurations/UserConfiguration.cs` + 各 Validator，一致性由 `tests/App.Tests/FieldValidationConsistencyTests.cs` 守护。

## 3. 前端结构（frontend/）

```
frontend/
├── index.html / vite.config.ts / playwright.config.ts / eslint.config.js
├── src/
│   ├── main.ts / App.vue / env.d.ts
│   ├── api/          # request.ts（Axios 统一解包/40100 处置）、auth.ts、user.ts、loginLog.ts、product.ts
│   ├── components/   # AppLayout.vue（侧边栏含「进销存」子菜单：商品管理）
│   ├── composables/  # useOrderStore.ts（演示用）
│   ├── router/       # index.ts（按功能路由懒加载）
│   ├── stores/       # auth.ts（Pinia）
│   ├── utils/        # datetime.ts
│   └── views/        # 按功能目录组织（PascalCase）
│       ├── LoginView.vue / HomeView.vue
│       ├── UserManagement/       # UsersView + UserDetailView + UserFormDrawer
│       ├── LoginLogManagement/   # LoginLogsView
│       ├── ProductManagement/    # ProductsView + ProductFormDrawer + CategoryManagerModal（进销存/商品管理）
│       └── 演示页：ComponentShowcaseView、FormShowcaseView、ListShowcaseView、FormPageFormView、FormDetailView
│            └── FormShowcase/components/OrderFormDrawer.vue
└── e2e/              # app-layout / component-showcase / form-showcase / list-showcase / login-log / login / user-management / product-management 各一个 spec.ts
```

**基准参照**：
- 列表页标准实现：`src/views/ListShowcaseView.vue`（规格 `specs/list-showcase/`），新增列表页复制其结构再替换业务字段。
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
- 前端交互模式：`app-layout`、`list-showcase`、`action-column`、`button-loading`、`composable-style`、`form-detail-showcase`、`frontend-component-showcase`、`frontend-e2e`
- 业务：`user-management`
- ERP（开发中，未提交）：`erp-inventory-query`、`erp-partner`、`erp-product`、`erp-purchase`、`erp-sale`

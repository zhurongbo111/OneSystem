---
created: 2026-09-09
updated: 2026-09-20
---

# 设计规格：项目脚手架（project-scaffold）

## 1. 总体设计

按 `AGENTS.md` 与 frontend / backend 专项规则搭建 monorepo 骨架：

```
.
├── specs/001-project-scaffold/
├── backend/
│   ├── App.sln
│   ├── src/
│   │   ├── App.Api/            # Controller、中间件、全局异常、NLog、OTel、Program
│   │   ├── App.Core/           # Features（Request/RequestValidator/RequestHandler/Response）、实体、仓储接口、统一响应、异常、JWT 签发、中介（IMediator/Mediator）
│   │   └── App.Infrastructure/ # AppDbContext、Repository 实现（脚手架阶段为内存实现）、UnitOfWork 实现
│   └── tests/
│       └── App.Tests/          # xUnit 单元测试
└── frontend/                   # Vite + Vue 3 + TS + Arco Design + Pinia + router + Axios
```

依赖方向：`App.Api → App.Core`、`App.Api → App.Infrastructure`、`App.Infrastructure → App.Core`；`App.Tests → App.Api`（经 `WebApplicationFactory` 间接覆盖全链路）。禁止反向依赖。

## 2. 后端设计

### 2.1 项目与包

| 项目 | 说明 | 关键包 |
|---|---|---|
| App.Api | Web API 入口 | NLog.Web.AspNetCore、OpenTelemetry.* |
| App.Core | 业务核心 | FluentValidation、System.IdentityModel.Tokens.Jwt、Microsoft.Extensions.*（Abstractions / DependencyInjection / Logging / Configuration / Http） |
| App.Infrastructure | 数据访问 | Microsoft.EntityFrameworkCore、Npgsql.EntityFrameworkCore.PostgreSQL |
| App.Tests | 单元测试 | xunit、Microsoft.NET.Test.Sdk、Microsoft.AspNetCore.Mvc.Testing、Microsoft.EntityFrameworkCore.InMemory |

- 统一目标框架 `net8.0`；启用可空引用类型；`ImplicitUsings` 开启。
- `App.Api` 的 `Program` 声明为 `internal partial class Program`，供测试工厂使用。

### 2.2 统一响应（App.Core/Responses）

```csharp
public class ApiResponse { int Code; string Message; }             // code=0, message="success"
public class ApiResponse<T> : ApiResponse { T? Data; }
public static class ApiResponseExtensions
{
    public static ApiResponse<T> Ok<T>(T data);
    public static ApiResponse<T> Fail<T>(int code, string message);
    public static ApiResponse Fail(int code, string message);
}
```

- Controller 直接 `return Ok(data)` / `Fail(code, msg)`；所有接口（含健康检查、认证）统一该结构。
- HTTP 状态码恒为 200，业务状态由 `code` 表达（与 AGENTS.md 4.1 一致）。
- JSON 序列化 camelCase（ASP.NET Core 默认）。

### 2.3 错误码（App.Core/Errors/ErrorCode.cs）

| code | 常量 | 含义 |
|---:|---|---|
| 0 | Success | 成功 |
| 40000 | Validation | 参数错误 |
| 40100 | Unauthorized | 未登录或 token 无效 |
| 40300 | Forbidden | 无权限 |
| 40400 | NotFound | 资源不存在 |
| 50000 | Internal | 服务内部错误 |
| 40001 | LoginFailed | 用户名或密码错误（本功能业务码） |

### 2.4 异常与全局处理

- `App.Core/Errors/BusinessException.cs`：携带 `Code` + `Message`，由 RequestHandler 抛出业务错误。
- `App.Api/Middleware/GlobalExceptionMiddleware`：
  - `BusinessException` → 返回其 `Code` / `Message`；
  - 其他异常 → `50000`，日志记录异常 + 上下文（含 `Activity.Current?.TraceId`）。
- 全局异常中间件置于管道最前，替代 `UseExceptionHandler`。

### 2.5 JWT 认证

- 配置节 `Jwt`（`JwtOptions`：`Secret`、`Issuer`、`Audience`、`ExpiresMinutes`），由环境变量注入：

| 环境变量 | 配置键 | 说明 |
|---|---|---|
| `JWT__SECRET` | `Jwt:Secret` | HMAC-SHA256 密钥（≥32 字符），禁止入库 |
| `JWT__EXPIRES_MINUTES` | `Jwt:ExpiresMinutes` | 默认 120 |
| `JWT__ISSUER` / `JWT__AUDIENCE` | `Jwt:Issuer` / `Jwt:Audience` | 默认 "app-api" / "app-web" |

- **Dev 兜底**：`ASPNETCORE_ENVIRONMENT=Development` 且未配置 `JWT__SECRET` 时，启动时生成随机密钥并打 Warning 日志（本地开箱即用，重启后 token 失效）；Production 下缺失则启动抛异常。
- **签发**（App.Core/Auth/TokenService）：claims 含 `sub`（用户 id）、`username`、`displayName`、`iss`/`aud`/`exp`；`HS256`。
- **校验**：使用 ASP.NET Core 默认认证框架 `Microsoft.AspNetCore.Authentication.JwtBearer`（`App.Api` 注册，见 `App.Api/Authentication/JwtBearerExtensions`）：
  - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`：`TokenValidationParameters` 校验 Issuer / Audience / 签名密钥 / 有效期（`ClockSkew` 30 秒），配置值与签发共用 `JwtOptions`；
  - `AddAuthorization` 设置 `FallbackPolicy = RequireAuthenticatedUser()`：除显式标注 `[AllowAnonymous]` 的接口外，全部默认要求登录，等价于原"白名单外全局校验"语义；
  - 白名单改用特性表达（不再维护路径前缀）：`AuthController.Login`、`HealthController` 标注 `[AllowAnonymous]`；`UsersController` 标注 `[Authorize]`；
  - 未认证统一 40100：`JwtBearerEvents.OnChallenge` 跳过默认 HTTP 401（`HandleResponse()`），改为返回 HTTP 200 + `{ code: 40100, message: "未登录或 token 无效" }`，保持全站 HTTP 200 约定；`OnAuthenticationFailed` 记录 warning 日志（含 traceId）；
  - 校验通过后由认证中间件写入 `HttpContext.User`（ClaimsPrincipal；`sub` 经默认入站映射为 `ClaimTypes.NameIdentifier`），`ICurrentUser` 从该 principal 读取。
- **管道**：`UseRouting → UseAuthentication → UseAuthorization → MapControllers`，不再使用自研认证中间件。

### 2.6 接口设计

| 接口 | 方法 | 认证 | 请求 | `data` 响应 | 错误码 |
|---|---|---|---|---|---|
| `/api/auth/login` | POST | 否 | `LoginRequest { username, password }` | `LoginResponse { token, user }` | 40000 参数空；40001 用户名或密码错误 |
| `/api/users/me` | GET | 是 | — | `UserDto { id, username, displayName }` | 40100 |
| `/health` | GET | 否 | — | `"healthy"`（string） | — |

- 用例模型（App.Core/Features）：`LoginRequest`、`LoginResponse`、`UserDto` 随用例同目录；对外只暴露模型，不暴露实体。
- Controller：`AuthController`、`UsersController`、`HealthController`，仅做参数绑定 + 统一经 `IMediator.Send(Request)` 触发对应用例 + 返回统一响应（参数校验由 `RequestValidator` 承担）。

### 2.7 用例结构、仓储与示例账号

每个 API 对应 `App.Core/Features/<Feature>/<Action>/` 下一组文件（Request / RequestValidator / RequestHandler / Response，详见后端规则 §4.1），不设 Service 层；用例请求实现 `IRequest<TResponse>` 标记、处理器实现 `IRequestHandler<TRequest, TResponse>`，Controller 只依赖 `Abstractions/IMediator` 经 `Send(Request)` 分发：

- `Features/Auth/Login/`：`LoginRequest`（实现 `IRequest<LoginResponse>`）+ `LoginRequestValidator`（FluentValidation，仅格式校验、不查库）+ `LoginRequestHandler`（实现 `IRequestHandler<LoginRequest, LoginResponse>`）+ `LoginResponse`：
  - 格式校验由 `Mediator` 全局统一执行（见技术决策），失败 → `BusinessException(40000)`；
  - 查库约束（账号是否存在、密码是否正确）在 Handler 内判断 → `BusinessException(40001, "用户名或密码错误")`；
  - 成功 → `TokenService.Issue(UserDto)` 签发 JWT，返回 `LoginResponse { token, user }`。
- `Features/Users/GetCurrentUser/`：空请求 `GetCurrentUserRequest`（实现 `IRequest<UserDto>`，占位统一入口签名，不定义 Validator）+ `GetCurrentUserRequestHandler`（实现 `IRequestHandler<GetCurrentUserRequest, UserDto>`），从 `ICurrentUser`（App.Api 基于已认证 claims 实现）还原 `UserDto`。
- `Abstractions/IUserRepository`（接口，App.Core）与 `Repositories/InMemoryUserRepository`（App.Infrastructure，**脚手架临时实现**，首个业务功能替换为 EF Core 实现）：
  - 种子账号：`admin` / `admin123`，`displayName = "管理员"`。
- `Abstractions/IUnitOfWork`（App.Core 接口 / App.Infrastructure `Persistence/UnitOfWork` 实现）：为跨仓储写操作提供显式事务边界；脚手架阶段无真实写库场景，先落接口与实现供后续用例使用。
  - 事务生命周期约定：每个用例内 `BeginTransactionAsync` → 写操作 → `CommitAsync` / 异常 `RollbackAsync` 各一次；已有未提交事务时再次 `BeginTransactionAsync` 属编程错误，实现需 fail-fast 抛异常（禁止静默复用 / 覆盖当前事务）。

### 2.8 数据库（App.Infrastructure）

- `AppDbContext : DbContext`，暂不挂实体（无表）；连接串来自 `ConnectionStrings:Default`（环境变量 `ConnectionStrings__Default`），未配置时允许启动（不发起连接）。
- 无迁移文件（无表结构）；后续建表走 Migrations。
- `App.Api` 注册 `AddDbContext<AppDbContext>(UseNpgsql)`。

### 2.9 日志与可观测性（App.Api）

- **NLog**（`nlog.config`，随 Api 项目复制输出）：
  - targets：控制台 + 文件 `logs/app-{shortdate}.log`（滚动归档，保留 10 份）；
  - 级别：`ASPNETCORE_ENVIRONMENT=Development` → Info；其他（prod）→ Warning；
  - 布局含 `${aspnet-TraceIdentifier}`（traceId）；
  - 接入：`builder.Host.UseNLog()`，业务代码只用 `ILogger<T>`。
- **OpenTelemetry**：`AddOpenTelemetry().WithTracing(AspNetCore + EF Core 自动埋点).WithMetrics(AspNetCore + Runtime 自动埋点)`；
  - 仅当环境变量 `OTEL_EXPORTER_OTLP_ENDPOINT` 非空时追加 OTLP 导出（dev 默认不导出，prod 导出至 collector）。

### 2.10 配置与运行

- `appsettings.json`（非敏感默认值）：`Jwt:Issuer/Audience/ExpiresMinutes`；敏感项（密钥、连接串）一律环境变量。
- `appsettings.Development.json`：空对象。
- dev 启动：`http://localhost:5080`（`launchSettings.json`）。
- 生产：`ASPNETCORE_ENVIRONMENT=Production` + 环境变量注入敏感配置。

## 3. 前端设计

### 3.1 技术选型与版本

Vue 3（`<script setup lang="ts">`，禁止 Options API）+ Arco Design Vue + Vite + Pinia + vue-router + Axios + TypeScript + ESLint + Prettier。

### 3.2 目录结构

```
frontend/
├── index.html
├── vite.config.ts          # vue 插件 + dev proxy（/api → http://localhost:5080）
├── .env.development        # VITE_API_BASE_URL=/api（走 proxy）
├── .env.production         # VITE_API_BASE_URL 由部署时定义
└── src/
    ├── main.ts             # createApp + Arco + Pinia + Router + 引入 arco 样式
    ├── api/
    │   ├── request.ts      # Axios 实例 + 拦截器（统一解包 / 40100 处理）
    │   └── auth.ts         # login API 与类型
    ├── router/index.ts     # 路由注册 + 全局守卫（登录态）
    ├── stores/auth.ts      # Pinia：token / user，持久化 localStorage
    ├── views/
    │   ├── LoginView.vue   # 登录页
    │   └── HomeView.vue    # 首页（展示当前用户 + 退出登录）
    └── utils/（预留）
```

### 3.3 接口层（`src/api/request.ts`）

- 基于 Axios：`baseURL = import.meta.env.VITE_API_BASE_URL`；
- **请求拦截器**：从 localStorage 读 `token`，附加 `Authorization: Bearer <token>`；
- **响应拦截器**：
  - `code === 0` → resolve 出 `data`（业务代码只拿数据本身）；
  - `code !== 0` → `Message.error(message)` 并 reject；
  - `code === 40100` → 清除 localStorage 凭证 + 清空 Pinia 认证状态（`useAuthStore().logout()`），防重复跳转（标志位）后以 **SPA 路由跳转**（`router.replace({ name: 'login', query: { redirect: 当前 fullPath } })`）进入登录页；已在登录页时不跳转。
  - **不整页刷新**：整页跳转会与进行中的导航抢跳（页面白屏、表单不渲染）、丢失应用内存状态；改由 SPA 跳转后，必须同时清空 Pinia 认证状态——否则守卫读到的 `isLoggedIn`（= `token`）仍为 true，会把 `/login` 重定向回首页，跳转失效。
  - **依赖方向**：请求层不直接依赖 router / store（`stores/auth` 已依赖 `api/request`，直接反向依赖会成环），改为 `setUnauthorizedHandler(handler)` 注入处置回调，由路由层在模块初始化时注册。

### 3.4 状态与路由

- `stores/auth.ts`（Pinia）：`token`、`user`；`login()` 调 `api/auth`，成功后写入并持久化 localStorage；`logout()` 清空 token / user 与 localStorage 凭证（**只清状态，不负责跳转**，跳转由调用方决定）。
- 路由：`/login` → `LoginView`；`/` → `HomeView`（`meta: { requiresAuth: true }`）；`() => import(...)` 懒加载。
- 全局前置守卫：未登录访问 `requiresAuth` 路由 → 重定向 `/login`（携带 `redirect` 回跳目标）。

### 3.5 页面交互

- **登录页**：Arco `a-form` 用户名/密码 + `a-button`；提交调 `login`，成功后按 `route.query.redirect` 回跳（缺省 `/`），失败由接口层统一提示。
- **首页**：展示 `user.displayName` 与 `username`（`a-card`）；退出登录入口在布局顶部栏的「用户菜单」下拉（`AppLayout.vue`），调 `logout()` 后跳登录页。

### 3.6 环境与联调

- `.env.development`：`VITE_API_BASE_URL=/api`；`vite.config.ts` 配 `server.proxy` 将 `/api` 转发到 `http://localhost:5080`（`changeOrigin: true`）。
- 构建：`npm run build`（`tsc -b && vite build`）；`npm run dev` 默认端口 5173。

## 4. 技术决策

| 决策 | 理由 |
|---|---|
| 采用 ASP.NET Core 默认 JwtBearer 认证（`AddAuthentication().AddJwtBearer()` + `[Authorize]`），仅用 `JwtBearerEvents.OnChallenge` 改写统一响应 | 认证 / 校验逻辑交给框架内置 handler，随框架演进维护，无需自研中间件；白名单用 `[AllowAnonymous]` 表达（`FallbackPolicy` 默认要求登录），40100 语义通过 OnChallenge 保持 HTTP 200 统一响应，两者均符合框架惯例 |
| 每 API 一组 Request/RequestValidator/RequestHandler/Response（用例 / 垂直切片） | 一个接口一份逻辑与模型、职责单一；RequestHandler 公共方法天然可单测；校验规则与用例同目录、可发现 |
| 全部 RequestHandler 统一实现 `IRequestHandler<TRequest, TResponse>` 入口 | 统一 `HandleAsync(Request, CancellationToken)` 签名，Controller / DI / 单测面向同一接口；无请求参数的用例以空 Request 模型占位签名 |
| 自研轻量中介 `IMediator.Send(Request)`（简化版 MediatR）而非引入 MediatR 包 | Controller 只面对单一中介入口，不感知具体 Handler，入口统一可替换；请求经 `IRequest<TResponse>` 标记声明响应类型并供运行时分发；注册仍显式（无注册期反射扫描），避免第三方 CQRS 依赖 |
| RequestValidator 只做格式校验，查库约束放 RequestHandler | 校验器保持无状态纯规则；依赖数据的判定与写操作同处一个逻辑 / 事务上下文 |
| 格式校验使用 FluentValidation | 声明式规则 + 可测试，替代手写 if 校验 |
| 格式校验由 `Mediator` 全局统一执行（分发前） | 所有用例强制先校验再处理，无遗漏风险；Handler 不注入 / 不执行校验器，`HandleAsync` 只剩业务逻辑；校验规则仍按用例注册（显式注册、禁止扫描），空请求用例无校验器自动跳过 |
| 不设 Service 层，RequestHandler 直接依赖仓储 | 避免贫血的业务编排层；跨仓储事务用 IUnitOfWork 显式控制 |
| 登录示例账号放 `InMemoryUserRepository`（App.Infrastructure） | 脚手架无真实用户表；以仓储接口（App.Core.Abstractions）划边界，首个业务功能直接替换为 EF Core 实现 |
| `AppDbContext` 暂空、不建迁移 | 无实体则无表；后续功能建表时再走 Migrations |
| HTTP 状态码恒 200，业务码表达结果 | 与 AGENTS.md 4.1 统一响应约定一致，前端按 `code` 分支处理 |
| dev 下 JWT 密钥自动生成兜底 | 本地开箱即用；prod 缺失即启动失败，避免裸奔 |
| 集成测试工厂切换环境用 `builder.UseEnvironment(...)` 而非 `UseSetting("ASPNETCORE_ENVIRONMENT", ...)` | WebApplicationFactory 下 `UseSetting` 设置 `ASPNETCORE_ENVIRONMENT` 对 `IWebHostEnvironment` **不生效**（实证：工厂一直实际运行在 Development）。`UseEnvironment` 直接替换环境名可靠生效；且 Production 下 JWT 密钥校验读取**进程环境变量**（`JWT__SECRET`，Program 早期、in-memory 配置不可见），工厂需在静态构造中 `Environment.SetEnvironmentVariable("JWT__SECRET", ...)` 提供测试密钥 |

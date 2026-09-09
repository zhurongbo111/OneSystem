# 设计规格：项目脚手架（project-scaffold）

## 1. 总体设计

按 `AGENTS.md` 与 frontend / backend 专项规则搭建 monorepo 骨架：

```
.
├── specs/project-scaffold/
├── backend/
│   ├── App.sln
│   ├── src/
│   │   ├── App.Api/            # Controller、中间件、全局异常、NLog、OTel、Program
│   │   ├── App.Core/           # Handler、实体、DTO、统一响应、异常、JWT 签发
│   │   └── App.Infrastructure/ # AppDbContext、（Repository 实现，脚手架阶段为空）
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
| App.Core | 业务核心 | System.IdentityModel.Tokens.Jwt、Microsoft.Extensions.*（Abstractions / DependencyInjection / Logging / Configuration / Http） |
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

- `App.Core/Errors/BusinessException.cs`：携带 `Code` + `Message`，Handler 抛出业务错误。
- `App.Api/Middleware/GlobalExceptionMiddleware`：
  - `BusinessException` → 返回其 `Code` / `Message`；
  - 其他异常 → `50000`，日志记录异常 + 上下文（含 `Activity.Current?.TraceId`）。
- 自研中间件置于管道最前（在认证中间件之前），替代 `UseExceptionHandler`。

### 2.5 JWT 认证

- 配置节 `Jwt`（`JwtOptions`：`Secret`、`Issuer`、`Audience`、`ExpiresMinutes`），由环境变量注入：

| 环境变量 | 配置键 | 说明 |
|---|---|---|
| `JWT__SECRET` | `Jwt:Secret` | HMAC-SHA256 密钥（≥32 字符），禁止入库 |
| `JWT__EXPIRES_MINUTES` | `Jwt:ExpiresMinutes` | 默认 120 |
| `JWT__ISSUER` / `JWT__AUDIENCE` | `Jwt:Issuer` / `Jwt:Audience` | 默认 "app-api" / "app-web" |

- **Dev 兜底**：`ASPNETCORE_ENVIRONMENT=Development` 且未配置 `JWT__SECRET` 时，启动时生成随机密钥并打 Warning 日志（本地开箱即用，重启后 token 失效）；Production 下缺失则启动抛异常。
- **签发**（App.Core/Auth/TokenService）：claims 含 `sub`（用户 id）、`username`、`displayName`、`iss`/`aud`/`exp`；`HS256`。
- **校验**（App.Api/Middleware/JwtAuthenticationMiddleware，自研以便统一 40100 语义）：
  - 白名单放行（前缀匹配）：`/api/auth/login`、`/health`；
  - 无 token / token 无效 → 直接写 `code: 40100`、`message: "未登录或 token 无效"`，不再进入后续管道；
  - 校验通过 → 写入 `HttpContext.User`（ClaimsPrincipal）。

### 2.6 接口设计

| 接口 | 方法 | 认证 | 请求 | `data` 响应 | 错误码 |
|---|---|---|---|---|---|
| `/api/auth/login` | POST | 否 | `LoginRequest { username, password }` | `LoginResult { token, user }` | 40000 参数空；40001 用户名或密码错误 |
| `/api/users/me` | GET | 是 | — | `UserDto { id, username, displayName }` | 40100 |
| `/health` | GET | 否 | — | `"healthy"`（string） | — |

- DTO（App.Core）：`UserDto`、`LoginRequest`、`LoginResult`；对外只暴露 DTO。
- Controller：`AuthController`、`UsersController`、`HealthController`，仅做参数校验 + 调用 Handler + 返回统一响应。

### 2.7 Handler 与示例账号（App.Core）

- `IAuthHandler / AuthHandler`：`Task<LoginResult> LoginAsync(LoginRequest, CancellationToken)`
  - 参数空 → `BusinessException(40000)`；
  - 账号校验失败 → `BusinessException(40001, "用户名或密码错误")`；
  - 成功 → 生成 token + `UserDto`。
- `IUserHandler / UserHandler`：`UserDto GetCurrentUser(ClaimsPrincipal)` — 从 claims 还原 `UserDto`。
- `IUserAccountService / InMemoryUserAccountService`（App.Core，**脚手架临时实现**，注释标明由首个业务功能替换为 Repository）：
  - 测试账号：`admin` / `admin123`，`displayName = "管理员"`。
- 事务：脚手架阶段无跨 Repository 写操作；后续按规则在 Handler 显式事务。

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
  - `code === 40100` → 清除 localStorage 凭证，防重复跳转（标志位）后 `router.push('/login')`。

### 3.4 状态与路由

- `stores/auth.ts`（Pinia）：`token`、`user`；`login()` 调 `api/auth`，成功后写入并持久化 localStorage；`logout()` 清空并跳登录页。
- 路由：`/login` → `LoginView`；`/` → `HomeView`（`meta: { requiresAuth: true }`）；`() => import(...)` 懒加载。
- 全局前置守卫：未登录访问 `requiresAuth` 路由 → 重定向 `/login`。

### 3.5 页面交互

- **登录页**：Arco `a-form` 用户名/密码 + `a-button`；提交调 `login`，成功跳首页，失败由接口层统一提示。
- **首页**：展示 `user.displayName` 与 `username`（`a-card`）；右上角"退出登录"按钮调 `logout`。

### 3.6 环境与联调

- `.env.development`：`VITE_API_BASE_URL=/api`；`vite.config.ts` 配 `server.proxy` 将 `/api` 转发到 `http://localhost:5080`（`changeOrigin: true`）。
- 构建：`npm run build`（`tsc -b && vite build`）；`npm run dev` 默认端口 5173。

## 4. 技术决策

| 决策 | 理由 |
|---|---|
| JWT 校验用自研中间件而非 JwtBearer 包 | 需统一返回业务码 40100（HTTP 200），自研更易控制响应结构与白名单 |
| 登录示例账号放 `InMemoryUserAccountService` | 脚手架无真实用户表；接口化（`IUserAccountService`）便于首个业务功能替换为 Repository |
| `AppDbContext` 暂空、不建迁移 | 无实体则无表；后续功能建表时再走 Migrations |
| HTTP 状态码恒 200，业务码表达结果 | 与 AGENTS.md 4.1 统一响应约定一致，前端按 `code` 分支处理 |
| dev 下 JWT 密钥自动生成兜底 | 本地开箱即用；prod 缺失即启动失败，避免裸奔 |

---
created: 2026-09-09
updated: 2026-09-20
---

# 任务清单：项目脚手架（project-scaffold）

## 后端

- [x] B1 创建解决方案 `backend/App.sln` 与 `src/App.Api`、`src/App.Core`、`src/App.Infrastructure`、`tests/App.Tests` 四个项目（net8.0），按规则配置依赖方向
- [x] B2 引入 NuGet 包（EF Core / Npgsql / NLog / OpenTelemetry / JWT / xUnit / Mvc.Testing / InMemory），版本锁定兼容 net8.0
- [x] B3 实现统一响应 `ApiResponse` / `ApiResponse<T>` / 工厂与错误码常量（App.Core）
- [x] B4 实现 `BusinessException` 与 `GlobalExceptionMiddleware`（含 traceId 日志）
- [x] B5 实现 JWT：`JwtOptions`、`TokenService` 签发、`JwtAuthenticationMiddleware` 校验与白名单、dev 密钥兜底
- [x] B6 实现用例（每 API 一组 Request/RequestValidator/RequestHandler/Response）：登录与获取当前用户示例；`IUserRepository` 接口 + 内存实现、`IUnitOfWork`
- [x] B7 实现 `AuthController`、`UsersController`、`HealthController`
- [x] B8 实现 `AppDbContext`（空）、仓储内存实现与 `IUnitOfWork`、EF Core UseNpgsql 与 DI 注册
- [x] B9 配置 NLog（控制台 + 滚动文件、dev=Info/prod=Warning、traceId 布局）并接入 `UseNLog`
- [x] B10 接入 OpenTelemetry（Tracing + Metrics 自动埋点，OTLP 条件导出）
- [x] B11 配置 `appsettings*.json`、`launchSettings.json`（dev 端口 5080）
- [x] B12 编写单元测试：统一响应、全局异常、登录 / 当前用户用例、集成测试；`dotnet build` 与 `dotnet test` 通过
- [x] B13 新增统一入口接口 `IRequestHandler<TRequest,TResponse>`（App.Core/Abstractions）：登录 / 获取当前用户两个用例实现接口并保持同一 `HandleAsync(Request, ct)` 签名；Controller 面向接口注入；`AddCore` 注册改为接口映射；单测同步；`dotnet build` 与 `dotnet test` 通过
- [x] B14 实现自研用例中介（简化版 MediatR）：`Abstractions` 新增 `IRequest<TResponse>` / `IMediator`，`Mediation/Mediator` 按请求运行时类型经 DI 分发到已注册 `IRequestHandler`；登录 / 获取当前用户请求实现 `IRequest` 标记；Controller 改为只注入 `IMediator`；`AddCore` 注册 `IMediator`；新增 `MediatorTests`；`dotnet build` 与 `dotnet test` 通过
- [x] B16 格式校验全局统一（后端规则 §4.2）：`Mediator.Send` 分发前按请求运行时类型经 DI 解析 `IValidator<TRequest>` 统一执行，失败抛 `BusinessException(40000)`，未注册校验器自动跳过；`LoginRequestHandler` 移除校验器注入与校验逻辑；单测同步（Mediator 新增校验用例）；`dotnet build` 与 `dotnet test` 通过
- [x] B15 将 JWT 校验改为 ASP.NET Core 默认认证：`AddAuthentication().AddJwtBearer()`（`TokenValidationParameters` 与签发共用 `JwtOptions`）+ `FallbackPolicy` 默认要求登录，`JwtBearerEvents.OnChallenge` 统一返回 `code: 40100`（HTTP 200）；`AuthController.Login` / `HealthController` 标注 `[AllowAnonymous]`、`UsersController` 标注 `[Authorize]`；删除 `JwtAuthenticationMiddleware` 与 `TokenService.Validate`，同步单测；`dotnet build` 与 `dotnet test` 通过

## 前端

- [x] F1 使用 Vite 创建 `frontend` 项目（vue-ts 模板），安装 Arco Design Vue / Pinia / vue-router / Axios / ESLint / Prettier
- [x] F2 实现 `src/api/request.ts`：Axios 实例 + 请求/响应拦截器（解包、错误提示、40100 处理）与 `src/api/auth.ts`
- [x] F3 实现 `src/stores/auth.ts`（token/user 持久化）
- [x] F4 实现 `src/router/index.ts`（懒加载 + 登录守卫）
- [x] F5 实现 `src/views/LoginView.vue` 与 `src/views/HomeView.vue`，`main.ts` 接入 Arco / Pinia / Router
- [x] F6 配置 `vite.config.ts`（dev proxy）与 `.env.development` / `.env.production`
- [x] F7 `npm run build` 通过；与 dev 后端联调：登录 → 首页展示用户 → 退出跳登录页（agent-browser 真实浏览器全流程实测通过，含未登录守卫拦截）

## 前端（40100 处置修正，2026-09-20）

> 背景：实现漂移——§3.3 约定 `router.push('/login')`，实际却是 `window.location.href = '/login'`（整页刷新）。
> 整页跳转与进行中的导航抢跳会造成页面白屏（e2e 中表现为登录表单不渲染、`fill` 等到用例超时），并丢失应用内存状态。

- [x] F8 40100 统一处置改为 SPA 路由跳转：`request.ts` 移除整页跳转、新增注入式 `setUnauthorizedHandler`；`router/index.ts` 注入处置逻辑（清 Pinia 认证状态 → `router.replace` 登录页并带 `redirect`）——清认证状态与跳转的先后顺序不可颠倒，否则守卫会把 `/login` 重定向回首页
- [x] F9 e2e 覆盖：`e2e/login.spec.ts` 新增「凭证失效 → 路由跳转登录页（未整页刷新）→ 重新登录回到原页面」用例
- [x] F10 同步 `.codebuddy/rules/frontend/RULE.mdc` §8 / §10.1 的 40100 处置判据（请求层仅经注入回调跳转；登录 helper 的 about:blank 防御理由更新为「避免 40100 处置与测试导航交叉」）

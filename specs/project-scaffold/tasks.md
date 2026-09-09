# 任务清单：项目脚手架（project-scaffold）

## 后端

- [x] B1 创建解决方案 `backend/App.sln` 与 `src/App.Api`、`src/App.Core`、`src/App.Infrastructure`、`tests/App.Tests` 四个项目（net8.0），按规则配置依赖方向
- [x] B2 引入 NuGet 包（EF Core / Npgsql / NLog / OpenTelemetry / JWT / xUnit / Mvc.Testing / InMemory），版本锁定兼容 net8.0
- [x] B3 实现统一响应 `ApiResponse` / `ApiResponse<T>` / 工厂与错误码常量（App.Core）
- [x] B4 实现 `BusinessException` 与 `GlobalExceptionMiddleware`（含 traceId 日志）
- [x] B5 实现 JWT：`JwtOptions`、`TokenService` 签发、`JwtAuthenticationMiddleware` 校验与白名单、dev 密钥兜底
- [x] B6 实现 DTO（`UserDto`/`LoginRequest`/`LoginResult`）、`IUserAccountService` 内存实现、`AuthHandler`、`UserHandler`
- [x] B7 实现 `AuthController`、`UsersController`、`HealthController`
- [x] B8 实现 `AppDbContext`（空）与 DI 注册、EF Core UseNpgsql
- [x] B9 配置 NLog（控制台 + 滚动文件、dev=Info/prod=Warning、traceId 布局）并接入 `UseNLog`
- [x] B10 接入 OpenTelemetry（Tracing + Metrics 自动埋点，OTLP 条件导出）
- [x] B11 配置 `appsettings*.json`、`launchSettings.json`（dev 端口 5080）
- [x] B12 编写单元测试：统一响应、全局异常、`AuthHandler`、`UserHandler`、集成测试；`dotnet build` 与 `dotnet test` 通过（18/18）

## 前端

- [x] F1 使用 Vite 创建 `frontend` 项目（vue-ts 模板），安装 Arco Design Vue / Pinia / vue-router / Axios / ESLint / Prettier
- [x] F2 实现 `src/api/request.ts`：Axios 实例 + 请求/响应拦截器（解包、错误提示、40100 处理）与 `src/api/auth.ts`
- [x] F3 实现 `src/stores/auth.ts`（token/user 持久化）
- [x] F4 实现 `src/router/index.ts`（懒加载 + 登录守卫）
- [x] F5 实现 `src/views/LoginView.vue` 与 `src/views/HomeView.vue`，`main.ts` 接入 Arco / Pinia / Router
- [x] F6 配置 `vite.config.ts`（dev proxy）与 `.env.development` / `.env.production`
- [x] F7 `npm run build` 通过；与 dev 后端联调：登录 → 首页展示用户 → 退出跳登录页（agent-browser 真实浏览器全流程实测通过，含未登录守卫拦截）

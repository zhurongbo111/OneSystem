---
created: 2026-09-09
updated: 2026-09-16
---

# 需求规格：项目脚手架（project-scaffold）

## 1. 背景

仓库当前只有 `AGENTS.md` 总则与 frontend / backend 专项规则，尚无实际代码。需要按总则与专项规则搭建前后端项目骨架，使其可运行、可联调，为后续业务功能开发（SDD）提供基础。

## 2. 目标

1. 建立后端 .NET 8 解决方案骨架（Api / Core / Infrastructure / Tests 四项目），实现总则第 4 节 API 契约与后端规则约定的公共能力。
2. 建立前端 Vue 3 + Arco Design 项目骨架，实现接口层统一封装、路由、状态管理、登录/首页示例页面。
3. 提供端到端登录联调能力：前端登录页调用后端登录接口，签发 JWT 并受保护访问示例资源。
4. 提供健康检查接口，便于部署与联调探活。

## 3. 功能点

- F1 后端项目骨架：`App.sln` 与 `App.Api`、`App.Core`、`App.Infrastructure`、`App.Tests` 四项目，依赖方向符合后端规则 §3。
- F2 统一响应：所有接口返回 `{ code, message, data }`；`code=0` 成功。
- F3 全局异常处理：未处理异常统一转为 `code: 50000`，响应 HTTP 状态码保持 200（业务码表达错误），日志含 traceId。
- F4 JWT 认证：`POST /api/auth/login` 签发 token；`/api/auth` 白名单放行；其余接口校验失败返回 `code: 40100`。
- F5 示例资源：`GET /api/users/me`（需登录，返回当前用户 DTO），验证认证链路。
- F6 健康检查：`GET /health` 返回 `{ code: 0, message: "success", data: "healthy" }`，无需认证。
- F7 可观测性：NLog（控制台 + 滚动文件，dev=Info / prod=Warning）对接 `ILogger<T>`；OpenTelemetry Tracing + Metrics 接入（dev 默认不导出）。
- F8 环境配置：dev / prod 通过 `ASPNETCORE_ENVIRONMENT` 区分；数据库连接串、JWT 密钥等敏感配置从环境变量读取。
- F9 后端单元测试：统一响应、异常处理、登录用例 RequestHandler 的公共方法单测通过。
- F10 前端项目骨架：Vite + Vue 3 + TS + Arco Design + Pinia + vue-router + Axios，目录结构符合前端规则第 2 节。
- F11 前端接口层：`src/api/` 统一封装，拦截器解包 `{ code, message, data }`，`code !== 0` 时 `Message.error` 提示；请求自动附加 `Authorization: Bearer <token>`；收到 40100 清凭证并跳转登录页（防重复跳转）。
- F12 前端页面：登录页（调用登录接口）、首页（展示当前登录用户，需登录态，未登录跳转登录页）。
- F13 后端用例统一入口：用例由请求标记 `IRequest<TResponse>` 与处理器 `IRequestHandler<TRequest,TResponse>` 构成；Controller 只依赖自研中介 `IMediator`（`App.Core/Mediation` 实现，简化版 MediatR）经 `Send(Request)` 触发用例，不直接依赖具体 Handler（规范见后端规则 §4.2）。

## 4. 验收标准

1. `cd backend && dotnet build` 成功（net8.0）；`dotnet test` 全部通过。
2. `cd backend/src/App.Api && dotnet run` 启动后，`GET /health` 返回 `{ "code": 0, "message": "success", "data": "healthy" }`。
3. `POST /api/auth/login` 传入规格约定的测试账号返回 `code: 0` 与 JWT；未带/错误 token 访问 `GET /api/users/me` 返回 `code: 40100`；携带有效 token 返回当前用户 DTO。
4. `cd frontend && npm install && npm run dev` 启动后，登录页可成功登录并跳转首页，首页展示当前用户名；退出登录后访问首页被重定向到登录页。
5. `cd frontend && npm run build` 构建成功，无 TypeScript 错误。
6. 代码与 `AGENTS.md`、backend / frontend 专项规则一致（目录结构、命名、注释语言等）。

## 5. 范围外（不做）

- 真实数据库建库建表：脚手架阶段 `AppDbContext` 先空实现（不挂实体），PostgreSQL 连接串从环境变量读取；登录账号使用内存示例仓储（`InMemoryUserRepository`），首个业务功能接入真实数据表。
- 用户注册、密码加密存储、权限角色体系。
- CI/CD、Docker 化部署（见 AGENTS.md 第 10 节待补充清单）。
- 前端单元测试（按前端规则走联调集成验证）。

---
created: 2026-09-09
updated: 2026-09-09
---

# 需求规格：API 文档 Swagger（api-swagger）

## 1. 背景

后端脚手架（project-scaffold）已提供登录、当前用户、健康检查等接口，但缺少接口文档。前端联调与后续功能开发只能依赖 `specs/*/design.md` 中的接口表格，无法在线浏览、调试接口，联调成本偏高。

## 2. 目标

1. 后端接入 Swagger（OpenAPI 3.0 文档 + Swagger UI），dev 环境开箱即用，可直接在线调试现有全部接口。
2. Swagger 文档包含 JWT Bearer 安全方案：UI 提供 Authorize 入口，登录后接口可在文档页直接调用。
3. 接口说明（Controller 动作与请求/响应模型的 XML 注释）进入 Swagger 文档，替代人工维护的接口表格。
4. 生产环境不暴露 Swagger（UI 与 JSON 均不可访问），避免泄露 API 结构。

## 3. 功能点

- F1 Swagger 接入：`App.Api` 引入 Swashbuckle（Swashbuckle.AspNetCore），生成 OpenAPI 文档（key `v1`），UI 默认入口 `/swagger`、JSON 位于 `/swagger/v1/swagger.json`。
- F2 环境开关：仅 `ASPNETCORE_ENVIRONMENT=Development` 启用 Swagger（UI + JSON）；Production（含测试宿主默认）不注册、不映射。
- F3 安全方案：定义 JWT Bearer 安全方案（scheme `Bearer`，`authorizationCode` 留空，经 UI 右上角 Authorize 粘贴 token）；受保护接口在文档中标注 401 响应；`[AllowAnonymous]` 标注的接口（登录、健康检查）在 UI 上**不显示锁图标、不带 security 要求**，与真实认证语义一致。
- F4 XML 注释：`App.Api` 与 `App.Core` 均开启 XML 文档文件生成（公共成员缺注释以 NoWarn 1591 抑制，不破坏既有代码），`AddSwaggerGen` 引入两个项目 XML 注释（Controller 动作 + Request/Response 模型属性注释完整进文档）。
- F5 dev 匿名可访问：dev 环境下访问 `/swagger`、`/swagger/*` 不触发 40100 统一响应、可匿名打开；实现方式为 Swagger 端点注册在认证/授权**之前**（命中即短路，不进入认证管道），而非在认证层做路径特判。
- F6 可测试：新增集成测试覆盖 F1~F5 的用户可感知行为（见 design.md §3）。

## 4. 验收标准

1. `cd backend && dotnet build` 成功；`dotnet test` 全部通过（含新增 Swagger 集成测试）。
2. `dotnet run`（Development）启动后，浏览器打开 `http://localhost:5080/swagger` 可看到 Swagger UI，列出 `/api/auth/login`、`/api/users/me`、`/health` 三个接口。
3. UI 上 Authorize 粘贴登录接口签发的 token 后，在文档页调用 `GET /api/users/me` 返回 `code: 0`。
4. `GET /swagger/v1/swagger.json` 匿名可访问，含 `Bearer` securityScheme 与受保护接口的 401 响应定义。
5. Production 环境下 `/swagger` 与 `/swagger/v1/swagger.json` 均不可访问。
6. Swagger UI 中 `[AllowAnonymous]` 接口（`/api/auth/login`、`/health`）无锁图标（swagger.json 中对应 operation 无 `security` 字段）；`/api/users/me` 保留锁图标。

## 5. 范围外（不做）

- 不引入 Scalar / Scalar.AspNetCore 等替代 UI；不改写既有接口行为（HTTP 200 + 业务码约定不变）。
- 不为 `App.Core` 单独开启 XML 文档文件（其注释经程序集元数据已可被 Swagger 读取）。
- 不改前端；前端联调方式由"查规格文档"自然过渡为"开 Swagger UI"，无代码改动。

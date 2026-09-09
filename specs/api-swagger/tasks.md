# 任务清单：API 文档 Swagger（api-swagger）

## 后端

- [x] B1 `App.Api` 引入 `Swashbuckle.AspNetCore` 6.9.0；`App.Api` / `App.Core` 开启 XML 文档文件（NoWarn 1591）
- [x] B2 `Program.cs` 仅 dev 环境 `AddSwaggerGen`（OpenAPI Info、双 XML 注释、Bearer 安全方案、全局 SecurityRequirement）+ 管道 `UseSwagger` / `UseSwaggerUI`（置于认证/授权之前）
- [x] B3 Swagger 端点注册在认证/授权之前（dev 下命中即短路、匿名可访问，无需 `JwtBearerExtensions` 路径特判）；prod 下中间件未注册，`/swagger*` 回落认证挑战返回 40100
- [x] B4 Controller 响应标注：`AuthController.Login` / `HealthController.GetHealth` / `UsersController.Me` 补 `[ProducesResponseType]`（含受保护接口的 401 语义标注）
- [x] B5 新增 `SwaggerIntegrationTests`（DevFactory + ProductionFactory）：dev 下 swagger.json / UI 可匿名访问、Bearer 安全方案存在、受保护接口标注 401、带 token 调 `/api/users/me` 成功；Production 下 `/swagger/v1/swagger.json` 返回 40100 且非 OpenAPI 文档
- [x] B6 `dotnet build`（0 警告 0 错误）与 `dotnet test`（28 通过）全部通过；dev 启动实测 `/swagger` UI 可用（UI 200、swagger.json 含 Bearer 方案与三个接口路径）

# 设计规格：API 文档 Swagger（api-swagger）

## 1. 技术选型

| 项 | 选择 | 版本 |
|---|---|---|
| 包 | `Swashbuckle.AspNetCore`（合并包，含 Swashbuckle.SwaggerGen + SwaggerUI） | 6.9.0（兼容 net8.0 的稳定线） |
| 文档 key | `v1` | — |
| 入口 | UI `/swagger`（默认 index.html 指向 `v1/swagger.json`）；JSON `/swagger/v1/swagger.json` | — |

选 Swashbuckle（而非 .NET 8 实验性的 `Microsoft.AspNetCore.OpenApi`）：UI 与 Bearer 安全方案开箱即用，成熟稳定。

## 2. 后端设计

### 2.1 环境开关（Program.cs）

- 仅当 `builder.Environment.IsDevelopment()` 为真时：
  - `builder.Services.AddSwaggerGen(...)`（注册配置）；
  - 管道 `app.UseSwagger()` + `app.UseSwaggerUI()`，**置于 `UseRouting` 之后、`UseAuthentication` / `UseAuthorization` 之前**（dev 启动后 `/swagger` 可达，且 Swagger 端点命中即短路、不进入认证管道）。
- 非 dev（Production）：不注册、不映射，`/swagger*` 路径回落到认证挑战，按全站约定返回 `code: 40100`（HTTP 200），不泄露任何 API 结构。
- 开关判断集中在 `Program.cs` 两个 `if` 块（服务注册 + 管道）内，不散落多处。
- **管道顺序要点**：`UseSwagger` / `UseSwaggerUI` 注册端点（`/swagger/v1/swagger.json`、`/swagger/index.html` 等）后，dev 下请求命中端点即在认证**之前**返回，无需任何"放行"处理；`/swagger`（无尾段）由 UI 中间件 302 到 `/swagger/index.html`。

### 2.2 SwaggerGen 配置

```csharp
var apiXmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
var coreXmlFile = $"{typeof(LoginRequest).Assembly.GetName().Name}.xml";
builder.Services.AddSwaggerGen(options =>
{
    // 引入 Controller 动作与 Request/Response 模型的 XML 注释（两个文件，见 §2.4）
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, apiXmlFile));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, coreXmlFile));
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "App API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "粘贴登录接口签发的 JWT（不带 Bearer 前缀亦可）",
    });
    // 不使用文档级 AddSecurityRequirement（其经 IDocumentFilter 写入文档顶层 security，作用于全部 operation，
    // 无法区分匿名接口；且 M.O.S 序列化会跳过空列表，operation 级无法以 security: [] 覆盖文档级）
    // 改为 Filter 按 [AllowAnonymous] 白名单语义逐 operation 标注（见 SwaggerSecurityOperationFilter）
    options.OperationFilter<SwaggerSecurityOperationFilter>();
});
```

- **security 标注策略（operation 级，替代文档级全局）**：`App.Api/Swagger/SwaggerSecurityOperationFilter`（实现 `Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter`）检测动作方法或其控制器类型上的 `AllowAnonymousAttribute`——命中（登录、健康检查）则不加 security（UI 不显示锁图标），其余接口（含仅靠 `FallbackPolicy` 默认要求登录的）显式添加 operation 级 Bearer security（UI 显示锁图标）。与认证管道白名单共用同一 `[AllowAnonymous]` 特性标注，文档与真实认证语义严格一致，后续新接口无需额外维护文档层白名单。
- `OpenApiInfo.Title = "App API"`；版本号 `v1` 写死（脚手架期无多版本需求，不引入版本配置节）。

### 2.3 401 响应标注

- `UsersController.Me` 动作追加 `[ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDto>))]` 与 `[ProducesResponseType(401)]`。
- 说明：本项目全站 HTTP 200 + 业务码约定，401 响应在 Swagger 中仅为**语义标注**；401 的说明文案（"实际为 HTTP 200 + `code: 40100`"）写在动作 XML 注释（summary）中随文档展示——Swashbuckle 6.x 无公开的单状态码响应描述特性，MVC 的 `ProducesResponseType` 也无 `Description` 成员。
- `HealthController`、`AuthController` 补充 `[ProducesResponseType(200, ...)]` 明确 body 类型（`ApiResponse<string>` / `ApiResponse<LoginResponse>`）。

### 2.4 XML 注释

- `App.Api.csproj` 与 `App.Core.csproj` 均增加 `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `<NoWarn>$(NoWarn);1591</NoWarn>`（既有公共成员缺注释不报错，注释逐步补齐）。
- `AddSwaggerGen` 同时 `IncludeXmlComments` 两个文件（Api 的 `App.Api.xml` 在前、Core 的 `App.Core.xml` 在后），`Path.Combine(AppContext.BaseDirectory, ...)` 定位；Controller 动作注释与 Request/Response 模型属性注释完整进文档。
- XML 文件名经 `typeof(Program).Assembly.GetName().Name`（→ `App.Api`）与 `typeof(LoginRequest).Assembly.GetName().Name`（→ `App.Core`）拼接，避免硬编码。

### 2.5 Swagger 与认证的管道顺序（关键交互）

`FallbackPolicy = RequireAuthenticatedUser()` 对全部端点生效。若 Swagger 端点在认证**之后**注册，dev 下匿名访问 `/swagger` 会被挑战拦截返回 `code: 40100`，UI 打不开。

**实现结论：无需任何"路径放行"逻辑。** 只要 `UseSwagger` / `UseSwaggerUI` 注册在认证/授权**之前**，Swagger 端点（`/swagger/v1/swagger.json`、`/swagger/index.html` 等）在 dev 下命中即短路返回，请求根本不进入认证管道；`/swagger` 由 UI 中间件 302 到 `index.html`。因此：

- `JwtBearerExtensions` **不改动**（无 `IsSwaggerPath`、无 `OnForbidden`），保持"未认证统一 40100"的单一语义，避免在认证层引入路径特判。
- prod 下 Swagger 中间件未注册，`/swagger*` 请求无匹配端点，回落到认证挑战 → `code: 40100`（HTTP 200），不返回 404、不泄露结构，与全站其它未匹配受保护路径行为一致。
- 影响面：`/api/*` 与 `/health` 行为不变；认证层零特判。

### 2.6 边界与影响

| 场景 | 行为 |
|---|---|
| dev，匿名 `GET /swagger` | 302 → `/swagger/index.html`（200），Swagger UI 正常打开（端点在认证前短路） |
| dev，匿名 `GET /swagger/v1/swagger.json` | 200，OpenAPI JSON |
| dev，UI Authorize 后调 `/api/users/me` | 200 + `code: 0`，返回用户 |
| dev，未 Authorize 调 `/api/users/me` | 200 + `code: 40100`（既有行为不变） |
| prod，`GET /swagger*` | 200 + `code: 40100`（中间件未注册，回落认证挑战；不返回 OpenAPI 文档） |
| 既有集成测试（`ApiIntegrationTests.Factory`，Production） | 不受影响（Swagger 端点前置于认证，既有接口行为不变） |

## 3. 测试设计（tests/App.Tests）

新增 `SwaggerIntegrationTests`：
- **DevFactory**（`WebApplicationFactory`，Development 环境，InMemory 库）覆盖 dev 下 4 个用例；
- **ProductionFactory**（`WebApplicationFactory`，`UseEnvironment("Production")` + 进程级 `JWT__SECRET` 环境变量，InMemory 库）覆盖 prod 1 个用例。

> **环境切换说明**：`WebApplicationFactory` 下 `UseSetting("ASPNETCORE_ENVIRONMENT", ...)` 对运行时环境**不生效**（经诊断：既有 `ApiIntegrationTests.Factory` 虽标注 Production 但实际运行于 Development）。切换环境须用 `builder.UseEnvironment(...)`；且 Program 早期的 JWT 密钥校验读取**进程环境变量**（非 in-memory 配置），故 Production 用例需先 `Environment.SetEnvironmentVariable("JWT__SECRET", ...)`（对 Development 用例无副作用，其 dev 兜底逻辑不读该变量）。

| 用例 | 断言 |
|---|---|
| SwaggerJson_dev环境_可匿名访问且包含Bearer安全方案 | `GET /swagger/v1/swagger.json` 200；`components.securitySchemes.Bearer.scheme == "bearer"` |
| SwaggerJson_dev环境_受保护接口标注401 | `/api/users/me` 的 `responses` 含 `401` |
| SwaggerJson_dev环境_匿名接口不带security要求 | `/api/auth/login`（POST）与 `/health`（GET）的 operation **无** `security` 字段；`/api/users/me` 保留 `security: [Bearer]` |
| SwaggerUi_dev环境_可匿名打开 | `GET /swagger/index.html` 200 且内容含 `swagger-ui` |
| Swagger_dev环境_带token调用受保护接口_返回用户 | 登录取 token → `GET /api/users/me`（带 Authorization 头）200 + `code: 0` |
| Swagger_Production环境_文档不可访问 | `GET /swagger/v1/swagger.json` 200 + `code: 40100`，且响应非 OpenAPI 文档（无 `openapi` 字段） |

命名与既有 `{类名}_{方法名}_{场景}` 中文风格保持一致。

## 4. 技术决策

| 决策 | 理由 |
|---|---|
| Swashbuckle.AspNetCore 6.9.0 而非 ASP.NET Core OpenApi 包 | UI / Bearer 方案开箱即用；OpenApi 包仅出 JSON 需另配 UI，且 8.0 下属实验特性 |
| 环境开关放在 Program.cs（注册 + 管道两处 if） | 最小改动面；prod 零注册零暴露，不引入配置节 |
| Swagger 端点注册在认证/授权**之前**，不做任何路径放行 | 端点命中即短路，请求不进入认证管道；`[AllowAnonymous]` 无法作用于 Swashbuckle 内部 endpoint，路径特判会让认证层出现例外逻辑。前置注册是最简且无副作用的方案 |
| prod 下 `/swagger*` 回落认证挑战返回 40100（而非 404） | 与全站"统一 40100、HTTP 200"约定一致，不泄露结构；40100 本身不暴露 swagger 存在（与任意未知受保护路径一致） |
| `App.Core` 也生成 XML 文档 | 模型注释完整进 Swagger，零运行时成本 |
| 401 仅作文档标注（summary 注明实际 HTTP 200 + 40100） | 全站 HTTP 200 约定（AGENTS.md §4.1）优先于 Swagger 状态码语义；Swashbuckle 6.x 无单状态码响应描述特性 |
| security 用 `IOperationFilter` 逐 operation 标注（非文档级 `AddSecurityRequirement`） | 文档级 security 作用于全部 operation，匿名接口也会显示锁图标（M.O.S 序列化跳过空列表，operation 级无法覆盖）；operation 级标注与认证管道共用 `[AllowAnonymous]` 单一事实源，新增匿名接口零维护成本；Filter 仅 dev 生效（随 AddSwaggerGen 注册），对 prod 与运行时零影响 |

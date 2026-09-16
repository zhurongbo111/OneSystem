---
created: 2026-09-10
updated: 2026-09-16
---

# 设计规格：用户管理（user-management）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 本功能是脚手架后**首个接入真实 PostgreSQL** 的功能，按后端规则 §4"每 API 一个用例"组织。

## 1. 总体设计

```
管理员（前端 /users 页面）
  → UsersController（仅参数绑定 + IMediator.Send）
    → App.Core/Features/Users/<Action>/*RequestHandler
      → IUserRepository（App.Core 接口）→ UserRepository（App.Infrastructure，EF Core）
        → AppDbContext → PostgreSQL（Users 表）

登录日志（前端 /login-logs 页面）
  → LoginLogsController → Features/LoginLogs/GetLoginLogs/GetLoginLogsRequestHandler
    → IUserLoginLogRepository → UserLoginLogRepository → AppDbContext → PostgreSQL（UserLoginLogs 表）

登录：AuthController.Login → LoginRequestHandler
  → IUserRepository + PasswordHasher + TokenService（校验与签发）
  → IUserRepository.UpdateLastLoginAsync + IUserLoginLogRepository.AddAsync（成功登录留痕）
  → IClientInfo（客户端 IP / User-Agent）
```

- 数据访问从"内存示例仓储"切换为 EF Core + PostgreSQL。
- 密码统一经 `PasswordHasher`（PBKDF2-HMAC-SHA256）哈希后入库；登录时校验哈希。
- 用户表为脚手架后第一张真实表，通过 EF Core Migrations 建表；登录日志表 `UserLoginLogs` 在同一次初始迁移中建立。
- **登录日志独立成表**（不从 `Users.LastLoginAt` 推导）：本期只记录成功登录，作为登录审计轨迹。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/User.cs`（改造）

> 时间字段统一用 `DateTimeOffset`（实体 / DTO / 仓储签名 / 请求入参），避免 `DateTime.Kind` 在序列化与跨层传递中丢失造成时区歧义；Npgsql 仍映射 `timestamptz`，表结构不变（见 §5）。

| 字段 | C# 类型 | 说明 |
|---|---|---|
| `Id` | `Guid` | 主键（PG `uuid`） |
| `Username` | `string` | 登录名，唯一，创建后不可改 |
| `PasswordHash` | `string` | 密码哈希（PBKDF2 编码串），禁止明文 |
| `DisplayName` | `string` | 显示名 |
| `Email` | `string?` | 邮箱，可空；非空时唯一 |
| `Phone` | `string?` | 手机号，可空；非空时唯一 |
| `Status` | `UserStatus` | 用户状态（`Enabled=1` / `Disabled=0`） |
| `LastLoginAt` | `DateTimeOffset?` | 最近登录时间；**列表展示用的冗余字段**，登录明细见 `UserLoginLogs`（§2.3） |
| `CreatedAt` | `DateTimeOffset` | 创建时间 |
| `UpdatedAt` | `DateTimeOffset` | 更新时间 |
| `CreatedBy` | `Guid?` | 创建人用户 id（操作者为系统种子时为空） |
| `UpdatedBy` | `Guid?` | 更新人用户 id |

新增 `App.Core/Entities/UserStatus.cs`：

```csharp
public enum UserStatus { Disabled = 0, Enabled = 1 }
```

### 2.2 表结构 `Users`（PostgreSQL）

| 列 | 类型 | 约束 |
|---|---|---|
| `Id` | `uuid` | PK |
| `Username` | `varchar(50)` | NOT NULL，唯一索引 |
| `PasswordHash` | `text` | NOT NULL |
| `DisplayName` | `varchar(50)` | NOT NULL |
| `Email` | `varchar(100)` | NULL，唯一索引（`IS NOT NULL` 部分索引） |
| `Phone` | `varchar(20)` | NULL，唯一索引（`IS NOT NULL` 部分索引） |
| `Status` | `smallint` | NOT NULL，默认 `1` |
| `LastLoginAt` | `timestamptz` | NULL |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |
| `CreatedBy` | `uuid` | NULL |
| `UpdatedBy` | `uuid` | NULL |

- 无 `IsDeleted`：本期只禁用不删除。
- 时间列（`LastLoginAt` / `CreatedAt` / `UpdatedAt` / `LoginAt`）对应 C# `DateTimeOffset`，映射为 `timestamptz`，库内按 UTC 存储；接口入参 / 出参保留偏移量，避免 `DateTime.Kind` 丢失导致的时区歧义。
- 实体配置 `App.Infrastructure/Persistence/Configurations/UserConfiguration.cs`（`IEntityTypeConfiguration<User>`，表名 `Users`，字段长度、必填、索引）。
- 唯一性说明：数据库层 `Username` 唯一索引大小写敏感；应用层 `ExistsByUsernameAsync` / `ExistsByEmailAsync` 额外做**大小写不敏感**存在性判断（`ToLower` 比较），即 `alice` 与 `ALICE` 视为重复；`Email` / `Phone` 仅在非空时判定。
- 入参归一化：`Username` 去首尾空白后按原样存储；`Email` 去空白并统一转小写存储（唯一性忽略大小写）；`Phone` 去首尾空白。

### 2.3 实体 `App.Core/Entities/UserLoginLog.cs` 与表 `UserLoginLogs`

实体字段：

| 字段 | C# 类型 | 说明 |
|---|---|---|
| `Id` | `Guid` | 主键（PG `uuid`） |
| `UserId` | `Guid` | 登录用户 id |
| `Username` | `string` | 登录名**快照**（审计用） |
| `DisplayName` | `string` | 显示名**快照**（登录时点） |
| `LoginAt` | `DateTimeOffset` | 登录时间 |
| `IpAddress` | `string?` | 客户端 IP；取不到时为 `null` |
| `UserAgent` | `string?` | 客户端 User-Agent；超长截断，取不到时为 `null` |

表结构 `UserLoginLogs`（PostgreSQL）：

| 列 | 类型 | 约束 |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | NOT NULL，FK → `Users(Id)`（无级联删除：用户不可删除） |
| `Username` | `varchar(50)` | NOT NULL |
| `DisplayName` | `varchar(50)` | NOT NULL |
| `LoginAt` | `timestamptz` | NOT NULL |
| `IpAddress` | `varchar(64)` | NULL |
| `UserAgent` | `varchar(512)` | NULL |

- 索引：`IX_UserLoginLogs_LoginAt`（`LoginAt DESC`，列表默认排序）；`IX_UserLoginLogs_UserId`（外键列，PG 不自动创建）。
- **不设"结果 / 失败原因"列**：本期只记录成功登录，失败尝试不入库；后续若需记录失败，另行规格扩展。
- 无软删除、无更新：日志只追加（append-only），不提供修改 / 删除接口。
- 实体配置 `App.Infrastructure/Persistence/Configurations/UserLoginLogConfiguration.cs`。
- `Username` / `DisplayName` 冗余快照：日志自包含（查询免 join），且审计语义要求记录"当时的登录名 / 显示名"。

### 2.4 EF Core 与迁移

- `AppDbContext` 增加 `DbSet<User> Users`、`DbSet<UserLoginLog> UserLoginLogs`；`OnModelCreating` 应用 `ApplyConfigurationsFromAssembly`。
- 迁移文件放 `App.Infrastructure/Persistence/Migrations/`，仍只需**一个** `InitialCreate`（实现尚未开始，`Users` 与 `UserLoginLogs` 在同一次初始迁移中创建）。
- 生成命令：`dotnet ef migrations add InitialCreate -p src/App.Infrastructure -s src/App.Api`（需为 `App.Api` 增加 `Microsoft.EntityFrameworkCore.Design` 包引用，并安装 `dotnet-ef` 工具）。
- **不**用 `HasData` 写入管理员（密码哈希含随机盐，无法稳定固化为迁移常量），改为启动时按需种子（见 2.5）。

### 2.5 启动初始化与种子 `App.Infrastructure/Persistence/DatabaseInitializer.cs`

- 静态方法 `InitializeAsync(IServiceProvider services, CancellationToken)`，在 `Program` 的 `app.Build()` 后、`app.Run()` 前，创建 scope 调用一次。
- 逻辑：
  1. `db.Database.IsRelational()` 为真时执行 `MigrateAsync()`（应用迁移）；非关系型（集成测试 InMemory）跳过。
  2. 若 `Users` 表为空，则创建内置管理员：`Username=admin`、`DisplayName=管理员`、`Status=Enabled`、`PasswordHash=PasswordHasher.Hash("admin123")`、`CreatedAt/UpdatedAt=UtcNow`、`CreatedBy/UpdatedBy=null`。
- 运行于**所有环境**：无注册入口，缺少引导管理员会导致系统被锁死。
- 自动迁移仅在 `IServiceProvider` 可解析环境且为 `Development` 时执行；非 dev（含生产）由运维执行 `dotnet ef database update`，种子仍按"表空才建"执行 → 若生产数据库不可达，启动将失败（属真实依赖，符合预期）。
- 集成测试使用被替换为 **InMemory** 的 `AppDbContext`：跳过迁移，执行种子 → 既有 `admin/admin123` 登录用例继续通过。

## 3. 后端设计

### 3.1 密码哈希 `App.Core/Auth/PasswordHasher.cs`（新增，无状态技术组件）

- 算法：PBKDF2-HMAC-SHA256，`iterations = 100000`，`salt = 16 bytes`，`key = 32 bytes`。
- 编码格式：`PBKDF2$<iterations>$<saltBase64>$<hashBase64>`。
- 方法：
  - `string Hash(string password)`
  - `bool Verify(string password, string storedHash)` —— 使用 `CryptographicOperations.FixedTimeEquals` 定长比较；格式非法返回 `false`。
- 在 `App.Core/DependencyInjection.AddCore` 注册为 `Singleton`（对齐既有 `TokenService`）。

### 3.2 仓储接口（扩展 / 新增）

`App.Core/Abstractions/IUserRepository.cs`：

| 方法 | 说明 |
|---|---|
| `Task<User?> GetByIdAsync(Guid id, ...)` | 按 id 查询 |
| `Task<User?> GetByUsernameAsync(string username, ...)` | 按用户名查询（登录用；已存在） |
| `Task<bool> ExistsByUsernameAsync(string username, ...)` | 用户名是否存在 |
| `Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId, ...)` | 邮箱是否存在（编辑时排除自身） |
| `Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeUserId, ...)` | 手机号是否存在（编辑时排除自身） |
| `Task<(IReadOnlyList<User> Items, int Total)> GetPagedAsync(string? keyword, UserStatus? status, int page, int pageSize, ...)` | 分页 + 关键词 + 状态筛选 |
| `Task AddAsync(User user, ...)` | 新增并持久化 |
| `Task UpdateAsync(User user, ...)` | 更新并持久化 |
| `Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, ...)` | 仅更新最近登录时间（登录成功后，不触碰 `UpdatedAt`） |

新增 `App.Core/Abstractions/IUserLoginLogRepository.cs`：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(UserLoginLog log, ...)` | 追加一条登录日志并持久化 |
| `Task<(IReadOnlyList<UserLoginLog> Items, int Total)> GetPagedAsync(string? username, DateTimeOffset? startTime, DateTimeOffset? endTime, int page, int pageSize, ...)` | 分页查询，`LoginAt DESC` 排序 |

- 单一仓储的一次写操作由该仓储方法自身保证持久化（内部 `SaveChangesAsync`）。登录成功时"更新 `LastLoginAt`（`IUserRepository`）+ 追加登录日志（`IUserLoginLogRepository`）"是**跨仓储的两次写**，按后端规则 §4.4 使用 `IUnitOfWork` 包成同一事务：`BeginTransactionAsync` → 两次写 → `CommitAsync`，异常时 `RollbackAsync` 并抛出（理由见 §5）。非关系型提供程序（集成测试 InMemory）跳过显式事务。
- 实现：`App.Infrastructure/Repositories/UserRepository.cs`、`UserLoginLogRepository.cs`（EF Core，`AsNoTracking` 用于只读查询）；**删除** `InMemoryUserRepository` 及 `AddInfrastructure` 中对应注册。
- 用户分页排序：`CreatedAt DESC`；关键词对 `Username` / `DisplayName` 做 `Contains`（`ILIKE` 语义，忽略大小写）。
- 登录日志筛选：`Username` 做 `Contains`（忽略大小写）；时间范围为闭区间（`LoginAt >= startTime` 且 `LoginAt <= endTime`，仅对非空参数生效）。入参为 `DateTimeOffset`（前端给出 UTC ISO 串），偏移量显式，**无需再做 UTC 归一化**。

### 3.3 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40002 | `UsernameExists` | 用户名已存在 |
| 40003 | `EmailExists` | 邮箱已被使用 |
| 40004 | `PhoneExists` | 手机号已被使用 |
| 40005 | `UserDisabled` | 账号已被禁用（登录时） |
| 40006 | `CannotDisableSelf` | 不能禁用当前登录账号 |

> `40001 LoginFailed`（用户名或密码错误）、`40400 NotFound`、`40000 Validation`、`40100 Unauthorized` 复用全局定义。
> 登录日志为只读查询，**不新增业务码**；参数不合法统一走全局 `40000`。

### 3.4 用例与接口

每个 API 对应 `App.Core/Features/<模块>/<Action>/`（Request / RequestValidator / RequestHandler / Response），出参复用共享模型。

| 接口 | 方法 | 认证 | 请求 | `data` 响应 | 错误码 |
|---|---|---|---|---|---|
| `/api/users` | GET | 是 | Query：`keyword?`、`status?`、`page`、`pageSize` | `PagedResult<UserListItemDto>` | 40000 |
| `/api/users` | POST | 是 | `CreateUserRequest { username, displayName, email?, phone?, password }` | `UserDetailDto` | 40000 / 40002 / 40003 / 40004 |
| `/api/users/{id:guid}` | GET | 是 | — | `UserDetailDto` | 40400 |
| `/api/users/{id:guid}` | PUT | 是 | `UpdateUserRequest { displayName, email?, phone? }` | `UserDetailDto` | 40000 / 40003 / 40004 / 40400 |
| `/api/users/{id:guid}/status` | PUT | 是 | `UpdateUserStatusRequest { status }` | `UserDetailDto` | 40000 / 40400 / 40006 |
| `/api/users/{id:guid}/password` | PUT | 是 | `ResetPasswordRequest { newPassword }` | `null` | 40000 / 40400 |
| `/api/login-logs` | GET | 是 | Query：`username?`、`startTime?`、`endTime?`、`page`、`pageSize` | `PagedResult<LoginLogListItemDto>` | 40000 |
| `/api/users/me` | GET | 是 | — | `UserDto`（既有，保持不变） | 40100 |
| `/api/auth/login` | POST | 否 | `LoginRequest`（既有） | `LoginResponse`（既有） | 40000 / 40001 / 40005 |

用例目录与职责：

| 目录 | 说明 |
|---|---|
| `Features/Users/GetUsers/` | 分页列表；筛选 + 映射 `UserListItemDto` |
| `Features/Users/GetUserById/` | 详情；不存在抛 `40400` |
| `Features/Users/CreateUser/` | 新增；锁定唯一性（用户名 / 邮箱 / 手机号），哈希密码，写审计字段 |
| `Features/Users/UpdateUser/` | 编辑；校验存在性 + 邮箱/手机号唯一（排除自身）；`email` / `phone` 遵循 `AGENTS.md` §4.5 全量覆盖语义（缺字段 / 空串清空落 `null`） |
| `Features/Users/UpdateUserStatus/` | 启停；禁止禁用当前登录账号（`ICurrentUser.Id == 目标 id` 且 `status == Disabled` → `40006`） |
| `Features/Users/ResetPassword/` | 重置密码；哈希后更新 |
| `Features/Users/GetCurrentUser/` | 既有，保持不变（基于 claims，不查库） |
| `Features/LoginLogs/GetLoginLogs/` | 登录日志分页查询（只读）；登录名 + 时间范围筛选，`LoginAt DESC` |

共享模型：

- `UserDto`（既有，登录 / 当前用户用）：`id, username, displayName`。
- `UserListItemDto`（新增）：`id, username, displayName, email, phone, status, lastLoginAt, createdAt`。
- `UserDetailDto`（新增）：`id, username, displayName, email, phone, status, lastLoginAt, createdAt, updatedAt`。
- `LoginLogListItemDto`（新增，放 `Features/LoginLogs/`）：`id, userId, username, displayName, loginAt, ipAddress, userAgent`。
- `PagedResult<T>`（新增，放 `App.Core/Responses/`）：`items, total, page, pageSize`（`AGENTS.md` §4.3）。

### 3.5 校验规则（FluentValidation，仅格式层）

字段约束（长度 / 正则 / 密码区间）**只在一处定义**：`App.Core/Entities/UserFieldConstraints.cs` 常量。EF 实体配置（`HasMaxLength`）与各 `RequestValidator` 均引用该常量，保证"校验与数据库约束一致"，禁止在 Validator 中硬编码长度或重复正则。

`UserFieldConstraints`：

| 常量 | 值 |
|---|---|
| `UsernameMinLength` / `UsernameMaxLength` | 3 / 50 |
| `UsernamePattern` | `^[a-zA-Z0-9_]{3,50}$` |
| `DisplayNameMaxLength` | 50 |
| `EmailMaxLength` | 100 |
| `PhoneMaxLength` | 20 |
| `PhonePattern` | `^1[3-9]\d{9}$` |
| `PasswordMinLength` / `PasswordMaxLength` | 6 / 32 |
| `IpAddressMaxLength` | 64 |
| `UserAgentMaxLength` | 512 |

| 请求 | 规则 |
|---|---|
| `LoginRequest` | `username` 必填、≤50；`password` 必填、6–32（与创建 / 重置同一区间，避免同字段规则分叉） |
| `CreateUserRequest` | `username` 必填、3–50 位、`^[a-zA-Z0-9_]{3,50}$`；`displayName` 必填、≤50；`email` 选填但合法且 ≤100；`phone` 选填、`^1[3-9]\d{9}$`；`password` 必填、6–32 位 |
| `UpdateUserRequest` | `displayName` 必填、≤50；`email` 选填合法且 ≤100；`phone` 选填、合法 |
| `UpdateUserStatusRequest` | `status` 必须为 `0` 或 `1` |
| `ResetPasswordRequest` | `newPassword` 必填、6–32 位（与 `CreateUserRequest.password` 同区间） |
| `GetUsersRequest` | `page ≥ 1`；`pageSize` 1–100；`status` 为空或 0/1；`keyword` ≤50（对齐 `Username` / `DisplayName` 长度） |
| `GetLoginLogsRequest` | `page ≥ 1`；`pageSize` 1–100；`username` ≤50（对齐 `UserLoginLogs.Username`）；`startTime` / `endTime` 可空，两者同时提供时 `startTime <= endTime` |

- 存在性 / 唯一性等需查库的约束一律在 `RequestHandler` 内判断（后端规则 §4.1）。
- `password` 是明文入参（不入库、无对应表列），其 6–32 区间属业务规则，同样收敛到 `UserFieldConstraints`，使登录 / 创建 / 重置三处一致。
- 一致性由单测守护（`FieldValidationConsistencyTests`）：EF 模型实际 `HasMaxLength` 必须等于常量，且各 Validator 的"边界值通过 / 越界拒绝"行为一致。

### 3.6 登录适配 `Features/Auth/Login/LoginRequestHandler.cs`（改造）

1. `GetByUsernameAsync(trim)`；不存在或 `PasswordHasher.Verify` 失败 → `40001`（不区分用户名/密码错误，**不写日志**）。
2. `Status == Disabled` → `40005`（**不写日志**）。
3. 取 `now = DateTimeOffset.UtcNow`，`UpdateLastLoginAsync(user.Id, now)`。
4. 追加登录日志：`AddAsync(new UserLoginLog { Id = Guid.NewGuid(), UserId, Username, DisplayName, LoginAt = now, IpAddress = IClientInfo.IpAddress, UserAgent = IClientInfo.UserAgent })`。
5. 返回 `LoginResponse { token, user: UserDto }`（`UserDto` 由实体映射）。

- 新增依赖：`IUserLoginLogRepository`、`IUnitOfWork`、`IClientInfo`（见 §3.8）。
- 步骤 3、4 为跨仓储写操作，由 `IUnitOfWork` 包成同一事务（`Begin` → 写 → `Commit`，异常 `Rollback`），见 §3.2 / §5。
- 失败分支均在写日志之前抛出，因此"只记录成功登录"由流程顺序天然保证（无需结果判断）。

### 3.7 Controller 与 DI 注册

- `App.Api/Controllers/UsersController.cs`：新增上述动作，全部经 `IMediator.Send`；路由参数用 `{id:guid}` 约束，避免与 `me` 冲突。
- 新增 `App.Api/Controllers/LoginLogsController.cs`：`[Route("api/login-logs")]`，`GET` 动作经 `IMediator.Send` 返回 `ApiResponse<PagedResult<LoginLogListItemDto>>`。
- `App.Core/DependencyInjection.AddCore`：注册新增的 `IRequestHandler<,>` 与对应 `IValidator<>`；注册 `PasswordHasher`。
- `App.Infrastructure.DependencyInjection.AddInfrastructure`：`IUserRepository → UserRepository`、`IUserLoginLogRepository → UserLoginLogRepository`（`InMemoryUserRepository` 移除）。
- `Program`：在既有 `AddHttpContextAccessor()` 之后，与 `ICurrentUser` 相邻注册 `Scoped<IClientInfo, ClientInfoAccessor>()`。

### 3.8 客户端信息抽象 `App.Core/Abstractions/IClientInfo.cs`（新增）

- 接口：
  - `string? IpAddress { get; }`
  - `string? UserAgent { get; }`
- 实现 `App.Api/Http/ClientInfoAccessor.cs`（`internal sealed`，基于 `IHttpContextAccessor`，与既有 `App.Api/Authentication/CurrentUserAccessor` 同理：Handler 不直接接触 HTTP 上下文，单测可注入桩对象）：
  - `IpAddress`：`HttpContext.Connection.RemoteIpAddress?.ToString()`；无 HTTP 上下文或取不到时为 `null`。
  - `UserAgent`：`Request.Headers.UserAgent.ToString()`；超过 512 字符截断；空串按 `null` 返回。
- **不在本期解析 `X-Forwarded-For`**：未配置受信代理时该头部可被伪造；等部署形态确定后统一引入 Forwarded Headers 中间件处理。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── user.ts                     # 用户管理接口层（类型 + 请求函数）
│   └── loginLog.ts                 # 登录日志接口层（类型 + 请求函数）
└── views/
    ├── UserManagement/
    │   ├── UsersView.vue           # 用户列表页（参照 ListShowcaseView）
    │   ├── UserFormDrawer.vue      # 新增/编辑抽屉（参照 FormShowcaseView）
    │   └── UserDetailView.vue      # 用户详情页（参照 FormDetailView）
    └── LoginLogManagement/
        └── LoginLogsView.vue       # 登录日志列表页（只读，参照 ListShowcaseView）
```

### 4.2 接口层

`src/api/user.ts`：

- 类型：`UserStatus`（`0 | 1`）、`UserListItem`、`UserDetail`、`PagedResult<T>`、`UserListQuery`、`CreateUserPayload`、`UpdateUserPayload`。
- 函数：`getUsers`、`getUser`、`createUser`、`updateUser`、`updateUserStatus`、`resetUserPassword`（均经 `src/api/request.ts` 封装）。

`src/api/loginLog.ts`：

- 类型：`LoginLogListItem`（`id, userId, username, displayName, loginAt, ipAddress, userAgent`）、`LoginLogQuery`（`username?, startTime?, endTime?, page, pageSize`）。
- 函数：`getLoginLogs`（经 `src/api/request.ts` 封装）。
- 时间参数：页面所选日期范围在接口层转换为**本地当天 00:00:00 / 23:59:59 的 UTC ISO 字符串**，后端按 UTC 直接比较（保证"选今天"能查到刚产生的记录）。

### 4.3 路由与菜单（`src/router/index.ts`、`src/components/AppLayout.vue`）

- 路由（`AppLayout` 子路由，`meta.requiresAuth: true`）：
  - `/users` → `UsersView`（name `users`）。
  - `/users/detail/:id` → `UserDetailView`（name `userDetail`）。
  - `/login-logs` → `LoginLogsView`（name `loginLogs`）。
- 侧边菜单新增「用户管理」（key `users`，图标 `IconUser`）与「登录日志」（key `loginLogs`，图标 `IconHistory`）；用户新增/编辑为抽屉，不开独立路由。

### 4.4 页面交互

- **用户列表页 `UsersView.vue`**：按前端规则第 5 节实现
  - 工具条筛选行：关键词（用户名/显示名）+ 状态下拉（全部/启用/禁用）+ 搜索/重置。
  - 操作行：新增（primary，打开抽屉）、刷新、列设置。
  - 表格：序号、用户名、显示名、邮箱、手机号、状态（`a-tag`，启用绿色 / 禁用红色）、最近登录时间、创建时间、操作列（编辑、重置密码、启用/禁用、详情）。
  - 前端分页关闭、改用**服务端分页**（`page` / `pageSize`，默认 20，`pageSizeOptions [10,20,50]`）；条件变化回到第 1 页。
  - 启停用 `a-popconfirm` 确认；重置密码弹 `a-modal`（输入新密码，6–32 位校验）。
- **表单抽屉 `UserFormDrawer.vue`**：`mode: 'create' | 'edit'`
  - create：用户名、显示名、邮箱、手机号、初始密码；edit：仅显示名、邮箱、手机号（用户名只读展示）。
  - `a-form :rules` + `formRef.validate()`；提交成功 `Message.success` + 关闭 + 通知列表刷新。
  - **打开抽屉时的表单状态（强制）**：打开时先 `Object.assign(form, emptyForm())` 重置表单，再按需拉详情回填；`detailLoading` 期间所有字段 `:disabled`（用户名在 edit 下始终只读 / 禁用）。
    - 不重置的后果：表单残留上一次打开的数据，切换编辑对象时在详情返回前会显示**上一个用户**的信息（数据串台）。
    - 不回填前禁用字段的后果：用户在详情返回前输入的内容会被随后的回填**覆盖**，提交的是旧值（慢网络下必现）。
- **用户详情页 `UserDetailView.vue`**：`a-page-header`（返回列表）+ `a-descriptions`（含创建/更新时间等只读字段）；id 不存在显示 `a-result status="404"` + 返回列表。
- **登录日志页 `LoginLogsView.vue`**（**只读**）：
  - 工具条筛选行：登录名输入（模糊，placeholder「搜索登录名」）+ 登录时间范围 `a-range-picker` + 搜索 / 重置。
  - 操作行：仅刷新、列设置（无新增/编辑/删除：日志 append-only）。
  - 表格：序号、登录名、显示名、登录时间、IP、User-Agent；**服务端分页**（`page` / `pageSize`，默认 20）；筛选条件变化回到第 1 页；无匹配数据显示 `a-empty` 空状态。

## 5. 技术决策

| 决策 | 理由 |
|---|---|
| 主键用 `Guid`（PG `uuid`），DTO 仍以 `string` 暴露 | 既有 `UserDto.Id` / JWT `sub` / `ICurrentUser.Id` 均为 string，用 Guid 无需改动契约，避免 ID 枚举 |
| 用户名创建后不可改 | 用户名是登录凭据与唯一标识，保持稳定避免歧义；编辑仅改展示类字段 |
| 密码用 PBKDF2-HMAC-SHA256 自研 `PasswordHasher` | 不引入额外依赖（`Rfc2898DeriveBytes` 内置），带随机盐、定长比较；替换脚手架明文密码 |
| 只禁用不删除 | 保留历史数据与审计；无删除接口，规避关联数据风险 |
| 启动时按需种子管理员，且对所有环境生效 | 无注册入口，缺引导管理员会导致系统不可用；"表空才建"保证幂等、不覆盖已有数据 |
| 自动迁移仅 Development | 生产改库应由发布流程显式执行，避免应用启动即改结构；种子仍幂等执行 |
| 服务端分页（非前端 Mock 分页） | 用户数据来自数据库，需服务端分页；沿用 `AGENTS.md` §4.3 分页契约 |
| 列表/详情拆分 `UserListItemDto` / `UserDetailDto`，`UserDto` 仅用于认证 | 列表不需要全部字段；认证的 `UserDto` 保持精简，`GetCurrentUser` 无需查库 |
| 禁用不做 token 即时吊销 | JWT 无状态，即时吊销需黑名单 / 每请求查库，本期不做（已列入范围外） |
| 登录日志独立成表，而非只留 `Users.LastLoginAt` | 审计需要明细（每次时间 / IP / User-Agent）；`LastLoginAt` 仅作列表快速展示的冗余字段 |
| 只记录成功登录，失败尝试不入库 | 本期目标是"登录留痕"；失败原因归类与风控留待后续规格，避免过早引入结果枚举 |
| 登录日志冗余 `Username` / `DisplayName` 快照 | 日志自包含、查询免 join；审计语义要求记录"当时的登录名 / 显示名" |
| 日志表 append-only，无软删除 / 无更新接口 | 审计数据不可篡改；用户不可删除，故 FK 无需级联删除 |
| 登录的两次写（更新时间 + 写日志）用 `IUnitOfWork` 包成同一事务 | 后端规则 §4.4 要求跨仓储的写操作使用工作单元保证原子性；"更新时间"与"写日志"应同时成功或同时失败。写入失败即抛出（不吞异常），数据库故障会中断登录；`UnitOfWork` 在非关系型提供程序下跳过显式事务以兼容集成测试 |
| 客户端 IP / UA 经 `IClientInfo` 抽象获取 | 沿用 `ICurrentUser` 模式，Handler 不接触 `HttpContext`，单测可注入桩 |
| 不在本期解析 `X-Forwarded-For` | 未配置受信代理时该头部可伪造；等部署形态确定后统一由 Forwarded Headers 中间件处理 |
| 时间筛选由前端转换为 UTC ISO 串，后端不做时区推断 | 前端按本地当天 00:00:00 / 23:59:59 转 UTC 串；后端入参为 `DateTimeOffset`、偏移量显式，`timestamptz` 比较语义明确，"选今天"能覆盖刚产生的记录 |
| 时间字段统一 `DateTimeOffset`（实体 / DTO / 仓储签名 / 请求入参） | `DateTime` 的 `Kind` 在 JSON 序列化与跨层传递中易丢失，导致 UTC 与本地时间混用；`DateTimeOffset` 自带偏移量，Npgsql 仍映射 `timestamptz`（DDL 不变），且入参无需再做 UTC 归一化（原 `GetLoginLogsRequestHandler.ToUtc` 归一化逻辑随之移除）。唯一例外：`TokenService` 签发 JWT 时 `JwtSecurityToken` 仅接受 `DateTime`，故在签发边界显式 `.UtcDateTime` 转换（不引入裸 `DateTime.UtcNow`） |
| 字段约束收敛到 `UserFieldConstraints` 单一来源，EF 配置与 Validator 共用 | 原先长度 / 正则散落在各 Validator 与 EF 配置，容易分叉（如登录密码 ≤128 与创建 6–32 不一致）；统一常量后校验与库约束同源，由一致性单测守护 |

## 6. 对既有功能的影响

- **登录**：从内存仓储切到 EF 仓储 + 哈希校验 + 状态校验；`admin/admin123` 由启动种子重建，行为对集成测试保持兼容；成功登录额外写入 `UserLoginLogs`，`LoginRequestHandler` 新增 `IUserLoginLogRepository` / `IClientInfo` 两个依赖（既有 `LoginRequestHandlerTests` 需同步改造）。
- **`GetCurrentUser`**：不变（仍基于 claims）。
- **`AppDbContext` / 初始化器**：新增 `DbSet<UserLoginLog>`；`Users` 与 `UserLoginLogs` 在同一个 `InitialCreate` 迁移中建立（实现尚未开始，无需增量迁移）；初始化器只需保证用户表种子，日志表随迁移一并创建。
- **集成测试**：`AppDbContext` 被测试替换为 InMemory，初始化器需容忍非关系型提供程序（跳过迁移）；新增"登录后可查到登录日志"用例。
- **`Program`**：新增 `Scoped<IClientInfo, ClientInfoAccessor>` 注册。
- **删除** `InMemoryUserRepository`，修改 `AddInfrastructure` 注册。
- **`ErrorCode`**：追加 5 个业务码（40002–40006）；登录日志不新增码。
- **前端布局**：`AppLayout.vue` 侧边菜单新增「用户管理」「登录日志」；`/login-logs` 纳入登录守卫。

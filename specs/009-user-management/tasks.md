---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：用户管理（user-management）

> 按 `design.md` 顺序实现；每完成一项勾选。涉及后端改动的任务需补单测，涉及前端改动的任务需补 e2e（`AGENTS.md` §6）。

## 1. 后端 · 数据层

- [x] T1 扩展 `App.Core/Entities/User.cs`（`Guid` 主键、`PasswordHash`、`Email`、`Phone`、`Status`、`LastLoginAt`、审计字段）；新增 `App.Core/Entities/UserStatus.cs`
- [x] T2 新增 `App.Core/Entities/UserLoginLog.cs`（登录日志实体：`UserId`、`Username`、`DisplayName`、`LoginAt`、`IpAddress`、`UserAgent`）
- [x] T3 新增 `App.Core/Auth/PasswordHasher.cs`（PBKDF2 哈希 / 校验），并在 `AddCore` 注册单例
- [x] T4 扩展 `App.Core/Abstractions/IUserRepository.cs`（详情 / 存在性 / 分页 / 新增 / 更新 / 更新登录时间）
- [x] T5 新增 `App.Core/Abstractions/IUserLoginLogRepository.cs`（追加日志 / 分页查询）
- [x] T6 `AppDbContext` 增加 `DbSet<User>`、`DbSet<UserLoginLog>` 与 `OnModelCreating`；新增 `Persistence/Configurations/UserConfiguration.cs`、`UserLoginLogConfiguration.cs`
- [x] T7 新增 `App.Infrastructure/Repositories/UserRepository.cs`、`UserLoginLogRepository.cs`（EF 实现）；删除 `InMemoryUserRepository` 并更新 `AddInfrastructure` 注册
- [x] T8 `App.Api` 增加 `Microsoft.EntityFrameworkCore.Design` 包；生成 `InitialCreate` 迁移（`Users` + `UserLoginLogs` 表与索引）
- [x] T9 新增 `App.Infrastructure/Persistence/DatabaseInitializer.cs`（关系型先迁移；表空则种子 `admin/admin123`），在 `Program` 启动时调用

## 2. 后端 · 请求上下文抽象

- [x] T10 新增 `App.Core/Abstractions/IClientInfo.cs`（`IpAddress` / `UserAgent`）；新增 `App.Api/Http/ClientInfoAccessor.cs`（基于 `IHttpContextAccessor`）；在 `Program` 注册 `Scoped`

## 3. 后端 · 登录适配

- [x] T11 `App.Core/Errors/ErrorCode.cs` 追加业务码（40002–40006）
- [x] T12 改造 `Features/Auth/Login/LoginRequestHandler.cs`：哈希校验 + 状态校验 + 更新 `LastLoginAt` + 成功时写入登录日志；返回映射后的 `UserDto`

## 4. 后端 · 用例与接口

- [x] T13 新增共享模型：`Features/Users/UserListItemDto.cs`、`UserDetailDto.cs`、`Features/LoginLogs/LoginLogListItemDto.cs`、`App.Core/Responses/PagedResult.cs`
- [x] T14 `Features/Users/GetUsers/`（Request / Validator / Handler）
- [x] T15 `Features/Users/GetUserById/`（Request / Handler；无 Validator 或空校验）
- [x] T16 `Features/Users/CreateUser/`（Request / Validator / Handler；唯一性 + 哈希 + 审计）
- [x] T17 `Features/Users/UpdateUser/`（Request / Validator / Handler）
- [x] T18 `Features/Users/UpdateUserStatus/`（Request / Validator / Handler；禁止禁用自己）
- [x] T19 `Features/Users/ResetPassword/`（Request / Validator / Handler）
- [x] T20 `Features/LoginLogs/GetLoginLogs/`（Request / Validator / Handler；登录名 + 时间范围筛选，登录时间倒序）
- [x] T21 `UsersController` 新增 6 个动作（经 `IMediator`，`{id:guid}` 约束）；新增 `LoginLogsController`（`GET /api/login-logs`）
- [x] T22 `AddCore` 注册新增处理器与校验器

## 5. 后端 · 测试

- [x] T23 新增测试辅助（InMemory `AppDbContext` + 建库种子），供依赖 EF 仓储的 Handler 单测使用；同步改造既有 `LoginRequestHandlerTests`
- [x] T24 `PasswordHasher` 单测（哈希非明文、正确密码通过、错误密码失败、非法格式返回 false）
- [x] T25 各 `RequestHandler` 单测：正常分支 + 异常分支（不存在、唯一冲突、禁用自己、状态校验等）
- [x] T26 `LoginRequestHandler` 单测补充：登录成功写入登录日志（登录名 / 时间正确）；登录失败与禁用账号不写日志
- [x] T27 `GetLoginLogs` 单测：分页、登录名模糊筛选、时间范围筛选、`LoginAt DESC` 排序、空结果
- [x] T28 集成测试补充：列表分页/筛选、新增/编辑/启停/重置密码、禁用账号登录返回 40005、成功登录后可查询到对应登录日志
- [x] T29 `dotnet build` 与 `dotnet test` 全绿

## 6. 前端

- [x] T30 新增 `src/api/user.ts`（类型 + 6 个接口函数）
- [x] T31 新增 `src/api/loginLog.ts`（类型 + `getLoginLogs`；日期范围转 UTC ISO）
- [x] T32 新增 `src/views/UserManagement/UsersView.vue`（服务端分页列表 + 筛选 + 工具条 + 状态标签）
- [x] T33 新增 `src/views/UserManagement/UserFormDrawer.vue`（新增/编辑抽屉，含重置密码模态）
- [x] T34 新增 `src/views/UserManagement/UserDetailView.vue`（详情页，404 兜底）
- [x] T35 新增 `src/views/LoginLogManagement/LoginLogsView.vue`（只读列表 + 登录名/时间范围筛选 + 服务端分页 + 空状态）
- [x] T36 路由新增 `/users`、`/users/detail/:id`、`/login-logs`；`AppLayout.vue` 侧边菜单新增「用户管理」「登录日志」
- [x] T37 `npm run build`、`npm run lint` 通过

## 7. e2e

- [x] T38 新增 `frontend/e2e/user.spec.ts`：列表渲染与分页/筛选、新增用户、编辑用户、启停、重置密码后新密码登录、详情页返回
- [x] T39 新增 `frontend/e2e/login-log.spec.ts`：菜单进入 `/login-logs`、登录后记录存在（含 `admin`）、按登录名筛选、按时间范围（今天）筛选、分页与空状态
- [x] T40 启动 dev 前后端（含 PostgreSQL 迁移）并跑通 `npm run test:e2e`

## 8. 收尾修正（时区与校验一致性）

- [x] T41 时间字段统一 `DateTimeOffset`（`User` / `UserLoginLog` 实体、`UserListItemDto` / `UserDetailDto` / `LoginLogListItemDto`、两个仓储接口与实现、`GetLoginLogsRequest` 入参、各 Handler 与 `DatabaseInitializer`、`TokenService` 签发边界显式 `UtcDateTime`）；移除 `GetLoginLogsRequestHandler.ToUtc` 归一化逻辑
- [x] T42 迁移 `ConvertTimestampsToDateTimeOffset`：`Up` / `Down` 为空（Npgsql 下 `DateTime` 与 `DateTimeOffset` 同为 `timestamptz`，结构无变化），仅同步 EF 模型快照
- [x] T43 新增 `App.Core/Entities/UserFieldConstraints.cs` 作为字段约束单一来源；EF 配置（`UserConfiguration`、`UserLoginLogConfiguration`、`ClientInfoAccessor` UA 截断）与全部 `RequestValidator` 改为引用；`LoginRequestValidator` 密码区间由 ≤128 收敛为 6–32，手机号补 `MaximumLength`
- [x] T44 新增 `FieldValidationConsistencyTests`（EF 模型列长度 = 常量；密码区间登录 / 创建 / 重置一致；显示名 / 邮箱 / 手机号创建与编辑一致；关键词长度不超列长）；修正 `登录_错误密码_返回40001` 用例密码为合法长度
- [x] T45 `dotnet build` / `dotnet test`（93 通过）与 `npm run test:e2e`（42 通过，其中 1 条重试后通过）全绿

## 9. 收尾修正（编辑抽屉回填竞态）

> 由 `specs/010-button-loading/` 的 e2e 全量验证暴露：`编辑用户显示名生效` 用例不稳定（隔离重复运行 3 次曾 2 次失败）。

- [x] T46 `specs/009-user-management/design.md` §4.4 与 `requirement.md` 验收标准 15 补「打开抽屉先重置表单 + 回填完成前字段禁用」的行为约定（规格先行）
- [x] T47 `UserFormDrawer.vue`：`visible` watcher 统一先 `Object.assign(form, emptyForm())` 再按需 `loadUser`；各字段 `:disabled="detailLoading"`，消除"残留上一个用户数据"与"输入被异步回填覆盖导致提交旧值"
- [x] T48 `e2e/user.spec.ts` 新增「详情接口较慢时表单先重置并禁用，回填后编辑仍生效」回归用例（延迟详情接口放大竞态窗口）；`npm run test:e2e` 46 通过

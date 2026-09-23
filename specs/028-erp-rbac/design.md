---
created: 2026-09-17
updated: 2026-09-23
---

# 设计规格：角色与权限体系（erp-rbac）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `009-user-management`（用户域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格为**横向改造**：新增角色域，并为 `012`–`027` 的全部 Controller 动作标注权限点（改造面广但每处改动小）。
> **演进（erp-audit-log）**：角色（创建 / 更新 / 删除）与用户角色变更的写操作已接入操作日志（`specs/029-erp-audit-log/design.md` §0.1）。

## 0. 权限点清单（唯一事实源）

### 0.1 命名规则

- key 统一 `<域>.<动作>`，全小写驼峰，`.` 分隔，最长 60 字符；`<域>` 与前端域目录 / 后端 `Features/<域>` / 菜单 key 对齐（前端规则 §4.1 四者对齐原则的延伸）。
- **菜单级权限 = `<域>.view`**：控制菜单项可见、列表与详情可访问；分组（`025` §0.2）可见性 = 组内**任一**子项 `.view` 命中。
- **按钮级权限**：写操作与导出等动作各自成点；`print` 复用 `.view`（打印是只读展示）。
- 「示例页面」分组（组件示例 / 列表示例 / 表单与详情示例）**不纳入权限**（演示性质，登录即可见），在 `.codebuddy/CONTEXT.md` §3 说明。

### 0.2 权限点总表

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 商品 | `products` | `view` / `create` / `update` / `status` / `export` | `/api/products*`、`/products` |
| 分类 | `categories` | `view` / `create` / `update` / `delete` | `/api/categories*`、`/categories` |
| 往来单位 | `partners` | `view` / `create` / `update` / `status` / `export` | `/api/partners*`、`/partners` |
| 仓库（`038`） | `warehouses` | `view` / `create` / `update` / `status` | `/api/warehouses*`、`/warehouses` |
| 采购订单（`024`） | `purchaseOrders` | `view` / `create` / `update` / `void` / `close` | `/api/purchase-orders*`、`/purchase-orders` |
| 采购入库 | `purchases` | `view` / `create` / `void` / `export` | `/api/purchase-orders*`（`024` 前）、`/purchases` |
| 采购退货 | `purchaseReturns` | `view` / `create` / `void` / `settle` / `export` | `/api/purchase-returns*`、`/purchase-returns` |
| 销售订单（`024`） | `salesOrders` | `view` / `create` / `update` / `void` / `close` | `/api/sales-orders*`、`/sales-orders` |
| 销售出库 | `sales` | `view` / `create` / `void` / `export` | `/api/sales-orders*`（`024` 前）、`/sales` |
| 销售退货 | `salesReturns` | `view` / `create` / `void` / `settle` / `export` | `/api/sales-returns*`、`/sales-returns` |
| 库存查询 | `inventory` | `view` / `export` | `/api/inventory*`、`/inventory` |
| 库存流水 | `stockMovements` | `view` / `export` | `/api/stock-movements*`、`/stock-movements` |
| 库存盘点 | `stockTakes` | `view` / `create` / `export` | `/api/stock-takes*`、`/stock-takes` |
| 调拨（`039`） | `transfers` | `view` / `create` / `void` / `export` | `/api/transfers*`、`/transfers` |
| 批次（`040`） | `batches` | `view` / `create` / `update` | `/api/batches*`、`/batches` |
| 收付款 | `settlements` | `view` / `create` / `void` / `export` | `/api/settlements*`、`/settlements` |
| 往来对账 | `reconciliation` | `view` | `/api/reconciliation`、`/reconciliation` |
| 发票（`032`） | `invoices` | `view` / `create` / `void` / `export` | `/api/invoices*`、`/invoices` |
| 客户价格（`036`） | `partnerPrices` | `view` / `create` / `update` / `delete` | `/api/partner-prices*`、`/partner-prices` |
| 报表 | `reports` | `view` / `export` | `/api/reports/*`、`/reports/*`（5 个报表页共用） |
| 成本重算 | `costs` | `recalculate` | `/api/costs/recalculate`、成本报表页操作行 |
| 用户管理 | `users` | `view` / `create` / `update` / `status` / `resetPassword` | `/api/users*`、`/users` |
| 登录日志 | `loginLogs` | `view` | `/api/login-logs`、`/login-logs` |
| 角色权限 | `roles` | `view` / `create` / `update` / `delete` | `/api/roles*`、`/roles` |
| 操作日志（`029`） | `auditLogs` | `view` | `/api/audit-logs*`、`/audit-logs` |
| 部门（`030`） | `departments` | `view` / `create` / `update` / `delete` / `status` | `/api/departments*`、`/departments` |
| 岗位（`030`） | `positions` | `view` / `create` / `update` / `delete` / `status` | `/api/positions*`、`/positions` |
| 员工（`030`） | `employees` | `view` / `create` / `update` / `status` / `export` | `/api/employees*`、`/employees` |
| 会计科目（`031`） | `accounts` | `view` / `create` / `update` / `delete` / `status` | `/api/accounts*`、`/accounts` |
| 税率（`031`） | `taxRates` | `view` / `create` / `update` / `delete` / `status` | `/api/tax-rates*`、`/tax-rates` |
| 凭证（`033`） | `vouchers` | `view` / `create` / `void` / `close` / `updateMapping` | `/api/vouchers*`、`/api/accounting-periods*`、`/api/account-mappings*`、`/vouchers`、`/vouchers/new`、`/vouchers/detail/:id` |
| 财务报表（`033`） | `financialReports` | `view` | `/api/reports/account-balance`、`/api/reports/balance-sheet`、`/api/reports/income-statement`、`/financial-reports` |
| 资金账户（`034`） | `bankAccounts` | `view` / `create` / `update` / `delete` / `status` | `/api/bank-accounts*`、`/bank-accounts` |
| 资金日记账（`034`） | `cashJournals` | `view` | `/api/cash-journals*`、`/cash-journals` |
| 站内消息（`041`） | `notifications` | `view` | `/api/notifications*`、顶栏铃铛 |
| 单据审批（`042`） | `approvals` | `view` / `approve` | `/api/approvals*`、审批页 |

- 新增功能一律在本表续行；**未登记权限点的动作不得合并**（`tasks.md` 有集成测试遍历守卫）。
- 权限点名称（中文，用于权限树展示）由后端清单接口返回（`design.md` §3.3），前端不硬编码。

### 0.3 内置角色与默认权限

| 角色 | `IsBuiltin` | 权限 | 可否编辑 / 删除 |
|---|---|---|---|
| `SuperAdmin`（超级管理员） | 是 | **全部权限点**（解析时直接放行，不逐点存储） | 不可（`40175`）；不可取消用户绑定 |
| `Staff`（普通员工，默认角色） | 是 | 全部业务权限，**排除** `users.*` / `roles.*` / `auditLogs.*` / `costs.recalculate` | 权限可编辑（可增减），不可删除 |

- 新增用户默认绑定 `Staff`（用户表单可改）；迁移时既有用户全部绑定 `Staff`。
- 用户**至少一个角色**（Validator 必填，避免「无角色黑洞」导致登录后无任何入口）。

### 0.4 免权限校验白名单（`[SkipPermissionCheck]`）

| 动作 | 原因 |
|---|---|
| `AuthController.Login`、`HealthController.GetHealth` | 已 `[AllowAnonymous]`（无需登录，自然无需权限） |
| `UsersController.Me`、`UsersController.MyPermissions` | 当前用户自身信息 / 权限集合，登录即可访问（否则会形成「需要权限才能查权限」死锁） |

- 白名单在代码中以特性显式标注，并在 `tasks.md` 的集成测试中断言「所有动作要么标注 `[RequirePermission]`、要么在白名单内」。

## 1. 总体设计

```
角色管理（前端 /roles）
  → RolesController
    → App.Core/Features/Roles/<Action>/*RequestHandler
      → IRoleRepository / IUserRoleRepository（App.Core）→ EF Core 实现（App.Infrastructure）
        → PostgreSQL（Roles / RolePermissions / UserRoles）

权限强制校验（横切，所有业务动作）
  HTTP 请求 → 认证（JWT，既有）→ PermissionAuthorizationFilter（App.Api）
    → 读取动作 [RequirePermission] / [SkipPermissionCheck]
    → IPermissionResolver.GetPermissionsAsync(userId)（单请求内缓存；SuperAdmin 直接全量）
    → 不通过 → HTTP 200 + { code: 40300, message: "无权限" }

前端可见性
  stores/auth.ts（permissions: string[]）
    → AppLayout 菜单过滤（菜单项 → 权限点映射见 §0.2）
    → 页面按钮 v-if="auth.hasPermission(key)"
    → 路由 meta.permission 校验 → /403
```

核心原则：

- **权限点由代码定义、角色由界面配置**：权限点清单是代码常量（改动需发版），角色 → 权限映射存库（运行时可变）；避免「权限点也能在生产库随意新增」导致前后端清单漂移。
- **默认拒绝**：动作未标注权限点即返回 `40300`，漏标在测试中暴露，而不是默认放开。
- **单一校验入口**：只在 `PermissionAuthorizationFilter` 校验，Handler 不写权限判断（业务用例保持纯净、可单测）。
- **前后端双保险**：前端过滤只为体验（隐藏不可用入口），后端校验才是安全边界。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/Role.cs` 与表 `Roles`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Name` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 角色名称（大小写不敏感唯一，应用层 `ToLower` 比较，同 `009` 惯例） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `IsBuiltin` | `bool` | `boolean` | NOT NULL，默认 `false` | 内置角色（`SuperAdmin` / `Staff`）：不可删除；`SuperAdmin` 不可编辑 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

### 2.2 实体 `App.Core/Entities/RolePermission.cs` 与表 `RolePermissions`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `RoleId` | `Guid` | `uuid` | PK（复合），FK → `Roles(Id)` | |
| `PermissionKey` | `string` | `varchar(60)` | PK（复合） | 权限点 key（§0.2） |

- 无审计字段（集合型从属表）；保存角色权限为**全量替换**（先删后插，同一事务）。

### 2.3 实体 `App.Core/Entities/UserRole.cs` 与表 `UserRoles`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `UserId` | `Guid` | `uuid` | PK（复合），FK → `Users(Id)` | |
| `RoleId` | `Guid` | `uuid` | PK（复合），FK → `Roles(Id)` | |

- 用户角色为多对多；更新用户角色同样**全量替换**。

### 2.4 迁移与种子

- 迁移：`dotnet ef migrations add AddErpRbac -p src/App.Infrastructure -s src/App.Api`（增量迁移，建 3 张表 + 唯一索引）。
- `DatabaseInitializer`（`009` 既有）扩展（幂等）：
  1. 内置角色不存在则创建：`SuperAdmin`、`Staff`（`Staff` 的初始权限 = §0.3 集合，逐条写入 `RolePermissions`）；
  2. admin 用户绑定 `SuperAdmin`（若尚未绑定）；
  3. **既有用户批量绑定 `Staff`**（无任何角色绑定的用户，一次 SQL / 批量插入；避免老用户升级后「无任何权限」）。
- 不删除任何用户角色绑定（幂等，可重复执行）。

## 3. 后端设计

### 3.1 权限常量与解析

| 类型 | 位置 | 说明 |
|---|---|---|
| `Permissions` | `App.Core/Auth/Permissions.cs` | 权限点常量（`public const string ProductsView = "products.view";` …）+ `All`（全量 `IReadOnlyList<string>`，供 SuperAdmin 解析与权限点接口使用）+ 分组元数据（每组名称 + key 列表，供 `GET /api/permissions` 渲染权限树） |
| `IPermissionResolver` | `App.Core/Abstractions/` | `Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, ...)`；实现 `App.Infrastructure/Auth/PermissionResolver.cs`：查 `UserRoles ⋈ RolePermissions`（`SuperAdmin` → 直接返回 `Permissions.All`）；**Scoped 生命周期 + 单请求内缓存**（同一请求多次校验只查一次库） |
| `RequirePermissionAttribute` | `App.Api/Authorization/` | `[RequirePermission(Permissions.ProductsCreate)]`，动作级；可多个（全部满足） |
| `SkipPermissionCheckAttribute` | `App.Api/Authorization/` | 白名单标记（§0.4） |
| `PermissionAuthorizationFilter` | `App.Api/Authorization/` | 全局 `IAsyncAuthorizationFilter`：`[AllowAnonymous]` 跳过 → `[SkipPermissionCheck]` 跳过 → 取 `[RequirePermission]` 集合 → 未标注即拒绝 → 逐点校验 → 不通过写 `ApiResponse.Fail(40300, "无权限")`（HTTP 200） |

- 过滤器依赖 `ICurrentUser`（既有）与 `IPermissionResolver`；未登录由既有认证管道先行返回 `40100`（本过滤器不会被触及）。
- 注册：`Program`/`AddControllers` 处全局添加（`options.Filters.Add<PermissionAuthorizationFilter>()`）。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40173 | `RoleNameExists` | 角色名称已存在 |
| 40174 | `RoleInUse` | 角色已被用户绑定，禁止删除 |
| 40175 | `RoleBuiltinImmutable` | 内置角色不可编辑（`SuperAdmin`）/ 不可删除 |
| 40300 | `Forbidden` | 无权限（**由预留码转为生产码**，`AGENTS.md` §4.2 同步） |

> `40000` / `40400` 复用全局；`40300` 前端处置同 `40000`（统一 `Message.error`）。
> **演进（错误码改值，`2026-09-22`）**：初稿分配 `40119`–`40121`，与 `013`（`40119` 往来单位类型收窄）/ `023`（`40120` 已核销禁止作废）已占用码冲突；按 `specs/ROADMAP.md` §6「下一个可用」改取 `40173`–`40175`，`028` 占用后下一个可用为 `40176`。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/roles` | GET | `Roles/GetRoles` | `PagedResult<RoleListItemDto>` | `roles.view` / 40000 |
| `/api/roles` | POST | `Roles/CreateRole` | `RoleDetailDto` | `roles.create` / 40000 / 40173 |
| `/api/roles/{id:guid}` | GET | `Roles/GetRoleById` | `RoleDetailDto` | `roles.view` / 40400 |
| `/api/roles/{id:guid}` | PUT | `Roles/UpdateRole` | `RoleDetailDto` | `roles.update` / 40000 / 40173 / 40175 / 40400 |
| `/api/roles/{id:guid}` | DELETE | `Roles/DeleteRole` | `null` | `roles.delete` / 40174 / 40175 / 40400 |
| `/api/permissions` | GET | `Permissions/GetPermissions` | `IReadOnlyList<PermissionGroupDto>`（分组 + key + 名称） | `roles.view` |
| `/api/users/me/permissions` | GET | `Users/GetMyPermissions` | `IReadOnlyList<string>` | `[SkipPermissionCheck]` |

- 用户侧扩展（`009` 改造）：`CreateUserRequest` / `UpdateUserRequest` 追加 `roleIds`（必填非空）；`UserListItemDto` / `UserDetailDto` 追加 `roles`（`{ id, name }[]`）；`LoginResponse` 追加 `permissions`。
- 权限树数据来自 `Permissions` 常量（`PermissionGroupDto { GroupName, Items: [{ Key, Name }] }`），前端不硬编码清单。

### 3.4 关键用例流程（Handler）

**CreateRole**：`ExistsByNameAsync(name, null)` → `40173`；组 `Role`（`IsBuiltin = false`）+ 权限点集合（**校验每个 key ∈ `Permissions.All`**，非法 → `40000`）→ `IUnitOfWork`：插角色 + 全量插 `RolePermissions` → `CommitAsync`。

**UpdateRole**：取角色（不存在 → `40400`）→ `IsBuiltin && Name == SuperAdmin` → `40175`（`Staff` 可改名 / 改权限，但不可删）→ 名称唯一（排除自身）→ 权限点合法性校验 → 同一事务内**全量替换** `RolePermissions` + 更新审计。

**DeleteRole**：取角色（不存在 → `40400`）→ `IsBuiltin` → `40175` → 有用户绑定（`IUserRoleRepository.CountByRoleAsync`）→ `40174`；否则删除角色 + 其权限行（同一事务）。

**GetMyPermissions**：`IPermissionResolver` 求当前用户权限集合（`SuperAdmin` → 全量）。

**用户角色绑定**（`CreateUser` / `UpdateUser` 改造）：`roleIds` 去重后校验**全部存在**（否则 `40400`）、非空（Validator）；同一事务内全量替换 `UserRoles`。

### 3.5 校验规则（FluentValidation，仅格式层，引用 `RoleFieldConstraints`）

| 请求 | 规则 |
|---|---|
| `CreateRoleRequest` / `UpdateRoleRequest` | `name` 必填 2–20（`RoleFieldConstraints.NameMinLength/NameMaxLength`）；`remark` ≤ 200（复用 `OrderFieldConstraints.RemarkMaxLength`）；`permissionKeys` 1–200 项、每项 ≤ 60 字符（`PermissionKeyMaxLength`） |
| `GetRolesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20（对齐 `Roles.Name` 列长，后端规则 §5.3 ③） |
| `CreateUserRequest` / `UpdateUserRequest`（`009` 扩展） | `roleIds` 必填非空、去重后 1–20 项 |

- 角色名唯一、内置角色限制、权限点合法性、用户绑定存在性等业务约束在 Handler（后端规则 §4.1）。
- 新增常量类 `App.Core/Entities/RoleFieldConstraints.cs`（`NameMinLength` / `NameMaxLength` / `PermissionKeyMaxLength` / `RolesPerUserMaxCount`）；`Name` 列长与 `RoleConfiguration` 一致，由 `FieldValidationConsistencyTests` 守护。

### 3.6 Swagger

- **不分组**（同既有约定）：7 个新增接口按现有方式出现在单文档 Swagger 中；`401` / `403` 语义标注沿用 `003` 约定（`[ProducesResponseType(403)]` 仅作文档标注）。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── role.ts                    # 角色 + 权限点清单接口层
├── stores/
│   └── auth.ts                    # 扩展：permissions / hasPermission / fetchPermissions
└── views/
    ├── RoleManagement/
    │   ├── RolesView.vue          # 角色列表
    │   └── RoleFormDrawer.vue     # 新增 / 编辑抽屉（含权限勾选树）
    └── ForbiddenView.vue          # /403 无权限页（views/ 根，无功能域归属）
```

- `views/UserManagement/` 改造：`UsersView.vue` 追加角色列；`UserFormDrawer.vue` 追加角色多选（不新建文件）。

### 4.2 接口层

- `src/api/role.ts`：`getRoles` / `getRole` / `createRole` / `updateRole` / `deleteRole` / `getPermissions` + 类型。
- `src/api/user.ts` 扩展：`roles` 字段与 `roleIds` 提交字段；`src/api/auth.ts` 扩展：`LoginResponse` 增加 `permissions`、新增 `getMyPermissions`。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `roles` | `roles` | `RolesView` |
| `403` | `forbidden` | `ForbiddenView`（顶层路由，**不进 `AppLayout`**，`meta: { requiresAuth: true }`） |

- `AppLayout.vue`「系统」分组追加「角色权限」（`025` §0.2 总表已预留 `roles`）。
- 既有路由追加 `meta.permission`（如 `products` → `products.view`），全局前置守卫校验：无权限 → `next({ name: 'forbidden' })`。

### 4.4 权限基础设施（前端）

| 位置 | 内容 |
|---|---|
| `stores/auth.ts` | `permissions: string[]`（登录响应赋值 + `fetchPermissions()` 刷新）；`hasPermission(key): boolean`（`SuperAdmin` 等价于全量，由后端返回全量集合，前端不做特例）；`hasAnyPermission(keys)` |
| `AppLayout.vue` | 菜单项配置追加 `permission?: string`；`computed` 过滤：`permission` 为空（示例页面）或 `hasPermission` 命中才展示；分组内无可见子项则整组不展示；当前路由被过滤时保持选中逻辑不变 |
| 页面按钮 | `v-if="auth.hasPermission('<key>')"`：各域新增 / 编辑 / 启停 / 作废 / 关闭 / 结算 / 导出 / 重算按钮 |
| 请求层 | `40300` 与 `40000` 同处置（`Message.error`），不跳转（用户仍在页面，只是无权限） |
| 路由守卫 | `to.meta.permission` 存在且无权限 → `/403` |
| `ForbiddenView.vue` | `a-result status="403"` + 「返回首页」按钮 |

### 4.5 页面交互

**角色列表 `RolesView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（关键词）；操作行（「新增」primary + 刷新 + 列设置）；列：序号、角色名称、权限数、用户数、类型（`a-tag`：内置 `blue` / 自定义 `gray`）、备注、创建时间、操作列（编辑 / 删除——内置 `SuperAdmin` 置灰禁用；删除 `a-popconfirm` + `deletingId`）。

**角色抽屉 `RoleFormDrawer.vue`**：字段少（名称 / 备注 + 权限树）但权限树较高 → 宽 560 抽屉（`007` §0 形态：字段少用抽屉；权限树不视为子表格）；`a-tree`（`checkable`，数据来自 `getPermissions` 分组）；打开时按 `mode` 重置 + 回填（`SuperAdmin` 打开时权限树全选且禁用）；提交 `submitting` + 防重入。

**用户抽屉 `UserFormDrawer.vue` 改造**：追加「角色」（`a-select multiple`，数据源 `getRoles`（`pageSize=100`），必填）；打开时先重置再回填（沿用 `009` 的防串台约定）。

### 4.6 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 角色列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 角色抽屉提交 | `submitting` | 提交按钮 |
| 角色删除 | `deletingId` | popconfirm 确认按钮 |
| 权限树加载（抽屉打开） | `permissionsLoading` | 抽屉内容区 `a-spin` |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 权限点内置为代码常量 | `Permissions` 静态类 | 权限点必然与代码（接口动作）绑定，存库会导致清单漂移与「删不掉的孤儿权限」；角色映射存库保证运行时灵活 |
| 菜单级与按钮级统一用同一套 key | `<域>.<动作>`，`.view` 兼作菜单级 | 一套机制覆盖两种粒度，前端只需一个 `hasPermission`；避免两套映射表 |
| 默认拒绝（未标注即无权限） | 过滤器强制 | 漏标会被测试与运行时的 `40300` 暴露，而不是静默放开；新增接口必须显式登记权限点 |
| 校验放 Api 层过滤器 | `[RequirePermission]` + 全局 Filter | Handler 保持「业务用例」纯净、可单测；权限是接口层关注点；一处实现，全部生效 |
| 权限不塞进 JWT | 每请求解析（Scoped 缓存） | 角色变更**即时生效**，无需重登 / 无需 token 黑名单；权限点数（约 80）与请求量下，单次查询成本可忽略 |
| 多角色取并集 | `UserRoles` 多对多 | 现实场景（一人兼岗）普遍；单角色模型迟早要改 |
| 用户至少一个角色 | Validator 必填 | 避免「无角色」用户登录后无任何入口（体验黑洞）；也避免角色列表删除后被孤立 |
| `SuperAdmin` 直接放行不逐点存储 | 解析时返回 `Permissions.All` | 新增权限点自动继承，无需回填内置角色；杜绝「新功能上线后管理员没权限」 |
| `Staff` 权限可编辑、不可删除 | `IsBuiltin = true` | 保留一个「可预期的默认角色」；同时允许管理员按需收紧 |
| 角色权限 / 用户角色全量替换 | 先删后插 | 语义简单、无差异计算；集合量小（≤ 200），性能无虞 |
| 既有用户回填 `Staff` | 迁移 + 幂等种子 | 升级后老用户立即可用（符合 `ROADMAP` §6.5「先跑起来」的平滑要求） |
| 前端权限仅作体验 | 后端为唯一安全边界 | 前端集合可被篡改，不能作为安全依据；两者职责分离 |
| 不做行级 / 数据范围权限 | 范围外 | 需要组织模型与查询层注入范围，属独立工程；本期先把「入口级」权限补齐。**演进（`030`）**：组织 / 部门 / 岗位 / 员工主数据已由 `specs/030-erp-org-employee/` 提供，但**数据级权限（按部门 / 数据范围过滤）仍不做**，查询层范围注入未变 |
| 打印复用 `.view` | 不设 `.print` | 打印是只读展示，单独设点会让权限树冗余；若后续需要「可看不可打」再拆分 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口与 `IPermissionResolver`；`TestCurrentUser` 同既有约定。

- **角色用例**：`CreateRole` 成功（断言角色 + 权限行同时入 `IUnitOfWork`）/ 重名 `40173` / 非法权限 key `40000`；`UpdateRole` 成功（全量替换断言）/ `SuperAdmin` → `40175` / 重名排除自身；`DeleteRole` 成功 / 有用户绑定 `40174` / 内置 `40175` / 不存在 `40400`；`GetRoles` 筛选与分页映射；`GetPermissions` 分组清单与 `Permissions.All` 一致。
- **权限解析**：`SuperAdmin` → 返回 `Permissions.All`；多角色 → 并集；无角色 → 空集；单请求内只查库一次（假实现计数断言）。
- **过滤器**：有权限放行；无权限 → `40300`（HTTP 200）；未标注 `[RequirePermission]` → `40300`；`[SkipPermissionCheck]` 放行；未登录不进入本过滤器（`40100` 由认证管道产生）。
- **用户角色**：`CreateUser` / `UpdateUser` 的 `roleIds` 全量替换与存在性校验（不存在 → `40400`）、去重；`UserListItemDto.roles` 映射。
- **清单守卫（集成测试）**：遍历全部 Controller 动作，断言「标注 `[RequirePermission]` 且 key ∈ `Permissions.All`」或「在 §0.4 白名单内」——**新增动作漏标即测试失败**。
- **种子与迁移**：`DatabaseInitializer` 幂等（连续两次执行不重复建角色 / 不重复绑定）；既有用户回填 `Staff`；admin 绑定 `SuperAdmin`。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Roles.Name` EF `HasMaxLength` 20 == `RoleFieldConstraints.NameMaxLength`；`PermissionKey` 列长 60 == `PermissionKeyMaxLength`；`keyword` 20 通过 / 21 拒绝（对齐 `Roles.Name` 列长）。

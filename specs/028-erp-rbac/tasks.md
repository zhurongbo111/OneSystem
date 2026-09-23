---
created: 2026-09-17
updated: 2026-09-23
---

# 任务清单：角色与权限体系（erp-rbac）

> 依据 `specs/028-erp-rbac/design.md` 拆分。含角色域新增 + 权限强制校验 + **为 `012`–`027` 全部动作补权限点**（改造面最广） + 前端菜单 / 按钮过滤。**按阶段顺序实现，每阶段结束跑全量测试**。
> 前置：`012`–`027` 已实现（本规格为其补权限点）；`009`（用户域）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> **重要**：阶段 C 会把全部接口置于「默认拒绝」之下，实现期间请分域推进并保持测试全绿，避免半途出现大面积 `40300`。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与权限基建

- [x] 1.1 新增实体 `Role` / `RolePermission` / `UserRole` + EF 配置 + `AppDbContext` 3 个 `DbSet`；`RoleFieldConstraints`
- [x] 1.2 增量迁移 `dotnet ef migrations add AddErpRbac -p src/App.Infrastructure -s src/App.Api`
- [x] 1.3 `App.Core/Auth/Permissions.cs`（§0.2 全量常量 + `All` + 分组元数据）
- [x] 1.4 `IPermissionResolver` + `App.Infrastructure/Auth/PermissionResolver.cs`（SuperAdmin 全量、多角色并集、Scoped 单请求缓存）+ 注册
- [x] 1.5 `RequirePermissionAttribute` / `SkipPermissionCheckAttribute` / `PermissionAuthorizationFilter`（默认拒绝；`40300` HTTP 200）+ 全局注册
- [x] 1.6 `ErrorCode.cs` 追加 `40173` / `40174` / `40175`（初稿 `40119`–`40121` 与 `013` / `023` 撞码，见 `design.md` §3.2 演进注记）；`40300` 注释由「预留」改为「生产码」
- [x] 1.7 `DatabaseInitializer` 扩展：内置角色 `SuperAdmin` / `Staff`（含 `Staff` 初始权限）、admin 绑定 `SuperAdmin`、既有用户回填 `Staff`（幂等）

## 二、后端：角色域用例与接口

- [x] 2.1 共享出参 `Features/Roles/RoleListItemDto.cs` / `RoleDetailDto.cs` + `RolesDtoMapper.cs`
- [x] 2.2 新增用例 `Roles/GetRoles`、`Roles/CreateRole`、`Roles/GetRoleById`、`Roles/UpdateRole`、`Roles/DeleteRole`
- [x] 2.3 新增用例 `Permissions/GetPermissions`（分组 + key + 名称，数据源 `Permissions`）
- [x] 2.4 仓储 `IRoleRepository` / `IUserRoleRepository`（`ExistsByNameAsync` / `GetPagedAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` / `ReplacePermissionsAsync` / `CountByRoleAsync` / `GetRoleIdsByUserAsync` / `ReplaceUserRolesAsync`）+ 实现 + 注册
- [x] 2.5 `RolesController`（5 端点）+ `PermissionsController`（1 端点）+ DI 注册
- [x] 2.6 `009` 用户侧改造：`CreateUser` / `UpdateUser` 支持 `roleIds`（全量替换）；`UserListItemDto` / `UserDetailDto` 追加 `roles`；`GetUsers` / `GetUserById` 联查角色
- [x] 2.7 `Users/GetMyPermissions` 用例（`[SkipPermissionCheck]`）+ `GET /api/users/me/permissions`
- [x] 2.8 `LoginResponse` 追加 `permissions`（`LoginRequestHandler` 调 `IPermissionResolver`）

## 三、后端：为全部既有动作补权限点（按域推进）

> 每域完成后跑 `dotnet test`，保持全绿；`[SkipPermissionCheck]` 仅用于 §0.4 白名单。

- [x] 3.1 基础档案域：`ProductsController` / `CategoriesController` / `PartnersController` 全部动作标注
- [x] 3.2 采购域：`PurchaseOrdersController` / `PurchaseReturnsController`（含 `settle` / `void` / `export`）
- [x] 3.3 销售域：`SalesOrdersController` / `SalesReturnsController`
- [x] 3.4 库存域：`InventoryController` / `StockMovementsController` / `StockTakesController`
- [x] 3.5 资金域：`SettlementsController`（含 `/api/reconciliation`）/ `ReportsController`
- [x] 3.6 系统域：`UsersController`（`view` / `create` / `update` / `status` / `resetPassword`）/ `LoginLogsController` / `AuthController`（登录匿名）/ `HealthController`（匿名）
- [x] 3.7 导出动作（`027`）标注 `<域>.export`；成本重算标注 `costs.recalculate`
- [x] 3.8 集成测试「清单守卫」：遍历全部 Controller 动作断言「已标注且 key 合法」或「白名单内」

## 四、单元测试（后端）

- [x] 4.1 角色用例：CRUD 成功 / 重名 `40173` / 非法权限 key `40000` / 内置 `40175` / 有用户绑定 `40174` / 不存在 `40400`
- [x] 4.2 权限解析：SuperAdmin 全量 / 多角色并集 / 无角色空集 / 单请求仅查库一次
- [x] 4.3 过滤器：放行 / `40300` / 未标注即拒绝 / 白名单放行 / 未登录走 `40100`
- [x] 4.4 用户角色：全量替换、存在性 `40400`、去重、DTO 映射、`roleIds` 必填
- [x] 4.5 种子与迁移回填：幂等、既有用户绑定 `Staff`、admin 绑定 `SuperAdmin`
- [x] 4.6 字段约束一致性（`Roles.Name` 20 / `PermissionKey` 60 / `keyword` 20-21）
- [x] 4.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归，含权限默认拒绝后的全量回归）—— 605 通过 / 0 失败

## 五、前端

- [x] 5.1 `src/api/role.ts`（角色 CRUD + `getPermissions`）；`api/user.ts` / `api/auth.ts` 扩展（`roles` / `roleIds` / `permissions` / `getMyPermissions`）
- [x] 5.2 `stores/auth.ts`：`permissions` / `hasPermission` / `hasAnyPermission` / `fetchPermissions`
- [x] 5.3 `views/RoleManagement/RolesView.vue`（列表 + 类型标签 + 删除 popconfirm + `deletingId`）
- [x] 5.4 `views/RoleManagement/RoleFormDrawer.vue`（名称 / 备注 + `a-tree` 权限勾选；`SuperAdmin` 全选禁用；`permissionsLoading`）
- [x] 5.5 `views/UserManagement/UsersView.vue` 追加角色列；`UserFormDrawer.vue` 追加角色多选（必填）
- [x] 5.6 `views/ForbiddenView.vue`（`a-result status="403"` + 返回首页）
- [x] 5.7 `router/index.ts`：新增 `roles` 与顶层 `403`；既有路由补 `meta.permission`；守卫接入
- [x] 5.8 `AppLayout.vue`：菜单项补 `permission` 并过滤（分组内全无权限则不展示）；「系统」分组追加「角色权限」
- [x] 5.9 各域页面按钮接入 `v-if="auth.hasPermission(...)"`（新增 / 编辑 / 启停 / 作废 / 关闭 / 结算 / 导出 / 重算 / 用户启停 / 重置密码）
- [x] 5.10 请求层：`40300` 统一 `Message.error`（同 `40000` 处置）
- [x] 5.11 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [x] 6.1 新增 `e2e/rbac.spec.ts`：角色列表 / 新增（勾选部分权限）/ 重名 40173 / 编辑 / 删除（内置禁用、有用户绑定 40174）
- [x] 6.2 同文件：建「仅商品查看」角色 → 建用户绑定 → 以该用户登录：菜单仅「基础档案 → 商品管理」、无新增 / 编辑按钮
- [x] 6.3 同文件：该用户直接访问 `/purchases` → 显示 403 页；直接调无权限接口 → 提示无权限（`40300`）
- [x] 6.4 同文件：`admin`（SuperAdmin）不受限（可见全部菜单、可进用户管理）
- [x] 6.5 既有各域 spec 适配：用例中「新增 / 编辑 / 作废」等操作依赖权限，统一使用 `admin` 登录（既有 `login` helper 已用 admin，应无需大改；需回归确认）—— `user.spec.ts` 的 `createUser` 已补角色必选
- [x] 6.6 `cd frontend && npm run test:e2e` 全量通过 —— 131 通过 / 0 失败（`npm run e2e:run` 一键环境：Docker PostgreSQL + dev 前后端）

## 七、规格与上下文联动

- [x] 7.1 `AGENTS.md` §4.2：`40300` 由「预留码」改为生产码（默认拒绝语义 + 指针到 `028` §0.2 / §0.4）
- [x] 7.2 `specs/009-user-management/design.md` 加「演进（erp-rbac）」注记：用户角色绑定（`roleIds` / `roles`）、`LoginResponse.permissions`、`GET /api/users/me/permissions`
- [x] 7.3 `specs/005-app-layout/design.md` 加注记：菜单项 `permission` 与过滤规则、路由 `meta.permission`、顶层 `/403`
- [x] 7.4 `specs/012`–`027` 各 `design.md` 加注记：动作权限点（指针到 `028` §0.2；`018` 示例页注明不纳入权限）
- [x] 7.5 `.codebuddy/CONTEXT.md` §2（Roles Feature / 仓储 / 权限常量 / 过滤器 / 错误码）、§3（RoleManagement 域、`ForbiddenView`、`api/role.ts`、菜单与路由权限过滤）、§6（`028` 计入已实现 + 横向改造指针）同步
- [x] 7.6 `specs/ROADMAP.md` 状态列更新（`028` → 已实现）；§6.5「`028` 之前登录即可见」注记为已落地

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 清单守卫测试通过：不存在「未标注权限点且不在白名单内」的动作。

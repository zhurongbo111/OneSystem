---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：组织架构与员工档案（erp-org-employee）

> 依据 `specs/030-erp-org-employee/design.md` 拆分。含部门树 + 岗位字典 + 员工档案（含账号绑定、导出）+ 权限 / 菜单续行 + e2e。
> 前置：`009`（用户）、`028`（权限基础设施）、`027`（导出基建）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Department` / `Position` / `Employee` + 枚举 `DepartmentStatus` / `PositionStatus` / `EmployeeStatus` / `Gender`；`AppDbContext` 追加 3 个 `DbSet`
- [ ] 1.2 字段约束常量类 `DepartmentFieldConstraints` / `PositionFieldConstraints` / `EmployeeFieldConstraints`（§2.4）
- [ ] 1.3 EF 配置 `DepartmentConfiguration` / `PositionConfiguration` / `EmployeeConfiguration`（唯一索引、部分唯一索引 `HasFilter`、FK、列长取常量）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpOrgEmployee -p src/App.Infrastructure -s src/App.Api`

## 二、后端：部门域用例与接口

- [ ] 2.1 读模型 `DepartmentTreeNode` + `IDepartmentRepository`（`GetTreeAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `HasChildrenAsync` / `GetEmployeeCountsAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `DepartmentTreeNodeDto` / `DepartmentDetailDto` + `DepartmentDtoMapper`
- [ ] 2.3 用例 `Departments/GetDepartments`（树组装）/ `CreateDepartment` / `GetDepartmentById` / `UpdateDepartment`（含防环 `40141`）/ `DeleteDepartment`（`40140`）/ `UpdateDepartmentStatus`
- [ ] 2.4 `DepartmentsController`（6 端点，标注 `[RequirePermission]`）+ DI 注册

## 三、后端：岗位域用例与接口

- [ ] 3.1 读模型 `PositionListItem` / `PositionPickItem` + `IPositionRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `CountEmployeesAsync` / `GetPickListAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 3.2 共享出参 `PositionListItemDto` / `PositionDetailDto` / `PositionPickDto` + `PositionDtoMapper`
- [ ] 3.3 用例 `Positions/GetPositions` / `CreatePosition` / `GetPositionById` / `UpdatePosition` / `DeletePosition`（`40144`）/ `UpdatePositionStatus` / `GetPositionPicks`
- [ ] 3.4 `PositionsController`（7 端点）+ DI 注册

## 四、后端：员工域用例与接口

- [ ] 4.1 读模型 `EmployeeListItem` / `EmployeeDetail` / `EmployeePickUserItem` + `IEmployeeRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsByNoAsync` / `ExistsByPhoneAsync` / `ExistsByEmailAsync` / `ExistsByUserIdAsync` / `GetAvailableUsersAsync` / `GetAllForExportAsync` / `AddAsync` / `UpdateAsync`）+ 实现 + 注册
- [ ] 4.2 共享出参 `EmployeeListItemDto` / `EmployeeDetailDto` / `EmployeePickUserDto` + `EmployeeDtoMapper`
- [ ] 4.3 用例 `Employees/GetEmployees` / `CreateEmployee`（`40145`–`40148`）/ `GetEmployeeById` / `UpdateEmployee`（工号不可改）/ `UpdateEmployeeStatus`（离职补 `ResignDate`）/ `GetAvailableUsers`
- [ ] 4.4 导出用例 `Employees/ExportEmployees`（复用 `IExcelExporter`，筛选透传、超限 `40000`）
- [ ] 4.5 `EmployeesController`（6 端点，含 `export` 返回 `File(...)`）+ DI 注册

## 五、后端：错误码

- [ ] 5.1 `ErrorCode.cs` 追加 `40138`–`40148`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40149`

## 六、单元测试（后端）

- [ ] 6.1 部门用例：树组装、编码 / 同级重名、防环（自身 / 后代）、删除保护（子部门 / 员工）
- [ ] 6.2 岗位用例：编码 / 名称唯一、被引用禁删、筛选分页、picks 仅启用
- [ ] 6.3 员工用例：工号 / 手机 / 邮箱 / 账号唯一性、账号已绑 `40146`、部门 / 岗位 / 账号存在性、工号不可改、离职补日期、available-users
- [ ] 6.4 导出用例：筛选透传、`MaxRows+1`、超限 `40000`、列头一致
- [ ] 6.5 字段约束一致性单测（三实体 `HasMaxLength` == 常量；`keyword` 边界）
- [ ] 6.6 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 七、前端

- [ ] 7.1 `api/department.ts` / `api/position.ts` / `api/employee.ts`（含 `getPositionPicks` / `getAvailableUsers` / `exportEmployees`）
- [ ] 7.2 `views/OrgManagement/DepartmentsView.vue`（部门树表格 + 新增顶级 / 展开收起 / 操作列）
- [ ] 7.3 `views/OrgManagement/DepartmentFormDrawer.vue`（上级 `a-tree-select`，编辑时排除自身及后代）
- [ ] 7.4 `views/OrgManagement/PositionsView.vue` + `PositionFormDrawer.vue`
- [ ] 7.5 `views/OrgManagement/EmployeesView.vue`（筛选 / 列表 / 导出 / 操作列）+ `EmployeeFormDrawer.vue`（含关联账号选择）
- [ ] 7.6 `router/index.ts` 新增 `departments` / `positions` / `employees` 三条路由（`meta.permission`）；`AppLayout.vue`「系统」分组追加三项 + `MENU_ROUTE_MAP`
- [ ] 7.7 各按钮接入 `v-if="auth.hasPermission(...)"`（新增 / 编辑 / 启停 / 删除 / 导出）
- [ ] 7.8 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 八、E2E（Playwright）

- [ ] 8.1 新增 `e2e/org-employee.spec.ts`：建三级部门树 → 同级重名 `40139` → 删除有子部门 / 有员工的部门 `40140`
- [ ] 8.2 同文件：建岗位 → 编码 / 名称重复 `40142` / `40143` → 被员工引用后删除 `40144`
- [ ] 8.3 同文件：建员工（绑账号）→ 工号重复 `40145` → 绑定已占用账号 `40146` → 筛选 / 编辑 / 离职 → 导出（`download` 事件、文件非空）
- [ ] 8.4 同文件：无 `employees.view` 的账号登录 → 菜单不含「员工档案」、直达路由显示 403
- [ ] 8.5 `cd frontend && npm run test:e2e` 全量通过（含既有用例回归）

## 九、规格与上下文联动

- [ ] 9.1 `specs/028-erp-rbac/design.md` §0.2 续行 `departments.*` / `positions.*` / `employees.*`；§5「不做组织模型」加演进注记（组织由 `030` 提供，数据级权限仍不做）；刷新其 `updated`
- [ ] 9.2 `specs/025-erp-report/design.md` §0.2「系统」分组续行「部门管理 / 岗位管理 / 员工档案」；刷新其 `updated`
- [ ] 9.3 `specs/042-erp-approval/design.md` 注明可用组织 / 岗位定位审批人（可选消费，不改变其简化模型）
- [ ] 9.4 `specs/009-user-management/design.md` 加注记：员工↔账号绑定关系（员工侧实现，不改用户域接口）
- [ ] 9.5 `.codebuddy/CONTEXT.md` §2（Entities / Features / 仓储 / 错误码）、§3（OrgManagement 域、api 文件、菜单分组）、§6 同步
- [ ] 9.6 `specs/ROADMAP.md` §4.2 状态列更新（`030` → 已实现）；§1 / §3 覆盖矩阵「组织」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 清单守卫通过：19 个新增端点均标注合法权限点；权限点 / 菜单已在 `028` / `025` 对应表续行。

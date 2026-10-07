---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：数据级权限 / 数据范围（erp-data-scope）

> 依据 `specs/046-erp-data-scope/design.md` 拆分。横向改造：角色加数据范围 + 九类受约束资源查询过滤 + 前端角色表单 + e2e。
> 前置：`028`（角色 / 权限 / 过滤器）、`030`（员工↔账号、部门树）、`029`（操作日志）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增枚举 `DataScope`（`App.Core/Entities/`）；`Role` 增 `DataScope` 列
- [ ] 1.2 EF 配置更新（`RoleConfiguration`：`smallint`、默认 `All`）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpDataScope -p src/App.Infrastructure -s src/App.Api`（含回填 `UPDATE "Roles" SET "DataScope" = 0`）
- [ ] 1.4 `DatabaseInitializer`：内置角色 `SuperAdmin` / `Staff` 幂等置 `All`

## 二、后端：解析组件

- [ ] 2.1 `DataScopeFilter`（`sealed record`）+ `IDataScopeProvider`（`App.Core/Abstractions/`）
- [ ] 2.2 `DataScopeProvider`（`App.Infrastructure/Auth/`）：取最宽范围 + 部门子树展开 `VisibleUserIds` + `All` 短路 + `CanAccess`；Scoped + 单请求缓存；注册 DI
- [ ] 2.3 仓储扩充：`IUserRoleRepository.GetRoleIdsByUserAsync`、`IEmployeeRepository.GetUserIdsByDepartmentIdsAsync`（批量）、`IDepartmentRepository.GetDescendantIdsAsync`

## 三、后端：受约束资源接入（`design.md` §0.2 九类）

- [ ] 3.1 各资源 `GetPagedAsync` 增可空 `visibleUserIds` 过滤（采购入库 / 销售出库 / 采购退货 / 销售退货 / 收付款 / 报价单 / 发票 / 凭证 / 操作日志）
- [ ] 3.2 各资源列表 `Get*` Handler 注入 `IDataScopeProvider`，取 `DataScopeFilter` 下传仓储
- [ ] 3.3 各资源 `Export*` 复用同一过滤参数
- [ ] 3.4 各资源 `Get*ById` Handler 增 `CanAccess(CreatedBy)` 校验（不通过 `40300`）；按 id 的写动作同判
- [ ] 3.5 角色域：`CreateRole` / `UpdateRole` 增 `dataScope`；出参 `RoleListItemDto` / `RoleDetailDto` 增 `dataScope`（含文案）+ 映射

## 四、单元测试（后端）

- [ ] 4.1 解析：单角色各范围 / 多角色并集 / `SuperAdmin` → `All` / 无绑定员工退化 `Self`
- [ ] 4.2 过滤：九类资源列表 `visibleUserIds` 生效、`null` 不过滤
- [ ] 4.3 越权：详情 / 按 id 动作越权 → `40300`；范围内放行
- [ ] 4.4 角色域：`dataScope` 持久化、`SuperAdmin` 强制 `All`（`40175`）
- [ ] 4.5 迁移回填回归：既有角色 `DataScope == All`
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 五、前端

- [ ] 5.1 `api/role.ts`：类型增 `dataScope` + `DATA_SCOPE_META`（文案 / 颜色）
- [ ] 5.2 `views/RoleManagement/RolesView.vue`：抽屉表单增「数据范围」`a-select`；列表增列；`SuperAdmin` 置灰
- [ ] 5.3 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/data-scope.spec.ts`：受限角色（本部门）用户 A 只见本部门单据；异部门用户 B 单据不可见
- [ ] 6.2 同文件：A 访问 B 的单据详情 → `40300`；`All` 角色行为与升级前一致
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含 `rbac.spec.ts` 回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.1 / §0.2 增数据范围指针（不新增权限点）；刷新其 `updated`
- [ ] 7.2 `specs/029-erp-audit-log/design.md` §0.1 增「受数据范围约束」注记
- [ ] 7.3 `.codebuddy/CONTEXT.md` §2（`Auth/`、读模型 / 仓储）/ §3 同步
- [ ] 7.4 `specs/ROADMAP.md` §4.2 状态更新（`046` → 已实现）；§3 覆盖矩阵「L1 平台 · 权限 / 审计」缺口同步；§6 下一个可用保持 `40176`

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 九类资源可见性一致生效；越权详情 `40300`；迁移后既有角色行为无变化。

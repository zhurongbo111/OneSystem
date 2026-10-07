---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：行政事务申请（oa-admin-affair）

> 依据 `specs/063-oa-admin-affair/design.md` 拆分；顺序即实现顺序。
> 前置：`027`（导出）、`028`（权限）、`029`（操作日志）、`030`（员工档案）、`041`（站内信通道）、`042`（审批口径参照）均已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `e2e:run`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增枚举 `AdminRequestType` / `AdminRequestStatus`（`App.Core/Entities/`）
- [ ] 1.2 新增实体 `AdminRequest` + `AdminRequestFieldConstraints`
- [ ] 1.3 EF 配置（表 + 唯一索引 `(RequestNo)` + 列表索引 `(Status, CreatedAt DESC)` / `(ApplicantUserId, CreatedAt DESC)`）
- [ ] 1.4 `AppDbContext` 增 `DbSet`
- [ ] 1.5 迁移 `dotnet ef migrations add AddOaAdminAffair -p src/App.Infrastructure -s src/App.Api`（无种子）

## 二、后端：仓储与共享组件

- [ ] 2.1 `IAdminRequestRepository` + `AdminRequestRepository`（§3.1 全部方法，含 `GenerateNoAsync`）
- [ ] 2.2 读模型 `AdminRequestListItem` / `AdminRequestDetail`
- [ ] 2.3 `AdminRequestStatusRules`（流转白名单 + 撤销 / 完成判据单点）
- [ ] 2.4 审批人取值复用 `041` 的 `IPermissionedUserQuery`（权限点 `adminAffairs.approve`）
- [ ] 2.5 `AdminRequestNotifier`（站内信：待审批 / 审批结果；发信失败只记日志）
- [ ] 2.6 `Infrastructure/DependencyInjection.cs` 注册仓储

## 三、后端：用例与端点

- [ ] 3.1 `Features/AdminRequests/GetAdminRequests`（四件套，类型 / 状态 / 我的申请 / 关键词）
- [ ] 3.2 `Features/AdminRequests/GetAdminRequestById`
- [ ] 3.3 `Features/AdminRequests/CreateAdminRequest`（单号 `AF` + 落库 + 向审批人发信）
- [ ] 3.4 `Features/AdminRequests/CancelAdminRequest`（申请人本人 + `Pending`，`40208`）
- [ ] 3.5 `Features/AdminRequests/ApproveAdminRequest`（自审 `40210`，回流 `40209`，向申请人发信）
- [ ] 3.6 `Features/AdminRequests/RejectAdminRequest`（自审 `40210`，意见必填 `40000`，向申请人发信）
- [ ] 3.7 `Features/AdminRequests/CompleteAdminRequest`（仅 `Approved`，写 `CompletedAt`）
- [ ] 3.8 `Features/AdminRequests/ExportAdminRequests`（复用过滤参数）
- [ ] 3.9 `Errors/ErrorCode` 增 `40208` / `40209` / `40210`；`Core/DependencyInjection.cs` 注册用例与校验器
- [ ] 3.10 `AdminRequestsController`（`/api/admin-requests`，8 动作，全部 `[RequirePermission]`；`export` 固定段在前）
- [ ] 3.11 导出表格模型登记 `App.Core/Exports/`
- [ ] 3.12 全部写用例接入 `IAuditLogger`（`029` §0.1 续行；通过 / 驳回用 `Approve`，撤销 / 完成用 `StatusChange`）

## 四、单元测试（后端）

- [ ] 4.1 状态：白名单放行、终态拒绝 `40209`
- [ ] 4.2 撤销：非本人或非 `Pending` → `40208`
- [ ] 4.3 自审：申请人 == 审批人 → `40210`
- [ ] 4.4 驳回意见必填（空 → `40000`）；通过可空
- [ ] 4.5 单号：跨日重置、长度符合约束
- [ ] 4.6 发信：提交 / 决策各触发一次；发信失败不影响状态流转
- [ ] 4.7 `FieldValidationConsistencyTests` 追加 `AdminRequestFieldConstraints`
- [ ] 4.8 `cd backend && dotnet build` / `dotnet test` 通过（含 `ApiPermissionMatrix` 守卫回归）

## 五、前端

- [ ] 5.1 `api/adminRequest.ts`（类型 + 类型 / 状态文案与颜色 + 可用动作映射）
- [ ] 5.2 `views/AdminAffairManagement/AdminRequestsView.vue`（tab + 筛选 + 导出 + 按状态与权限显隐操作列）
- [ ] 5.3 `views/AdminAffairManagement/AdminRequestDetailDrawer.vue` + `AdminAffairDecideDrawer.vue` + `AdminRequestFormDrawer.vue`
- [ ] 5.4 `router/index.ts` 注册 `/admin-affairs` + `ROUTE_PERMISSIONS`；`AppLayout.vue` 「办公」分组追加「行政事务」
- [ ] 5.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/admin-affair.spec.ts`：提交申请 → 审批人铃铛收信
- [ ] 6.2 同文件：通过 → 完成；驳回未填意见被拦（`40000`）
- [ ] 6.3 同文件：自审 → `40210`；终态无可用动作；导出下载成功
- [ ] 6.4 `cd frontend && npm run e2e:run` 全量通过

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `adminAffairs.*`（5 点）
- [ ] 7.2 `specs/029-erp-audit-log/design.md` §0.1 续行资源 `AdminRequest`
- [ ] 7.3 `specs/041-erp-stock-alert/design.md` §2.1 续行 `NotificationType` 9 / 10 + 文件头演进指针
- [ ] 7.4 `specs/042-erp-approval/design.md` §0 留「审批口径被 `063` 复用（不走阈值规则表）」一行指针
- [ ] 7.5 `specs/027-erp-export/design.md` §0.1 导出范围表续行本域
- [ ] 7.6 `.codebuddy/CONTEXT.md` §2（实体 / 仓储 / 读模型 / 导出模型）/ §3（`api/adminRequest.ts`、前端域、菜单分组）同步
- [ ] 7.7 `specs/ROADMAP.md` §4.2 状态更新（`063` → 已实现）；§3 覆盖矩阵同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run e2e:run` 全绿）。
- 提交 → 审批 → 完成闭环成立；自审拦截与驳回意见必填生效；终态只读。

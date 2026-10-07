---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：公告通知（oa-announcement）

> 依据 `specs/060-oa-announcement/design.md` 拆分；顺序即实现顺序。
> 前置：`028`（权限）、`030`（部门 / 员工）、`041`（站内信通道）均已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `e2e:run`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增枚举 `AnnouncementStatus` / `AnnouncementScope`（`App.Core/Entities/`）
- [ ] 1.2 新增实体 `Announcement` / `AnnouncementDepartment` / `AnnouncementRead` + `AnnouncementFieldConstraints`
- [ ] 1.3 EF 配置（`Persistence/Configurations/`：三张表 + 唯一索引 `(AnnouncementId, DepartmentId)` / `(AnnouncementId, UserId)`、级联删、列表索引）
- [ ] 1.4 `AppDbContext` 增 `DbSet`
- [ ] 1.5 迁移 `dotnet ef migrations add AddOaAnnouncement -p src/App.Infrastructure -s src/App.Api`（无种子）

## 二、后端：仓储与解析组件

- [ ] 2.1 `IAnnouncementRepository` + `Infrastructure/Repositories/AnnouncementRepository.cs`（§3.1 全部方法）
- [ ] 2.2 读模型 `AnnouncementListItem` / `AnnouncementDetail`（`App.Core/Abstractions/`，`sealed record`）
- [ ] 2.3 `IDepartmentRepository.GetDescendantIdsAsync`（部门子树，`030` 域）
- [ ] 2.4 `IEmployeeRepository.GetUserIdsByDepartmentIdsAsync`（批量取用户，避免 N+1）
- [ ] 2.5 `AnnouncementAudienceResolver`（可见性单点 + `CanAccess`）并注册 DI

## 三、后端：用例与端点

- [ ] 3.1 `Features/Announcements/GetAnnouncements`（四件套，含可见范围过滤 + `isRead` 筛选）
- [ ] 3.2 `Features/Announcements/GetAnnouncementById`（范围外 → `40300`）
- [ ] 3.3 `Features/Announcements/CreateAnnouncement`（草稿 + 部门全量写入）
- [ ] 3.4 `Features/Announcements/UpdateAnnouncement`（仅草稿 `40201`，全量替换）
- [ ] 3.5 `Features/Announcements/DeleteAnnouncement`（仅草稿 `40201`，级联删从表）
- [ ] 3.6 `Features/Announcements/PublishAnnouncement`（草稿 → 已发布 `40202`；同一事务展开范围 + `INotificationWriter` 批量发信）
- [ ] 3.7 `Features/Announcements/WithdrawAnnouncement`（已发布 → 已撤回 `40202`）
- [ ] 3.8 `Features/Announcements/MarkAnnouncementRead`（幂等）
- [ ] 3.9 `Errors/ErrorCode` 增 `40201` / `40202`；`Core/DependencyInjection.cs` 注册用例与校验器
- [ ] 3.10 `AnnouncementsController`（`/api/announcements`，8 动作，全部 `[RequirePermission]`）
- [ ] 3.11 全部写用例接入 `IAuditLogger`（`029` §0.1 续行；发布 / 撤回用 `StatusChange`）

## 四、单元测试（后端）

- [ ] 4.1 状态机：合法 2 条放行、其余路径 `40202`；非草稿编辑 / 删除 `40201`
- [ ] 4.2 可见范围：`All` 不过滤；部门含下级命中；未绑定员工仅 `All`
- [ ] 4.3 已读：重复标记不报错；`ReadCount` 正确
- [ ] 4.4 发布：发信被调用；**发信失败不影响发布结果**（记日志）
- [ ] 4.5 删除级联：从表记录一并清除
- [ ] 4.6 `FieldValidationConsistencyTests` 追加 `AnnouncementFieldConstraints`
- [ ] 4.7 `cd backend && dotnet build` / `dotnet test` 通过（含 `ApiPermissionMatrix` 守卫回归）

## 五、前端

- [ ] 5.1 `api/announcement.ts`（类型 + `ANNOUNCEMENT_STATUS_META` / `ANNOUNCEMENT_SCOPE_LABELS`）
- [ ] 5.2 `views/AnnouncementManagement/AnnouncementsView.vue`（列表 + 筛选 + 按状态 / 权限显隐操作列）
- [ ] 5.3 `views/AnnouncementManagement/AnnouncementDetailView.vue`（详情 + 进入即记已读 + 未读名单抽屉）
- [ ] 5.4 `views/AnnouncementManagement/AnnouncementFormDrawer.vue`（新建 / 编辑共用）
- [ ] 5.5 `router/index.ts` 注册路由 + `ROUTE_PERMISSIONS`；`AppLayout.vue` 新增「办公」分组与「公告」菜单项
- [ ] 5.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/announcement.spec.ts`：管理员发布公告 → 普通用户铃铛收信、详情可访问
- [ ] 6.2 同文件：已读计数 +1；范围外用户访问详情 → `40300`
- [ ] 6.3 同文件：草稿可编辑、已发布不可编辑 / 删除；撤回后为终态
- [ ] 6.4 `cd frontend && npm run e2e:run` 全量通过

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `announcements.*`（5 点）
- [ ] 7.2 `specs/029-erp-audit-log/design.md` §0.1 续行资源 `Announcement`（创建 / 更新 / 删除 / 状态变更）
- [ ] 7.3 `specs/041-erp-stock-alert/design.md` §2.1 续行 `NotificationType.Announcement = 6` + 文件头演进指针
- [ ] 7.4 `.codebuddy/CONTEXT.md` §2（实体 / 仓储 / 读模型）/ §3（`api/announcement.ts`、前端域、菜单分组）同步
- [ ] 7.5 `specs/ROADMAP.md` §4.2 状态更新（`060` → 已实现）；§3 覆盖矩阵同步（业务码 / 序号保持）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run e2e:run` 全绿）。
- 发布 → 可见 → 已读闭环成立；范围外详情 `40300`；发信失败不影响发布。

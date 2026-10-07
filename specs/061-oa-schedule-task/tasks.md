---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：日程与任务待办（oa-schedule-task）

> 依据 `specs/061-oa-schedule-task/design.md` 拆分；顺序即实现顺序。
> 前置：`028`（权限）、`030`（员工档案）、`041`（站内信通道 + 去重台账 + 定时宿主模式）均已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `e2e:run`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增枚举 `ScheduleStatus` / `TaskStatus` / `TaskPriority`（`App.Core/Entities/`）
- [ ] 1.2 新增实体 `Schedule` / `ScheduleParticipant` / `TodoTask` + `ScheduleFieldConstraints` / `TodoTaskFieldConstraints` / `OaReminderFieldConstraints`
- [ ] 1.3 EF 配置（三张表 + 索引 `(Status, StartAt)` / `(Status, DueAt)` / `(OwnerEmployeeId, Status)` + 唯一索引 `(ScheduleId, EmployeeId)` + 级联删）
- [ ] 1.4 `AppDbContext` 增 `DbSet`
- [ ] 1.5 迁移 `dotnet ef migrations add AddOaScheduleTask -p src/App.Infrastructure -s src/App.Api`（无种子）

## 二、后端：仓储与共享组件

- [ ] 2.1 `IScheduleRepository` + `ScheduleRepository`（§3.1 全部方法）
- [ ] 2.2 `ITodoTaskRepository` + `TodoTaskRepository`（§3.1 全部方法）
- [ ] 2.3 读模型 `ScheduleListItem` / `ScheduleDetail` / `ScheduleReminderItem` / `TaskListItem` / `TaskDetail` / `TaskReminderItem`
- [ ] 2.4 `ScheduleStatusRules` / `TaskStatusRules`（迁移白名单单点）
- [ ] 2.5 `IOaReminderScanner` + `OaReminderScanner`（`App.Core/Alerts/`，入参 `utcNow`）
- [ ] 2.6 `OaReminderBackgroundService`（`App.Api/HostedServices/`，配置节 `OaReminder`，`Enabled = false` 不注册）
- [ ] 2.7 `Infrastructure/DependencyInjection.cs` 注册仓储（新仓储）

## 三、后端：用例与端点

- [ ] 3.1 `Features/Schedules/GetSchedules` / `GetScheduleById` / `CreateSchedule` / `UpdateSchedule` / `CancelSchedule` / `ScanReminders`（各四件套）
- [ ] 3.2 `Features/Tasks/GetTasks` / `GetTaskById` / `CreateTask` / `UpdateTask` / `UpdateTaskStatus` / `DeleteTask`（各四件套）
- [ ] 3.3 `Errors/ErrorCode` 增 `40203` / `40204`；`Core/DependencyInjection.cs` 注册用例、校验器与 `OaReminderScanner`
- [ ] 3.4 `SchedulesController`（`/api/schedules`，含 `POST /scan-reminders` 固定段）/ `TasksController`（`/api/tasks`），全部 `[RequirePermission]`
- [ ] 3.5 全部写用例接入 `IAuditLogger`（`029` §0.1 续行；日程取消、任务状态变更用 `StatusChange`）

## 四、单元测试（后端）

- [ ] 4.1 日程：状态白名单；归属 `40204`；可见性（非参与人 `40204`）；`startAt >= endAt` → `40000`
- [ ] 4.2 任务：状态白名单与终态 `40203`；归属 `40204`；`Done` 写 `CompletedAt`、重开清空
- [ ] 4.3 提醒：命中 / 未命中 / 上限 `MaxItemsPerScan` 截断 / 去重跳过 / 接收人去重 / 发信失败降级
- [ ] 4.4 参与人全量替换（编辑后旧参与人清除）
- [ ] 4.5 `FieldValidationConsistencyTests` 追加三个新约束类
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过（含 `ApiPermissionMatrix` 守卫回归）

## 五、前端

- [ ] 5.1 `api/schedule.ts` / `api/task.ts`（类型 + 状态 / 优先级文案与颜色 + `TASK_STATUS_TRANSITIONS`）
- [ ] 5.2 `views/ScheduleTaskManagement/SchedulesView.vue` + `ScheduleDetailView.vue` + `ScheduleFormDrawer.vue`
- [ ] 5.3 `views/ScheduleTaskManagement/TasksView.vue` + `TaskFormDrawer.vue`（含行内状态推进）
- [ ] 5.4 `router/index.ts` 注册 `/schedules` `/tasks` + `ROUTE_PERMISSIONS`；`AppLayout.vue` 「办公」分组追加「日程」「任务」
- [ ] 5.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/schedule-task.spec.ts`：建日程 → 参与人在「我参与的」可见；非参与人访问详情被拒
- [ ] 6.2 同文件：建任务 → 推进「待办 → 进行中 → 已完成」；终态再编辑 → `40203`
- [ ] 6.3 同文件：手动触发提醒扫描 → 铃铛收到提醒（含去重不重复）
- [ ] 6.4 `cd frontend && npm run e2e:run` 全量通过

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `schedules.*`（4 点）/ `tasks.*`（5 点）
- [ ] 7.2 `specs/029-erp-audit-log/design.md` §0.1 续行资源 `Schedule` / `TodoTask`
- [ ] 7.3 `specs/041-erp-stock-alert/design.md` §2.1 续行 `NotificationType` 7 / 8、§2.3 续行 `AlertType` 3 / 4 + 文件头演进指针
- [ ] 7.4 `.codebuddy/CONTEXT.md` §2（实体 / 仓储 / 读模型 / 提醒宿主）/ §3（`api/schedule.ts`、`api/task.ts`、前端域、菜单分组）同步
- [ ] 7.5 `specs/ROADMAP.md` §4.2 状态更新（`061` → 已实现）；§3 覆盖矩阵同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run e2e:run` 全绿）。
- 日程可见性、任务状态白名单、提醒去重三处口径一致生效。

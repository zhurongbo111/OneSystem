---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：日程与任务待办（oa-schedule-task）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `041-erp-stock-alert`（定时扫描 + 去重台账 + 站内信）与 `044-erp-hcm-payroll`（同规格多域、员工下拉）为结构参照。
> **复用而非新建**：触达与去重走 `041`，人员数据走 `030`，权限走 `028`；**不改动任何 ERP 账务 / 库存 / 资金路径**。

## 0. 约定正文（唯一事实源）

### 0.1 状态、可见性与归属判据

| 枚举 | 取值 | 文案 | 说明 |
|---|---|---|---|
| `ScheduleStatus` | `Scheduled = 0` | 已安排 | 可编辑 / 取消 |
| | `Cancelled = 1` | 已取消 | **终态**；不可编辑 / 取消 |
| `TaskStatus` | `Todo = 0` | 待办 | |
| | `InProgress = 1` | 进行中 | |
| | `Done = 2` | 已完成 | **终态** |
| | `Cancelled = 3` | 已取消 | **终态** |
| `TaskPriority` | `Low = 0` / `Normal = 1` / `High = 2` | 低 / 中 / 高 | 仅展示排序，无流程含义 |

- **日程状态迁移白名单**：`Scheduled → Cancelled`；其余拒绝（`40202` 语义由任务复用同码，见 §0.3）。
- **任务状态迁移白名单**：`Todo → InProgress | Cancelled`、`InProgress → Done | Cancelled`；`Done` / `Cancelled` 为终态，不可再变更（`40203`）。
- **日程可见性**：仅**创建人 + 参与人**；其他人访问详情 / 操作 → 本码 `40204`（区别于功能级 `40300`：功能级是「没有 `schedules.view`」）。
- **任务可见性**：**不裁剪**（具备 `tasks.view` 即可见全部），协作性质决定。
- **归属判据（`40204`）**：日程编辑 / 取消仅**创建人**；任务编辑 / 删除仅**创建人或负责人**。
- **时间判据**：日程 `StartAt < EndAt`（否则 `40000`）；任务 `DueAt` 可空。

### 0.2 权限点与菜单

| 域 | key 前缀 | 权限点 | 对应接口 / 页面 |
|---|---|---|---|
| 日程 | `schedules` | `view` / `create` / `update` / `delete`（取消） | `/api/schedules*`、`/schedules` |
| 任务 | `tasks` | `view` / `create` / `update` / `status` / `delete` | `/api/tasks*`、`/tasks` |

- **菜单**：「办公」分组下「日程」（`/schedules`）与「任务」（`/tasks`）。
- **权限点清单唯一来源** `specs/028-erp-rbac/design.md` §0.2（实现时在其表内续行）。

### 0.3 业务错误码

| code | 含义 |
|---:|---|
| `40203` | 任务处于终态（已完成 / 已取消），不可编辑 / 删除 / 再变更状态 |
| `40204` | 非创建人 / 参与人（日程）或非创建人 / 负责人（任务），无权操作该对象 |

- 本规格占用 `40203`–`40204`（`ROADMAP` §6）；`40000` / `40300` / `40400` 复用。

### 0.4 提醒口径（唯一事实源）

| 提醒类型 | 触发对象 | 判定 | 接收人 |
|---|---|---|---|
| `ScheduleReminder` | 日程（`Scheduled`） | `StartAt` ∈ `[今天, 今天 + RemindLeadDays 天]` | 创建人 + 全部参与人 |
| `TaskDue` | 任务（非终态） | `DueAt` 非空且 `DueAt <= 今天 + RemindLeadDays 天` | 负责人 + 创建人 |

- `RemindLeadDays = 1`（`OaReminderFieldConstraints`，单一来源）。
- **去重键**：`(AlertType, ResourceKey, AlertDate)`，`ResourceKey` 形如 `schedule:<id>` / `task:<id>`，`AlertDate` = 扫描当日（UTC 日期）；**复用 `041` 的 `AlertRecords` 表**（`AlertType` 续行 `ScheduleReminder = 3` / `TaskDue = 4`，实现时在 `041` §2.3 登记）。
- 站内信 `LinkRouteName`：`schedules` / `tasks`，`LinkQuery = {"id":"<对象id>"}`。
- 单次扫描上限 `MaxItemsPerScan = 500`（同 `041` 防爆量口径）；接收人去重（同一日程创建人兼参与人只发一条）；无接收人则记 warning 并跳过。

## 1. 总体设计

```
日程 / 任务（前端 /schedules、/tasks + 抽屉表单）
  → SchedulesController / TasksController
    → Features/Schedules/<Action> | Features/Tasks/<Action>
      → IScheduleRepository / ITodoTaskRepository → PostgreSQL

到期提醒（复用 041 模式）
  OaReminderBackgroundService（BackgroundService，配置节 OaReminder，Enabled = false 时不注册）
    → IOaReminderScanner.ScanAsync(utcNow)（App.Core，纯业务、入参注入、可单测）
      → IScheduleRepository.GetDueForReminderAsync(...) / ITodoTaskRepository.GetDueForReminderAsync(...)
      → IAlertRecordRepository（041 去重：ExistsAsync / AddRangeAsync）
      → INotificationWriter（041 站内信写入）

手动触发（运维 / e2e）
  POST /api/schedules/scan-reminders → Schedules/ScanReminders → 同一个 IOaReminderScanner
```

核心原则：

- **业务在 Core、宿主只调度**：`IOaReminderScanner` 为纯业务组件（同 `041` `IStockAlertScanner`），`BackgroundService` 只负责周期调用与异常隔离。
- **只读 + 追加**：扫描只读日程 / 任务，只写 `Notifications` 与 `AlertRecords`，**不改业务数据**。
- **去重库层兜底**：`AlertRecords` 唯一索引拒绝并发重复；冲突计入「跳过」。
- **状态机单点**：日程 / 任务迁移白名单集中在 `ScheduleStatusRules` / `TaskStatusRules`（`Features/Schedules` / `Features/Tasks` 内），编辑与状态端点共用（同 `043` / `045` 口径）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/Schedule.cs` 与表 `Schedules`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Title` | `string` | `varchar(100)` | NOT NULL | 标题 |
| `StartAt` / `EndAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 起止时间（`StartAt < EndAt`） |
| `Location` | `string?` | `varchar(100)` | NULL | 地点 |
| `Remark` | `string?` | `varchar(500)` | NULL | 备注 |
| `Status` | `ScheduleStatus` | `smallint` | NOT NULL，默认 `0`，索引 | 状态 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人（创建人 = `CreatedBy`） |

- 索引：`(Status, StartAt)`（时间范围筛选 + 提醒扫描）、`(CreatedBy)`。

### 2.2 实体 `App.Core/Entities/ScheduleParticipant.cs` 与表 `ScheduleParticipants`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ScheduleId` | `Guid` | `uuid` | NOT NULL，外键（级联删） | 日程 |
| `EmployeeId` | `Guid` | `uuid` | NOT NULL | 参与人员工 |
| `EmployeeName` | `string` | `varchar(50)` | NOT NULL | 姓名**快照** |

- **唯一索引** `(ScheduleId, EmployeeId)`；编辑时全量替换（先删后插，同事务）。

### 2.3 实体 `App.Core/Entities/TodoTask.cs` 与表 `TodoTasks`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Title` | `string` | `varchar(100)` | NOT NULL | 标题 |
| `OwnerEmployeeId` | `Guid` | `uuid` | NOT NULL | 负责人（员工） |
| `OwnerName` | `string` | `varchar(50)` | NOT NULL | 负责人姓名**快照** |
| `DueAt` | `DateTimeOffset?` | `timestamptz` | NULL | 截止时间 |
| `Priority` | `TaskPriority` | `smallint` | NOT NULL，默认 `1` | 优先级 |
| `Status` | `TaskStatus` | `smallint` | NOT NULL，默认 `0`，索引 | 状态 |
| `Remark` | `string?` | `varchar(500)` | NULL | 备注 |
| `CompletedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 完成时间（置 `Done` 时写、重开清空） |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 索引：`(Status, DueAt)`（提醒扫描 + 筛选）、`(OwnerEmployeeId, Status)`、`(CreatedBy)`。
- **无协作人表**：任务为「一个负责人」模型（协作人属范围外，见需求 §5）。

### 2.4 枚举与字段约束

- `App.Core/Entities/ScheduleStatus.cs` / `TaskStatus.cs` / `TaskPriority.cs`（§0.1）。
- 新增 `App.Core/Entities/ScheduleFieldConstraints.cs`（`TitleMaxLength = 100`、`LocationMaxLength = 100`、`RemarkMaxLength = 500`、查询 `keyword` 上限 100）、`App.Core/Entities/TodoTaskFieldConstraints.cs`（`TitleMaxLength = 100`、`RemarkMaxLength = 500`）、`App.Core/Entities/OaReminderFieldConstraints.cs`（`RemindLeadDays = 1`、`MaxItemsPerScan = 500`）。
- 迁移：`dotnet ef migrations add AddOaScheduleTask -p src/App.Infrastructure -s src/App.Api`（三张表 + 索引）；**无种子**。

## 3. 后端设计

### 3.1 仓储

| 接口 | 方法 | 说明 |
|---|---|---|
| `IScheduleRepository` | `GetPagedAsync(Guid currentUserId, bool mineOnly, DateTimeOffset? from, DateTimeOffset? to, ScheduleStatus? status, page, pageSize, ...)` → `IReadOnlyList<ScheduleListItem>` | `mineOnly` = 我创建或我参与 |
| | `GetByIdAsync(id, ...)` → `ScheduleDetail?`（含参与人姓名列表） | 详情 |
| | `AddAsync` / `UpdateAsync` / `UpdateStatusAsync(id, status, ...)` | 写路径 |
| | `ReplaceParticipantsAsync(scheduleId, participants, ...)` / `IsParticipantAsync(scheduleId, userId, ...)` | 参与人全量替换 / 可见性判据（经 `Employees.UserId`） |
| | `GetDueForReminderAsync(today, leadDays, maxCount, ...)` → `IReadOnlyList<ScheduleReminderItem>` | 提醒扫描（联查参与人） |
| `ITodoTaskRepository` | `GetPagedAsync(TodoTaskFilter filter, page, pageSize, ...)` → `IReadOnlyList<TodoTaskListItem>` | `filter`：我负责的 / 我创建的 / 状态 / 截止日范围 |
| | `GetByIdAsync(id, ...)` → `TodoTaskDetail?` | |
| | `AddAsync` / `UpdateAsync` / `UpdateStatusAsync(id, status, completedAt, ...)` / `DeleteAsync(id, ...)` | |
| | `GetDueForReminderAsync(today, leadDays, maxCount, ...)` → `IReadOnlyList<TaskReminderItem>` | 提醒扫描（负责人 + 创建人） |

- 读模型（`sealed record`，`App.Core/Abstractions/`）：`ScheduleListItem` / `ScheduleDetail` / `TaskListItem` / `TaskDetail` / `ScheduleReminderItem` / `TaskReminderItem`；显示名一律用**快照列**（不逐行联查用户表）。
- 员工下拉复用 `030` 的 `GET /api/positions`… 实为 `getEmployees`（仅在职），前端直接复用既有接口（不新增）。

### 3.2 用例

| 域 | 用例 | 说明 |
|---|---|---|
| `Features/Schedules/` | `GetSchedules` | 列表（`mineOnly` / 时间范围 / 状态筛选） |
| | `GetScheduleById` | 详情；非创建人且非参与人 → `40204` |
| | `CreateSchedule` | 建日程（`Scheduled`）+ 参与人全量写 |
| | `UpdateSchedule` | 仅创建人（`40204`）+ 仅 `Scheduled`（终态拒绝） |
| | `CancelSchedule` | 仅创建人；`Scheduled → Cancelled` |
| | `ScanReminders` | 手动触发提醒扫描（复用 `IOaReminderScanner`，返回统计） |
| `Features/Tasks/` | `GetTasks` | 列表（负责人 / 创建人 / 状态 / 截止日筛选） |
| | `GetTaskById` | 详情 |
| | `CreateTask` | 建任务（`Todo`） |
| | `UpdateTask` | 仅创建人或负责人（`40204`）+ 非终态（`40203`）；**不改负责人**（请求体不含） |
| | `UpdateTaskStatus` | 状态白名单；终态拒绝（`40203`）；置 `Done` 写 `CompletedAt`，重开清空 |
| | `DeleteTask` | 仅创建人 + 非终态（`40203` / `40204`） |

- 端点：`SchedulesController`（`/api/schedules`），`TasksController`（`/api/tasks`）；`POST /api/schedules/scan-reminders` 固定段置于 `{id:guid}` 之前。
- 提醒组件：`App.Core/Alerts/OaReminderScanner.cs` 实现 `IOaReminderScanner`（入参 `utcNow`）；宿主 `App.Api/HostedServices/OaReminderBackgroundService.cs`（配置节 `OaReminder`，`Enabled = false` 时不注册，间隔 `IntervalMinutes` 同 `041`）。
- 全部写用例接入操作日志（`029` §0.1 续行）：资源 `Schedule` / `TodoTask`；状态流转用 `StatusChange`（日程取消、任务状态变更）。
- 权限标注：`[RequirePermission]` 按 §0.2。

### 3.3 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateScheduleRequest` / `UpdateScheduleRequest` | `title` 必填 ≤ 100；`startAt` / `endAt` 必填且 `startAt < endAt`；`location` ≤ 100；`remark` ≤ 500；`participantEmployeeIds` **至少一项**且去重 |
| `CreateTaskRequest` / `UpdateTaskRequest` | `title` 必填 ≤ 100；`ownerEmployeeId` 必填且存在且在职业员工（否则 `40000`）；`dueAt` 可选；`priority` 枚举合法；`remark` ≤ 500 |
| `UpdateTaskStatusRequest` | `status` 枚举合法（白名单在 Handler，`40203`） |
| 分页 | `page ≥ 1`、`pageSize` 默认 20 / 上限 100 |

## 4. 前端设计

- **域目录** `views/ScheduleTaskManagement/`（域内含日程与任务两组页面，与 `044` `HrmManagement/` 同形；菜单分列两处、路由前缀 `/schedules` `/tasks`）：

| 文件 | 说明 |
|---|---|
| `SchedulesView.vue` | 日程列表（「我参与的」开关 + 时间范围 + 状态筛选 + 操作列「详情 / 编辑 / 取消」按创建人与状态显隐） |
| `ScheduleDetailView.vue` | 详情（时间 / 地点 / 参与人 / 备注 + 编辑 / 取消） |
| `ScheduleFormDrawer.vue` | 新建 / 编辑共用（时间范围 `a-range-picker`（含时间）+ 参与人 `a-select` 多选，取 `getEmployees({ status: 1 })`） |
| `TasksView.vue` | 任务列表（负责人 / 状态 / 截止日筛选 + 行内状态推进 + 优先级标签） |
| `TaskFormDrawer.vue` | 新建 / 编辑共用（负责人下拉 / 截止时间 / 优先级 / 备注；编辑态不含负责人） |

- 接口层 `api/schedule.ts` / `api/task.ts`（类型 + `SCHEDULE_STATUS_META` / `TASK_STATUS_META` / `TASK_PRIORITY_META` 文案与颜色映射、`TASK_STATUS_TRANSITIONS` 白名单）。
- 路由与菜单：`router/index.ts` 注册（懒加载 + `ROUTE_PERMISSIONS` 登记 `schedules.view` / `tasks.view`）；`AppLayout.vue` 「办公」分组追加「日程」「任务」。
- 交互约定消费：列表页 `006` §0、操作列 `011` §0、按钮 loading `010` §0、表单 / 详情 `007` §0、组合式分区 `008` §0、图标 `018` §0。

## 5. 技术决策

| 决策 | 结论 | 理由 |
|---|---|---|
| 日程与任务合并为一个规格 | 两个业务域（`Features/Schedules` + `Features/Tasks`）同规格交付 | 二者共享「提醒 + 员工下拉 + 我的视图」三处实现，拆开会产生重复设计（同 `044` 考勤 + 薪酬） |
| 时间字段 | `DateTimeOffset`（`timestamptz`） | 后端规则 §5.2 强制；日程跨时区展示由前端按本地时区渲染 |
| 提醒去重 | **复用 `041` `AlertRecords`**，扩 `AlertType` | 单一去重台账，避免每域一套「当日只提醒一次」逻辑 |
| 扫描宿主 | 复用 `041` 的 `BackgroundService` 模式（业务在 Core、可单测） | 与 `041` 一致；`Enabled = false` 默认不注册，不影响既有启动 |
| 参与人存储 | 独立从表 + 姓名快照 | 编辑全量替换简单可靠；姓名快照避免列表联查用户表 |
| 任务负责人不可改 | 编辑请求体不含 `ownerEmployeeId` | 归属判据（`40204`）依赖负责人，改动会让「谁有权编辑」漂移；改派 = 重建任务 |
| 手动触发提醒 | 提供 `POST /api/schedules/scan-reminders` | e2e 与运维验证走同一入口，不维护第二套逻辑（同 `041`） |

## 6. 测试要点

- **单测**：日程 / 任务状态白名单（合法路径放行、终态拒绝 `40203`）；归属判据（`40204`）；日程可见性（非参与人 `40204`）；`startAt < endAt`；提醒扫描（命中 / 未命中 / 上限截断 / 去重跳过 / 发信失败降级）；参与人全量替换。
- **约束一致性**：三个新 `*FieldConstraints` 追加 `FieldValidationConsistencyTests`。
- **权限守卫**：`ApiPermissionMatrix` 覆盖两个 Controller 全部动作。
- **e2e**（`e2e/schedule-task.spec.ts`）：建日程 → 参与人可见（「我参与的」）+ 非参与人详情 `40300`/`40204`；建任务 → 推进至已完成 → 再编辑 `40203`；提醒手动扫描后铃铛收信。

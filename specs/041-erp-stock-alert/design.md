---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：库存预警通知（erp-stock-alert）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格新增 1 个业务能力（扫描）+ 1 张结果表 + 1 张去重台账 + 1 个后台宿主，**不改动任何既有写入路径**。
> 判定规则不重复定义：低库存见 `specs/038-erp-multi-warehouse/design.md` §0；近效期 / 过期见 `specs/040-erp-batch-expiry/design.md` §0。

## 0. 告警信号约定（唯一事实源）

| 信号类型（`AlertType`） | 判定（只读查询） | 跳转目标 |
|---|---|---|
| `LowStock = 0` 低库存 | 按「商品 × 仓」汇总：`SUM(Quantity) < MAX(SafetyStock)` 且 `MAX(SafetyStock) > 0` 且商品启用（`038` §0 口径） | `/inventory?warehouseId=&keyword=<商品编码>` |
| `ExpiringBatch = 1` 近效期 | 批次未过期且 `ExpiryDate <= 今天 + 30`（`BatchFieldConstraints.NearExpiryDays`）且该 `(商品, 仓, 批次)` 库存 > 0（`040` §0 口径） | `/batches?productId=&keyword=<批次号>` |
| `ExpiredBatch = 2` 过期 | 批次已过期（`ExpiryDate < 今天`）且该 `(商品, 仓, 批次)` 库存 > 0 | `/batches?productId=&keyword=<批次号>` |

- **接收人**：具备 `inventory.view` 权限的**启用**用户（`028` 的权限点）；无匹配用户 → 记 warning 日志并跳过该信号。
- **去重键**：`(AlertType, ResourceKey, AlertDate)`，其中 `ResourceKey` 形如 `product:<productId>:warehouse:<warehouseId>`（低库存）/ `batch:<batchId>:warehouse:<warehouseId>`（批次）；`AlertDate` 为扫描当日（UTC 日期）。**同键同日只告警一次**。
- **内容模板**（固定中文，含业务标识便于人读）：
  - 低库存：标题「库存不足提醒」，内容「上海仓 商品 A（A001）当前库存 10，低于安全库存 100」；
  - 近效期：标题「批次近效期提醒」，内容「商品 A（A001）批次 B1 将于 2026-10-01 到期（剩 10 天），当前库存 5」；
  - 过期：标题「批次已过期提醒」，内容「商品 A（A001）批次 B0 已于 2026-09-01 过期，当前库存 5，请及时处理」。
- **扫描结果语义**：每条信号 × 每个接收人 = 一条站内信；统计返回「信号数 / 生成消息数 / 跳过（去重）数 / 接收人数」。
- **无外部推送**：站内信即在系统内的唯一通道（范围外）。

## 1. 总体设计

```
定时扫描（宿主）
  StockAlertBackgroundService（BackgroundService，按 IntervalMinutes）
    → IStockAlertScanner.ScanAsync(utcNow)（App.Core，纯业务、可单测）
      → IStockAlertQueryRepository（只读：低库存信号 / 近效期与过期批次信号）
      → IAlertRecordRepository（去重：ExistsAsync / AddAsync）
      → IPermissionedUserQuery（按权限点取启用用户，`028` 的权限数据）
      → INotificationWriter（写入站内信，含批量）

手动触发（前端「立即扫描」）
  POST /api/notifications/scan → Notifications/ScanStockAlerts → 同一个 IStockAlertScanner

站内信（前端顶栏铃铛 + /notifications）
  → NotificationsController
    → Features/Notifications/GetNotifications | GetNotificationSummary
      | MarkNotificationRead | MarkAllNotificationsRead
      → INotificationRepository → PostgreSQL（Notifications）
```

核心原则：

- **业务逻辑在 Core、宿主只做调度**：`IStockAlertScanner` 是纯业务组件（入参 `utcNow`，可注入时钟 / 可单测），`BackgroundService` 只负责周期调用与异常隔离（后端规则：禁止把业务写进宿主）。
- **只读 + 追加**：扫描只读库存 / 批次 / 权限数据，只写 `Notifications` 与 `AlertRecords`，**不改任何业务数据**（不会因为告警而改库存或单据）。
- **去重在库层兜底**：`AlertRecords` 唯一索引 `(AlertType, ResourceKey, AlertDate)`，并发重复扫描由数据库拒绝（捕获唯一冲突并计入跳过）。
- **扫描与站内信解耦**：扫描器产出「信号 + 接收人 + 内容」，通过 `INotificationWriter` 落库；站内信能力（`041`）同时服务后续通知（如 `042` 的待审批提醒）。
- **手动触发与定时共用同一实现**：不维护第二套扫描逻辑（e2e 与运维验证都走同一入口）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`。

### 2.1 实体 `App.Core/Entities/Notification.cs` 与表 `Notifications`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `UserId` | `Guid` | `uuid` | NOT NULL，索引 | 接收人 |
| `Type` | `NotificationType` | `smallint` | NOT NULL | 0 = 低库存 1 = 近效期 2 = 过期 3 = 待审批（`042` 续行）4 = 审批结果（`042` 续行）5 = 逾期应收（预留） |
| `Title` | `string` | `varchar(50)` | NOT NULL | 标题 |
| `Content` | `string` | `varchar(500)` | NOT NULL | 内容 |
| `LinkRouteName` | `string?` | `varchar(50)` | NULL | 跳转路由名（前端 `router.push({ name, query })`） |
| `LinkQuery` | `string?` | `varchar(500)` | NULL | 跳转 query（JSON 文本，`jsonb`） |
| `ResourceKey` | `string?` | `varchar(100)` | NULL | 业务对象标识（用于去重键与排查） |
| `ReadAt` | `DateTimeOffset?` | `timestamptz` | NULL | 已读时间（空 = 未读） |
| `CreatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 生成时间 |

- 索引：`(UserId, ReadAt)`（未读查询）、`(UserId, CreatedAt DESC)`（列表）、`(Type)`。
- 无软删除、无更新接口（除标记已读）；外键不级联（用户不可删除，只停用）。

### 2.2 实体 `App.Core/Entities/AlertRecord.cs` 与表 `AlertRecords`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AlertType` | `AlertType` | `smallint` | NOT NULL | 与 `NotificationType` 的前三类取值一致 |
| `ResourceKey` | `string` | `varchar(100)` | NOT NULL | 业务对象键（§0） |
| `AlertDate` | `DateOnly` | `date` | NOT NULL | 告警日期（UTC 日期） |
| `CreatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 写入时间 |

- **唯一索引** `(AlertType, ResourceKey, AlertDate)`（去重兜底）。
- 台账只增不改；保留期不做清理（范围外）。

### 2.3 枚举与字段约束

- `App.Core/Entities/NotificationType.cs`（§2.1 取值）、`App.Core/Entities/AlertType.cs`（0–2，与前三者一致）；`042` 追加 `NotificationType.ApprovalPending = 3` 与 `ApprovalDecided = 4`（逾期应收预留值随之改为 5；追加枚举值不涉及迁移）。
- 新增 `App.Core/Entities/NotificationFieldConstraints.cs`：`TitleMaxLength = 50`、`ContentMaxLength = 500`、`ResourceKeyMaxLength = 100`、`LinkRouteNameMaxLength = 50`、`LinkQueryMaxLength = 500`、`RecentCount = 5`（顶栏下拉最近条数）、查询 `keyword` 上限 50（对齐 `ResourceKey` / `Title` 匹配列长）。
- 迁移：`dotnet ef migrations add AddErpStockAlert -p src/App.Infrastructure -s src/App.Api`（两张表 + 索引）。

## 3. 后端设计

### 3.1 仓储与查询接口

| 接口 | 方法 | 说明 |
|---|---|---|
| `INotificationRepository` | `AddRangeAsync(IReadOnlyList<Notification>, ...)` | 批量写入站内信 |
| | `GetPagedAsync(Guid userId, NotificationType? type, bool? isRead, int page, int pageSize, ...)` | 本人消息列表（`CreatedAt DESC`） |
| | `CountUnreadAsync(Guid userId, ...)` | 未读数 |
| | `MarkReadAsync(Guid id, Guid userId, DateTimeOffset readAt, ...)` | 单条已读（仅本人） |
| | `MarkAllReadAsync(Guid userId, DateTimeOffset readAt, ...)` | 全部已读 |
| `IAlertRecordRepository` | `ExistsAsync(AlertType, string resourceKey, DateOnly alertDate, ...)` / `AddRangeAsync(...)` | 去重判断与写入 |
| `IStockAlertQueryRepository` | `GetLowStockSignalsAsync(..., int maxCount, ...)` | 低库存信号（联查 `Products` / `Warehouses` / `Inventory`；单次扫描上限 `maxCount`） |
| | `GetExpiringBatchSignalsAsync(DateOnly today, int nearDays, ...)` / `GetExpiredBatchSignalsAsync(DateOnly today, ...)` | 批次信号（联查 `Batches` / `Inventory` / `Products` / `Warehouses`） |
| `IPermissionedUserQuery` | `GetEnabledUserIdsByPermissionAsync(string permissionKey, ...)` | 按权限点取启用用户 id（`028` 权限数据反向查询，放在该接口内以便单测替身） |

- 单次扫描上限：`StockAlertFieldConstraints.MaxSignalsPerScan = 500`（防异常数据量把一次扫描撑爆；超出记 warning 并写入前 500 条）。

### 3.2 扫描器（`App.Core/Alerts/StockAlertScanner.cs`）

```
ScanAsync(utcNow):
  1. alertDate = DateOnly.FromDateTime(utcNow.UtcDateTime)
  2. 取接收人（inventory.view 权限的启用用户）；为空 → 返回统计（signals=0，日志 warning）
  3. 取三类信号（各自查询，受上限约束）
  4. 逐信号：拼 ResourceKey → 去重判断（ExistsAsync / 已在本批内）→ 跳过或记录台账
  5. 对未跳过的信号：为每个接收人构造 Notification（标题 / 内容 / 跳转）→ 批量写入
  6. 返回统计（各类型信号数 / 生成消息数 / 跳过数 / 接收人数）
```

- 模板文案内联在扫描器（固定中文，`AlertSummary` 静态辅助拼装金额 / 数量 / 日期格式，复用 `029` 的 `AuditSummary` 若已实现，否则本域内联）。
- `ScanAsync` 内部**不打开事务**：台账与消息各自批量写入（先去重台账、再写消息）；极端情况（消息写入失败）会留下「当日不再告警但用户没收到」的窗口——用「先写消息、后写台账」的顺序规避（消息重复优于消息丢失），并在 §5 记录该取舍。

### 3.3 定时宿主（`App.Api/HostedServices/StockAlertBackgroundService.cs`）

- `BackgroundService`：启动后延迟 `StartupDelaySeconds = 60` 首跑，随后按 `IntervalMinutes` 循环；每轮 `try/catch` 捕获全部异常并 `ILogger` 记 error（**不终止循环**）。
- 配置节 `StockAlert`（`appsettings.json`）：`Enabled`（默认 `true`）、`IntervalMinutes`（默认 `60`，最小 5）、`StartupDelaySeconds`（默认 60）；`Enabled = false` 时不注册宿主（`Program` 条件注册）。
- 时钟：宿主向扫描器传 `DateTimeOffset.UtcNow`（唯一允许读时钟的位置；扫描器接收入参以便单测）。

### 3.4 错误码

> **无新增错误码**。手动扫描无业务失败分支（无接收人 / 无信号都返回 0 统计）；越权由 `028` 的权限点返回 `40300`；参数不合法走 `40000`。

### 3.5 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/notifications` | GET | `Notifications/GetNotifications` | `PagedResult<NotificationListItemDto>` | `notifications.view` / 40000 |
| `/api/notifications/summary` | GET | `Notifications/GetNotificationSummary` | `NotificationSummaryDto { unreadCount, recent: [] }`（含最近 `RecentCount` 条） | `notifications.view` / 40000 |
| `/api/notifications/{id:guid}/read` | PUT | `Notifications/MarkNotificationRead` | `null` | `notifications.view` / 40400 |
| `/api/notifications/read-all` | PUT | `Notifications/MarkAllNotificationsRead` | `{ affectedCount }` | `notifications.view` / 40000 |
| `/api/notifications/scan` | POST | `Notifications/ScanStockAlerts` | `StockAlertScanResultDto`（统计） | `notifications.scan` / 40000 |

- 路由注意：`summary` / `read-all` / `scan` 为固定段，置于 `{id:guid}` 之前。
- 所有读取接口**只看本人消息**（`ICurrentUser`），不接受 `userId` 入参（避免越权读他人消息）。
- `notifications.scan` 为 `028` §0.2 续行的新权限点（高风险、低频，建议仅管理员）。

### 3.6 校验规则（FluentValidation，仅格式层）

| 请求 | 规则 |
|---|---|
| `GetNotificationsRequest` | `page ≥ 1`；`pageSize` 1–100；`type` 可空合法值；`isRead` 可空；`keyword` ≤ 50 |
| 其余请求 | 无入参（`MarkNotificationRead` 仅路由 id） |

### 3.7 Swagger

- **不分组**（同既有约定）：5 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── notification.ts                 # 站内信接口层 + 类型文案映射
└── views/
    └── NotificationManagement/
        └── NotificationsView.vue       # 站内信列表
```

- 顶栏铃铛为 `AppLayout.vue` 的改造（不新建组件；下拉内容简单，直接内联）。

### 4.2 接口层

- `src/api/notification.ts`：`getNotifications` / `getNotificationSummary` / `markNotificationRead` / `markAllNotificationsRead` / `scanStockAlerts` + 类型 + 类型文案与颜色映射常量（全项目唯一来源）。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `notifications` | `notifications` | `NotificationsView` |

- `AppLayout.vue`：**顶栏铃铛**（不在侧边菜单）；`025` §0.2 总表在「系统」分组预留的 `notifications` 子项**不启用**（避免与顶栏重复入口）——本规格落地时在 §0.2 表内标注「入口为顶栏铃铛，不进侧边菜单」。

### 4.4 页面交互

**顶栏铃铛（`AppLayout.vue` 改造）**：`IconBell`（Tabler）+ 未读 `a-badge`（`unreadCount > 0` 时显示，`99+` 封顶）；点击展开 `a-popover`：最近 `RecentCount` 条（标题 / 类型标签 / 相对时间；未读加粗）+ 「查看全部」跳 `/notifications`；**点击单条** → 标记已读 + 按 `linkRouteName` / `linkQuery` 跳转；切换路由时刷新未读数（`router.afterEach` 拉一次 summary；不做轮询，避免打扰请求）。

**站内信列表 `NotificationsView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：筛选行（类型下拉 + 已读状态下拉（全部 / 未读 / 已读）+ 搜索 / 重置）；操作行（「全部已读」`a-popconfirm` + 「立即扫描」（`notifications.scan` 权限，`scanning` loading）+ 刷新 + 列设置）；列：序号、类型（`a-tag`）、标题、内容（`ellipsis` + `tooltip`）、时间、状态（未读 `a-tag arcoblue` / 已读灰字）、操作列（1 个「查看」→ 标记已读并跳转链接；无链接时仅标记已读）；服务端分页。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 消息列表 / 顶栏下拉加载 | `loading` / `summaryLoading` | 搜索 / 翻页 + 表格 / 下拉内容区 |
| 标记已读（单条） | `readingId` | 行内「查看」按钮 |
| 全部已读 | `markingAll` | 操作行按钮 |
| 立即扫描 | `scanning` | 操作行按钮（+ 防重入） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 站内信而非外部推送 | 只做 `Notifications` 表 + 顶栏 | 外部通道需 SMTP / webhook 配置与凭据管理（`AGENTS.md` §7 敏感配置），且有投递失败重试问题；站内信零依赖、可验证 |
| 业务逻辑在 Core、宿主只调度 | `IStockAlertScanner` + `BackgroundService` | 扫描规则可单测（不依赖定时器）；宿主符合「薄封装」原则，异常隔离不影响服务 |
| 当日一次去重 | `AlertRecords` 唯一索引 | 未补货期间每小时刷屏会让用户直接忽略告警；一天一次是「提醒」与「骚扰」的平衡点 |
| 去重台账与消息分离 | 先写消息、后写台账 | 极端失败下宁可「重复告警」也不「漏告警」（消息重复优于消息丢失）；台账写入冲突按跳过处理 |
| 接收人 = 权限点持有者 | `inventory.view` 反向查询 | `028` 已建立权限体系，用权限点表达「谁能收到」最自然，无需再做订阅配置（范围外） |
| 手动扫描接口 | `POST /api/notifications/scan` | 运维应急 + e2e 可覆盖扫描结果（定时任务在 e2e 中不可控）；权限点单列（高风险低频） |
| 扫描上限 500 | `MaxSignalsPerScan` | 防异常数据（如阈值被批量设错）一次生成海量消息；超限记 warning，下轮继续 |
| 顶栏不轮询 | 路由切换时刷新未读数 | 轮询会持续打扰后端且收益低；用户操作后（标记已读）本地即时更新 |
| 消息不删除 | 只标记已读 | 通知历史有追溯价值（谁在何时被提醒过）；存储量小 |
| `AlertType` 与 `NotificationType` 取值对齐（前三类） | 两个枚举 | 台账只关心前几类信号；未来若新增「非告警类通知」（如 `042` 的待审批）只需扩 `NotificationType`，不影响去重台账 |
| 跳转用路由名 + query JSON | `LinkRouteName` / `LinkQuery` | 前端 `router.push({ name, query })` 直接可用，避免拼 URL 字符串；路由改名后由前端映射表兜底（找不到路由时仅标记已读不跳转） |
| 无 RBAC 特例以外 | 复用 `notifications.view` + 新增 `notifications.scan` | 权限清单唯一事实源（`028` §0.2 续行） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储与查询接口；`ScanAsync` 的时间由入参注入（不读时钟）。

- **`StockAlertScanner`**：
  - 正常：3 类信号各 1 条 + 2 个接收人 → 生成 6 条消息、返回统计正确、台账各写 1 条；
  - 去重：同一天第二次扫描 → 跳过（`skipped` 计数正确、无新增消息）；
  - 跨天：`alertDate` 变化后可再次告警；
  - 无接收人 → 生成 0 条消息且不写台账（记 warning）；
  - 上限：信号数超 `MaxSignalsPerScan` → 只处理前 500 条；
  - 判定口径：低库存（阈值 0 不告警、库存 = 阈值不告警）、近效期（今天 + 30 命中、+31 不命中、已过期不重复算近效期）、过期（仍有库存才告警、库存 0 不告警）。
- **`Notifications` 用例**：`GetNotifications` 只看本人（断言传参 `userId` 来自 `ICurrentUser`）与筛选 / 分页；`GetNotificationSummary` 未读数与最近条数；`MarkNotificationRead` 成功 / 非本人 `40400`；`MarkAllNotificationsRead` 影响条数；`ScanStockAlerts` 调用扫描器并返回统计。
- **配置与宿主**：`StockAlert:Enabled = false` → 不注册宿主（可由 `Program` 集成的配置测试断言）；`IntervalMinutes < 5` → 规范化到 5（或启动校验失败，按实现取其一并断言）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Notifications.Title` 50 / `Content` 500 / `ResourceKey` 100 与常量一致；`keyword` 50 / 51；`AlertRecords` 唯一索引存在（EF 模型断言）。

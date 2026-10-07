---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：公告通知（oa-announcement）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `041-erp-stock-alert`（站内信通道 + 只读列表页）与 `043-erp-crm-presale`（状态流转 + 纯追加表）为结构参照。
> **复用而非新建**：触达走 `041` 的 `INotificationWriter`，组织范围依赖 `030` 的部门树，权限走 `028`；**不改动任何 ERP 账务 / 库存 / 资金路径**。

## 0. 约定正文（唯一事实源）

### 0.1 状态机与可见范围

| 枚举 | 取值 | 文案 | 说明 |
|---|---|---|---|
| `AnnouncementStatus` | `Draft = 0` | 草稿 | 可编辑 / 删除 / 发布 |
| | `Published = 1` | 已发布 | 可撤回；对可见范围可见并已发站内信 |
| | `Withdrawn = 2` | 已撤回 | **终态**；不可编辑 / 删除 / 再发布 |

| 枚举 | 取值 | 文案 | 可见范围 |
|---|---|---|---|
| `AnnouncementScope` | `All = 0` | 全员 | 全部登录用户 |
| | `Departments = 1` | 指定部门 | 指定部门（**含下级**）内已绑定账号的启用员工 |

- **状态迁移白名单**：`Draft → Published`（发布）、`Published → Withdrawn`（撤回）；其余迁移一律拒绝（`40202`）。
- **编辑 / 删除判据**：仅 `Draft` 可 `PUT` / `DELETE`，否则 `40201`。
- **可见性判据**：`All` → 全部登录用户；`Departments` → 当前用户经 `Employees.UserId` 所属部门 ∈ 指定部门子树。**未绑定员工**的用户仅 `All` 可见。
- **有效期（`ExpiresAt`）**：仅作展示提示（列表标「已过期」），**不做自动隐藏 / 自动撤回**（范围外）。

### 0.2 权限点与菜单

| 域 | key 前缀 | 权限点 | 对应接口 / 页面 |
|---|---|---|---|
| 公告 | `announcements` | `view` / `create` / `update` / `delete` / `publish` | `/api/announcements*`、`/announcements` |

- `publish` 同时保护发布与撤回（同为状态流转动作）。
- **菜单**：新增顶级分组「办公」→「公告」（`/announcements`）；分组可见性 = 组内任一子项 `.view`（`028` §0.1）。
- **权限点清单唯一来源** `specs/028-erp-rbac/design.md` §0.2（实现时在其表内续行）。

### 0.3 业务错误码

| code | 含义 |
|---:|---|
| `40201` | 仅草稿状态可编辑 / 删除 |
| `40202` | 公告状态不允许该操作（迁移不在白名单） |

- 本规格占用 `40201`–`40202`（`ROADMAP` §6）；`40000` / `40300` / `40400` 复用。

### 0.4 站内信续行

- 发布时写入 `NotificationType.Announcement = 6`（`041` §2.1 表续行；`046`–`058` 未占用 `6`）。**发信失败只记 warning 日志**，不影响发布事务结果（与 `042` 同口径）。

## 1. 总体设计

```
公告管理（前端 /announcements + 表单抽屉）
  → AnnouncementsController
    → Features/Announcements/CreateAnnouncement | UpdateAnnouncement（仅草稿）
      | DeleteAnnouncement（仅草稿）| GetAnnouncements | GetAnnouncementById

状态流转
  → PublishAnnouncement：Draft → Published
      → 同一事务：置状态 / PublishedAt+By → 展开可见范围 → INotificationWriter 批量写站内信
  → WithdrawAnnouncement：Published → Withdrawn

阅读回执
  → MarkAnnouncementRead → AnnouncementReads（唯一键 (AnnouncementId, UserId)，幂等）

可见性（横切，读路径）
  → AnnouncementAudienceResolver.ResolveAsync(scope, departmentIds)
      → 部门子树（IDepartmentRepository）+ Employees(UserId) → visibleUserIds
  → 列表：WHERE 范围命中（All 不过滤）；详情：范围外 → 40300
```

核心原则：

- **复用既有通道**：触达只调 `INotificationWriter`（`041`），不新建消息表、不写第二套铃铛逻辑。
- **范围解析单点**：`AnnouncementAudienceResolver` 是可见性与应读人数的**唯一**实现，列表 / 详情 / 发信共用。
- **回执纯追加**：`AnnouncementReads` 只增不改（唯一索引兜底幂等），与 `043` 的 `Activities` 同口径。
- **发布前零副作用**：草稿不产生任何站内信与回执；发布失败整体回滚（不出现「状态已改但没发信/发了一半」）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/Announcement.cs` 与表 `Announcements`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Title` | `string` | `varchar(100)` | NOT NULL | 标题 |
| `Content` | `string` | `varchar(4000)` | NOT NULL | 正文（纯文本，保留换行） |
| `Scope` | `AnnouncementScope` | `smallint` | NOT NULL，默认 `0` | 可见范围 |
| `Status` | `AnnouncementStatus` | `smallint` | NOT NULL，默认 `0`，索引 | 状态 |
| `IsPinned` | `bool` | `boolean` | NOT NULL，默认 `false` | 置顶 |
| `ExpiresAt` | `DateTimeOffset?` | `timestamptz` | NULL | 有效期（空 = 长期，仅展示提示） |
| `PublishedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 发布时间 |
| `PublishedBy` | `Guid?` | `uuid` | NULL | 发布人 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 索引：`(Status, IsPinned DESC, PublishedAt DESC)`（列表默认序）、`(Status)`。

### 2.2 实体 `App.Core/Entities/AnnouncementDepartment.cs` 与表 `AnnouncementDepartments`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AnnouncementId` | `Guid` | `uuid` | NOT NULL，外键（级联删） | 公告 |
| `DepartmentId` | `Guid` | `uuid` | NOT NULL | 可见部门（`Scope = Departments` 时有效） |

- **唯一索引** `(AnnouncementId, DepartmentId)`。
- 编辑时**全量替换**（先删后插，同事务）。

### 2.3 实体 `App.Core/Entities/AnnouncementRead.cs` 与表 `AnnouncementReads`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `AnnouncementId` | `Guid` | `uuid` | NOT NULL，外键（级联删） | 公告 |
| `UserId` | `Guid` | `uuid` | NOT NULL | 阅读人 |
| `ReadAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 已读时间 |

- **唯一索引** `(AnnouncementId, UserId)`（幂等兜底）；**纯追加表**，无 `UpdatedAt` / `UpdatedBy`（与 `041` `AlertRecords` / `043` `Activities` 同口径）。

### 2.4 枚举与字段约束

- `App.Core/Entities/AnnouncementStatus.cs`（§0.1）、`App.Core/Entities/AnnouncementScope.cs`（§0.1）。
- 新增 `App.Core/Entities/AnnouncementFieldConstraints.cs`：`TitleMaxLength = 100`、`ContentMaxLength = 4000`、查询 `keyword` 上限 100（对齐 `Title` 列长）。
- 迁移：`dotnet ef migrations add AddOaAnnouncement -p src/App.Infrastructure -s src/App.Api`（三张表 + 索引）；**无种子**。

## 3. 后端设计

### 3.1 仓储与解析组件

| 接口 / 类型 | 位置 | 方法 / 说明 |
|---|---|---|
| `IAnnouncementRepository` | `App.Core/Abstractions/` | `GetPagedAsync(status?, isRead?, keyword?, Guid currentUserId, IReadOnlyCollection<Guid>? visibleUserIds, page, pageSize, ...)` → `IReadOnlyList<AnnouncementListItem>` |
| | | `GetByIdAsync(id, Guid currentUserId, ...)` → `AnnouncementDetail?`（含 `ReadCount` / `AudienceCount`） |
| | | `AddAsync` / `UpdateAsync` / `UpdateStatusAsync(id, status, publishedAt, publishedBy, ...)` / `DeleteAsync`（级联删从表） |
| | | `ReplaceDepartmentsAsync(announcementId, departmentIds, ...)` / `GetDepartmentIdsAsync(announcementId, ...)` |
| | | `MarkReadAsync(announcementId, userId, readAt, ...)`（唯一冲突即忽略，幂等） / `GetUnreadUserIdsAsync(announcementId, ...)` |
| `AnnouncementAudienceResolver` | `App.Core/Features/Announcements/`（无状态技术组件，按职责命名） | `ResolveAsync(scope, departmentIds, ct)` → `IReadOnlySet<Guid> VisibleUserIds`（`All` 短路为 null = 不过滤）；`CanAccess(scope, departmentIds, userId)`（按 id 详情越权判据） |

- 部门子树：`IDepartmentRepository` 追加 `GetDescendantIdsAsync(rootIds)`（`030` 的部门树；`046` 若先落地则复用其同名方法）。
- 应读人数 / 未读名单：`IEmployeeRepository` 追加批量 `GetUserIdsByDepartmentIdsAsync`（若 `046` 已落地则复用）；用户显示名经 `IUserRepository.GetDisplayNamesByIdsAsync` 批量解析（`042` 惯例，避免 N+1）。
- 读模型（`sealed record`，`App.Core/Abstractions/`）：`AnnouncementListItem`（`Id` / `Title` / `Status` / `Scope` / `IsPinned` / `ExpiresAt` / `PublishedAt` / `ReadCount` / `AudienceCount` / `IsRead`）、`AnnouncementDetail`（+ `Content` / `PublishedByName` / `DepartmentNames` / `UnreadUsers`）。

### 3.2 用例（`Features/Announcements/`，8 个）

| 用例 | 说明 |
|---|---|
| `GetAnnouncements` | 列表：默认序 `IsPinned DESC, PublishedAt DESC`；非管理用户仅见 `Published` 且范围命中；`isRead` 筛选 |
| `GetAnnouncementById` | 详情：范围外或未发布（非管理）→ `40300` / `40400`；返回已读统计与未读名单 |
| `CreateAnnouncement` | 建草稿（`Status = Draft`）+ 写 `AnnouncementDepartments`（`Scope = Departments` 时必填至少一个部门，否则 `40000`） |
| `UpdateAnnouncement` | 仅草稿（`40201`）；明细 / 部门**全量替换** |
| `DeleteAnnouncement` | 仅草稿（`40201`）；级联删从表 |
| `PublishAnnouncement` | 仅草稿（`40202`）；置 `Published` + `PublishedAt` / `PublishedBy`；**同一事务**展开范围 → `INotificationWriter` 批量发信 |
| `WithdrawAnnouncement` | 仅已发布（`40202`）；置 `Withdrawn`（不回滚已发站内信） |
| `MarkAnnouncementRead` | 幂等写回执（已读重复调用不报错） |

- 端点 `AnnouncementsController`（`/api/announcements`）：`GET` / `GET {id:guid}` / `POST` / `PUT {id:guid}` / `DELETE {id:guid}` / `POST {id:guid}/publish` / `POST {id:guid}/withdraw` / `POST {id:guid}/read`；`{id:guid}/...` 固定段动作在后端路由内为独立动作（无路由歧义）。
- 全部写用例接入操作日志（`029` §0.1 续行）：资源 `Announcement`，发布 / 撤回用动作 `StatusChange`，创建 / 编辑用 `Create` / `Update`。
- 权限标注：`[RequirePermission]` 按 §0.2（未标注即 `40300`，`028` 默认拒绝）。

### 3.3 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateAnnouncementRequest` / `UpdateAnnouncementRequest` | `title` 必填 ≤ 100；`content` 必填 ≤ 4000；`scope` 枚举合法；`scope = Departments` 时 `departmentIds` **至少一项**且去重；`expiresAt`（可选）解析为 `DateTimeOffset` |
| 分页 | `page ≥ 1`、`pageSize` 默认 20 / 上限 100（`AGENTS.md` §4.3） |

## 4. 前端设计

- **域目录** `views/AnnouncementManagement/`（与后端 `Features/Announcements`、路由前缀 `/announcements`、e2e `announcement.spec.ts` 四者对齐）：

| 文件 | 说明 |
|---|---|
| `AnnouncementsView.vue` | 列表（状态 / 未读筛选 + 置顶标记 + 已读标记 + 操作列「详情 / 编辑 / 发布 / 撤回 / 删除」按状态与权限显隐） |
| `AnnouncementDetailView.vue` | 详情（正文 + 置顶 / 有效期 / 发布信息 + 管理侧「已读统计 + 未读名单」抽屉）；进入即调 `read` 记回执 |
| `AnnouncementFormDrawer.vue` | 新建 / 编辑共用（标题 / 正文 `a-textarea` / 可见范围 `a-radio` + 部门 `a-tree-select`（多选、仅启用）/ 置顶 / 有效期 `a-date-picker`） |

- 接口层 `api/announcement.ts`（类型 + `ANNOUNCEMENT_STATUS_META` / `ANNOUNCEMENT_SCOPE_LABELS` 文案与颜色映射）。
- 路由与菜单：`router/index.ts` 注册（懒加载 + `ROUTE_PERMISSIONS` 登记 `announcements.view`）；`AppLayout.vue` 新增顶级分组「办公」（首个子项「公告」）。
- 交互约定消费（正文见对照表，`CONTEXT.md` §6）：列表页 `006` §0、操作列 `011` §0、按钮 loading `010` §0、表单 / 详情 `007` §0、组合式分区 `008` §0、图标 `018` §0。

## 5. 技术决策

| 决策 | 结论 | 理由 |
|---|---|---|
| 消息通道 | **复用** `041` `INotificationWriter`，不新建表 | 单一通道（`041` §0 已定「站内信即唯一通道」），避免两套铃铛 / 两套已读语义 |
| 可见范围 | 业务属性（`Scope` + 部门集合），**不接 `046` 数据级权限** | 数据范围是「按角色裁剪可见数据」，公告范围是「发布人指定受众」，语义不同（`ROADMAP` §6.11） |
| 已读回执 | 独立纯追加表 + 唯一索引 | 与 `041` `AlertRecords` / `043` `Activities` 同口径；幂等由库层兜底 |
| 撤回语义 | 只改状态，**不回滚已发站内信** | 消息可能已被阅读，回滚会造成「铃铛里的消息凭空消失」 |
| 有效期 | 仅展示提示，不自动隐藏 | 自动隐藏会让「已读统计」口径漂移；裁剪进 §5 范围外 |

## 6. 测试要点

- **单测**（`tests/App.Tests/`）：状态机白名单（合法 2 条 / 非法路径拒绝 `40202`）；草稿限制（`40201`）；可见范围过滤（`All` 不过滤、部门含下级、未绑定员工仅 `All`）；已读幂等；发布时发信调用与**发信失败不影响发布**；`DeleteAnnouncement` 级联删从表。
- **约束一致性**：`AnnouncementFieldConstraints` 追加 `FieldValidationConsistencyTests`。
- **权限守卫**：`ApiPermissionMatrix` 覆盖 8 个动作（漏标即失败）。
- **e2e**（`e2e/announcement.spec.ts`）：管理员发布 → 普通用户铃铛收信 + 详情可访问 + 已读计数 +1；范围外用户访问详情 → `40300`；草稿态编辑可用、已发布态编辑不可用。

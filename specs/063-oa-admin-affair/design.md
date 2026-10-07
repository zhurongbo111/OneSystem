---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：行政事务申请（oa-admin-affair）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `042-erp-approval`（审批人取值、自审禁止、站内信提醒）与 `045-erp-crm-service`（状态流转白名单）为结构参照。
> **复用而非新建**：审批口径与站内信复用 `041` / `042`，人员数据走 `030`，权限走 `028`；**不接 `042` 的阈值规则表**、不改动任何 ERP 账务 / 库存 / 资金路径。

## 0. 约定正文（唯一事实源）

### 0.1 类型与状态机

| 枚举 | 取值 | 文案 |
|---|---|---|
| `AdminRequestType` | `OfficeSupply = 0` | 办公用品 |
| | `Seal = 1` | 用印 |
| | `Vehicle = 2` | 车辆 |
| | `Other = 3` | 其他 |
| `AdminRequestStatus` | `Pending = 0` | 待审批 |
| | `Approved = 1` | 已批准 |
| | `Rejected = 2` | 已驳回（**终态**） |
| | `Completed = 3` | 已完成（**终态**） |
| | `Cancelled = 4` | 已撤销（**终态**） |

- **状态迁移白名单**：`Pending → Approved | Rejected | Cancelled`、`Approved → Completed`；其余一律拒绝（`40209`）。
- **撤销判据**：仅**申请人本人**且仅 `Pending`（否则 `40208`）。
- **完成判据**：仅 `Approved`（否则 `40209`）。

### 0.2 审批口径（复用 `042`，不走阈值）

| 项 | 规则 |
|---|---|
| 审批人 | 具备 `adminAffairs.approve` 权限的启用用户（`028`） |
| 触发 | **一律需要审批**（不接 `042` 的 `ApprovalRules` 阈值表） |
| 自审禁止 | 申请人 == 审批人 → `40210`（同 `042` `40137` 口径） |
| 审批意见 | 通过时可空、**驳回时必填**（≤ 200 字符） |
| 待审批禁改 | `Pending` 状态下申请人只能撤销，不能编辑（要改就撤销后重提） |

- 与 `042` 的关系：**共享「审批人取值 + 自审禁止 + 驳回必填意见」三处口径，不共享规则表与状态枚举**（`042` 的 `ApprovalStatus` 面向单据审批，本规格状态含办理环节，语义不同）。实现时在 `042` §0 留一行指针。

### 0.3 权限点与菜单

| 域 | key 前缀 | 权限点 | 对应接口 / 页面 |
|---|---|---|---|
| 行政事务 | `adminAffairs` | `view` / `create` / `cancel` / `approve` / `export` | `/api/admin-requests*`、`/admin-affairs` |

- `approve` 同时保护通过 / 驳回 / 完成（同为审批侧动作）。
- **菜单**：「办公」分组下「行政事务」（`/admin-affairs`）。
- **权限点清单唯一来源** `specs/028-erp-rbac/design.md` §0.2（实现时在其表内续行）。

### 0.4 单号与业务错误码

| 单据 | 前缀 | 形式 |
|---|---|---|
| 行政申请单 | `AF` | `<前缀> + yyyyMMdd + 4 位序号`（`015` §3.6 前缀参数化机制） |

| code | 含义 |
|---:|---|
| `40208` | 仅申请人本人且为「待审批」时可撤销 |
| `40209` | 申请单状态不允许该流转（迁移不在白名单） |
| `40210` | 不可自审（申请人 == 审批人） |

- 本规格占用 `40208`–`40210`（`ROADMAP` §6）；`40000` / `40300` / `40400` 复用。

### 0.5 站内信续行

- 提交时写 `NotificationType.AdminAffairPending = 9`（接收人 = 具备 `adminAffairs.approve` 的启用用户，经 `041` `IPermissionedUserQuery`）；决策（通过 / 驳回 / 完成）时写 `AdminAffairDecided = 10`（接收人 = 申请人）。实现时在 `041` §2.1 续行。
- `LinkRouteName = adminAffairs`，`LinkQuery = {"id":"<申请单id>"}`；**发信失败只记 warning 日志**，不影响状态流转（同 `042`）。

## 1. 总体设计

```
申请（前端 /admin-affairs + 表单抽屉）
  → AdminRequestsController
    → Features/AdminRequests/CreateAdminRequest
        → GenerateNoAsync（AF 前缀）→ 落库（Pending）
        → INotificationWriter：向 adminAffairs.approve 用户发「待审批」（0.5）
      | CancelAdminRequest（申请人本人 + Pending，40208）

审批（审批抽屉）
  → ApproveAdminRequest / RejectAdminRequest / CompleteAdminRequest
    → 自审禁止（40210）→ 状态白名单（40209；驳回意见必填 40000）
    → INotificationWriter：向申请人发「审批结果」
```

核心原则：

- **不引入第二套审批引擎**：审批人取值与自审判据复用 `042` 口径，状态与规则表自持（`ROADMAP` §6.11）。
- **状态机单点**：`AdminRequestStatusRules`（`Features/AdminRequests/` 内）为唯一判据，撤销 / 审批 / 完成端点共用。
- **可追溯**：决策人 / 决策时间 / 决策意见 / 完成时间均落库；写路径接入操作日志（`029`）。
- **发信与主流程解耦**：发信失败不阻塞状态流转，仅记日志。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/AdminRequest.cs` 与表 `AdminRequests`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `RequestNo` | `string` | `varchar(20)` | NOT NULL，**唯一索引** | 单号（`AF…`） |
| `Type` | `AdminRequestType` | `smallint` | NOT NULL | 申请类型 |
| `Subject` | `string` | `varchar(100)` | NOT NULL | 事由 |
| `Detail` | `string` | `varchar(500)` | NOT NULL | 详细说明 |
| `Quantity` | `int?` | `integer` | NULL | 数量（办公用品常用，> 0） |
| `ExpectedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 期望完成 / 归还时间 |
| `Status` | `AdminRequestStatus` | `smallint` | NOT NULL，默认 `0`，索引 | 状态 |
| `ApplicantUserId` | `Guid` | `uuid` | NOT NULL，索引 | 申请人（当前用户） |
| `ApplicantName` | `string` | `varchar(50)` | NOT NULL | 申请人姓名**快照** |
| `DecidedBy` | `Guid?` | `uuid` | NULL | 审批人 |
| `DecidedByName` | `string?` | `varchar(50)` | NULL | 审批人姓名**快照** |
| `DecidedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 决策时间 |
| `DecisionRemark` | `string?` | `varchar(200)` | NULL | 审批意见（驳回必填） |
| `CompletedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 完成时间 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人（= 申请人） |

- 索引：`(Status, CreatedAt DESC)`（列表 / 待我审批）、`(ApplicantUserId, CreatedAt DESC)`（我的申请）、`(RequestNo)` 唯一。
- 单号生成：`SELECT MAX(RequestNo) WHERE RequestNo LIKE 'AF<yyyyMMdd>%'` → 序号 + 1（`015` §3.6 同口径）。

### 2.2 枚举与字段约束

- `App.Core/Entities/AdminRequestType.cs` / `AdminRequestStatus.cs`（§0.1）。
- 新增 `App.Core/Entities/AdminRequestFieldConstraints.cs`：`RequestNoMaxLength = 20`、`SubjectMaxLength = 100`、`DetailMaxLength = 500`、`DecisionRemarkMaxLength = 200`、`ApplicantNameMaxLength = 50`、查询 `keyword` 上限 100。
- 迁移：`dotnet ef migrations add AddOaAdminAffair -p src/App.Infrastructure -s src/App.Api`（一张表 + 索引）；**无种子**。

## 3. 后端设计

### 3.1 仓储

| 接口 | 方法 | 说明 |
|---|---|---|
| `IAdminRequestRepository` | `GetPagedAsync(AdminRequestType? type, AdminRequestStatus? status, Guid currentUserId, bool mineOnly, string? keyword, page, pageSize, ...)` → `IReadOnlyList<AdminRequestListItem>` | `mineOnly` = 我提交的；`keyword` 模糊匹配事由 / 单号 / 申请人姓名 |
| | `GetByIdAsync(id, ...)` → `AdminRequestDetail?` | 详情 |
| | `GenerateNoAsync(DateOnly date, ...)` | 单号生成（`AF`） |
| | `AddAsync` / `UpdateStatusAsync(id, status, decidedBy, decidedByName, decidedAt, decisionRemark, completedAt, ...)` | 写路径（无一般 `UpdateAsync`——待审批禁改） |

- 读模型（`sealed record`，`App.Core/Abstractions/`）：`AdminRequestListItem` / `AdminRequestDetail`（姓名取快照列，不联查用户表）。
- 按权限取审批人：复用 `041` 的 `IPermissionedUserQuery.GetEnabledUserIdsByPermissionAsync("adminAffairs.approve", ...)`（SuperAdmin 视为具备）。

### 3.2 用例（`Features/AdminRequests/`，8 个）

| 用例 | 说明 |
|---|---|
| `GetAdminRequests` | 列表（类型 / 状态 / 我的申请 / 关键词） |
| `GetAdminRequestById` | 详情 |
| `CreateAdminRequest` | 生成单号（`AF`）+ 落库（`Pending`）+ 向审批人发信 |
| `CancelAdminRequest` | 仅申请人本人 + `Pending`（`40208`） |
| `ApproveAdminRequest` | 自审禁止（`40210`）+ `Pending → Approved`（`40209`）+ 向申请人发信 |
| `RejectAdminRequest` | 自审禁止 + 驳回意见必填（`40000`）+ `Pending → Rejected` + 向申请人发信 |
| `CompleteAdminRequest` | 仅 `Approved → Completed`（`40209`）+ 写 `CompletedAt` + 向申请人发信 |
| `ExportAdminRequests` | 导出（复用 `GetPagedAsync` 过滤；`027` 文件流契约例外） |

- 端点 `AdminRequestsController`（`/api/admin-requests`，8 动作）：`GET` / `GET {id:guid}` / `POST` / `POST {id:guid}/cancel` / `POST {id:guid}/approve` / `POST {id:guid}/reject` / `POST {id:guid}/complete` / `GET export`（固定段 `export` 在前）。
- 全部写用例接入操作日志（`029` §0.1 续行）：资源 `AdminRequest`；撤销 / 审批 / 驳回 / 完成用 `StatusChange`（驳回 / 通过动作在 `042` 惯例下用 `Approve`，本规格统一用 `StatusChange` 并在 §0.1 实现时登记）。

  嗯，这里要一致：`042` 用 `Approve`（`AuditAction.Approve = 7`）。行政审批也用 `Approve` 更贴切。我改为：创建用 `Create`，撤销用 `StatusChange`，通过 / 驳回用 `Approve`，完成用 `StatusChange`。

- 权限标注：`[RequirePermission]` 按 §0.3。

### 3.3 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateAdminRequestRequest` | `type` 枚举合法必填；`subject` 必填 ≤ 100；`detail` 必填 ≤ 500；`quantity`（可选）> 0；`expectedAt`（可选）解析为 `DateTimeOffset` |
| `RejectAdminRequestRequest` | `decisionRemark` **必填** ≤ 200（为空 → `40000`） |
| 分页 / 导出 | `page ≥ 1`、`pageSize` 默认 20 / 上限 100；导出上限 `ExportFieldConstraints.MaxRows` |

## 4. 前端设计

- **域目录** `views/AdminAffairManagement/`（与后端 `Features/AdminRequests`、路由前缀 `/admin-affairs`、e2e `admin-affair.spec.ts` 对齐）：

| 文件 | 说明 |
|---|---|
| `AdminRequestsView.vue` | 列表（「待我审批」/「我的申请」/「全部」tab + 类型 / 状态 / 关键词筛选 + 导出 + 操作列按状态与权限显隐「详情 / 撤销 / 审批 / 完成」） |
| `AdminRequestDetailDrawer.vue` | 详情抽屉（类型 / 事由 / 说明 / 数量 / 期望时间 / 申请人 + 审批信息） |
| `AdminAffairDecideDrawer.vue` | 审批抽屉（申请摘要 + 通过 / 驳回（意见必填）+ 完成） |
| `AdminRequestFormDrawer.vue` | 新建（类型 `a-radio` / 事由 / 说明 `a-textarea` / 数量 `a-input-number` / 期望时间 `a-date-picker`） |

- 接口层 `api/adminRequest.ts`（类型 + `ADMIN_REQUEST_TYPE_LABELS` / `ADMIN_REQUEST_STATUS_META` / `ADMIN_REQUEST_ACTIONS` 可用动作映射）。
- 路由与菜单：`router/index.ts` 注册（懒加载 + `ROUTE_PERMISSIONS` 登记 `adminAffairs.view`）；`AppLayout.vue` 「办公」分组追加「行政事务」。
- 交互约定消费：列表页 `006` §0、操作列 `011` §0、按钮 loading `010` §0、表单 / 详情 `007` §0、组合式分区 `008` §0、图标 `018` §0。

## 5. 技术决策

| 决策 | 结论 | 理由 |
|---|---|---|
| 审批机制 | 固定单级（具备 `adminAffairs.approve` 者审批），**不接 `042` 阈值规则表** | 阈值面向金额型 ERP 单据；行政申请一律需审批，语义不符（`ROADMAP` §6.11） |
| 审批口径 | 复用 `042` 的「审批人取值 + 自审禁止 + 驳回必填意见」 | 避免同一平台两种自审语义；不新建审批引擎 |
| 状态枚举 | 自持（含办理环节 `Completed`） | `042` 的 `ApprovalStatus` 无「已完成」概念，强行复用会把办理状态混进审批状态 |
| 待审批禁改 | 无 `UpdateAsync`，只能撤销后重提 | 审批中的内容变化会让「审批人看到的内容」与「最终内容」不一致 |
| 类型用单表 + 枚举 | 不做每类一张表 | 字段一致（事由 / 说明 / 数量 / 期望时间），分表会造成四份用例与页面 |
| 数量不做库存联动 | 只记申请数量 | 办公用品库存台账属另一套模型（范围外），联动会引入未定义的库存口径 |

## 6. 测试要点

- **单测**：状态白名单（合法路径放行、终态拒绝 `40209`）；撤销归属（非本人 / 非 `Pending` → `40208`）；**自审禁止 `40210`**；驳回意见必填 `40000`；单号生成（跨日重置、长度约束）；发信调用与失败降级；`Pending` 无编辑入口。
- **约束一致性**：`AdminRequestFieldConstraints` 追加 `FieldValidationConsistencyTests`。
- **权限守卫**：`ApiPermissionMatrix` 覆盖 8 个动作。
- **e2e**（`e2e/admin-affair.spec.ts`）：提交 → 审批人铃铛收信 → 通过 → 完成；驳回未填意见被拦；自审 → `40210`（构造带 `adminAffairs.approve` 的申请人角色）；导出下载成功。

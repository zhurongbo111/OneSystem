---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：公文收发登记（oa-document）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `045-erp-crm-service`（状态流转白名单 + 不做删除的归档口径）与 `015-erp-purchase` §3.6（单号前缀参数化生成）为结构参照。
> **复用而非新建**：人员数据走 `030`，权限走 `028`，导出走 `027`；**不改动任何 ERP 账务 / 库存 / 资金路径**。

## 0. 约定正文（唯一事实源）

### 0.1 方向与状态机

| 枚举 | 取值 | 文案 | 说明 |
|---|---|---|---|
| `DocumentDirection` | `Incoming = 0` | 收文 | 上级 / 外部来文 |
| | `Outgoing = 1` | 发文 | 本单位对外发文 |
| `DocumentStatus` | `Registered = 0` | 已登记 | 可编辑 / 删除 / 流转 |
| | `Processing = 1` | 办理中 | 可流转 |
| | `Archived = 2` | 已归档 | **终态**；不可编辑 / 删除 / 再流转 |
| | `Cancelled = 3` | 已作废 | **终态** |

- **状态迁移白名单**：`Registered → Processing | Archived | Cancelled`、`Processing → Archived | Cancelled`；其余一律拒绝（`40206`）。
- **编辑 / 删除判据**：仅 `Registered`（否则 `40205`）。
- **不可改字段**：`DocumentNo`、`Direction`（请求体不含；`AGENTS.md` §4.5）。
- `Archived` 时记录 `ArchivedAt`。

### 0.2 单号规则

| 方向 | 前缀 | 形式 | 示例 |
|---|---|---|---|
| 收文 | `OD` | `<前缀> + yyyyMMdd + 4 位序号` | `OD202610070001` |
| 发文 | `OW` | 同上 | `OW202610070001` |

- 生成机制沿用 `specs/015-erp-purchase/design.md` §3.6（前缀参数化 + 当日序号），**按方向分别计数**；唯一索引兜底并发冲突（沿用 `OrderNoConflictException` 处理）。
- 前缀分配已在 `ROADMAP` §6.7 登记（起草时），本规格不另立前缀。

### 0.3 权限点与菜单

| 域 | key 前缀 | 权限点 | 对应接口 / 页面 |
|---|---|---|---|
| 公文 | `documents` | `view` / `create` / `update` / `delete` / `status` / `export` | `/api/official-documents*`、`/documents` |

- **菜单**：「办公」分组下「公文」（`/documents`）。
- **权限点清单唯一来源** `specs/028-erp-rbac/design.md` §0.2（实现时在其表内续行）。

### 0.4 业务错误码

| code | 含义 |
|---:|---|
| `40205` | 仅「已登记」状态可编辑 / 删除 |
| `40206` | 公文状态不允许该流转（迁移不在白名单） |

- 本规格占用 `40205`–`40206`（`ROADMAP` §6 区间为 `40205`–`40207`，保留 `40207` 备用）；`40000` / `40300` / `40400` 复用。

## 1. 总体设计

```
公文台账（前端 /documents + 详情 / 表单抽屉）
  → OfficialDocumentsController
    → Features/OfficialDocuments/CreateOfficialDocument
        → IOfficialDocumentRepository.GenerateNoAsync(direction, date)（OD / OW）
        → AddAsync（唯一索引兜底并发）
      | UpdateOfficialDocument | DeleteOfficialDocument（仅 Registered，40205）
      | GetOfficialDocuments | GetOfficialDocumentById
      | UpdateOfficialDocumentStatus（白名单，40206）
      | ExportOfficialDocuments（复用 GetPagedAsync 过滤，027 文件流契约例外）
```

核心原则：

- **纯台账**：本域不触碰 ERP 业务数据，也不触发审批 / 库存 / 凭证；状态流转只改自身一行。
- **单号后端生成**：前端不可指定单号；并发安全由唯一索引 + 既有冲突处理兜底。
- **流转白名单单点**：`DocumentStatusRules`（`Features/OfficialDocuments/` 内）为唯一判据，状态端点与编辑 / 删除判据共用。
- **归档即终态**：归档后只读（同 `045` 服务工单「关闭即归档」口径）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；业务日期用 `DateOnly` → `date`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/OfficialDocument.cs` 与表 `OfficialDocuments`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `DocumentNo` | `string` | `varchar(20)` | NOT NULL，**唯一索引** | 单号（§0.2） |
| `Direction` | `DocumentDirection` | `smallint` | NOT NULL，索引 | 方向（不可改） |
| `Title` | `string` | `varchar(200)` | NOT NULL | 标题 |
| `CounterpartName` | `string` | `varchar(100)` | NOT NULL | 对方单位（收文 = 来文单位 / 发文 = 主送单位） |
| `CounterpartDocNo` | `string?` | `varchar(50)` | NULL | 对方文号（收文常用，可空） |
| `DocumentDate` | `DateOnly` | `date` | NOT NULL | 收文 / 发文日期 |
| `HandlerEmployeeId` | `Guid?` | `uuid` | NULL | 经办人（员工） |
| `HandlerName` | `string?` | `varchar(50)` | NULL | 经办人姓名**快照** |
| `Status` | `DocumentStatus` | `smallint` | NOT NULL，默认 `0`，索引 | 状态 |
| `ArchivedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 归档时间 |
| `Remark` | `string?` | `varchar(500)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 索引：`(Direction, Status, DocumentDate DESC)`（列表默认序 + 筛选）、`(DocumentNo)` 唯一。
- 单号生成：`SELECT MAX(DocumentNo) WHERE DocumentNo LIKE '<前缀><yyyyMMdd>%'` → 序号 + 1（`015` §3.6 同口径，按方向分别计数）。

### 2.2 枚举与字段约束

- `App.Core/Entities/DocumentDirection.cs` / `DocumentStatus.cs`（§0.1）。
- 新增 `App.Core/Entities/OfficialDocumentFieldConstraints.cs`：`DocumentNoMaxLength = 20`、`TitleMaxLength = 200`、`CounterpartNameMaxLength = 100`、`CounterpartDocNoMaxLength = 50`、`RemarkMaxLength = 500`、查询 `keyword` 上限 200。
- `DocumentNo` 长度与生成规则一致（`20` = 2 前缀 + 8 日期 + 4 序号 + 余量）；超出即约束失败（守卫测试）。
- 迁移：`dotnet ef migrations add AddOaOfficialDocument -p src/App.Infrastructure -s src/App.Api`（一张表 + 索引）；**无种子**。

## 3. 后端设计

### 3.1 仓储

| 接口 | 方法 | 说明 |
|---|---|---|
| `IOfficialDocumentRepository` | `GetPagedAsync(DocumentDirection? direction, DocumentStatus? status, DateOnly? from, DateOnly? to, string? keyword, page, pageSize, ...)` → `IReadOnlyList<OfficialDocumentListItem>` | 列表（默认 `DocumentDate DESC, DocumentNo DESC`；`keyword` 模糊匹配标题 / 单号 / 对方单位） |
| | `GetByIdAsync(id, ...)` → `OfficialDocumentDetail?` | 详情 |
| | `GenerateNoAsync(DocumentDirection direction, DateOnly date, ...)` | 单号生成（§0.2） |
| | `AddAsync` / `UpdateAsync` / `UpdateStatusAsync(id, status, archivedAt, ...)` / `DeleteAsync(id, ...)` | 写路径 |
| | `ExistsNoAsync(documentNo, ...)` | 唯一性校验（并发兜底另由唯一索引 + `OrderNoConflictException` 处理） |

- 读模型（`sealed record`，`App.Core/Abstractions/`）：`OfficialDocumentListItem` / `OfficialDocumentDetail`（经办人姓名取快照列，不联查用户表）。
- 导出复用 `GetPagedAsync`（同一筛选参数），列 = 列表列 + 备注；登记 `027` §0.1 范围表（实现时续行）。

### 3.2 用例（`Features/OfficialDocuments/`，7 个）

| 用例 | 说明 |
|---|---|
| `GetOfficialDocuments` | 列表（方向 / 状态 / 日期范围 / 关键词） |
| `GetOfficialDocumentById` | 详情 |
| `CreateOfficialDocument` | 生成单号 + 落库；`direction` 必填、经办人可选（校验员工存在且在职业） |
| `UpdateOfficialDocument` | 仅 `Registered`（`40205`）；**不含 `documentNo` / `direction`** |
| `UpdateOfficialDocumentStatus` | 白名单（`40206`）；`Archived` 写 `ArchivedAt` |
| `DeleteOfficialDocument` | 仅 `Registered`（`40205`） |
| `ExportOfficialDocuments` | 导出（`027` 契约例外：成功返回二进制流） |

- 端点 `OfficialDocumentsController`（`/api/official-documents`）：`GET` / `GET {id:guid}` / `POST` / `PUT {id:guid}` / `POST {id:guid}/status` / `DELETE {id:guid}` / `GET export`（固定段 `export` 置于 `{id:guid}` 之前）。
- 全部写用例接入操作日志（`029` §0.1 续行）：资源 `OfficialDocument`；状态流转用 `StatusChange`。
- 权限标注：`[RequirePermission]` 按 §0.3。

### 3.3 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateOfficialDocumentRequest` | `direction` 枚举合法必填；`title` 必填 ≤ 200；`counterpartName` 必填 ≤ 100；`counterpartDocNo` ≤ 50；`documentDate` 必填；`handlerEmployeeId`（可选）存在且在职业员工；`remark` ≤ 500 |
| `UpdateOfficialDocumentRequest` | 同上（`direction` **不在请求体**） |
| `UpdateOfficialDocumentStatusRequest` | `status` 枚举合法（白名单在 Handler，`40206`） |
| 列表 / 导出查询 | `page ≥ 1`、`pageSize` 默认 20 / 上限 100；导出上限 `ExportFieldConstraints.MaxRows` |

## 4. 前端设计

- **域目录** `views/DocumentManagement/`（与后端 `Features/OfficialDocuments`、路由前缀 `/documents`、e2e `official-document.spec.ts` 对齐）：

| 文件 | 说明 |
|---|---|
| `DocumentsView.vue` | 列表（方向 tab / 状态筛选 + 日期范围 + 关键词 + 导出 + 操作列「详情 / 编辑 / 流转 / 删除」按状态显隐） |
| `DocumentDetailDrawer.vue` | 详情抽屉（单号 / 方向 / 对方单位与文号 / 日期 / 经办人 / 状态 / 备注 + 流转按钮） |
| `DocumentFormDrawer.vue` | 新建 / 编辑共用（方向 `a-radio`（编辑态禁用）/ 标题 / 对方单位 / 对方文号 / 日期 / 经办人下拉 `getEmployees({ status: 1 })` / 备注） |

- 接口层 `api/officialDocument.ts`（类型 + `DOCUMENT_DIRECTION_LABELS` / `DOCUMENT_STATUS_META` / `DOCUMENT_STATUS_TRANSITIONS`）；导出走 `api/export.ts` 既有分流。
- 路由与菜单：`router/index.ts` 注册（懒加载 + `ROUTE_PERMISSIONS` 登记 `documents.view`）；`AppLayout.vue` 「办公」分组追加「公文」。
- 交互约定消费：列表页 `006` §0、操作列 `011` §0、按钮 loading `010` §0、表单 / 详情 `007` §0、组合式分区 `008` §0、图标 `018` §0。

## 5. 技术决策

| 决策 | 结论 | 理由 |
|---|---|---|
| 收文 / 发文共用一张表 | `Direction` 区分，**不做两张表** | 字段高度重合（标题 / 对方单位 / 日期 / 经办人 / 状态），分表会造成双份用例与双份页面 |
| 单号前缀按方向分列 | `OD` / `OW`，各自独立计数 | 收发文序号分开更符合台账习惯；前缀已在 `ROADMAP` §6.7 登记 |
| 状态流转 | 手工登记（登记 / 办理中 / 归档 / 作废） | 公文流转线下办理，系统内只记进度；**不接 `042`**（避免为纯台账引入审批引擎） |
| 不做删除保护 | `Archived` / `Cancelled` 不删，`Registered` 可删 | 误登记需能撤销；已归档属正式台账，保留记录 |
| 导出 | 复用 `027` 能力与契约例外 | 不新增导出通道；范围表续行登记 |

## 6. 测试要点

- **单测**：单号生成（按方向计数、跨日重置、长度约束）；`40205` 非登记态编辑 / 删除；`40206` 非法流转；归档写 `ArchivedAt`；关键词检索命中标题 / 单号 / 对方单位；导出复用筛选。
- **并发**：单号冲突（唯一索引 + `OrderNoConflictException`）回归。
- **约束一致性**：`OfficialDocumentFieldConstraints` 追加 `FieldValidationConsistencyTests`。
- **权限守卫**：`ApiPermissionMatrix` 覆盖 7 个动作。
- **e2e**（`e2e/official-document.spec.ts`）：登记收文 → 单号格式断言；流转至归档 → 编辑 / 删除不可用；导出下载成功。

---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：CRM 服务工单（erp-crm-service）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `043-erp-crm-presale`（CRM 域）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制复用 `028`；客户 / 员工复用 `013` / `030`；菜单续行 `043` 已建的「CRM」分组。

## 0. 约定正文（唯一事实源）

### 0.1 状态、优先级与流转

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `TicketStatus` | `Pending = 0` / `Processing = 1` / `Resolved = 2` / `Closed = 3` | 待处理 / 处理中 / 已解决 / 已关闭 | `orange` / `blue` / `green` / `gray` |
| `TicketPriority` | `Low = 0` / `Medium = 1` / `High = 2` | 低 / 中 / 高 | `gray` / `blue` / `red` |

**允许的状态流转**（其余组合 → `40172`）：

| 当前 | 可转 |
|---|---|
| `Pending` | `Processing` / `Resolved` / `Closed` |
| `Processing` | `Resolved` / `Closed` |
| `Resolved` | `Closed` / `Processing`（**重开**） |
| `Closed` | 无（**终态**） |

- 置 `Resolved` 时记录 `ResolvedAt`（首次）；重开（`Resolved → Processing`）**清空** `ResolvedAt`。
- `Closed` 工单不可编辑、不可改状态。

### 0.2 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 服务工单 | `TicketNo` | 唯一；`SV + yyyyMMdd + 4 位序号` | — |

### 0.3 菜单归属（在 `025` §0.2 表续行）

「CRM（`crm`）」分组（`043` 已建）续行一子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| CRM（`crm`） | 服务工单（`serviceTickets`） | `045` |

### 0.4 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 服务工单 | `serviceTickets` | `view` / `create` / `update` / `status` / `assign` | `/api/service-tickets*`、`/service-tickets` |

## 1. 总体设计

```
CRM 服务（前端 /service-tickets，域目录 CrmManagement/，与 043 同域）
  → ServiceTicketsController
    → App.Core/Features/ServiceTickets/<Action>/*RequestHandler
      → IServiceTicketRepository
        + IPartnerRepository（客户存在性）+ IEmployeeRepository（负责人，037）
        → PostgreSQL（ServiceTickets）
```

核心原则：**工单是留痕记录**——不做删除（关闭即归档）；状态流转受控（`40172`）。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举 `smallint`。

### 2.1 实体 `App.Core/Entities/ServiceTicket.cs` 与表 `ServiceTickets`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `TicketNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `SV + yyyyMMdd + 4` |
| `PartnerId` | `Guid` | `uuid` | NOT NULL，FK → `Partners(Id)`，索引 | 客户 |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 客户名**快照** |
| `Contact` | `string?` | `varchar(30)` | NULL | 联系人 |
| `Phone` | `string?` | `varchar(20)` | NULL | 联系电话 |
| `Title` | `string` | `varchar(50)` | NOT NULL | 工单标题 |
| `Description` | `string?` | `varchar(500)` | NULL | 问题描述 |
| `Priority` | `TicketPriority` | `smallint` | NOT NULL，默认 `Medium` | 优先级 |
| `Status` | `TicketStatus` | `smallint` | NOT NULL，默认 `Pending` | 状态 |
| `OwnerId` | `Guid?` | `uuid` | NULL，FK → `Employees(Id)`，索引 | 负责人 |
| `ResolvedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 解决时间（置解决时记录） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| 审计 | | | | |

### 2.2 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `ServiceTicketFieldConstraints` | `NoMaxLength = 20` / `TitleMaxLength = 50` / `DescriptionMaxLength = 500` / `ContactMaxLength = 30` / `PhoneMaxLength = 20` / `RemarkMaxLength = 200` |

### 2.3 迁移

- 迁移：`dotnet ef migrations add AddErpCrmService -p src/App.Infrastructure -s src/App.Api`（建 1 表 + 索引；FK 不级联删除）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IServiceTicketRepository.GetPagedAsync(...)` | 列表（`keyword` 匹配单号 / 客户名 / 标题；状态 / 优先级 / 负责人 / 日期闭区间） |
| `IServiceTicketRepository.GetByIdAsync` / `AddAsync` / `UpdateAsync` | |
| `IServiceTicketRepository.GenerateNoAsync(date, ...)` | 单号 `SV` |

读模型：`ServiceTicketListItem`（含客户名 / 负责人名）、`ServiceTicketDetail`。

### 3.2 错误码（`ErrorCode.cs`，从 `40172` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40172 | `TicketStateInvalid` | 工单状态不允许该操作（非法流转 / 已关闭编辑） |

> 下一个可用业务码 → `40173`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/service-tickets` | GET | `ServiceTickets/GetServiceTickets` | `PagedResult<ServiceTicketListItemDto>` | `serviceTickets.view` / 40000 |
| `/api/service-tickets` | POST | `ServiceTickets/CreateServiceTicket` | `ServiceTicketDetailDto` | `serviceTickets.create` / 40000 / 40400 |
| `/api/service-tickets/{id:guid}` | GET | `ServiceTickets/GetServiceTicketById` | `ServiceTicketDetailDto` | `serviceTickets.view` / 40400 |
| `/api/service-tickets/{id:guid}` | PUT | `ServiceTickets/UpdateServiceTicket` | `ServiceTicketDetailDto` | `serviceTickets.update` / 40000 / 40172 / 40400 |
| `/api/service-tickets/{id:guid}/status` | PUT | `ServiceTickets/UpdateServiceTicketStatus` | `ServiceTicketDetailDto` | `serviceTickets.status` / 40172 / 40400 |
| `/api/service-tickets/{id:guid}/assign` | PUT | `ServiceTickets/AssignServiceTicket` | `ServiceTicketDetailDto` | `serviceTickets.assign` / 40172 / 40400 |

### 3.4 关键用例流程（Handler）

- **CreateServiceTicket**：客户存在（`40400`）；负责人可选（存在性）；生成单号；落库。
- **UpdateServiceTicket**：`Status == Closed` → `40172`；客户 / 负责人存在性；更新。
- **UpdateServiceTicketStatus**：按 §0.1 校验流转 → `40172`；置 `Resolved` 记 `ResolvedAt`，重开清空。
- **AssignServiceTicket**：`Status == Closed` → `40172`；负责人存在性（`40400`）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateServiceTicketRequest` / `UpdateServiceTicketRequest` | `partnerId` 必填 GUID；`title` 必填 1–50；`description` ≤ 500；`contact` ≤ 30；`phone` ≤ 20；`priority` 枚举合法；`ownerId` 可空 GUID；`remark` ≤ 200 |
| `UpdateServiceTicketStatusRequest` | `status` 枚举合法 |
| `GetServiceTicketsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录（与 `043` 同域 `CrmManagement/`）

```
src/
├── api/
│   └── serviceTicket.ts             # 服务工单接口层
└── views/
    └── CrmManagement/
        ├── ServiceTicketsView.vue         # 工单列表
        ├── ServiceTicketFormPage.vue      # 工单登记 / 编辑
        └── ServiceTicketDetailView.vue    # 工单详情（状态推进 + 指派）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `service-tickets` | `serviceTickets` | `ServiceTicketsView` |
| `service-tickets/new` | `serviceTicketCreate` | `ServiceTicketFormPage` |
| `service-tickets/detail/:id` | `serviceTicketDetail` | `ServiceTicketDetailView` |

- 「CRM」分组续行「服务工单」；`meta.permission = 'serviceTickets.view'`。

### 4.3 页面交互

- **`ServiceTicketsView.vue`**：筛选（关键词 / 状态 / 优先级 / 负责人）；列：工单号、客户、标题、优先级（`a-tag`）、状态（`a-tag`）、负责人、创建时间、操作列（查看 / 编辑 / 受理 / 解决 / 关闭，按状态显示可用动作）。
- **`ServiceTicketFormPage.vue`**：客户 / 联系人 / 电话 / 标题 / 描述 / 优先级 / 负责人 / 备注。
- **`ServiceTicketDetailView.vue`**：基本信息 + 状态推进按钮（按允许流转）+ 指派负责人；已关闭只读。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 工单不做删除 | 关闭即归档 | 服务记录需留痕（同审计诉求） |
| 状态流转白名单 | §0.1 表 | 避免非法跳转（如从待处理直接关闭仍需允许，故列白名单而非严格顺序） |
| 重开清空解决时间 | `Resolved → Processing` | 重开后"未解决"，旧解决时间失去意义 |
| 不做 SLA / 分类树 | 一期克制 | 引入分类与时限会带来字典与定时任务，另立评估 |

## 6. 单元测试设计

- **工单**：`CreateServiceTicket` 客户 / 负责人存在性；`UpdateServiceTicket` 已关闭 `40172`。
- **状态流转**：白名单内转换通过；白名单外 `40172`；置解决记 `ResolvedAt`；重开清空。
- **指派**：已关闭 `40172`；负责人存在性。
- **字段约束一致性**：常量与 EF 列长一致。
- **清单守卫**：6 个端点纳入 `028` 既有守卫。

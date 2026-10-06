---
created: 2026-09-20
updated: 2026-10-06
---

# 设计规格：CRM 售前（erp-crm-presale）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `024-erp-order-flow`（单据 + 状态机）与 `013`（往来）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制复用 `028`；负责人关联 `030` 的 `Employee`。

## 0. 约定正文（唯一事实源）

### 0.1 状态与枚举

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `LeadStatus` | `New = 0` / `Following = 1` / `Converted = 2` / `Abandoned = 3` | 新线索 / 跟进中 / 已转化 / 已废弃 | `blue` / `orange` / `green` / `gray` |
| `LeadSource` | `Website = 0` / `Phone = 1` / `Referral = 2` / `Exhibition = 3` / `Other = 4` | 网站 / 电话 / 推荐 / 展会 / 其他 | — |
| `OpportunityStage` | `Initial = 0` / `Requirement = 1` / `Proposal = 2` / `Negotiation = 3` / `Won = 4` / `Lost = 5` | 初步接洽 / 需求确认 / 方案报价 / 谈判 / 赢单 / 输单 | 终态 `Won` `green` / `Lost` `red`，其余 `blue` |
| `ActivityBizType` | `Lead = 0` / `Opportunity = 1` | 线索 / 商机 | — |
| `ActivityType` | `Call = 0` / `Visit = 1` / `Email = 2` / `Other = 3` | 电话 / 拜访 / 邮件 / 其他 | — |

- **线索流转**：`New → Following → Converted`，任意非终态可 `→ Abandoned`；`Converted` / `Abandoned` 为终态（不可再转商机，`40168`）。
- **商机阶段**：顺序推进；`Won` / `Lost` 为终态（终态后不可改阶段，`40169`）。
- **活动只增**：跟进记录不可编辑 / 删除（留痕）。

### 0.2 转商机规则

- 线索转商机：生成商机（名称取线索名、客户空、负责人同线索），线索置 `Converted` 并回填 `OpportunityId` / `OpportunityNo`。
- 一条线索**只能转一次**。

### 0.3 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 线索 | `LeadNo` | 唯一；`LD + yyyyMMdd + 4` | — |
| 商机 | `OpportunityNo` | 唯一；`OP + yyyyMMdd + 4` | — |

### 0.4 菜单归属（在 `025` §0.2 表续行）

新增顶级分组「CRM（`crm`）」（置于「销售」之后）：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| CRM（`crm`） | 线索（`leads`）/ 商机（`opportunities`）/ 服务工单（`serviceTickets`，`045`） | `043` / `045` |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 线索 | `leads` | `view` / `create` / `update` / `convert` / `status` | `/api/leads*`、`/leads` |
| 商机 | `opportunities` | `view` / `create` / `update` / `stage` | `/api/opportunities*`、`/opportunities` |

## 1. 总体设计

```
CRM 售前（前端 /leads、/opportunities，域目录 CrmManagement/）
  → LeadsController / OpportunitiesController / ActivitiesController
    → App.Core/Features/<Leads|Opportunities|Activities>/<Action>/*RequestHandler
      → ILeadRepository / IOpportunityRepository / IActivityRepository
        + IEmployeeRepository（负责人，037）
        + IUnitOfWork（转商机同一事务）
        → PostgreSQL（Leads / Opportunities / Activities）
```

核心原则：**售前是意向，不触碰交易**——线索 / 商机全程不锁库存、不写流水、不产生应收；活动只追加。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；纯日期（预计成交）用 `DateOnly` → `date`；金额 `numeric(18,2)`。

### 2.1 实体 `App.Core/Entities/Lead.cs` 与表 `Leads`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `LeadNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `LD + yyyyMMdd + 4` |
| `Name` | `string` | `varchar(50)` | NOT NULL | 线索名称 / 公司 |
| `Contact` | `string?` | `varchar(30)` | NULL | 联系人 |
| `Phone` | `string?` | `varchar(20)` | NULL | 电话 |
| `Source` | `LeadSource` | `smallint` | NOT NULL，默认 `Other`（**实体默认值**，不落库列默认值：`Other = 4` 与枚举 CLR 默认值 0 不同，落库默认值会让 `Website`(0) 的插入被数据库默认覆盖，见 EF 警告 20601） | 来源 |
| `Status` | `LeadStatus` | `smallint` | NOT NULL，默认 `New` | |
| `OwnerId` | `Guid?` | `uuid` | NULL，FK → `Employees(Id)`，索引 | 负责人 |
| `OpportunityId` / `OpportunityNo` | `Guid?` / `string?` | `uuid` / `varchar(20)` | NULL | 转出的商机 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.2 实体 `App.Core/Entities/Opportunity.cs` 与表 `Opportunities`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OpportunityNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `OP + yyyyMMdd + 4` |
| `Name` | `string` | `varchar(50)` | NOT NULL | 商机名称 |
| `LeadId` | `Guid?` | `uuid` | NULL，FK → `Leads(Id)` | 来源线索 |
| `PartnerId` | `Guid?` | `uuid` | NULL，FK → `Partners(Id)`，索引 | 关联客户（可选） |
| `PartnerName` | `string?` | `varchar(50)` | NULL | 客户名**快照** |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 预计金额 |
| `Stage` | `OpportunityStage` | `smallint` | NOT NULL，默认 `Initial` | 阶段 |
| `ExpectedCloseDate` | `DateOnly?` | `date` | NULL | 预计成交日期 |
| `OwnerId` | `Guid?` | `uuid` | NULL，FK → `Employees(Id)`，索引 | 负责人 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.3 实体 `App.Core/Entities/Activity.cs` 与表 `Activities`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `BizType` | `ActivityBizType` | `smallint` | NOT NULL | 线索 / 商机 |
| `BizId` | `Guid` | `uuid` | NOT NULL，索引 | 线索 / 商机 id |
| `Type` | `ActivityType` | `smallint` | NOT NULL | 电话 / 拜访 / 邮件 / 其他 |
| `Content` | `string` | `varchar(200)` | NOT NULL | 跟进内容 |
| `ActivityTime` | `DateTimeOffset` | `timestamptz` | NOT NULL | 跟进时间 |
| 审计 | | | |（`CreatedBy` 即记录人） |

### 2.4 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `LeadFieldConstraints` | `NoMaxLength = 20` / `NameMaxLength = 50` / `ContactMaxLength = 30` / `PhoneMaxLength = 20` / `RemarkMaxLength = 200` / `KeywordMaxLength = NameMaxLength`（= 列表模糊匹配列中的最大列长） |
| `OpportunityFieldConstraints` | `NoMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` / `KeywordMaxLength = NameMaxLength` / `AmountMaxValue = 9999999999999999.99`（`numeric(18,2)` 上限，下界 0；前端输入上限另行取安全整数域值，仅作 UI 约束） |
| `ActivityFieldConstraints` | `ContentMaxLength = 200` |

- 商机客户名称快照列长引用 `PartnerFieldConstraints.NameMaxLength`，不重复登记；活动为纯追加表，无 `UpdatedAt` / `UpdatedBy`（只有 `CreatedAt` / `CreatedBy`，`CreatedBy` 即记录人）。

### 2.5 迁移

- 迁移：`dotnet ef migrations add AddErpCrmPresale -p src/App.Infrastructure -s src/App.Api`（建 3 表 + 索引；FK 不级联删除）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `ILeadRepository.GetPagedAsync(keyword, source, status, ownerId, page, pageSize, ...)` / `GetByIdAsync(id)` / `AddAsync(lead)` / `UpdateAsync(lead)` / `GenerateNoAsync(prefix, date, ...)` | `GetByIdAsync` 返回**详情读模型** `LeadDetail`（含负责人姓名），详情 / 编辑 / 状态流转 / 转商机共用；`UpdateAsync` 承载编辑、状态流转与转商机回写（单号与创建审计字段不变） |
| `IOpportunityRepository.GetPagedAsync(keyword, stage, partnerId, ownerId, page, pageSize, ...)` / `GetByIdAsync(id)` / `AddAsync(opportunity)` / `UpdateAsync(opportunity)` / `GenerateNoAsync(prefix, date, ...)` | 同形；`GetByIdAsync` 返回 `OpportunityDetail`（含负责人姓名） |
| `IActivityRepository.GetByBizAsync(bizType, bizId, ...)` / `AddAsync(activity)` | 只增（无更新 / 删除方法） |

读模型：`LeadListItem`（含负责人名）、`LeadDetail`（含负责人名）、`OpportunityListItem`（含负责人名；客户名取自身快照列）、`OpportunityDetail`（含负责人名）、`ActivityItem`（含记录人显示名）。
负责人姓名与记录人显示名按 id 批量取回（本页一次查询），不做逐行联查。

### 3.2 错误码（`ErrorCode.cs`，从 `40168` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40168 | `LeadNotConvertible` | 线索已转化 / 已废弃，不可再转商机 |
| 40169 | `OpportunityClosed` | 商机已赢单 / 输单（终态），不可改阶段 |

> 下一个可用业务码 → `40170`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/leads` | GET | `Leads/GetLeads` | `PagedResult<LeadListItemDto>` | `leads.view` / 40000 |
| `/api/leads` | POST | `Leads/CreateLead` | `LeadDetailDto` | `leads.create` / 40000 / 40400 |
| `/api/leads/{id:guid}` | GET | `Leads/GetLeadById` | `LeadDetailDto` | `leads.view` / 40400 |
| `/api/leads/{id:guid}` | PUT | `Leads/UpdateLead` | `LeadDetailDto` | `leads.update` / 40000 / 40400 |
| `/api/leads/{id:guid}/status` | PUT | `Leads/UpdateLeadStatus` | `LeadDetailDto` | `leads.status` / 40400 |
| `/api/leads/{id:guid}/convert` | POST | `Leads/ConvertLead` | `{ opportunityId, opportunityNo }` | `leads.convert` / 40168 / 40400 |
| `/api/opportunities` | GET | `Opportunities/GetOpportunities` | `PagedResult<OpportunityListItemDto>` | `opportunities.view` / 40000 |
| `/api/opportunities` | POST | `Opportunities/CreateOpportunity` | `OpportunityDetailDto` | `opportunities.create` / 40000 / 40400 |
| `/api/opportunities/{id:guid}` | GET | `Opportunities/GetOpportunityById` | `OpportunityDetailDto` | `opportunities.view` / 40400 |
| `/api/opportunities/{id:guid}` | PUT | `Opportunities/UpdateOpportunity` | `OpportunityDetailDto` | `opportunities.update` / 40000 / 40400 |
| `/api/opportunities/{id:guid}/stage` | PUT | `Opportunities/UpdateOpportunityStage` | `OpportunityDetailDto` | `opportunities.stage` / 40169 / 40400 |
| `/api/activities` | GET | `Activities/GetActivities` | `IReadOnlyList<ActivityItemDto>` | `leads.view` / `opportunities.view`（按 `bizType`） |
| `/api/activities` | POST | `Activities/CreateActivity` | `ActivityItemDto` | `leads.update` / `opportunities.update`（按 `bizType`） |

- 活动的权限按 `bizType` 动态判定：`Lead` → `leads.*`，`Opportunity` → `opportunities.*`（在 Controller 动作上标注两个权限点之一，简化为**两个动作各标注对应域**，实现时按 `bizType` 在 Handler 内不校验权限、由两个独立端点区分——落地时**拆为** `/api/leads/{id}/activities` 与 `/api/opportunities/{id}/activities` 两个端点，避免动态权限歧义）。

### 3.4 关键用例流程（Handler）

- **ConvertLead**：取线索（`40400`）→ `Status ∈ {New, Following}` 否则 `40168` → `IUnitOfWork`：建商机（名称同线索、`LeadId`、负责人同线索、客户空、金额 0、阶段初步接洽）+ 回写线索（`Status = Converted`、`OpportunityId` / `OpportunityNo`）→ `CommitAsync`。
- **UpdateOpportunityStage**：取商机（`40400`）→ 当前 `Stage ∈ {Won, Lost}` → `40169` → 更新阶段。
- **UpdateLeadStatus**：终态（`Converted` / `Abandoned`）→ 拒绝改状态（`40168` 语义）；
- **状态流转规则单点**：`UpdateLeadStatus` 与 `UpdateLead`（编辑请求含 `status`，前端编辑表单全量提交）共用 `LeadStatusRules`；商机的 `UpdateOpportunity` 与 `UpdateOpportunityStage` 共用 `OpportunityStageRules`——同一条判据不在两处各写一份。
- **不可直接置「已转化」**：线索的 `Converted` 只能由 `ConvertLead` 产生（须同时回填商机 id / 单号），编辑或状态流转端点的目标状态为 `Converted` 一律 `40168`；「已废弃」可由编辑表单或状态流转产生。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateLeadRequest` / `UpdateLeadRequest` | `name` 必填 1–50；`contact` ≤ 30；`phone` ≤ 20；`source` / `status` 枚举合法（新建 `status` 只允许新线索 / 跟进中）；`ownerId` 可空 GUID；`remark` ≤ 200 |
| `CreateOpportunityRequest` / `UpdateOpportunityRequest` | `name` 必填 1–50；`partnerId` / `leadId` / `ownerId` 可空 GUID；`amount` ≥ 0；`stage` 枚举合法；`expectedCloseDate` 可空；`remark` ≤ 200 |
| `CreateActivityRequest` | `type` 枚举合法；`content` 必填 1–200；`activityTime` 必填（归属业务 id 由路由给出，`bizType` 由端点决定，不进请求体） |
| `GetLeadsRequest` / `GetOpportunitiesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

### 3.6 登记项（横向能力续行）

- **权限点（`028` §0.2 续行）**：`leads.view/create/update/convert/status`、`opportunities.view/create/update/stage`；活动端点按归属域复用 `leads.*` / `opportunities.*`（见 §0.5、§3.3）。
- **操作日志（`029` §0.1 续行）**：新增资源 `Lead` / `Opportunity` / `Activity`，写用例全部在「业务写之后、`CommitAsync` 之前」记日志（与业务同事务）；转商机在 `Lead` 资源上以动作 `Update` 记录，跟进活动只记 `Create`。
- **菜单（`025` §0.2 续行）**：新增顶级分组「CRM（`crm`）」，置于「销售」之后（见 §0.4）。
- **业务码**：`40168` / `40169` 占用，区间内下一个可用 `40170`（`ROADMAP` §6 同步）。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── lead.ts                    # 线索 CRUD / 状态流转 / 转商机 + 线索活动 + 活动共用类型与文案
│   ├── opportunity.ts             # 商机 CRUD / 阶段推进 + 商机活动
└── views/
    └── CrmManagement/
        ├── LeadsView.vue              # 线索列表
        ├── LeadFormDrawer.vue         # 线索新增 / 编辑（列表页内抽屉）
        ├── LeadDetailView.vue         # 线索详情（含跟进活动 + 转商机）
        ├── OpportunitiesView.vue      # 商机列表
        ├── OpportunityFormPage.vue    # 商机新增 / 编辑（独立页）
        └── OpportunityDetailView.vue  # 商机详情（含跟进活动 + 阶段推进）
```

- **接口文件归属与理由（前端规则 §3）**：跟进活动是**从属资源**（归属线索或商机，无独立页面域），故并入两个主域文件——`api/lead.ts` 定义 `ActivityItem` / `SaveActivityPayload` / `ACTIVITY_TYPE_LABELS` 等**共用类型与文案**及线索活动接口，`api/opportunity.ts` 复用其类型、只放商机活动接口；`LEAD_STATUS_META` / `LEAD_SOURCE_LABELS` / `OPPORTUNITY_STAGE_META` 等展示映射随各自域文件（与 `api/approval.ts` / `api/notification.ts` 同惯例）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `leads` | `leads` | `LeadsView` |
| `leads/detail/:id` | `leadDetail` | `LeadDetailView` |
| `opportunities` | `opportunities` | `OpportunitiesView` |
| `opportunities/new` | `opportunityCreate` | `OpportunityFormPage` |
| `opportunities/edit/:id` | `opportunityEdit` | `OpportunityFormPage` |
| `opportunities/detail/:id` | `opportunityDetail` | `OpportunityDetailView` |

- 「CRM」分组新增「线索」「商机」；`meta.permission = 'leads.view'` / `'opportunities.view'`（`ROUTE_PERMISSIONS` 登记：`leads` / `leadDetail` → `leads.view`，`opportunities` / `opportunityDetail` → `opportunities.view`，`opportunityCreate` → `opportunities.create`，`opportunityEdit` → `opportunities.update`）。
- 线索表单为**抽屉**（`LeadFormDrawer`，列表页内新增 / 编辑，故无独立路由）；商机表单为**独立页面**（字段较多且含客户 / 阶段 / 日期，新增与编辑共用 `OpportunityFormPage`，以路由名区分 `mode`）。

### 4.3 页面交互

- **`LeadsView.vue`**：筛选（关键词 / 来源 / 状态 / 负责人，输入态与已应用态分离）；列：序号、线索号、名称、联系人、电话、来源、状态（`a-tag`）、负责人、创建时间；操作列 4 项（查看 / 编辑 / 转商机 / 废弃）——超 3 项，末项「废弃」收纳进行内「更多」（`aria-label="更多操作"`，函数式二次确认）；终态线索（已转化 / 已废弃）只留「查看」；每项按权限点过滤（`leads.update` / `leads.convert` / `leads.status`）。
- **`LeadFormDrawer.vue`**：列表页内的新增 / 编辑抽屉（新增 / 编辑共用），状态下拉新建只列「新线索 / 跟进中」、编辑列「新线索 / 跟进中 / 已废弃」（「已转化」只能由转商机产生）；负责人下拉取在职员工。
- **`LeadDetailView.vue`**：基本信息 + 跟进活动时间线（`a-timeline`，跟进时间倒序）+ 新增活动抽屉（跟进方式 / 时间 / 内容）+ 转商机 / 废弃按钮（仅非终态显示，二次确认）。
- **`OpportunitiesView.vue`**：筛选（关键词 / 阶段 / 负责人）；列：序号、商机号、名称、客户、预计金额、阶段（`a-tag`）、预计成交日期、负责人、创建时间；操作列（查看 / 编辑，`opportunities.update`）。
- **`OpportunityDetailView.vue`**：基本信息 + 阶段推进（下拉选目标阶段 + 「推进阶段」按钮，终态时下拉禁用并提示）+ 跟进活动时间线（同线索，活动抽屉内联）+ 编辑入口（跳 `opportunities/edit/:id`）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 活动只增不删 | 追加表 | 跟进留痕；删改会破坏销售过程记录 |
| 线索转商机一次性 | 终态锁 | 避免重复商机（同报价转单逻辑） |
| 商机客户可空 | `PartnerId?` | 线索阶段可能还没有正式客户档案；赢单前可补 |
| 售前不碰交易 | 无库存 / 资金写入 | 意向未成交，不应影响经营数据 |
| 不建"客户联系人"子表 | 线索携带联系人字段 | 一期克制；客户联系人管理后续评估 |

## 6. 单元测试设计

- **线索**（`LeadRequestHandlerTests`）：`CreateLead` / `UpdateLead` 校验与全量覆盖语义；状态流转（终态限制 / 目标「已转化」拒绝）；`ConvertLead` 成功（商机字段 + 线索回写同一事务、调用顺序 `Begin → Generate → Add → Update → Commit`）/ 终态 `40168`；列表筛选透传与详情 `40400`。
- **商机**（`OpportunityRequestHandlerTests`）：`CreateOpportunity` / `UpdateOpportunity` 校验与客户名称快照；`UpdateOpportunityStage` 正常 / 终态 `40169`（编辑同判据）；来源线索 / 客户 / 负责人存在性。
- **活动**（`ActivityRequestHandlerTests`）：按归属（线索 / 商机）查询与新增互不串数据、记录人带出；归属业务缺失 `40400`；无改删端点。
- **字段约束一致性**（`CrmPresaleFieldConsistencyTests`）：三个常量类与 EF 列长一致；创建 / 编辑两处边界同源（名称 / 联系人 / 电话 / 备注 / 金额 / 活动内容）；查询关键词上限 = 匹配列最大列长；枚举筛选取值合法 / 非法。
- **横向守卫**：全部端点纳入 `028` 的 `ApiPermissionMatrixTests`（含拆分的活动端点）；写用例纳入 `029` 的 `AuditLogScopeGuardTests`（范围表续行 + 反向扫描）。

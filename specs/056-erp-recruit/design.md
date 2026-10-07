---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：招聘管理（erp-recruit）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `030-erp-org-employee`（组织 / 员工）为依赖与写入复用，以 `045-erp-crm-service`（状态流转 + 指派）为结构参照。
> 部门 / 岗位 / 员工实体与唯一性见 `specs/030-erp-org-employee/design.md` §0.3 / §2；单号前缀全域表见 `specs/ROADMAP.md` §6.7。

## 0. 约定正文（唯一事实源）

### 0.1 状态与阶段流转

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `JobRequisitionStatus` | `Open = 0` / `Closed = 1` | 开放 / 已关闭 | `green` / `gray` |
| `CandidateStage` | `Pending = 0` / `Interviewing = 1` / `Hired = 2` / `Rejected = 3` | 待筛选 / 面试中 / 已录用 / 已淘汰 | `blue` / `orange` / `green` / `gray` |
| `CandidateSource` | `Other = 0` / `Website = 1` / `Referral = 2` / `Campus = 3` | 其它 / 网站 / 内推 / 校招 | — |

**候选人阶段允许流转**（其余 → `40193`）：

| 当前 | 可转 |
|---|---|
| `Pending` | `Interviewing` / `Rejected` |
| `Interviewing` | `Hired` / `Rejected` |
| `Hired` | 无（**终态**；走转入职） |
| `Rejected` | 无（**终态**） |

### 0.2 需求关闭限制

- 需求 `Closed` 后：**不可编辑**、**不可新增其下候选人**、**不可对其候选人做阶段流转**（均 → `40192`）；已存在候选人保留。

### 0.3 转入职语义

- **仅 `Hired` 候选人**可转入职（否则 `40193`）；已关联员工（`EmployeeId` 非空）→ `40194`。
- **同事务**：建 `Employee`（`030` 的 `Employees`，`Status = Active`）+ 回写 `Candidate.EmployeeId`。
- 员工字段：`EmployeeNo` / `HireDate` 由请求传入；`DepartmentId` / `PositionId` 缺省取需求快照对应的部门 / 岗位；其余（性别 / 电话 / 邮箱 / 姓名）由候选人带入。
- 唯一性沿用 `030`：`EmployeeNo` 重复 → `40145`；`Phone` 非空重复 → `40147`。

### 0.4 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 招聘需求 | `RequisitionNo` | 唯一；`JR + yyyyMMdd + 4 位序号`（`ROADMAP` §6.7 续行） | — |
| 候选人 | `EmployeeId` | 非空时唯一（一个候选人最多转入职一次） | `40194`（应用层） |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 招聘需求 | `jobRequisitions` | `view` / `create` / `update` / `status` | `/api/job-requisitions*`、`/job-requisitions` |
| 候选人 | `candidates` | `view` / `create` / `update` / `stage` / `convert` | `/api/candidates*`、`/candidates` |

### 0.6 菜单归属（在 `025` §0.2 表续行）

「人事（`hrm`）」分组续行两子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 人事（`hrm`） | 招聘需求（`jobRequisitions`）/ 候选人（`candidates`） | `056` |

## 1. 总体设计

```
招聘（前端 /job-requisitions、/candidates，域目录 RecruitManagement/）
  → JobRequisitionsController / CandidatesController
    → App.Core/Features/<JobRequisitions|Candidates>/<Action>/*RequestHandler
      → IJobRequisitionRepository / ICandidateRepository
        + IDepartmentRepository / IPositionRepository / IEmployeeRepository（030）
        + IUnitOfWork（转入职同事务）
        → PostgreSQL（JobRequisitions / Candidates / Employees）
```

核心原则：

- **转入职是跨仓储写**：员工档案由 `030` 既有仓储创建，招聘侧只回写 `EmployeeId`（同事务）。
- **阶段受控**：白名单单点（§0.1），编辑与阶段端点共用。
- **需求是容器**：候选人挂需求，需求关闭即冻结该批招聘。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/JobRequisition.cs` 与表 `JobRequisitions`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `RequisitionNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `JR + yyyyMMdd + 4` |
| `PositionId` | `Guid?` | `uuid` | NULL，FK → `Positions(Id)` | 招聘岗位 |
| `PositionName` | `string?` | `varchar(50)` | NULL | 岗位名**快照** |
| `DepartmentId` | `Guid` | `uuid` | NOT NULL，FK → `Departments(Id)`，索引 | 用人部门 |
| `DepartmentName` | `string` | `varchar(50)` | NOT NULL | 部门名**快照** |
| `Headcount` | `int` | `integer` | NOT NULL，≥ 1 | 招聘人数 |
| `PublishDate` | `DateOnly` | `date` | NOT NULL | 发布日期 |
| `Status` | `JobRequisitionStatus` | `smallint` | NOT NULL，默认 `Open` | 见 §0.1 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

### 2.2 实体 `App.Core/Entities/Candidate.cs` 与表 `Candidates`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Name` | `string` | `varchar(50)` | NOT NULL | 姓名 |
| `Gender` | `Gender?` | `smallint` | NULL | 复用 `030` |
| `Phone` | `string` | `varchar(20)` | NOT NULL，索引 | 电话（不设唯一，候选人可重复投递） |
| `Email` | `string?` | `varchar(100)` | NULL | |
| `RequisitionId` | `Guid` | `uuid` | NOT NULL，FK → `JobRequisitions(Id)`，索引 | 应聘需求 |
| `RequisitionNo` | `string` | `varchar(20)` | NOT NULL | 需求单号**快照** |
| `Stage` | `CandidateStage` | `smallint` | NOT NULL，默认 `Pending` | 见 §0.1 |
| `Source` | `CandidateSource` | `smallint` | NOT NULL，默认 `Other` | |
| `EmployeeId` | `Guid?` | `uuid` | NULL，FK → `Employees(Id)`，非空时唯一索引 | 转入职后回写 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `JobRequisitionFieldConstraints` | `NoMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` / `HeadcountMinValue = 1` / `HeadcountMaxValue = 999` |
| `CandidateFieldConstraints` | `NameMaxLength = 50` / `PhoneMaxLength = 20` / `EmailMaxLength = 100` / `RemarkMaxLength = 200` |

### 2.4 迁移与种子

- 迁移：`dotnet ef migrations add AddErpRecruit -p src/App.Infrastructure -s src/App.Api`（建 2 表 + 索引；FK 不级联删除）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IJobRequisitionRepository.GetPagedAsync(...)` | 列表（关键词 / 状态 / 部门）；`GetByIdAsync` / `AddAsync` / `UpdateAsync` / `GenerateNoAsync` / `UpdateStatusAsync` |
| `ICandidateRepository.GetPagedAsync(...)` | 列表（关键词 / 需求 / 阶段）；`GetByIdAsync` / `AddAsync` / `UpdateAsync` / `UpdateStageAsync` / `MarkConvertedAsync` |

- 读模型：`JobRequisitionListItem`（含部门 / 岗位名 + 候选人数）、`CandidateListItem`（含需求单号 / 岗位 / 部门）、`CandidateDetail`。

### 3.2 错误码（`ErrorCode.cs`，从 `40192` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40192 | `RequisitionClosed` | 招聘需求已关闭，不可编辑 / 新增候选人 / 流转 |
| 40193 | `CandidateStageInvalid` | 候选人阶段不允许该操作（非法流转 / 非录用转入职） |
| 40194 | `CandidateConverted` | 候选人已转入职 |

> 下一个可用业务码 → `40195`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/job-requisitions` | GET | `JobRequisitions/GetJobRequisitions` | `PagedResult<..ListItemDto>` | `jobRequisitions.view` / 40000 |
| `/api/job-requisitions` | POST | `JobRequisitions/CreateJobRequisition` | `..DetailDto` | `jobRequisitions.create` / 40000 / 40400 |
| `/api/job-requisitions/{id:guid}` | GET/PUT | `JobRequisitions/GetJobRequisitionById` / `UpdateJobRequisition` | `..DetailDto` | `jobRequisitions.view` / `update` / 40000 / 40192 / 40400 |
| `/api/job-requisitions/{id:guid}/close` | PUT | `JobRequisitions/CloseJobRequisition` | `..DetailDto` | `jobRequisitions.status` / 40192 / 40400 |
| `/api/candidates` | GET | `Candidates/GetCandidates` | `PagedResult<..ListItemDto>` | `candidates.view` / 40000 |
| `/api/candidates` | POST | `Candidates/CreateCandidate` | `..DetailDto` | `candidates.create` / 40000 / 40192 / 40400 |
| `/api/candidates/{id:guid}` | GET/PUT | `Candidates/GetCandidateById` / `UpdateCandidate` | `..DetailDto` | `candidates.view` / `update` / 40000 / 40192 / 40400 |
| `/api/candidates/{id:guid}/stage` | PUT | `Candidates/UpdateCandidateStage` | `..DetailDto` | `candidates.stage` / 40192 / 40193 / 40400 |
| `/api/candidates/{id:guid}/convert` | POST | `Candidates/ConvertCandidate` | `..DetailDto` | `candidates.convert` / 40193 / 40194 / 40145 / 40147 / 40400 |

### 3.4 关键用例流程（Handler）

- **CreateJobRequisition**：部门存在（`40400`）；岗位可选（存在性）；生成单号；落库（`Open`）。
- **UpdateJobRequisition**：`Closed` → `40192`；部门 / 岗位存在性；更新。
- **CloseJobRequisition**：`Closed` → `40192`；置 `Closed`。
- **CreateCandidate**：需求存在（`40400`）；需求 `Closed` → `40192`；落库（`Pending`）。
- **UpdateCandidate**：需求 `Closed` → `40192`；更新（**不改 `Stage`**，阶段走专用端点）。
- **UpdateCandidateStage**：需求 `Closed` → `40192`；按 §0.1 白名单校验（含终态）→ `40193`；置阶段。
- **ConvertCandidate**：`Stage != Hired` → `40193`；`EmployeeId` 非空 → `40194`；`EmployeeNo` / `Phone` 唯一（`030` → `40145` / `40147`）；同事务建 `Employee`（部门 / 岗位缺省取需求）+ `MarkConvertedAsync`。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdateJobRequisitionRequest` | `departmentId` 必填 GUID；`positionId` 可空 GUID；`headcount` 1–999；`publishDate` 必填；`remark` ≤ 200 |
| `Create/UpdateCandidateRequest` | `name` 必填 1–50；`phone` 必填 1–20；`email` ≤ 100；`requisitionId` 必填 GUID；`gender` / `source` 枚举合法；`remark` ≤ 200 |
| `UpdateCandidateStageRequest` | `stage` 枚举合法 |
| `ConvertCandidateRequest` | `employeeNo` 必填 1–20；`hireDate` 必填；`departmentId` / `positionId` 可空 GUID |
| `Get*Request` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── jobRequisition.ts
│   └── candidate.ts
└── views/
    └── RecruitManagement/
        ├── JobRequisitionsView.vue   # 需求列表 + 抽屉表单 + 关闭
        └── CandidatesView.vue        # 候选人列表 + 抽屉表单 + 阶段推进 + 转入职
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `job-requisitions` | `jobRequisitions` | `JobRequisitionsView` |
| `candidates` | `candidates` | `CandidatesView` |

- 「人事」分组续行「招聘需求」「候选人」；`meta.permission` 分别 `jobRequisitions.view` / `candidates.view`。

### 4.3 页面交互

- **`JobRequisitionsView.vue`**：筛选（关键词 / 状态 / 部门）；列：需求单号、岗位、部门、人数、发布日期、状态、候选人数、操作列（编辑 / 关闭 / 查看候选人）。
- **`CandidatesView.vue`**：筛选（关键词 / 需求 / 阶段）；列：姓名、应聘岗位、需求单号、电话、来源、阶段（`a-tag`）、操作列（编辑 / 推进阶段 / 转入职，按阶段显示）；转入职弹窗（工号 / 入职日期 + 部门 / 岗位缺省展示）。
- 已录用 / 已淘汰（终态）行操作只保留查看。

### 4.4 接口层

- `api/jobRequisition.ts` / `api/candidate.ts`：列表 / 增改 / 关闭 / 阶段 / 转入职；`REQUISITION_STATUS_META` / `CANDIDATE_STAGE_META` / `CANDIDATE_SOURCE_META`。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 招聘与**绩效分规格** | 本规格只做招聘 | 二者各自体量足够，合并会稀释设计质量（§5 范围外） |
| 转入职**跨仓储同事务** | 复用 `030` 员工仓储 | 避免重复实现员工创建 / 唯一性；保证"要么都有、要么都没有" |
| 需求**关闭即冻结** | `40192` | 关闭表示招聘结束，不应再有新动作 |
| 阶段**白名单 + 终态** | §0.1 | 防止从"待筛选"直接"已录用"等非法跳转 |
| 候选人电话**不唯一** | 仅索引 | 同一人可投多个需求；唯一性留给员工档案 |
| 部门**必填**、岗位可选 | 岗位可空缺 | 有些招聘不指定岗位（储备岗） |
| 无面试记录 | 只做阶段 | 面试过程记录需评价表 / 面试官模型，另评 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **需求**：创建（部门存在性 / 单号）；关闭后编辑 / 新增候选人 `40192`。
- **候选人**：创建（需求存在 / 关闭 `40192`）；编辑不改阶段。
- **阶段流转**：白名单内通过；白名单外 / 终态 `40193`。
- **转入职**：非录用 `40193`；重复 `40194`；员工创建字段正确（部门 / 岗位缺省取需求）；`EmployeeNo` / `Phone` 冲突 `40145` / `40147`；同事务性。
- **列表**：筛选与候选人数聚合。
- **清单守卫**：端点纳入 `028` 既有权限标注守卫。

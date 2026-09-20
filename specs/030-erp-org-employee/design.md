---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：组织架构与员工档案（erp-org-employee）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `009-user-management`（用户域模板）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制**复用 `028`**（`Permissions` 常量 + `[RequirePermission]` + `PermissionAuthorizationFilter`），本规格只**续行权限点**、不新增校验路径。

## 0. 约定正文（唯一事实源）

### 0.1 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 部门 | `departments` | `view` / `create` / `update` / `delete` / `status` | `/api/departments*`、`/departments` |
| 岗位 | `positions` | `view` / `create` / `update` / `delete` / `status` | `/api/positions*`、`/positions` |
| 员工 | `employees` | `view` / `create` / `update` / `status` / `export` | `/api/employees*`、`/employees` |

- 命名与「菜单级 = `<域>.view`」（`028` §0.1）一致；`print` 无（本规格无打印）。
- 落地动作：在 `specs/028-erp-rbac/design.md` §0.2 表续行上述三行，并刷新其 `updated`（正文变更）。

### 0.2 菜单归属（在 `025` §0.2 表续行）

`025` §0.2 的「系统（`system`）」分组续行三子项（顺序即行内顺序）：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 系统（`system`） | 部门管理（`departments`）/ 岗位管理（`positions`）/ 员工档案（`employees`） | `030` |

- `MENU_ROUTE_MAP` 与 `e2e/helpers/menu.ts` 的「菜单项 → 分组」映射随之更新。

### 0.3 唯一性与删除保护（唯一事实源）

| 对象 | 字段 | 唯一范围 | 冲突错误码 |
|---|---|---|---|
| 部门 | `Code` | 全局 | `40138` |
| 部门 | `Name` | **同一 `ParentId` 下**（应用层比较，大小写不敏感，同 `009` 惯例） | `40139` |
| 岗位 | `Code` | 全局 | `40142` |
| 岗位 | `Name` | 全局 | `40143` |
| 员工 | `EmployeeNo` | 全局 | `40145` |
| 员工 | `Phone` | 非空时全局 | `40147` |
| 员工 | `Email` | 非空时全局 | `40148` |
| 员工 | `UserId` | 非空时全局（一个账号最多绑一个员工） | `40146` |

| 删除 / 变更动作 | 前置检查 | 错误码 |
|---|---|---|
| 删除部门 | 无子部门且无员工 | `40140` |
| 部门 `ParentId` 变更 | 不得为自身或自身后代 | `40141` |
| 删除岗位 | 无员工引用 | `40144` |

### 0.4 状态与枚举口径

| 枚举 | 取值 | 说明 |
|---|---|---|
| `DepartmentStatus` | `Enabled = 1` / `Disabled = 0` | 停用部门不参与新增下级 / 员工选择（历史数据保留） |
| `PositionStatus` | `Enabled = 1` / `Disabled = 0` | 同上 |
| `EmployeeStatus` | `Active = 1`（在职）/ `Resigned = 0`（离职） | 离职不删除记录 |
| `Gender` | `Unknown = 0` / `Male = 1` / `Female = 2` | 可空 |

## 1. 总体设计

```
组织人事（前端 /departments /positions /employees，域目录 OrgManagement/）
  → DepartmentsController / PositionsController / EmployeesController
    → App.Core/Features/<Departments|Positions|Employees>/<Action>/*RequestHandler
      → IDepartmentRepository / IPositionRepository / IEmployeeRepository（App.Core）→ EF Core 实现（App.Infrastructure）
        → PostgreSQL（Departments / Positions / Employees）
```

核心原则：

- **员工 ≠ 账号**：账号仍是 `009` 的 `Users`；员工通过可空、唯一的 `UserId` 关联账号；员工侧只做"绑定"，不做登录。
- **组织是主数据，权限是横切**：本规格不写权限判断，动作权限点由 `028` 的过滤器统一校验（`[RequirePermission]`）。
- **树形结构自己维护**：部门用 `ParentId` 自引用；防环在 Handler 内做（沿父链上溯判定），不依赖数据库约束。
- **复用既有能力**：导出复用 `027`（`IExcelExporter` + 文件下载契约例外）；权限复用 `028`；分页 / 统一响应 / 错误码遵循 `AGENTS.md` §4。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；**纯日期字段（入职 / 离职）用 `DateOnly` → `date`**（无时区语义，`027` 导出组件已支持 `DateOnly`，见 `ClosedXmlExcelExporter`）；枚举统一小整数 → `smallint`。审计字段：`CreatedAt` / `UpdatedAt`（NOT NULL）+ `CreatedBy` / `UpdatedBy`（可空）。

### 2.1 实体 `App.Core/Entities/Department.cs` 与表 `Departments`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 部门编码 |
| `Name` | `string` | `varchar(50)` | NOT NULL | 部门名称（同一上级下唯一，应用层） |
| `ParentId` | `Guid?` | `uuid` | NULL，FK → `Departments(Id)`，索引 | 上级部门（`NULL` = 顶级） |
| `SortOrder` | `int` | `integer` | NOT NULL，默认 `0` | 同级排序 |
| `Status` | `DepartmentStatus` | `smallint` | NOT NULL，默认 `Enabled` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |

### 2.2 实体 `App.Core/Entities/Position.cs` 与表 `Positions`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 岗位编码 |
| `Name` | `string` | `varchar(50)` | NOT NULL，唯一索引 | 岗位名称 |
| `Status` | `PositionStatus` | `smallint` | NOT NULL，默认 `Enabled` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |

### 2.3 实体 `App.Core/Entities/Employee.cs` 与表 `Employees`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `EmployeeNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 工号（创建后不可改） |
| `Name` | `string` | `varchar(50)` | NOT NULL | 姓名 |
| `Gender` | `Gender?` | `smallint` | NULL | |
| `Phone` | `string?` | `varchar(20)` | NULL，非空时唯一索引（部分索引） | |
| `Email` | `string?` | `varchar(100)` | NULL，非空时唯一索引（部分索引） | |
| `DepartmentId` | `Guid?` | `uuid` | NULL，FK → `Departments(Id)`，索引 | |
| `PositionId` | `Guid?` | `uuid` | NULL，FK → `Positions(Id)`，索引 | |
| `HireDate` | `DateOnly` | `date` | NOT NULL | 入职日期 |
| `ResignDate` | `DateOnly?` | `date` | NULL | 离职日期 |
| `Status` | `EmployeeStatus` | `smallint` | NOT NULL，默认 `Active` | |
| `UserId` | `Guid?` | `uuid` | NULL，FK → `Users(Id)`，非空时唯一索引 | 关联系统账号 |
| `Remark` | `string?` | `varchar(200)` | NULL | |

- 手机 / 邮箱 / `UserId` 的"非空唯一"用 **PostgreSQL 部分唯一索引**（`WHERE "Phone" IS NOT NULL`），EF `HasFilter` 表达；`NULL` 可多条共存。

### 2.4 字段约束常量类（单一来源，后端规则 §5.3）

| 常量类 | 常量 |
|---|---|
| `DepartmentFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` |
| `PositionFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 50` / `RemarkMaxLength = 200` |
| `EmployeeFieldConstraints` | `NoMaxLength = 20` / `NameMaxLength = 50` / `PhoneMaxLength = 20` / `EmailMaxLength = 100` / `RemarkMaxLength = 200` |

### 2.5 迁移与种子

- 迁移：`dotnet ef migrations add AddErpOrgEmployee -p src/App.Infrastructure -s src/App.Api`（建 3 张表 + 唯一 / 部分唯一索引 + FK）。
- 种子：**不预置**部门 / 岗位 / 员工（组织数据由用户维护）；不改 `DatabaseInitializer` 既有逻辑。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`，读模型同层）

| 接口 / 方法 | 说明 |
|---|---|
| `IDepartmentRepository.GetTreeAsync()` | 取全量部门（`AsNoTracking`）由 Handler 组装树，或直接返回 `DepartmentTreeNode` 读模型 |
| `IDepartmentRepository.GetByIdAsync(id)` / `ExistsByCodeAsync(code, excludeId)` / `ExistsByNameAsync(name, parentId, excludeId)` | 存在性与唯一性 |
| `IDepartmentRepository.HasChildrenAsync(id)` / `CountEmployeesAsync(id)`（或批量 `GetEmployeeCountsAsync()`） | 删除保护 / 树节点员工数 |
| `IDepartmentRepository.AddAsync` / `UpdateAsync` / `DeleteAsync` | |
| `IPositionRepository.GetPagedAsync(...)` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` / `CountEmployeesAsync(positionId)` / `GetPickListAsync()` | |
| `IEmployeeRepository.GetPagedAsync(...)` / `GetByIdAsync` / `ExistsByNoAsync` / `ExistsByPhoneAsync` / `ExistsByEmailAsync` / `ExistsByUserIdAsync` / `AddAsync` / `UpdateAsync` / `GetAvailableUsersAsync(employeeId)` / `GetAllForExportAsync(...)` | |

读模型（含联查字段才建，后端规则 §4.3）：

| 读模型 | 联查字段 |
|---|---|
| `DepartmentTreeNode` | `EmployeeCount`（该部门在职员工数）、`Children` |
| `EmployeeListItem` | `DepartmentName` / `PositionName` / `UserDisplayName` |
| `EmployeeDetail` | 同上 + 员工全字段 |
| `EmployeePickUserItem` | `Id` / `Username` / `DisplayName`（可选账号来源） |

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`，从 `40138` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40138 | `DepartmentCodeExists` | 部门编码已存在 |
| 40139 | `DepartmentNameExists` | 同一上级下部门名称已存在 |
| 40140 | `DepartmentInUse` | 部门存在子部门或员工，禁止删除 |
| 40141 | `DepartmentCycle` | 上级部门不能是自身或其下级 |
| 40142 | `PositionCodeExists` | 岗位编码已存在 |
| 40143 | `PositionNameExists` | 岗位名称已存在 |
| 40144 | `PositionInUse` | 岗位已被员工引用，禁止删除 |
| 40145 | `EmployeeNoExists` | 工号已存在 |
| 40146 | `EmployeeUserBound` | 该账号已绑定其他员工 |
| 40147 | `EmployeePhoneExists` | 手机号已存在 |
| 40148 | `EmployeeEmailExists` | 邮箱已存在 |

> 下一个可用业务码 → `40149`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/departments` | GET | `Departments/GetDepartments` | `IReadOnlyList<DepartmentTreeNodeDto>`（树） | `departments.view` |
| `/api/departments` | POST | `Departments/CreateDepartment` | `DepartmentDetailDto` | `departments.create` / 40000 / 40138 / 40139 / 40141 / 40400 |
| `/api/departments/{id:guid}` | GET | `Departments/GetDepartmentById` | `DepartmentDetailDto` | `departments.view` / 40400 |
| `/api/departments/{id:guid}` | PUT | `Departments/UpdateDepartment` | `DepartmentDetailDto` | `departments.update` / 40000 / 40138 / 40139 / 40141 / 40400 |
| `/api/departments/{id:guid}` | DELETE | `Departments/DeleteDepartment` | `null` | `departments.delete` / 40140 / 40400 |
| `/api/departments/{id:guid}/status` | PUT | `Departments/UpdateDepartmentStatus` | `DepartmentDetailDto` | `departments.status` / 40400 |
| `/api/positions` | GET | `Positions/GetPositions` | `PagedResult<PositionListItemDto>` | `positions.view` / 40000 |
| `/api/positions` | POST | `Positions/CreatePosition` | `PositionDetailDto` | `positions.create` / 40000 / 40142 / 40143 |
| `/api/positions/{id:guid}` | GET | `Positions/GetPositionById` | `PositionDetailDto` | `positions.view` / 40400 |
| `/api/positions/{id:guid}` | PUT | `Positions/UpdatePosition` | `PositionDetailDto` | `positions.update` / 40000 / 40142 / 40143 / 40400 |
| `/api/positions/{id:guid}` | DELETE | `Positions/DeletePosition` | `null` | `positions.delete` / 40144 / 40400 |
| `/api/positions/{id:guid}/status` | PUT | `Positions/UpdatePositionStatus` | `PositionDetailDto` | `positions.status` / 40400 |
| `/api/positions/picks` | GET | `Positions/GetPositionPicks` | `IReadOnlyList<PositionPickDto>`（启用） | `positions.view` |
| `/api/employees` | GET | `Employees/GetEmployees` | `PagedResult<EmployeeListItemDto>` | `employees.view` / 40000 |
| `/api/employees` | POST | `Employees/CreateEmployee` | `EmployeeDetailDto` | `employees.create` / 40000 / 40145–40148 / 40400 |
| `/api/employees/{id:guid}` | GET | `Employees/GetEmployeeById` | `EmployeeDetailDto` | `employees.view` / 40400 |
| `/api/employees/{id:guid}` | PUT | `Employees/UpdateEmployee` | `EmployeeDetailDto` | `employees.update` / 40000 / 40146–40148 / 40400 |
| `/api/employees/{id:guid}/status` | PUT | `Employees/UpdateEmployeeStatus` | `EmployeeDetailDto` | `employees.status` / 40400 |
| `/api/employees/available-users` | GET | `Employees/GetAvailableUsers` | `IReadOnlyList<EmployeePickUserItemDto>` | `employees.view` |
| `/api/employees/export` | GET | `Employees/ExportEmployees` | **文件流**（契约例外） | `employees.export` / 40000 |

- 详情 / 编辑共用 `EmployeeDetailDto`；列表与导出共用筛选入参（`ExportEmployeesRequest` 继承「列表筛选字段、忽略分页」，遵循 `027` 约定）。
- `UserId` 为可空；绑定校验：非空时 `ExistsByUserIdAsync(userId, excludeEmployeeId)` → `40146`；账号必须存在（否则 `40400`）。

### 3.4 关键用例流程（Handler）

- **CreateDepartment**：`ExistsByCodeAsync` → `40138`；`ParentId` 非空 → 目标存在（否则 `40400`）；同级重名 `ExistsByNameAsync(name, parentId, null)` → `40139`；`IsAncestor(parentId, newId)` 无需（新建无后代）；落库。
- **UpdateDepartment**：取部门（不存在 `40400`）→ 编码唯一（排除自身）→ `ParentId` 变更时：目标存在（`40400`）、**不得为自身或自身后代**（沿 `ParentId` 上溯，命中即 `40141`）→ 同级重名（排除自身）→ 更新。
- **DeleteDepartment**：取部门（`40400`）→ `HasChildrenAsync` 或 `CountEmployeesAsync > 0` → `40140` → 删除。
- **CreatePosition / UpdatePosition**：编码 / 名称唯一（更新排除自身）。
- **DeletePosition**：`CountEmployeesAsync(positionId) > 0` → `40144` → 删除。
- **CreateEmployee**：工号唯一 `40145`；手机 / 邮箱非空唯一 `40147` / `40148`；`DepartmentId` / `PositionId` 非空时存在性（`40400`）；`UserId` 非空 → 账号存在（`40400`）+ 未绑定（`40146`）；`HireDate` 必填；落库。
- **UpdateEmployee**：工号不可改（请求体不含 `employeeNo`，`AGENTS.md` §4.5 不可改字段）→ 唯一性检查针对手机 / 邮箱 / 账号（排除自身）→ `Status` 与 `ResignDate` 一致性：切「离职」时若 `ResignDate` 为空则置为当天（Handler 推导，见 §5 决策）。
- **UpdateEmployeeStatus**：切换 `Active` / `Resigned`；置离职时补 `ResignDate`。
- **GetAvailableUsers**：返回"启用用户中未被绑定者"∪"当前员工（编辑时）已绑定的用户"。

### 3.5 校验规则（FluentValidation，仅格式层，引用 `*FieldConstraints`）

| 请求 | 规则 |
|---|---|
| `CreateDepartmentRequest` / `UpdateDepartmentRequest` | `code` 必填 1–20；`name` 必填 1–50；`parentId` 可空 GUID；`sortOrder` 0–9999；`remark` ≤ 200；`status` 枚举合法 |
| `CreatePositionRequest` / `UpdatePositionRequest` | `code` 必填 1–20；`name` 必填 1–50；`remark` ≤ 200；`status` 枚举合法 |
| `CreateEmployeeRequest` / `UpdateEmployeeRequest` | `employeeNo`（创建）必填 1–20（更新不含）；`name` 必填 1–50；`gender` 可空枚举；`phone` ≤ 20（非空时格式：`^1[3-9]\d{9}$`）；`email` ≤ 100（非空时 Email 格式）；`hireDate` 必填；`resignDate` 可空且 ≥ `hireDate`；`remark` ≤ 200 |
| `GetPositionsRequest` / `GetEmployeesRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 长列上限（见下） |

- 关键词长度上限对齐匹配列（后端规则 §5.3 ③）：岗位 `keyword` 对齐 `Positions.Name`（50）；员工 `keyword` 对齐 `Employees.Name`（50）/ `EmployeeNo`（20）取 **50**。
- 唯一性、环、删除保护、账号绑定等业务约束在 Handler（后端规则 §4.1）。

### 3.6 DTO 与映射

- `Features/Departments/DepartmentDtoMapper`（`ToDepartmentTreeNodeDto` / `ToDepartmentDetailDto`）、`Features/Positions/PositionDtoMapper`、`Features/Employees/EmployeeDtoMapper`（含派生字段 `statusText` / 关联账号显示名）。
- 树组装（扁平 → 树）在 `GetDepartments` Handler 内完成（一次查询 + 内存建树，部门量为单组织量级）。

### 3.7 注册

- `App.Core/DependencyInjection.AddCore`：注册 3 个 Controller 对应用例的 `IRequestHandler<,>` 与各 `IValidator<>`（约 20 个 Handler）。
- `App.Infrastructure/DependencyInjection.AddInfrastructure`：注册 `IDepartmentRepository` / `IPositionRepository` / `IEmployeeRepository`。

### 3.8 Swagger

- **不分组**（同既有约定）；`FileResult` 动作用 `[ProducesResponseType]` 标注（`003` / `027` 约定）。

## 4. 前端设计

### 4.1 目录与归属决策

```
src/
├── api/
│   ├── department.ts              # 部门接口层
│   ├── position.ts                # 岗位接口层
│   └── employee.ts                # 员工接口层（含 availableUsers / export）
└── views/
    └── OrgManagement/             # 组织人事域（域内平铺，不套子目录）
        ├── DepartmentsView.vue        # 部门树列表
        ├── DepartmentFormDrawer.vue   # 部门新增 / 编辑抽屉
        ├── PositionsView.vue          # 岗位列表
        ├── PositionFormDrawer.vue     # 岗位新增 / 编辑抽屉
        ├── EmployeesView.vue          # 员工列表
        └── EmployeeFormDrawer.vue     # 员工新增 / 编辑抽屉
```

- **归属决策（前端规则 §3 / §4.1）**：部门 / 岗位 / 员工同属「组织人事」功能域，合并到一个域目录 `OrgManagement/`；三个后端 `Features/<Feature>`（`Departments` / `Positions` / `Employees`）语义同域，故前端不再各拆目录（对齐以「语义同域」为准）。`api/` 仍按三资源拆分三文件（前端规则 §3 默认一域一文件，三者为并列资源，分列更清晰）。
- **形态选择（前端规则 §5.5）**：三者字段均少且单屏可容纳 → 列表 + **抽屉** 新增 / 编辑（不建独立详情页，同 `012` 商品形态）。

### 4.2 接口层

- `api/department.ts`：`getDepartments` / `getDepartment` / `createDepartment` / `updateDepartment` / `deleteDepartment` / `updateDepartmentStatus` + 类型。
- `api/position.ts`：`getPositions` / `getPosition` / `createPosition` / `updatePosition` / `deletePosition` / `updatePositionStatus` / `getPositionPicks` + 类型。
- `api/employee.ts`：`getEmployees` / `getEmployee` / `createEmployee` / `updateEmployee` / `updateEmployeeStatus` / `getAvailableUsers` / `exportEmployees`（复用 `downloadBlob`）+ 类型。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `departments` | `departments` | `DepartmentsView` |
| `positions` | `positions` | `PositionsView` |
| `employees` | `employees` | `EmployeesView` |

- 均进 `AppLayout`，`meta.permission = '<域>.view'`；`AppLayout.vue`「系统」分组追加三项（`025` §0.2 续行）。

### 4.4 页面交互

**`DepartmentsView.vue`**（部门树；参照 `006-list-showcase/design.md` §0 的表格骨架，树形列特殊处理）：

- 工具条操作行：`新增顶级部门`（primary）+ `展开全部 / 收起全部` + 刷新。
- `a-table` 树形（`:data` 为树、`children-key="children"`、`row-key="id"`、`expandable`）；列：部门名称（树列）、编码、排序、在职人数、状态（`a-tag`）、操作列。
- 操作列：`新增下级` / `编辑` / `启停` / `删除`（`a-popconfirm`；有子部门 / 员工时后端拒绝并提示 `40140`）。
- 抽屉 `DepartmentFormDrawer.vue`：编码（编辑时不可改？→ 可改，编码非"不可改"字段，仅唯一）/ 名称 / 上级（`a-tree-select`，编辑时排除自身及后代）/ 排序 / 状态 / 备注。

**`PositionsView.vue`**：筛选（关键词 / 状态）+ 排序；列：编码、名称、状态、备注、创建时间、操作列（编辑 / 启停 / 删除 popconfirm）。

**`EmployeesView.vue`**：筛选（工号 / 姓名关键词、部门 `a-tree-select`、岗位 `a-select`、状态）；列：工号、姓名、性别、手机、部门、岗位、入职日期、状态（`a-tag`）、关联账号、操作列（编辑 / 在职离职切换 / 导出在工具条）。工具条：`新增员工` + `导出` + 刷新。

**`EmployeeFormDrawer.vue`**：工号（编辑时禁用）/ 姓名 / 性别 / 手机 / 邮箱 / 部门（`a-tree-select` 启用部门）/ 岗位（`a-select` 来自 `getPositionPicks`）/ 入职日期（`a-date-picker`）/ 离职日期 / 状态 / 关联账号（`a-select`，数据来自 `getAvailableUsers`）/ 备注。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 / 翻页 / 部门树加载 | `loading` | 搜索 / 翻页 + 表格 |
| 抽屉提交（三处） | `submitting` | 提交按钮 |
| 行内删除 | `deletingId` | popconfirm 确认按钮 |
| 行内启停 | `togglingId` | 切换按钮 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 员工与账号分离 | 员工 `UserId` 可空唯一 | 不是每个员工都要登录（仓管 / 工人）；也不是每个账号都是员工（系统 / 外部账号）；一对一避免多绑歧义 |
| 员工不做删除 | 仅"在职 ↔ 离职" | 与 `009` 用户"禁用以代删除"一致，保留历史单据 / 审计引用 |
| 部门用 `ParentId` 自引用 | 不做物化路径 / 闭包表 | 单组织量级（百级），一次查询内存建树即可；物化路径对本期是过度设计 |
| 防环在 Handler | 沿父链上溯 | 数据库 FK 不能表达"非后代"约束；上溯深度 = 树高，成本可忽略 |
| 名称唯一范围"同级" | 应用层比较（大小写不敏感） | 不同部门下允许同名（如两个"财务部"分属不同大区）符合现实 |
| 手机 / 邮箱 / 账号"非空唯一" | PG 部分唯一索引（`HasFilter`） | `NULL` 可多条共存，避免"空值冲突"；比应用层校验更可靠 |
| 入职 / 离职用 `DateOnly` | `date` 列 | 纯日期无时区语义；后端规则 §5.2 约束的是**时间点**字段，日期型用 `DateOnly` 规避 `DateTime` 的 Kind 缺陷；`027` 导出组件已支持 |
| 账号绑定冲突在提交时校验 | `ExistsByUserIdAsync` | 前端下拉过滤只是体验，后端唯一索引 + Handler 校验才是边界 |
| 离职补 `ResignDate` | Handler 推导（空则置当天） | 保证"离职"状态有日期，避免列表出现"离职但无日期"的歧义 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser` 同既有约定。

- **部门**：`CreateDepartment` 成功 / 编码重复 `40138` / 同级重名 `40139` / 上级不存在 `40400`；`UpdateDepartment` 成功 / 上级为自身 `40141` / 上级为后代 `40141` / 重名排除自身；`DeleteDepartment` 有子部门 `40140` / 有员工 `40140` / 成功；`GetDepartments` 树组装（层级 / 排序 / 员工数）。
- **岗位**：`CreatePosition` / `UpdatePosition` 编码 `40142`、名称 `40143`；`DeletePosition` 被引用 `40144`；`GetPositions` 筛选分页；`GetPositionPicks` 仅启用。
- **员工**：`CreateEmployee` 成功 / 工号重复 `40145` / 手机重复 `40147` / 邮箱重复 `40148` / 账号已绑 `40146` / 部门或岗位不存在 `40400` / 账号不存在 `40400`；`UpdateEmployee` 不含工号字段 / 唯一性排除自身；`UpdateEmployeeStatus` 置离职补 `ResignDate`；`GetAvailableUsers` 含当前员工已绑账号。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：三实体的 EF `HasMaxLength` == 对应常量；`keyword` 边界（岗位 50、员工 50）通过 / 越界拒绝。
- **导出**：`ExportEmployees` 筛选透传、`pageSize = MaxRows+1`、超限 `40000`、列头与列表一致。
- **清单守卫**：`028` 的集成测试遍历全部动作，本规格 19 个端点须全部标注合法权限点（自动纳入既有守卫）。

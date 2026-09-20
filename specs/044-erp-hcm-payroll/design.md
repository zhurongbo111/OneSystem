---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：考勤与薪酬（erp-hcm-payroll）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `030-erp-org-employee`（员工域）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 权限机制复用 `028`；员工主体复用 `030` 的 `Employee`。

## 0. 约定正文（唯一事实源）

### 0.1 枚举与状态

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `AttendanceType` | `Leave = 0` / `Overtime = 1` | 请假 / 加班 | `orange` / `blue` |
| `PayrollStatus` | `Draft = 0` / `Paid = 1` | 草稿 / 已发放 | `blue` / `green` |

- **工资单唯一性**：`(EmployeeId, Year, Month)` 唯一，一个员工一个月一条。
- **实发公式**：`NetPay = BaseSalary + Allowance − Deduction`（`numeric(18,2)`，后端计算）。
- **发放锁定**：`Paid` 状态禁止编辑 / 删除（`40171`）。

### 0.2 菜单归属（在 `025` §0.2 表续行）

新增顶级分组「人事（`hrm`）」（置于「系统」之前）：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 人事（`hrm`） | 员工档案（`employees`）/ 考勤登记（`attendance`）/ 薪酬（`payroll`） | `030` / `044` |

- **修订 `030` §0.2**：`030` 原把「组织 / 岗位 / 员工」归入「系统」分组；本规格落地时**将「员工档案」迁入「人事」分组**（组织 / 岗位仍留「系统」，或一并迁入，实现时以本条为准取「员工档案迁入人事」）。同步刷新 `030` 的 `updated`。

### 0.3 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 考勤登记 | `attendance` | `view` / `create` / `update` / `delete` | `/api/attendances*`、`/attendance` |
| 薪酬 | `payroll` | `view` / `create` / `update` / `delete` / `status` | `/api/payrolls*`、`/payrolls` |

## 1. 总体设计

```
考勤与薪酬（前端 /attendance、/payrolls，域目录 HrmManagement/）
  → AttendancesController / PayrollsController
    → App.Core/Features/<Attendances|Payrolls>/<Action>/*RequestHandler
      → IAttendanceRepository / IPayrollRepository
        + IEmployeeRepository（员工存在性 / 在职判定，037）
        + IUnitOfWork
        → PostgreSQL（Attendances / Payrolls）
```

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；纯日期（考勤起止）用 `DateOnly` → `date`；金额 `numeric(18,2)`。

### 2.1 实体 `App.Core/Entities/Attendance.cs` 与表 `Attendances`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `EmployeeId` | `Guid` | `uuid` | NOT NULL，FK → `Employees(Id)`，索引 | 员工 |
| `EmployeeName` | `string` | `varchar(50)` | NOT NULL | 员工姓名**快照** |
| `Type` | `AttendanceType` | `smallint` | NOT NULL | 请假 / 加班 |
| `StartDate` | `DateOnly` | `date` | NOT NULL | 起始日 |
| `EndDate` | `DateOnly` | `date` | NOT NULL，≥ `StartDate` | 结束日 |
| `Remark` | `string?` | `varchar(200)` | NULL | 事由 |
| 审计 | | | | |

- 业务校验：同一员工、同一 `Type`、日期区间**不重叠**（Handler 校验，重叠 → `40000`，message 说明）。

### 2.2 实体 `App.Core/Entities/Payroll.cs` 与表 `Payrolls`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `EmployeeId` | `Guid` | `uuid` | NOT NULL，FK → `Employees(Id)`，索引 | 员工 |
| `EmployeeName` | `string` | `varchar(50)` | NOT NULL | 姓名**快照** |
| `Year` | `int` | `integer` | NOT NULL | 年 |
| `Month` | `int` | `integer` | NOT NULL | 月（1–12） |
| `BaseSalary` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 基本工资 |
| `Allowance` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，≥ 0 | 津贴 |
| `Deduction` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，≥ 0 | 扣款 |
| `NetPay` | `decimal` | `numeric(18,2)` | NOT NULL | 实发 = 基本 + 津贴 − 扣款（后端计算） |
| `Status` | `PayrollStatus` | `smallint` | NOT NULL，默认 `Draft` | |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | |

- 唯一索引 `(EmployeeId, Year, Month)`。

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `AttendanceFieldConstraints` | `RemarkMaxLength = 200` |
| `PayrollFieldConstraints` | `AmountMaxValue = 9999999.99m` / `RemarkMaxLength = 200` |

### 2.4 迁移

- 迁移：`dotnet ef migrations add AddErpHcmPayroll -p src/App.Infrastructure -s src/App.Api`（建 2 表 + 唯一索引；FK 不级联删除）。
- 无种子数据（`BaseSalary` 由人工填写或按员工档案默认值，见 §5）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IAttendanceRepository.GetPagedAsync(...)` / `GetByIdAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` / `HasOverlapAsync(employeeId, type, start, end, excludeId)` | |
| `IPayrollRepository.GetPagedAsync(...)` / `GetByIdAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` / `ExistsAsync(employeeId, year, month)` / `AddRangeAsync(...)` / `GetEmployeesForPeriodAsync(year, month)` | 批量生成用 |

读模型：`AttendanceListItem`、`PayrollListItem`（含员工名 / 部门名可选）。

### 3.2 错误码（`ErrorCode.cs`，从 `40170` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40170 | `PayrollExists` | 该员工该期间的工资单已存在 |
| 40171 | `PayrollLocked` | 工资单已发放，禁止修改 / 删除 |

> 下一个可用业务码 → `40172`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/attendances` | GET | `Attendances/GetAttendances` | `PagedResult<AttendanceListItemDto>` | `attendance.view` / 40000 |
| `/api/attendances` | POST | `Attendances/CreateAttendance` | `AttendanceDetailDto` | `attendance.create` / 40000 / 40400 |
| `/api/attendances/{id:guid}` | PUT | `Attendances/UpdateAttendance` | `AttendanceDetailDto` | `attendance.update` / 40000 / 40400 |
| `/api/attendances/{id:guid}` | DELETE | `Attendances/DeleteAttendance` | `null` | `attendance.delete` / 40400 |
| `/api/payrolls` | GET | `Payrolls/GetPayrolls` | `PagedResult<PayrollListItemDto>` | `payroll.view` / 40000 |
| `/api/payrolls` | POST | `Payrolls/CreatePayroll` | `PayrollDetailDto` | `payroll.create` / 40000 / 40170 / 40400 |
| `/api/payrolls/{id:guid}` | PUT | `Payrolls/UpdatePayroll` | `PayrollDetailDto` | `payroll.update` / 40000 / 40171 / 40400 |
| `/api/payrolls/{id:guid}/status` | PUT | `Payrolls/UpdatePayrollStatus` | `PayrollDetailDto` | `payroll.status` / 40400 |
| `/api/payrolls/{id:guid}` | DELETE | `Payrolls/DeletePayroll` | `null` | `payroll.delete` / 40171 / 40400 |
| `/api/payrolls/generate` | POST | `Payrolls/GeneratePayrolls` | `{ created, skipped }` | `payroll.create` / 40000 |

### 3.4 关键用例流程（Handler）

- **CreateAttendance**：员工存在且**在职**（否则 `40400` / 业务拒绝）；`endDate ≥ startDate`；`HasOverlapAsync` → 重叠则 `40000`（message 含冲突区间）。
- **CreatePayroll**：员工存在；`ExistsAsync(employeeId, year, month)` → `40170`；计算 `NetPay`；落库。
- **UpdatePayroll**：非 `Draft` → `40171`；重算 `NetPay`。
- **GeneratePayrolls**：取该期间**在职员工**（`IEmployeeRepository`），对每条跳过已存在（`ExistsAsync`），批量新增草稿（`BaseSalary` 取员工档案默认值或 0）；返回 `{ created, skipped }`。
- **UpdatePayrollStatus**：`Draft → Paid`（或 `Paid → Draft` 反发放，仅 `payroll.status` 权限）；已发放编辑受限。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateAttendanceRequest` / `UpdateAttendanceRequest` | `employeeId` 必填 GUID；`type` 枚举合法；`startDate` / `endDate` 必填且 `endDate ≥ startDate`；`remark` ≤ 200 |
| `CreatePayrollRequest` / `UpdatePayrollRequest` | `employeeId` 必填 GUID；`year` 2000–2100；`month` 1–12；`baseSalary` / `allowance` / `deduction` 0–9999999.99；`remark` ≤ 200 |
| `GeneratePayrollsRequest` | `year` 2000–2100；`month` 1–12 |
| `GetAttendancesRequest` / `GetPayrollsRequest` | `page ≥ 1`；`pageSize` 1–100 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── attendance.ts              # 考勤接口层
│   └── payroll.ts                 # 薪酬接口层
└── views/
    └── HrmManagement/
        ├── AttendancesView.vue        # 考勤列表
        ├── AttendanceFormDrawer.vue   # 考勤新增 / 编辑
        ├── PayrollsView.vue           # 薪酬列表（含批量生成 / 发放）
        └── PayrollFormDrawer.vue      # 工资单新增 / 编辑
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `attendance` | `attendance` | `AttendancesView` |
| `payrolls` | `payrolls` | `PayrollsView` |

- 「人事」分组新增「考勤登记」「薪酬」（「员工档案」由 `030` 迁入，见 §0.2）；`meta.permission = 'attendance.view'` / `'payroll.view'`。

### 4.3 页面交互

- **`AttendancesView.vue`**：筛选（员工 `a-select`、类型、日期范围）；列：员工、类型、起止日期、天数、事由、操作列（编辑 / 删除）。
- **`PayrollsView.vue`**：筛选（期间年月、员工、状态）；工具条「批量生成」（选年月）「新增」+ 刷新；列：员工、期间、基本工资、津贴、扣款、实发、状态、操作列（编辑 / 发放 / 删除）；已发放行操作置灰。
- **`PayrollFormDrawer.vue`**：员工（新增时可选）/ 年月 / 基本工资 / 津贴 / 扣款 / 实发（只读实时预览）/ 备注。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 实发后端计算 | Handler 重算 | 不信任前端；口径唯一 |
| 期间唯一 | `(EmployeeId, Year, Month)` | 一人一月一条，避免重复发薪 |
| 发放锁定 | `Paid` 终态（可反发放） | 财务已发不应改动；反发放留后门（受限权限） |
| 考勤只登记 | 无审批 / 无打卡 | 一期克制，避免引入排班模型 |
| 薪酬不自动计税 | 扣款手工填 | 个税 / 社保规则复杂且政策敏感，另立 |
| 批量生成跳过已存在 | 幂等 | 可重复执行不产生重复工资单 |

## 6. 单元测试设计

- **考勤**：`CreateAttendance` 校验、员工存在 / 在职、`endDate ≥ startDate`、区间重叠拒绝；`UpdateAttendance` / `DeleteAttendance`。
- **工资单**：`CreatePayroll` `NetPay` 计算、期间唯一 `40170`；`UpdatePayroll` 已发放 `40171`；`DeletePayroll` 已发放拒绝；`UpdatePayrollStatus` 发放 / 反发放。
- **批量生成**：为在职员工生成、跳过已存在；返回 `created` / `skipped` 计数。
- **字段约束一致性**：两个常量类与 EF 列长 / 精度一致。
- **清单守卫**：10 个端点纳入 `028` 既有守卫。

---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：考勤与薪酬（erp-hcm-payroll）

> 依据 `specs/044-erp-hcm-payroll/design.md` 拆分。含考勤登记 + 月度工资单（含批量生成 / 发放）+ 权限 / 菜单（新建「人事」分组，迁入员工档案）+ e2e。
> 前置：`030`（员工档案）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Attendance` / `Payroll` + 枚举 `AttendanceType` / `PayrollStatus`；`AppDbContext` 追加 2 个 `DbSet`
- [ ] 1.2 字段约束常量类 `AttendanceFieldConstraints` / `PayrollFieldConstraints`
- [ ] 1.3 EF 配置（FK、索引、`Payrolls` 唯一 `(EmployeeId, Year, Month)`、快照列）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpHcmPayroll -p src/App.Infrastructure -s src/App.Api`

## 二、后端：考勤域

- [ ] 2.1 读模型 `AttendanceListItem` + `IAttendanceRepository`（`GetPagedAsync` / `GetByIdAsync` / `HasOverlapAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `AttendanceListItemDto` / `AttendanceDetailDto` + `AttendanceDtoMapper`
- [ ] 2.3 用例 `Attendances/GetAttendances` / `CreateAttendance`（在职 + 区间不重叠）/ `UpdateAttendance` / `DeleteAttendance`
- [ ] 2.4 `AttendancesController`（4 端点）+ DI 注册

## 三、后端：薪酬域

- [ ] 3.1 读模型 `PayrollListItem` + `IPayrollRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsAsync` / `GetEmployeesForPeriodAsync` / `AddAsync` / `AddRangeAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 3.2 共享出参 `PayrollListItemDto` / `PayrollDetailDto` + `PayrollDtoMapper`
- [ ] 3.3 用例 `Payrolls/GetPayrolls` / `CreatePayroll`（`40170`）/ `UpdatePayroll`（`40171`）/ `UpdatePayrollStatus` / `DeletePayroll`（`40171`）
- [ ] 3.4 用例 `Payrolls/GeneratePayrolls`（在职员工批量生成草稿，跳过已存在）
- [ ] 3.5 `PayrollsController`（6 端点）+ DI 注册

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40170` / `40171`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40172`

## 五、单元测试（后端）

- [ ] 5.1 考勤：校验、员工存在 / 在职、日期边界、区间重叠拒绝、编辑 / 删除
- [ ] 5.2 工资单：`NetPay` 计算、期间唯一 `40170`、已发放 `40171`（改 / 删）、发放 / 反发放
- [ ] 5.3 批量生成：在职员工生成、跳过已存在、计数正确
- [ ] 5.4 字段约束一致性单测
- [ ] 5.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 六、前端

- [ ] 6.1 `api/attendance.ts` / `api/payroll.ts`
- [ ] 6.2 `views/HrmManagement/AttendancesView.vue` + `AttendanceFormDrawer.vue`
- [ ] 6.3 `views/HrmManagement/PayrollsView.vue`（批量生成 / 发放 / 已发放置灰）+ `PayrollFormDrawer.vue`（实发实时预览）
- [ ] 6.4 `router/index.ts` 新增 `attendance` / `payrolls`；`AppLayout.vue` 新增「人事」分组（员工档案迁入 + 考勤 + 薪酬）+ `MENU_ROUTE_MAP`
- [ ] 6.5 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 6.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/hcm-payroll.spec.ts`：登记请假 / 加班 → 同员工同日重复登记被拒
- [ ] 7.2 同文件：批量生成当月工资单 → 编辑实发重算 → 发放 → 再改 `40171`
- [ ] 7.3 `cd frontend && npm run test:e2e` 全量通过（含 `org-employee.spec.ts` 因菜单迁入的回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/028-erp-rbac/design.md` §0.2 续行 `attendance.*` / `payroll.*`；刷新其 `updated`
- [ ] 8.2 `specs/025-erp-report/design.md` §0.2 新增「人事」分组；刷新其 `updated`
- [ ] 8.3 `specs/030-erp-org-employee/design.md` §0.2 修订：员工档案菜单迁入「人事」分组；刷新其 `updated`
- [ ] 8.4 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 8.5 `specs/ROADMAP.md` §4.2 状态更新（`044` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 工资单核算口径唯一；发放锁定生效；清单守卫通过（10 个新增端点合法标注权限点）。

---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：个税与社保（erp-payroll-tax）

> 依据 `specs/055-erp-payroll-tax/design.md` 拆分。扩展 `044`：工资单加列 + 个税计算器 + 1 个用例 + 改造批量生成 + 前端 + e2e。
> 前置：`044`（工资单 / 实发公式 / 发放锁定）、`030`（员工）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 `Payroll` 增 `SocialInsurance` / `SpecialDeduction` / `Tax`；EF 配置更新
- [ ] 1.2 `PayrollFieldConstraints` 续行；新增 `PayrollTaxRateTable`（预扣率表 + 减除费用 5000）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpPayrollTax -p src/App.Infrastructure -s src/App.Api`（3 列默认 0 回填）

## 二、后端：计算器与仓储

- [ ] 2.1 `PayrollTaxCalculator`（纯函数，累计预扣预缴法，§0.2）
- [ ] 2.2 `IPayrollRepository.GetYearToDateAsync(employeeId, year, uptoMonth)` + 实现

## 三、后端：用例与接口

- [ ] 3.1 新增用例 `Payrolls/CalculatePayrollTax`（`40171`）+ 端点 `POST /api/payrolls/{id:guid}/calculate-tax`
- [ ] 3.2 改造 `Payrolls/GeneratePayrolls`：默认社保 / 专项附加 0 + 个税自动计算 + 实发公式
- [ ] 3.3 改造 `CreatePayroll` / `UpdatePayroll`：接收社保 / 专项附加，`tax` 后端计算、`tax` 不入参
- [ ] 3.4 出参 DTO 增三字段 + `PayrollsDtoMapper` 更新

## 四、单元测试（后端）

- [ ] 4.1 计算器：税率边界、减除费用、累计已缴递减、`T ≤ 0`、税额非负
- [ ] 4.2 累计：1..M 取值（缺月按 0）、仅本年度
- [ ] 4.3 实发公式与边界；`Paid` 重算 `40171`；`tax` 不可注入
- [ ] 4.4 批量生成默认值与自动带税
- [ ] 4.5 字段约束一致性单测；`044` 用例回归
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/payroll.ts`：类型增三字段 + `calculatePayrollTax`
- [ ] 5.2 `views/HrmManagement/PayrollsView.vue`：增「社保 / 个税」列 + 「重算个税」行操作
- [ ] 5.3 `PayrollFormDrawer.vue`：增社保 / 专项附加输入 + 个税只读 + 实发预览
- [ ] 5.4 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/payroll-tax.spec.ts`：生成工资单 → 个税按累计法计算 → 实发正确；次月累计生效
- [ ] 6.2 同文件：已发放重算 `40171`；调整社保后重算个税生效
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含人事域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/044-erp-hcm-payroll/design.md` §0.1（实发公式）/ §2.2（字段）/ §3.3（端点）改写为最终态 + 演进指针；刷新 `updated`
- [ ] 7.2 `.codebuddy/CONTEXT.md` §2（实体 `Payroll` 字段 / `PayrollFieldConstraints`）/ §3（薪酬 api）同步
- [ ] 7.3 `specs/ROADMAP.md` §4.2 状态更新（`055` → 已实现）；§3 覆盖矩阵「人力域 · HCM」缺口同步
- [ ] 7.4 确认 `028` §0.2 / `ROADMAP` §6 无新增（权限点 / 错误码）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 个税按累计预扣预缴法计算；实发公式与 §0.1 一致；发放锁定语义不变。

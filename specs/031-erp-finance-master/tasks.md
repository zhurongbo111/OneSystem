---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：财务主数据（erp-finance-master）

> 依据 `specs/031-erp-finance-master/design.md` 拆分。含会计科目（树）、税率（字典）、预置科目种子、权限 / 菜单续行、e2e。
> 前置：`028`（权限基础设施）已实现；`033` / `034` 依赖本规格。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Account` / `TaxRate` + 枚举 `AccountCategory` / `AccountDirection` / `AccountStatus` / `TaxRateStatus`；`AppDbContext` 追加 2 个 `DbSet`
- [ ] 1.2 字段约束常量类 `AccountFieldConstraints` / `TaxRateFieldConstraints`
- [ ] 1.3 EF 配置 `AccountConfiguration`（唯一 `Code`、自引用 FK、索引）/ `TaxRateConfiguration`（唯一 `Code` / `Name`、`numeric(9,4)`）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpFinanceMaster -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.5 `DatabaseInitializer` 幂等预置标准科目（§2.4，`IsPreset = true`）

## 二、后端：科目域用例与接口

- [ ] 2.1 读模型 `AccountTreeNode` + `IAccountRepository`（`GetAllAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `HasChildrenAsync` / `IsReferencedByVoucherAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `AccountTreeNodeDto` / `AccountDetailDto` + `AccountDtoMapper`
- [ ] 2.3 用例 `Accounts/GetAccounts`（树组装）/ `CreateAccount` / `GetAccountById` / `UpdateAccount`（防环）/ `DeleteAccount`（`40150` / `40153`）/ `UpdateAccountStatus`
- [ ] 2.4 `AccountsController`（6 端点）+ DI 注册

## 三、后端：税率域用例与接口

- [ ] 3.1 读模型 `TaxRateListItem` + `ITaxRateRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 3.2 共享出参 `TaxRateListItemDto` / `TaxRateDetailDto` + `TaxRateDtoMapper`
- [ ] 3.3 用例 `TaxRates/GetTaxRates` / `CreateTaxRate` / `GetTaxRateById` / `UpdateTaxRate` / `DeleteTaxRate` / `UpdateTaxRateStatus`
- [ ] 3.4 `TaxRatesController`（6 端点）+ DI 注册

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40149`–`40153`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40154`

## 五、单元测试（后端）

- [ ] 5.1 科目用例：树组装、编码唯一、防环（自身 / 后代）、删除保护（子科目 / 预置 / 凭证引用）
- [ ] 5.2 税率用例：编码 / 名称唯一、`rate` 边界、筛选分页
- [ ] 5.3 种子幂等单测
- [ ] 5.4 字段约束一致性单测
- [ ] 5.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 六、前端

- [ ] 6.1 `api/account.ts` / `api/taxRate.ts`
- [ ] 6.2 `views/FinanceManagement/AccountsView.vue`（树表）+ `AccountFormDrawer.vue`
- [ ] 6.3 `views/FinanceManagement/TaxRatesView.vue` + `TaxRateFormDrawer.vue`
- [ ] 6.4 `router/index.ts` 新增 `accounts` / `tax-rates`；`AppLayout.vue` 新增「财务」分组两项 + `MENU_ROUTE_MAP`
- [ ] 6.5 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 6.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/finance-master.spec.ts`：预置科目可见 → 建下级科目 → 编码重复 `40149` → 删除有子科目 `40150`
- [ ] 7.2 同文件：建税率 → 编码 / 名称重复 `40151` / `40152` → 筛选 / 编辑 / 停用
- [ ] 7.3 `cd frontend && npm run test:e2e` 全量通过（既有用例回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/028-erp-rbac/design.md` §0.2 续行 `accounts.*` / `taxRates.*`；刷新其 `updated`
- [ ] 8.2 `specs/025-erp-report/design.md` §0.2 新增「财务」分组（含 `033` / `034` 预留项）；刷新其 `updated`
- [ ] 8.3 `specs/032-erp-invoice/design.md` 加注记：税率可改为引用本规格税率字典（演进，可选）
- [ ] 8.4 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 8.5 `specs/ROADMAP.md` §4.2 状态更新（`031` → 已实现）；§3 覆盖矩阵「财务主数据」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 清单守卫通过：12 个新增端点均标注合法权限点；权限点 / 菜单已在 `028` / `025` 对应表续行。

---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：多公司 / 多组织（erp-multi-org）

> 依据 `specs/057-erp-multi-org/design.md` 拆分。横向改造：公司域 + 全业务 / 账务表加 `CompanyId` + 查询隔离 + 顶栏切换 + e2e。
> 前置：`028`（权限）、`030`（组织）、`033`（账套）、`046`（数据范围）、`038`（默认实体范式）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：公司域

- [ ] 1.1 实体 `Company` / `UserCompany` + 枚举 `CompanyStatus` + `CompanyFieldConstraints`（含 `DefaultCompanyId`）；`AppDbContext` 追加两 DbSet
- [ ] 1.2 EF 配置（`Code` 唯一、`(UserId, CompanyId)` 唯一、索引）
- [ ] 1.3 `ICurrentCompany` / `CurrentCompany`（请求头 / 默认公司 / 缓存 / `40195`）+ DI
- [ ] 1.4 `ICompanyRepository` / `IUserCompanyRepository` + 实现 + 注册；读模型 `CompanyListItem` / `CompanyDetail` / `CompanyPickItem`
- [ ] 1.5 用例 `Companies/*`（列表 / 新增 / 详情 / 编辑 / 状态 / 删除 / picks）+ `CompaniesController` + DI

## 二、后端：数据隔离（§0.2 受影响表）

- [ ] 2.1 迁移 `AddErpMultiOrg`（建两表 + `InsertData` 固定 GUID 默认公司）
- [ ] 2.2 受影响表逐表加 `CompanyId`（可空 → 回填默认公司 → NOT NULL + FK + 索引）
- [ ] 2.3 唯一键修订（`AccountingPeriods` / `Inventory` / `Approvals` 等，§0.2）
- [ ] 2.4 各仓储查询追加 `CompanyId` 过滤；各写用例赋 `CompanyId = ICurrentCompany.CompanyId()`
- [ ] 2.5 明细表**不**加 `CompanyId`（由主表保证）
- [ ] 2.6 隔离守卫测试：反射断言 §0.2 全部实体含 `CompanyId`

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40195`–`40197`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40198`

## 四、单元测试（后端）

- [ ] 4.1 迁移回归：默认公司回填后既有用例全绿
- [ ] 4.2 当前公司：解析 / 默认 / 无权 `40195` / `SuperAdmin`
- [ ] 4.3 隔离：跨公司不可见；写入赋值；明细不重复
- [ ] 4.4 唯一键 `(CompanyId, …)` 生效
- [ ] 4.5 公司域：编码唯一、`40196` / `40197`
- [ ] 4.6 隔离守卫测试
- [ ] 4.7 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/company.ts` + `stores/company.ts`（当前公司）
- [ ] 5.2 `api/request.ts` 注入 `X-Company-Id`；`api/user.ts` 增「可访问公司」多选
- [ ] 5.3 `components/AppLayout.vue` 顶栏公司切换器
- [ ] 5.4 `views/CompanyManagement/CompaniesView.vue`
- [ ] 5.5 `router/index.ts` 新增路由；「系统」分组续行「公司管理」+ `MENU_ROUTE_MAP`
- [ ] 5.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/multi-org.spec.ts`：建公司 B → 授权用户 → 切换 → 单据 / 报表按公司隔离
- [ ] 6.2 同文件：无权公司切换 `40195`；停用 / 删除默认公司 `40197`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（既有 e2e 在默认公司下回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `companies.*`；刷新 `updated`
- [ ] 7.2 `specs/046-erp-data-scope/design.md` §0.1 / §0.3 增「公司层优先」；刷新 `updated`
- [ ] 7.3 `specs/025-erp-report/design.md` §0.2「系统」分组续行；刷新 `updated`
- [ ] 7.4 `specs/ROADMAP.md` §6.9「单主体起步」标注演进指针；§4.2 状态更新（`057` → 已实现）；§3 / §7 同步；§6 续行
- [ ] 7.5 `.codebuddy/CONTEXT.md` §2（实体 / 组件 / 仓储）/ §3（新增域 / 切换器 / store）同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 默认公司升级无感；跨公司数据隔离生效；隔离守卫测试通过。

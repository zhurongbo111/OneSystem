---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：CRM 售前（erp-crm-presale）

> 依据 `specs/043-erp-crm-presale/design.md` 拆分。含线索 + 商机 + 跟进活动 + 转商机 + 权限 / 菜单（新建 CRM 分组）+ e2e。
> 前置：`013`（客户）、`030`（员工 / 负责人）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Lead` / `Opportunity` / `Activity` + 枚举 `LeadStatus` / `LeadSource` / `OpportunityStage` / `ActivityBizType` / `ActivityType`；`AppDbContext` 追加 3 个 `DbSet`
- [ ] 1.2 字段约束常量类 `LeadFieldConstraints` / `OpportunityFieldConstraints` / `ActivityFieldConstraints`
- [ ] 1.3 EF 配置（唯一单号、FK、索引、快照列）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpCrmPresale -p src/App.Infrastructure -s src/App.Api`

## 二、后端：线索域

- [ ] 2.1 读模型 `LeadListItem` / `LeadDetail` + `ILeadRepository`（`GetPagedAsync` / `GetByIdAsync` / `AddAsync` / `UpdateAsync` / `GenerateNoAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `LeadListItemDto` / `LeadDetailDto` + `LeadDtoMapper`
- [ ] 2.3 用例 `Leads/GetLeads` / `CreateLead` / `GetLeadById` / `UpdateLead` / `UpdateLeadStatus` / `ConvertLead`（`40168`）
- [ ] 2.4 `LeadsController`（6 端点）+ DI 注册

## 三、后端：商机域

- [ ] 3.1 读模型 `OpportunityListItem` / `OpportunityDetail` + `IOpportunityRepository` + 实现 + 注册
- [ ] 3.2 共享出参 `OpportunityListItemDto` / `OpportunityDetailDto` + `OpportunityDtoMapper`
- [ ] 3.3 用例 `Opportunities/GetOpportunities` / `CreateOpportunity` / `GetOpportunityById` / `UpdateOpportunity` / `UpdateOpportunityStage`（`40169`）
- [ ] 3.4 `OpportunitiesController`（5 端点）+ DI 注册

## 四、后端：跟进活动

- [ ] 4.1 读模型 `ActivityItem` + `IActivityRepository`（`GetByBizAsync` / `AddAsync`）+ 实现 + 注册
- [ ] 4.2 出参 `ActivityItemDto` + `ActivityDtoMapper`
- [ ] 4.3 用例 `Activities/GetLeadActivities` / `CreateLeadActivity` + `Activities/GetOpportunityActivities` / `CreateOpportunityActivity`（按域拆分端点，避免动态权限）
- [ ] 4.4 `ActivitiesController`（4 端点）+ DI 注册

## 五、后端：错误码

- [ ] 5.1 `ErrorCode.cs` 追加 `40168` / `40169`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40170`

## 六、单元测试（后端）

- [ ] 6.1 线索：校验、状态流转与终态限制、`ConvertLead` 成功（同事务）与 `40168`
- [ ] 6.2 商机：阶段推进、终态 `40169`、客户 / 负责人存在性
- [ ] 6.3 活动：按域归属查询与新增；无改删端点
- [ ] 6.4 字段约束一致性单测
- [ ] 6.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 七、前端

- [ ] 7.1 `api/lead.ts` / `api/opportunity.ts`
- [ ] 7.2 `views/CrmManagement/LeadsView.vue` + `LeadFormDrawer.vue` + `LeadDetailView.vue`（活动时间线 + 转商机）
- [ ] 7.3 `views/CrmManagement/OpportunitiesView.vue` + `OpportunityFormPage.vue` + `OpportunityDetailView.vue`（活动时间线 + 阶段推进）
- [ ] 7.4 `router/index.ts` 新增 5 条路由；`AppLayout.vue` 新增「CRM」分组（线索 / 商机）+ `MENU_ROUTE_MAP`
- [ ] 7.5 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 7.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 八、E2E（Playwright）

- [ ] 8.1 新增 `e2e/crm-presale.spec.ts`：建线索 → 加跟进活动 → 转商机 → 线索「已转化」、商机出现
- [ ] 8.2 同文件：商机阶段推进至赢单 → 再改 `40169`；已转化线索再转 `40168`；线索 / 商机不影响库存与应收
- [ ] 8.3 `cd frontend && npm run test:e2e` 全量通过（既有用例回归）

## 九、规格与上下文联动

- [ ] 9.1 `specs/028-erp-rbac/design.md` §0.2 续行 `leads.*` / `opportunities.*`；刷新其 `updated`
- [ ] 9.2 `specs/025-erp-report/design.md` §0.2 新增「CRM」分组（含 `045` 预留项）
- [ ] 9.3 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 9.4 `specs/ROADMAP.md` §4.2 状态更新（`043` → 已实现）；§3 覆盖矩阵「外围 · CRM」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 转商机原子；售前不触碰库存 / 资金；清单守卫通过（全部新增端点合法标注权限点）。

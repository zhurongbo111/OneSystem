---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：招聘管理（erp-recruit）

> 依据 `specs/056-erp-recruit/design.md` 拆分。新增招聘需求 + 候选人（阶段流转）+ 转入职 + 权限 / 菜单续行 + 前端 + e2e。
> 前置：`030`（部门 / 岗位 / 员工）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `JobRequisition` / `Candidate` + 枚举 `JobRequisitionStatus` / `CandidateStage` / `CandidateSource`；`AppDbContext` 追加两 DbSet
- [ ] 1.2 字段约束 `JobRequisitionFieldConstraints` / `CandidateFieldConstraints`
- [ ] 1.3 EF 配置（`RequisitionNo` 唯一、`Candidate.EmployeeId` 非空唯一（部分索引）、FK、索引）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpRecruit -p src/App.Infrastructure -s src/App.Api`

## 二、后端：仓储与用例

- [ ] 2.1 读模型 `JobRequisitionListItem` / `CandidateListItem` / `CandidateDetail` + 两仓储（分页 / 详情 / 增改 / 单号 / `UpdateStatusAsync` / `UpdateStageAsync` / `MarkConvertedAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 DTO + `JobRequisitionsDtoMapper` / `CandidatesDtoMapper`
- [ ] 2.3 用例 `JobRequisitions/*`（列表 / 新增 / 详情 / 编辑 / 关闭；`40192`）
- [ ] 2.4 用例 `Candidates/*`（列表 / 新增 / 详情 / 编辑 / `UpdateCandidateStage`（`40193`）/ `ConvertCandidate`（`40193` / `40194`））
- [ ] 2.5 两 Controller + DI；写用例接入操作日志（`029` §0.1 续行，资源 `JobRequisition` / `Candidate`）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40192`–`40194`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40195`；§6.7 单号前缀续行 `JR`

## 四、单元测试（后端）

- [ ] 4.1 需求：创建（部门存在性）、关闭后限制 `40192`
- [ ] 4.2 候选人：创建（需求存在 / 关闭 `40192`）、编辑不改阶段
- [ ] 4.3 阶段：白名单内 / 外、终态 `40193`
- [ ] 4.4 转入职：`40193` / `40194` / `40145` / `40147`；字段正确；同事务
- [ ] 4.5 列表筛选与候选人数聚合；字段约束一致性单测
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/jobRequisition.ts` / `api/candidate.ts`（+ 三套 META 文案）
- [ ] 5.2 `views/RecruitManagement/JobRequisitionsView.vue`
- [ ] 5.3 `views/RecruitManagement/CandidatesView.vue`（阶段推进 + 转入职弹窗）
- [ ] 5.4 `router/index.ts` 新增 2 条路由；`AppLayout.vue`「人事」分组续行两项 + `MENU_ROUTE_MAP`
- [ ] 5.5 按钮接入 `auth.hasPermission` 与 loading（`010`）
- [ ] 5.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/recruit.spec.ts`：建需求 → 建候选人 → 阶段推进 → 转入职 → 员工档案出现
- [ ] 6.2 同文件：重复转入职 `40194`；关闭需求后新增候选人 `40192`；非法阶段 `40193`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含人事域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `jobRequisitions.*` / `candidates.*`；刷新 `updated`
- [ ] 7.2 `specs/025-erp-report/design.md` §0.2「人事」分组续行两项；刷新 `updated`
- [ ] 7.3 `specs/030-erp-org-employee/design.md` §0.2 增「招聘可转入职」指针；刷新 `updated`
- [ ] 7.4 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 读模型 / 仓储）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.5 `specs/ROADMAP.md` §4.2 状态更新（`056` → 已实现）；§4.2 / §5 把 `056` 目标改为「招聘」并**新增绩效候选（`059` 待评估）**；§3「人力域 · HCM」缺口同步；§6 / §6.7 续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + `npm run test:e2e` 全绿）。
- 招聘 → 候选人 → 阶段 → 转入职闭环可用；转入职与 `030` 唯一性一致。

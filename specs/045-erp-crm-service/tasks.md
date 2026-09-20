---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：CRM 服务工单（erp-crm-service）

> 依据 `specs/045-erp-crm-service/design.md` 拆分。含服务工单 + 状态流转 + 指派 + 权限 / 菜单续行（CRM 分组）+ e2e。
> 前置：`013`（客户）、`030`（员工）、`043`（CRM 分组已建）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `ServiceTicket` + 枚举 `TicketStatus` / `TicketPriority`；`AppDbContext` 追加 `ServiceTickets`
- [ ] 1.2 字段约束常量类 `ServiceTicketFieldConstraints`
- [ ] 1.3 EF 配置（唯一 `TicketNo`、FK、索引、快照列）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpCrmService -p src/App.Infrastructure -s src/App.Api`

## 二、后端：用例与接口

- [ ] 2.1 读模型 `ServiceTicketListItem` / `ServiceTicketDetail` + `IServiceTicketRepository`（`GetPagedAsync` / `GetByIdAsync` / `GenerateNoAsync` / `AddAsync` / `UpdateAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `ServiceTicketListItemDto` / `ServiceTicketDetailDto` + `ServiceTicketDtoMapper`
- [ ] 2.3 用例 `ServiceTickets/GetServiceTickets` / `CreateServiceTicket` / `GetServiceTicketById` / `UpdateServiceTicket`（`40172`）
- [ ] 2.4 用例 `ServiceTickets/UpdateServiceTicketStatus`（白名单流转 + `ResolvedAt` 记录 / 清空）/ `AssignServiceTicket`
- [ ] 2.5 `ServiceTicketsController`（6 端点）+ DI 注册

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40172`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40173`

## 四、单元测试（后端）

- [ ] 4.1 工单：客户 / 负责人存在性、已关闭编辑 `40172`
- [ ] 4.2 状态流转：白名单内通过、白名单外 `40172`、置解决记时间、重开清空
- [ ] 4.3 指派：已关闭 `40172`、负责人存在性
- [ ] 4.4 字段约束一致性单测
- [ ] 4.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 五、前端

- [ ] 5.1 `api/serviceTicket.ts`
- [ ] 5.2 `views/CrmManagement/ServiceTicketsView.vue`（按状态显示可用动作）
- [ ] 5.3 `views/CrmManagement/ServiceTicketFormPage.vue`
- [ ] 5.4 `views/CrmManagement/ServiceTicketDetailView.vue`（状态推进 + 指派）
- [ ] 5.5 `router/index.ts` 新增 3 条路由；`AppLayout.vue`「CRM」分组续行「服务工单」+ `MENU_ROUTE_MAP`
- [ ] 5.6 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 5.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/crm-service.spec.ts`：建工单 → 指派 → 受理 → 解决（记时间）→ 关闭
- [ ] 6.2 同文件：已关闭改状态 `40172`；已解决重开为处理中
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含 `crm-presale.spec.ts` 回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `serviceTickets.*`；刷新其 `updated`
- [ ] 7.2 `specs/025-erp-report/design.md` §0.2「CRM」分组续行「服务工单」
- [ ] 7.3 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 7.4 `specs/ROADMAP.md` §4.2 状态更新（`045` → 已实现）；§3 覆盖矩阵「外围 · CRM」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 状态流转白名单生效；工单不产生库存 / 资金影响；清单守卫通过（6 个新增端点合法标注权限点）。

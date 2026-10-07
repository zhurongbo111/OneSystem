---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：公文收发登记（oa-document）

> 依据 `specs/062-oa-document/design.md` 拆分；顺序即实现顺序。
> 前置：`027`（导出能力）、`028`（权限）、`029`（操作日志）、`030`（员工档案）均已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `e2e:run`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增枚举 `DocumentDirection` / `DocumentStatus`（`App.Core/Entities/`）
- [ ] 1.2 新增实体 `OfficialDocument` + `OfficialDocumentFieldConstraints`
- [ ] 1.3 EF 配置（表 + 唯一索引 `(DocumentNo)` + 列表索引 `(Direction, Status, DocumentDate DESC)`）
- [ ] 1.4 `AppDbContext` 增 `DbSet`
- [ ] 1.5 迁移 `dotnet ef migrations add AddOaOfficialDocument -p src/App.Infrastructure -s src/App.Api`（无种子）

## 二、后端：仓储与共享组件

- [ ] 2.1 `IOfficialDocumentRepository` + `OfficialDocumentRepository`（§3.1 全部方法，含 `GenerateNoAsync` / `ExistsNoAsync`）
- [ ] 2.2 读模型 `OfficialDocumentListItem` / `OfficialDocumentDetail`
- [ ] 2.3 `DocumentStatusRules`（流转白名单 + 编辑 / 删除判据单点）
- [ ] 2.4 单号生成复用 `015` §3.6 前缀参数化机制；`Infrastructure/DependencyInjection.cs` 注册仓储

## 三、后端：用例与端点

- [ ] 3.1 `Features/OfficialDocuments/GetOfficialDocuments`（四件套，方向 / 状态 / 日期范围 / 关键词）
- [ ] 3.2 `Features/OfficialDocuments/GetOfficialDocumentById`
- [ ] 3.3 `Features/OfficialDocuments/CreateOfficialDocument`（生成单号 + 唯一冲突兜底）
- [ ] 3.4 `Features/OfficialDocuments/UpdateOfficialDocument`（仅 `Registered`，`40205`；不含单号 / 方向）
- [ ] 3.5 `Features/OfficialDocuments/UpdateOfficialDocumentStatus`（白名单 `40206`；归档写 `ArchivedAt`）
- [ ] 3.6 `Features/OfficialDocuments/DeleteOfficialDocument`（仅 `Registered`，`40205`）
- [ ] 3.7 `Features/OfficialDocuments/ExportOfficialDocuments`（复用 `GetPagedAsync` 过滤）
- [ ] 3.8 `Errors/ErrorCode` 增 `40205` / `40206`；`Core/DependencyInjection.cs` 注册用例与校验器
- [ ] 3.9 `OfficialDocumentsController`（`/api/official-documents`，7 动作，全部 `[RequirePermission]`；`export` 固定段在前）
- [ ] 3.10 导出表格模型登记 `App.Core/Exports/`（列 = 列表列 + 备注）
- [ ] 3.11 全部写用例接入 `IAuditLogger`（`029` §0.1 续行；流转用 `StatusChange`）

## 四、单元测试（后端）

- [ ] 4.1 单号：按方向分别计数、跨日重置、`DocumentNo` 长度符合约束
- [ ] 4.2 状态：白名单放行、非法流转 `40206`、归档写 `ArchivedAt`
- [ ] 4.3 编辑 / 删除：非 `Registered` → `40205`
- [ ] 4.4 列表：方向 / 状态 / 日期范围 / 关键词（标题、单号、对方单位）命中
- [ ] 4.5 并发：单号冲突走 `OrderNoConflictException`
- [ ] 4.6 `FieldValidationConsistencyTests` 追加 `OfficialDocumentFieldConstraints`
- [ ] 4.7 `cd backend && dotnet build` / `dotnet test` 通过（含 `ApiPermissionMatrix` 守卫回归）

## 五、前端

- [ ] 5.1 `api/officialDocument.ts`（类型 + 方向 / 状态文案与颜色 + `DOCUMENT_STATUS_TRANSITIONS`）
- [ ] 5.2 `views/DocumentManagement/DocumentsView.vue`（列表 + 筛选 + 导出 + 按状态显隐操作列）
- [ ] 5.3 `views/DocumentManagement/DocumentDetailDrawer.vue` + `DocumentFormDrawer.vue`（新建 / 编辑共用，编辑态方向禁用）
- [ ] 5.4 `router/index.ts` 注册 `/documents` + `ROUTE_PERMISSIONS`；`AppLayout.vue` 「办公」分组追加「公文」
- [ ] 5.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/official-document.spec.ts`：登记收文 → 断言单号 `OD…` 格式
- [ ] 6.2 同文件：流转「已登记 → 办理中 → 已归档」；归档后编辑 / 删除不可用
- [ ] 6.3 同文件：状态筛选 + 导出下载成功
- [ ] 6.4 `cd frontend && npm run e2e:run` 全量通过

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `documents.*`（6 点）
- [ ] 7.2 `specs/029-erp-audit-log/design.md` §0.1 续行资源 `OfficialDocument`
- [ ] 7.3 `specs/027-erp-export/design.md` §0.1 导出范围表续行本域
- [ ] 7.4 `.codebuddy/CONTEXT.md` §2（实体 / 仓储 / 读模型 / 导出模型）/ §3（`api/officialDocument.ts`、前端域、菜单分组）同步
- [ ] 7.5 `specs/ROADMAP.md` §4.2 状态更新（`062` → 已实现）；§3 覆盖矩阵同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run e2e:run` 全绿）。
- 单号按方向正确生成；归档终态只读；导出可用。

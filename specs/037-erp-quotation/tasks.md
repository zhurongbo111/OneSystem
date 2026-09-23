---
created: 2026-09-20
updated: 2026-09-23
---

# 任务清单：报价单（erp-quotation）

> 依据 `specs/037-erp-quotation/design.md` 拆分。含报价单 CRUD + 转销售订单 + 取价 + 权限 / 菜单续行 + e2e。
> 前置：`012`（商品）、`013`（客户）、`024`（销售订单）、`036`（客户价，取价）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [x] 1.1 新增实体 `Quotation` / `QuotationItem` + 枚举 `QuotationStatus`；`AppDbContext` 追加 2 个 `DbSet`
- [x] 1.2 EF 配置 `QuotationConfiguration`（唯一 `QuotationNo`）/ `QuotationItemConfiguration`（FK + 快照列）；快照列长同源既有常量类（`OrderFieldConstraints` / `PartnerFieldConstraints` / `ProductFieldConstraints`），**不新建常量类**
- [x] 1.3 增量迁移 `dotnet ef migrations add AddErpQuotation -p src/App.Infrastructure -s src/App.Api`。实施说明：`036` 提交遗漏了 `AppDbContextModelSnapshot` 更新（快照停留在 `034`），生成的迁移曾重复包含 `PartnerPrices` 与 `Partners` 账期 / 额度列；已按最终态修正迁移（只建 2 张报价单表）并同步快照与 Designer，`dotnet ef migrations has-pending-model-changes` 确认为「无待生成变更」（该遗漏对既有 `036` 迁移无影响，仅影响后续迁移生成）

## 二、后端：用例与接口

- [x] 2.1 读模型 `QuotationListItem`（主表 + 明细行数）+ `IQuotationRepository`（`GetPagedAsync` / `GetDetailAsync` / `AddAsync` / `UpdateAsync` / `UpdateStatusAsync` / `GenerateNoAsync`）+ 实现 + 注册
- [x] 2.2 共享出参 `QuotationListItemDto` / `QuotationDetailDto` / `QuotationItemDto` / `ConvertQuotationResultDto` + `QuotationsDtoMapper`
- [x] 2.3 用例 `Quotations/GetQuotations` / `CreateQuotation` / `GetQuotationById` / `UpdateQuotation`（`40166`）/ `VoidQuotation`（`40166`）
- [x] 2.4 用例 `Quotations/ConvertToOrder`（同一事务：建销售订单 + 回写报价单；`40167`）
- [x] 2.5 `QuotationsController`（6 端点）+ DI 注册
- [x] 2.6 审计接入：4 个写路径经 `IAuditLogger` 记录（`AuditResource.Quotation` 新增 `= 28`，`AuditText.QuotationStatus` 新增文案），与业务同事务

## 三、后端：错误码

- [x] 3.1 `ErrorCode.cs` 追加 `40166` / `40167`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40168`

## 四、单元测试（后端）

- [x] 4.1 报价单用例：金额重算、客户 / 商品不存在 `40400`、明细为空 `40110`、非草稿编辑 `40166`、明细全量替换、审计写入
- [x] 4.2 转单：成功（订单 + 报价单回写同一事务、明细一致、调用序列 `Begin → Generate → Add → UpdateStatus → Commit`）、非草稿 / 已转 `40167`、不触碰库存
- [x] 4.3 取价：前端传入单价落库（不二次取价）
- [x] 4.4 字段约束一致性单测（单号列长、明细快照列长、数量 / 单价边界、有效期边界、行数上限、关键词长度、状态取值、日期区间）
- [x] 4.5 `cd backend && dotnet build` / `dotnet test` 通过（1013 通过 / 0 失败，含既有用例回归）

## 五、前端

- [x] 5.1 `api/quotation.ts`（6 接口 + 类型 + 日期工具）；`utils/quotation.ts`（状态文案 / 颜色 / 选项 / 已过期判据，§0.1 唯一来源）
- [x] 5.2 `views/QuotationManagement/QuotationsView.vue`（筛选 + 列设置 + 行内查看 / 编辑 / 转订单 / 作废）
- [x] 5.3 `views/QuotationManagement/QuotationFormPage.vue`（明细子表 + 选客户批量取价 + 来源标注）
- [x] 5.4 `views/QuotationManagement/QuotationDetailView.vue`（转换信息 + 转单 / 编辑 / 作废）
- [x] 5.5 `router/index.ts` 新增 4 条路由（列表 / 新建 / 编辑 / 详情，编辑与新建共用表单页）；`AppLayout.vue`「销售」分组续行「报价单」（置于「销售订单」之前）+ `MENU_ROUTE_MAP` / `MENU_GROUPS` / `MENU_PERMISSIONS`；`e2e/helpers/menu.ts` 映射同步
- [x] 5.6 按钮接入 `v-if="auth.hasPermission(...)"`（新建 `quotations.create`；行内与详情页编辑 / 转单 / 作废分别按 `quotations.update` / `convert` / `void`）
- [x] 5.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [x] 6.1 新增 `e2e/quotation.spec.ts`：建报价单（带出协议价并标注「协议价」）→ 编辑草稿（金额随之重算）→ 转销售订单 → 报价单「已转订单」、转单信息可跳销售订单且金额 / 状态一致
- [x] 6.2 同文件：已转订单再作废 / 再转 `40166` / `40167`（接口断言）；锁定态前端无编辑 / 作废 / 转单入口；报价单不影响库存与应收（库存不变、客户应收 0）
- [x] 6.3 `cd frontend && npm run e2e:run` 全量通过（含 `sale-order` / `partner-price` 回归）

## 七、规格与上下文联动

- [x] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `quotations.*`（含 `convert`）
- [x] 7.2 `specs/025-erp-report/design.md` §0.2「销售」分组续行「报价单」并刷新 `updated`
- [x] 7.3 `specs/024-erp-order-flow/design.md` 加「演进（erp-quotation）」注记（订单可来自报价转单）并刷新三件套 `updated`
- [x] 7.4 `specs/ROADMAP.md` §6.7 单号前缀表续行「报价单 `QT`」；§6 顶部错误码占用补 `40166`–`40167`（下一个可用 `40168`）
- [x] 7.5 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [x] 7.6 `specs/ROADMAP.md` §4.2 状态更新（`037` → 已实现）；§3 覆盖矩阵「销售」/「CRM」行同步；§5 P3 状态与沿革同步
- [x] 7.7 `specs/029-erp-audit-log/design.md` §0.1 续行 `Quotation`（创建 / 更新（含转单）/ 作废）并刷新 `updated`

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 转单原子（报价单回写与订单落库同一事务）；报价单不产生库存 / 资金影响。
- 清单守卫通过：6 个新增端点均标注合法权限点。

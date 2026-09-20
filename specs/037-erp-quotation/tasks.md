---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：报价单（erp-quotation）

> 依据 `specs/037-erp-quotation/design.md` 拆分。含报价单 CRUD + 转销售订单 + 取价 + 权限 / 菜单续行 + e2e。
> 前置：`012`（商品）、`013`（客户）、`024`（销售订单）、`036`（客户价，取价）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Quotation` / `QuotationItem` + 枚举 `QuotationStatus`；`AppDbContext` 追加 2 个 `DbSet`
- [ ] 1.2 EF 配置 `QuotationConfiguration`（唯一 `QuotationNo`）/ `QuotationItemConfiguration`（FK + 快照列）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpQuotation -p src/App.Infrastructure -s src/App.Api`

## 二、后端：用例与接口

- [ ] 2.1 读模型 `QuotationListItem` / `QuotationDetail` + `IQuotationRepository`（`GetPagedAsync` / `GetDetailAsync` / `AddAsync` / `UpdateAsync` / `UpdateStatusAsync` / `GenerateNoAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `QuotationListItemDto` / `QuotationDetailDto` / `QuotationItemDto` + `QuotationDtoMapper`
- [ ] 2.3 用例 `Quotations/GetQuotations` / `CreateQuotation` / `GetQuotationById` / `UpdateQuotation`（`40166`）/ `VoidQuotation`（`40166`）
- [ ] 2.4 用例 `Quotations/ConvertToOrder`（同一事务：建销售订单 + 回写报价单；`40167`）
- [ ] 2.5 `QuotationsController`（6 端点）+ DI 注册

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40166` / `40167`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40168`

## 四、单元测试（后端）

- [ ] 4.1 报价单用例：金额重算、客户 / 商品不存在 `40400`、非草稿编辑 / 作废 `40166`、明细全量替换
- [ ] 4.2 转单：成功（订单 + 报价单回写同一事务、明细一致）、非草稿 / 已转 `40167`、不触碰库存
- [ ] 4.3 取价：前端传入单价落库（不二次取价）
- [ ] 4.4 字段约束一致性单测（单号列长、数量 / 单价边界）
- [ ] 4.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归）

## 五、前端

- [ ] 5.1 `api/quotation.ts`
- [ ] 5.2 `views/QuotationManagement/QuotationsView.vue`
- [ ] 5.3 `views/QuotationManagement/QuotationFormPage.vue`（明细子表 + 选客户批量取价 + 来源标注）
- [ ] 5.4 `views/QuotationManagement/QuotationDetailView.vue`（转换信息 + 作废 / 转单）
- [ ] 5.5 `router/index.ts` 新增 3 条路由；`AppLayout.vue`「销售」分组续行「报价单」+ `MENU_ROUTE_MAP`
- [ ] 5.6 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 5.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/quotation.spec.ts`：建报价单（带出协议价并标注）→ 编辑草稿 → 转销售订单 → 报价单「已转订单」、订单出现
- [ ] 6.2 同文件：已转订单再作废 / 再转 `40166` / `40167`；报价单不影响库存与应收
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含 `sale-order` / `partner-price` 回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `quotations.*`；刷新其 `updated`
- [ ] 7.2 `specs/025-erp-report/design.md` §0.2「销售」分组续行「报价单」
- [ ] 7.3 `specs/024-erp-order-flow/design.md` 加「演进（erp-quotation）」注记（订单可来自报价转单）
- [ ] 7.4 `specs/ROADMAP.md` §6.7 单号前缀表续行「报价单 `QT`」
- [ ] 7.5 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 7.6 `specs/ROADMAP.md` §4.2 状态更新（`037` → 已实现）；§3 覆盖矩阵「销售」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 转单原子（报价单回写与订单落库同一事务）；报价单不产生库存 / 资金影响。
- 清单守卫通过：6 个新增端点均标注合法权限点。

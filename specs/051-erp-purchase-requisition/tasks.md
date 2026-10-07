---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：采购请购单（erp-purchase-requisition）

> 依据 `specs/051-erp-purchase-requisition/design.md` 拆分。新增请购单（三态）+ 转采购订单 + 权限 / 菜单续行 + 前端 + e2e。
> 前置：`024`（采购订单）、`012`（商品）、`030`（员工）、`037`（转单范式参照）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `PurchaseRequisition` / `PurchaseRequisitionItem` + 枚举 `RequisitionStatus`；`AppDbContext` 追加两 DbSet
- [ ] 1.2 EF 配置（`RequisitionNo` 唯一、FK、索引、快照列）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpPurchaseRequisition -p src/App.Infrastructure -s src/App.Api`

## 二、后端：仓储与用例

- [ ] 2.1 读模型 `PurchaseRequisitionListItem` / `PurchaseRequisitionDetail` + `IPurchaseRequisitionRepository`（分页 / 详情 / 增改（明细全量替换）/ 单号 / `MarkConvertedAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 DTO + `PurchaseRequisitionsDtoMapper`
- [ ] 2.3 用例 `CreatePurchaseRequisition` / `GetPurchaseRequisitions` / `GetPurchaseRequisitionById` / `UpdatePurchaseRequisition`（`40183`）/ `VoidPurchaseRequisition`（`40183`）
- [ ] 2.4 用例 `ConvertPurchaseRequisition`（同事务建 `PO` + 回写；`40184`）
- [ ] 2.5 `PurchaseRequisitionsController`（6 端点）+ DI；写用例接入操作日志（`029` §0.1 续行）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40183` / `40184`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40185`；§6.7 单号前缀续行 `RQ`

## 四、单元测试（后端）

- [ ] 4.1 创建：金额后端计算、申请人 / 商品存在性
- [ ] 4.2 编辑 / 作废：草稿可改、非草稿 `40183`
- [ ] 4.3 转单：同事务建单 + 回写、`40184`、明细 / 金额复制、订单 `Pending`
- [ ] 4.4 列表：筛选与明细行数
- [ ] 4.5 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/purchaseRequisition.ts`（+ `REQUISITION_STATUS_META`）
- [ ] 5.2 `views/PurchaseRequisitionManagement/PurchaseRequisitionsView.vue`
- [ ] 5.3 `PurchaseRequisitionFormPage.vue`（新建 / 编辑共用，明细子表 + 商品 pick）
- [ ] 5.4 `PurchaseRequisitionDetailView.vue`（转采购订单 / 作废）
- [ ] 5.5 `router/index.ts` 新增 3 条路由；`AppLayout.vue`「采购」分组首位续行 + `MENU_ROUTE_MAP`
- [ ] 5.6 按钮接入 `auth.hasPermission` 与 loading（`010`）
- [ ] 5.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/purchase-requisition.spec.ts`：建请购 → 转采购订单 → 采购订单列表可见
- [ ] 6.2 同文件：非草稿编辑 / 再转 / 作废 → `40183` / `40184`
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含采购域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/028-erp-rbac/design.md` §0.2 续行 `purchaseRequisitions.*`；刷新 `updated`
- [ ] 7.2 `specs/025-erp-report/design.md` §0.2「采购」分组续行首位；刷新 `updated`
- [ ] 7.3 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 读模型 / 仓储）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.4 `specs/ROADMAP.md` §4.2 状态更新（`051` → 已实现）；§3 覆盖矩阵「L3 采购」缺口同步；§6 / §6.7 续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 请购 → 转订单闭环可用；请购不产生库存 / 资金影响。

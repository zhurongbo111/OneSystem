---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：固定资产（erp-fixed-asset）

> 依据 `specs/053-erp-fixed-asset/design.md` 拆分。新增资产台账 + 月度折旧计提（自动凭证）+ 处置 + 科目 / 映射续行 + 前端 + e2e。
> 前置：`033`（凭证通道 / 映射）、`031`（科目 / 预置科目）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与种子

- [ ] 1.1 实体 `FixedAsset` + 枚举 `FixedAssetStatus` + `FixedAssetFieldConstraints`；`AppDbContext` 追加 `FixedAssets`
- [ ] 1.2 EF 配置（`AssetNo` 唯一、FK `ExpenseAccountId`、列类型）
- [ ] 1.3 `VoucherSourceType` 续行 `Depreciation`
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpFixedAsset -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.5 `DatabaseInitializer` 幂等追加预置科目 `1601` / `1602` / `6602` 与映射键 `FixedAsset` / `AccumDepreciation` / `DepreciationExpense`

## 二、后端：仓储与用例

- [ ] 2.1 读模型 `FixedAssetListItem` / `FixedAssetDetail` + `IFixedAssetRepository`（分页 / 详情 / 增改 / 单号 / `GetDepreciableAsync` / `UpdateDepreciationAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 DTO + `FixedAssetsDtoMapper`
- [ ] 2.3 用例 `CreateFixedAsset` / `GetFixedAssets` / `GetFixedAssetById` / `UpdateFixedAsset`（`40190`）
- [ ] 2.4 用例 `DepreciateFixedAssets`（§0.1 计算 + 汇总凭证 + 逐资产回写；`40188` / `40189` / `40154` / `40158`）
- [ ] 2.5 用例 `DisposeFixedAsset`（`40190`）
- [ ] 2.6 `FixedAssetsController`（6 端点）+ DI；写用例接入操作日志（`029` §0.1 续行，资源 `FixedAsset`）

## 三、后端：错误码

- [ ] 3.1 `ErrorCode.cs` 追加 `40188`–`40190`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40191`

## 四、单元测试（后端）

- [ ] 4.1 折旧计算：四舍五入、末月补差、上限、次月起提
- [ ] 4.2 计提：凭证 + 回写；`40188` / `40189`；`40154` 回滚；`40158`
- [ ] 4.3 处置：不再计提；重复处置 / 已处置编辑 `40190`
- [ ] 4.4 列表 / 详情净值推导
- [ ] 4.5 字段约束一致性单测
- [ ] 4.6 `cd backend && dotnet build` / `dotnet test` 通过

## 五、前端

- [ ] 5.1 `api/fixedAsset.ts`（+ `FIXED_ASSET_STATUS_META`）
- [ ] 5.2 `views/FixedAssetManagement/FixedAssetsView.vue`（列表 + 计提折旧工具条 + 期间选择）
- [ ] 5.3 `FixedAssetFormDrawer.vue`（登记 / 编辑 / 处置）
- [ ] 5.4 `router/index.ts` 新增路由；`AppLayout.vue`「财务」分组续行「固定资产」+ `MENU_ROUTE_MAP`
- [ ] 5.5 按钮接入 `auth.hasPermission` 与 loading（`010`）
- [ ] 5.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/fixed-asset.spec.ts`：建资产 → 计提 → 累计折旧 / 净值 / 凭证正确
- [ ] 6.2 同文件：重复计提 `40188`；处置后不再计提
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含总账域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/033-erp-general-ledger/design.md` §0.1（`VoucherSourceType` 续行）/ §0.3（三映射键续行）；刷新 `updated`
- [ ] 7.2 `specs/031-erp-finance-master/design.md` §2.4 预置科目续行；刷新 `updated`
- [ ] 7.3 `specs/028-erp-rbac/design.md` §0.2 续行 `fixedAssets.*`；刷新 `updated`
- [ ] 7.4 `specs/025-erp-report/design.md` §0.2「财务」分组续行；刷新 `updated`
- [ ] 7.5 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 读模型 / 仓储）/ §3（新增域 / 菜单 / api）同步
- [ ] 7.6 `specs/ROADMAP.md` §4.2 状态更新（`053` → 已实现）；§3 / §7 同步；§6 错误码续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 资产 → 折旧 → 凭证闭环可用；折旧口径与 §0.1 一致。

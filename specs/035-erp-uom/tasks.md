---
created: 2026-09-20
updated: 2026-09-20
---

# 任务清单：计量单位（erp-uom）

> 依据 `specs/035-erp-uom/design.md` 拆分。含单位字典 + 商品单位引用改造（`012`）+ 权限 / 菜单续行 + e2e。
> 前置：`012`（商品）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增实体 `Unit` + 枚举 `UnitStatus`；`AppDbContext` 追加 `Units`
- [ ] 1.2 字段约束常量类 `UnitFieldConstraints`；`ProductFieldConstraints.UnitMaxLength` 由 10 调为 20
- [ ] 1.3 EF 配置 `UnitConfiguration`（唯一 `Code` / `Name`）；`ProductConfiguration` 增 `UnitId` FK + `Unit` 列长
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpUom -p src/App.Infrastructure -s src/App.Api`（建表 + `Products.UnitId` + **数据回填** + 置 NOT NULL）

## 二、后端：单位域用例与接口

- [ ] 2.1 读模型 `UnitListItem` / `UnitPickItem` + `IUnitRepository`（`GetPagedAsync` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `ReferencedByProductsAsync` / `GetPickListAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 实现 + 注册
- [ ] 2.2 共享出参 `UnitListItemDto` / `UnitDetailDto` / `UnitPickDto` + `UnitDtoMapper`
- [ ] 2.3 用例 `Units/GetUnits` / `CreateUnit` / `GetUnitById` / `UpdateUnit` / `DeleteUnit`（`40165`）/ `UpdateUnitStatus` / `GetUnitPicks`
- [ ] 2.4 `UnitsController`（7 端点）+ DI 注册

## 三、后端：商品单位改造（`012`）

- [ ] 3.1 `Product` 增 `UnitId`；`CreateProductRequest` / `UpdateProductRequest` 的 `unit` → `unitId`
- [ ] 3.2 `CreateProduct` / `UpdateProduct` Handler：校验 `unitId` 存在（`40400`）+ 填 `Unit` 快照
- [ ] 3.3 商品列表 / 详情出参维持 `unit` 名称（可加 `unitId`）；`IProductRepository` 适配

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40163`–`40165`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40166`

## 五、单元测试（后端）

- [ ] 5.1 单位用例：编码 / 名称唯一、被引用禁删、筛选分页、picks 仅启用
- [ ] 5.2 商品改造：`unitId` 存在性与快照填写；出参单位为名称
- [ ] 5.3 迁移回填（集成）：文本去重生成单位 + 回填
- [ ] 5.4 字段约束一致性单测（含 `UnitMaxLength = 20`）
- [ ] 5.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例回归，含商品 / 单据链路）

## 六、前端

- [ ] 6.1 `api/unit.ts`
- [ ] 6.2 `views/UnitManagement/UnitsView.vue` + `UnitFormDrawer.vue`
- [ ] 6.3 `views/ProductManagement/ProductFormDrawer.vue` 单位改 `a-select`（`getUnitPicks`）
- [ ] 6.4 `router/index.ts` 新增 `units`；`AppLayout.vue`「基础档案」分组续行「计量单位」+ `MENU_ROUTE_MAP`
- [ ] 6.5 按钮接入 `v-if="auth.hasPermission(...)"`
- [ ] 6.6 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/unit.spec.ts`：建单位 → 编码 / 名称重复 `40163` / `40164` → 筛选 / 编辑 / 停用
- [ ] 7.2 同文件：商品表单选单位 → 删除被引用单位 `40165`
- [ ] 7.3 `cd frontend && npm run test:e2e` 全量通过（含 `product.spec.ts` 回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/028-erp-rbac/design.md` §0.2 续行 `units.*`；刷新其 `updated`
- [ ] 8.2 `specs/025-erp-report/design.md` §0.2「基础档案」分组续行「计量单位」
- [ ] 8.3 `specs/012-erp-product/design.md` 加「演进（erp-uom）」注记（`Unit` → `UnitId` + 快照）
- [ ] 8.4 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [ ] 8.5 `specs/ROADMAP.md` §4.2 状态更新（`035` → 已实现）；§3 覆盖矩阵「物料 / 往来 / 分类」行同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 迁移回填无数据丢失；清单守卫通过（7 个新增端点合法标注权限点）。

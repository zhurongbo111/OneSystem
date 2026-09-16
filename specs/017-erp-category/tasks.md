---
created: 2026-09-15
updated: 2026-09-16
---

# 任务清单：商品分类管理页（erp-category）

> 依据 `specs/017-erp-category/design.md` 拆分。含后端新增分页接口 + 前端搜索分页 + 编辑抽屉。按顺序实现，完成后勾选。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 分支：`feature/erp-inventory`（与 erp-product 同分支）。
> 提交 / 合并不列入待办：由用户主动发起指示（2026-09-13 用户约定）。

## 一、后端（分页查询接口）

- [x] 1.1 `CategoryFieldConstraints` 新增 `KeywordMaxLength`（= `NameMaxLength`，即 20，对齐被查询的 `Name` 列长）
- [x] 1.2 `ICategoryRepository` / `CategoryRepository` 新增 `GetPagedAsync(keyword, page, pageSize)`
- [x] 1.3 新增 `GetCategoriesPaged` 用例（Request + Validator + Handler）
- [x] 1.4 `DependencyInjection.cs` 注册 `GetCategoriesPagedRequestHandler`
- [x] 1.5 `CategoriesController` 新增 `GET /api/categories/paged`（`FromQuery` page / pageSize / keyword）
- [x] 1.6 `CategoryRequestHandlerTests` 新增分页 / 模糊匹配用例

## 二、前端 API

- [x] 2.1 `src/api/product.ts` 新增 `CategoryListQuery` 与 `getCategoriesPaged`

## 三、前端页面（搜索 + 分页 + 编辑抽屉）

- [x] 3.1 `CategoryFormDrawer.vue`（新增）：新增 / 编辑抽屉（名称 1-20，`mode` 区分，`editName` 回填，`submitting` 防重入）
- [x] 3.2 `CategoriesView.vue`（改造）：筛选行（关键词 + 搜索 / 重置）+ 操作行（新增 / 刷新）+ 表格（序号跨页 / 名称 / 创建时间 / 操作编辑·删除）+ 服务端分页 + `fetchSeq`
- [x] 3.3 `CategoriesView.vue`：接入 `CategoryFormDrawer`（新增 / 编辑），删除行内编辑（`editingId` / `editingName` / `onStartEdit` / `onCancelEdit` / `onEditSave`）与工具条行内新增输入

## 四、E2E（Playwright）

- [x] 4.1 `category-management.spec.ts` 重写：菜单进入渲染 / 抽屉新增 + 重名 40105 / 抽屉编辑 + 重名 / 删除 + 有引用 40106 / 搜索命中与重置 / 分页翻页与序号跨页

## 五、质量

- [x] 5.1 `cd backend && dotnet build` / `dotnet test` 通过
- [x] 5.2 `cd frontend && npm run type-check` / `lint` / `build` 全绿
- [ ] 5.3 `cd frontend && npm run test:e2e` 全绿（含分类管理新用例 + 商品管理回归）
  - 本轮未复跑：`playwright.config.ts` 不自动拉起服务，需先人工启动 dev 前后端（5080 / 5173）；本轮验证止于 5.2。

## 变更：规格与实现对齐（2026-09-16）

核对本清单时发现并处理：

- **1.1 补做**：`CategoryFieldConstraints` 此前未按 `design.md` §2.3 声明 `KeywordMaxLength`，`GetCategoriesPagedRequestValidator` 直接引用 `NameMaxLength`（20）——取值正确但缺常量声明 → 已补 `KeywordMaxLength`（= `NameMaxLength`）并改引用（后端规则 §5.3 字段约束单一来源、禁止硬编码）。
- **纠正取值**：`design.md` / 本清单原写 `KeywordMaxLength = 50`（理由是"对齐既有域 50"）属误读。各域约定是**对齐被查询列长**：商品搜 `Code`(32) / `Name`(50) 取 50，往来单位搜 `Name`(50) 取 50，单据搜 `OrderNo`(20) 取 20；分类只搜 `Name`（`varchar(20)`）故取 **20**。规格、常量与一致性测试已按此纠正，并把该约定写明到后端规则 §5.3（防再次误读为照抄他域数值）。
- 新增 `tests/App.Tests/CategoryFieldConsistencyTests.cs`：EF 列长 == 常量、名称长度在新增 / 编辑两处一致、关键词按 `KeywordMaxLength` 边界校验（后端规则 §5.3 强制一致性单测）。
- `design.md` §3 同步：分类已从 `ProductManagement/` 拆为独立域 `CategoryManagement/`（前端规则 §4.1 四者对齐）。
- `design.md` §5.1：操作列删除图标 `IconDelete` → `IconTrash`（Tabler，前端规则 §4.7）。
- `design.md` §2.3 / §6：常量说明与 e2e 分页描述改为与实现一致。
- 关联修复：`UsersController.Me` 补 `[ProducesResponseType(401)]`（`specs/003-api-swagger/design.md` §2.3 已要求，此前漏实现），恢复 `SwaggerJson_dev环境_受保护接口标注401` 用例通过；后端测试由 221 通过 / 1 失败变为 **225 通过 / 0 失败**。

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过。

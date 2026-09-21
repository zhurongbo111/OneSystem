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

- [x] 1.1 `CategoryFieldConstraints` 新增 `KeywordMaxLength`（= `NameMaxLength`，即 20）；`GetCategoriesPagedRequestValidator` 改为引用该常量，禁止硬编码（后端规则 §5.3 字段约束单一来源）
- [x] 1.2 `ICategoryRepository` / `CategoryRepository` 新增 `GetPagedAsync(keyword, page, pageSize)`
- [x] 1.3 新增 `GetCategoriesPaged` 用例（Request + Validator + Handler）
- [x] 1.4 `DependencyInjection.cs` 注册 `GetCategoriesPagedRequestHandler`
- [x] 1.5 `CategoriesController` 新增 `GET /api/categories/paged`（`FromQuery` page / pageSize / keyword）
- [x] 1.6 `CategoryRequestHandlerTests` 新增分页 / 模糊匹配用例
- [x] 1.7 新增 `tests/App.Tests/CategoryFieldConsistencyTests.cs`：EF 列长 == 常量、名称长度在新增 / 编辑两处一致、关键词按 `KeywordMaxLength` 边界校验
- [x] 1.8 关联修复：`UsersController.Me` 补 `[ProducesResponseType(401)]`（`specs/003-api-swagger/design.md` §2.3 已要求）

## 二、前端 API

- [x] 2.1 `src/api/product.ts` 新增 `CategoryListQuery` 与 `getCategoriesPaged`

## 三、前端页面（搜索 + 分页 + 编辑抽屉）

- [x] 3.1 `CategoryFormDrawer.vue`（新增）：新增 / 编辑抽屉（名称 1-20，`mode` 区分，`editName` 回填，`submitting` 防重入）
- [x] 3.2 `CategoriesView.vue`（改造）：筛选行（关键词 + 搜索 / 重置）+ 操作行（新增 / 刷新）+ 表格（序号跨页 / 名称 / 创建时间 / 操作编辑·删除，删除图标 Tabler `IconTrash`）+ 服务端分页 + `fetchSeq`
- [x] 3.3 `CategoriesView.vue`：接入 `CategoryFormDrawer`（新增 / 编辑），删除行内编辑（`editingId` / `editingName` / `onStartEdit` / `onCancelEdit` / `onEditSave`）与工具条行内新增输入
- [x] 3.4 分类从商品域拆为独立域目录 `CategoryManagement/`（前端规则 §4.1 四者对齐），删除 `CategoryManagerModal.vue`（入口统一走侧边菜单）

## 四、E2E（Playwright）

- [x] 4.1 `category.spec.ts` 重写：菜单进入渲染 / 抽屉新增 + 重名 40105 / 抽屉编辑 + 重名 / 删除 + 有引用 40106 / 搜索命中与重置 / 分页翻页与序号跨页

## 五、质量与规格同步

- [x] 5.1 `cd backend && dotnet build` / `dotnet test` 通过（含新增一致性用例与 `Me` 端点 401 断言）
- [x] 5.2 `cd frontend && npm run type-check` / `lint` / `build` 全绿
- [x] 5.3 `cd frontend && npm run test:e2e` 全绿（含分类管理新用例 + 商品管理回归）
- [x] 5.4 `design.md` 同步：§2.3 常量说明（关键词上限对齐被查询列长）、§3 分类独立域、§5.1 删除图标 `IconTrash`、§6 e2e 分页描述，均与实现一致

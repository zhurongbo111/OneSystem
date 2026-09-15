# 任务清单：商品分类管理页（erp-category）

> 依据 `specs/erp-category/design.md` 拆分。含后端新增分页接口 + 前端搜索分页 + 编辑抽屉。按顺序实现，完成后勾选。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 分支：`feature/erp-inventory`（与 erp-product 同分支）。
> 提交 / 合并不列入待办：由用户主动发起指示（2026-09-13 用户约定）。

## 一、后端（分页查询接口）

- [ ] 1.1 `CategoryFieldConstraints` 新增 `KeywordMaxLength = 50`
- [ ] 1.2 `ICategoryRepository` / `CategoryRepository` 新增 `GetPagedAsync(keyword, page, pageSize)`
- [ ] 1.3 新增 `GetCategoriesPaged` 用例（Request + Validator + Handler）
- [ ] 1.4 `DependencyInjection.cs` 注册 `GetCategoriesPagedRequestHandler`
- [ ] 1.5 `CategoriesController` 新增 `GET /api/categories/paged`（`FromQuery` page / pageSize / keyword）
- [ ] 1.6 `CategoryRequestHandlerTests` 新增分页 / 模糊匹配用例

## 二、前端 API

- [ ] 2.1 `src/api/product.ts` 新增 `CategoryListQuery` 与 `getCategoriesPaged`

## 三、前端页面（搜索 + 分页 + 编辑抽屉）

- [ ] 3.1 `CategoryFormDrawer.vue`（新增）：新增 / 编辑抽屉（名称 1-20，`mode` 区分，`editName` 回填，`submitting` 防重入）
- [ ] 3.2 `CategoriesView.vue`（改造）：筛选行（关键词 + 搜索 / 重置）+ 操作行（新增 / 刷新）+ 表格（序号跨页 / 名称 / 创建时间 / 操作编辑·删除）+ 服务端分页 + `fetchSeq`
- [ ] 3.3 `CategoriesView.vue`：接入 `CategoryFormDrawer`（新增 / 编辑），删除行内编辑（`editingId` / `editingName` / `onStartEdit` / `onCancelEdit` / `onEditSave`）与工具条行内新增输入

## 四、E2E（Playwright）

- [ ] 4.1 `category-management.spec.ts` 重写：菜单进入渲染 / 抽屉新增 + 重名 40105 / 抽屉编辑 + 重名 / 删除 + 有引用 40106 / 搜索命中与重置 / 分页翻页与序号跨页

## 五、质量

- [ ] 5.1 `cd backend && dotnet build` / `dotnet test` 通过
- [ ] 5.2 `cd frontend && npm run type-check` / `lint` / `build` 全绿
- [ ] 5.3 `cd frontend && npm run test:e2e` 全绿（含分类管理新用例 + 商品管理回归）

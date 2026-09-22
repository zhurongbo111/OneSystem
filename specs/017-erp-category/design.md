---
created: 2026-09-15
updated: 2026-09-22
---

# 设计规格：商品分类管理页（erp-category）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、认证 §4.6、测试 §6）、前端专项规则与后端专项规则。
> 后端新增分页查询接口（搜索 + 分页），前端 `CategoriesView` 升级为「搜索 + 分页」标准列表页，编辑改为抽屉（`CategoryFormDrawer`）。
> 列表页结构参照 `src/views/ProductManagement/ProductsView.vue`（同目录，搜索 + 分页模式）；按钮 loading 遵循前端规则 §4.6；分页契约遵循 `AGENTS.md` §4.3。
> **演进（erp-rbac）**：本域动作接入权限校验，权限点 `categories.view` / `create` / `update` / `delete`；菜单可见性与列表页操作按钮由前端按权限过滤。清单唯一来源见 `specs/028-erp-rbac/design.md` §0.2。

## 1. 总体设计

```
分类管理页（/categories）
  → CategoriesView.vue
    → CategoryFormDrawer.vue（新增 / 编辑抽屉）
    → src/api/product.ts（getCategoriesPaged / createCategory / updateCategory / deleteCategory）
      → GET /api/categories/paged（新增，分页 + 搜索）
      → POST/PUT/DELETE /api/categories(/{id})（既有，erp-product 交付）

  既有全量接口（保留，下拉数据源，不变）：
    ProductsView.vue / ProductFormDrawer.vue → getCategories() → GET /api/categories
```

## 2. 后端设计（App.Core / App.Api）

### 2.1 新增用例 `GetCategoriesPaged`

目录：`src/App.Core/Features/Categories/GetCategoriesPaged/`（参照 `GetProducts` 用例结构）。

- `GetCategoriesPagedRequest.cs`：`IRequest<PagedResult<CategoryDto>>`
  - `int Page { get; init; } = 1;`
  - `int PageSize { get; init; } = 20;`
  - `string? Keyword { get; init; }`（分类名称模糊匹配，可空）
- `GetCategoriesPagedRequestValidator.cs`：
  - `Page >= 1`；`PageSize` 1~100。
  - `Keyword` `MaximumLength` 取 `CategoryFieldConstraints.KeywordMaxLength`（**对齐被查询的 `Name` 列长，即 20**；查询只按 `Name` 模糊匹配，跨域不照抄他域数值），非空时生效。
- `GetCategoriesPagedRequestHandler.cs`：调 `_categoryRepository.GetPagedAsync(keyword, page, pageSize, ct)` 返回 `(items, total)`，映射 `CategoryDtoMapper.ToCategoryDto`，组装 `PagedResult<CategoryDto>`。

### 2.2 仓储 `ICategoryRepository` / `CategoryRepository`

新增方法：

```
Task<(IReadOnlyList<Category> Items, int Total)> GetPagedAsync(
    string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
```

- 实现参照 `ProductRepository.GetPagedAsync`：`AsNoTracking`，`keyword` 非空时 `Name.ToLower().Contains(keyword.Trim().ToLowerInvariant())`，`OrderBy(CreatedAt)` 正序（与既有全量列表一致），`CountAsync` + `Skip/Take`，`ToListAsync` 返回 `(items, total)`。

### 2.3 常量 `CategoryFieldConstraints`

新增 `public const int KeywordMaxLength = 20;`（=`NameMaxLength`）；查询只按 `Name` 模糊匹配，关键词上限必须**对齐被查询列长**——超过列长不可能命中（后端规则 §5.3）。

### 2.4 Controller `CategoriesController`

新增（置于 `GET /` 与 `POST` 之间）：

```
[HttpGet("paged")]
ApiResponse<PagedResult<CategoryDto>> GetPaged(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? keyword = null,
    CancellationToken cancellationToken = default)
    => ApiResponseFactory.Ok(mediator.Send(new GetCategoriesPagedRequest { Page = page, PageSize = pageSize, Keyword = keyword }, ct));
```

- `ProducesResponseType` 200 `PagedResult<CategoryDto>` + 401。
- 既有 `GET /`（全量）保持不变，注释补充「下拉数据源」用途说明。

### 2.5 DI 注册 `App.Core/DependencyInjection.cs`

商品分类用例区追加：
`services.AddScoped<IRequestHandler<GetCategoriesPagedRequest, PagedResult<CategoryDto>>, GetCategoriesPagedRequestHandler>();`（需 `using App.Core.Features.Categories.GetCategoriesPaged;` 与 `using App.Core.Responses;`，后者已在文件中）。

### 2.6 单测 `CategoryRequestHandlerTests.cs`

新增用例（参照既有 `列表_应按创建时间正序`）：
- `分页查询_关键词模糊匹配_应命中`：种多个分类，`Keyword` 命中子串，断言仅命中项返回。
- `分页查询_分页切片_应正确`：种 3 条，`PageSize=2`，断言第 1 页 2 条、`Total=3`、正序；第 2 页 1 条。
- `分页查询_无关键词_应全量正序`：`Keyword` 空，断言 `Total` 与正序。

## 3. 目录结构（前端）

```
src/views/CategoryManagement/       # 分类独立功能域（对齐 Features/Categories、/categories、category.spec.ts）
├── CategoriesView.vue              # 分类管理页（搜索 + 分页 + 操作列编辑/删除）
└── CategoryFormDrawer.vue          # 分类新增/编辑抽屉（新增）

src/views/ProductManagement/        # 商品域（不变）
├── ProductsView.vue                # 商品列表页（入口按钮跳转）
└── ProductFormDrawer.vue           # 商品新增/编辑/查看抽屉（就地新建分类行内化）
```

> 分类已从商品域拆为独立域目录（前端规则 §4.1 四者对齐；本期实现时曾位于 `ProductManagement/`），`CategoryManagerModal.vue` 已删除。

## 4. 前端 API（`src/api/product.ts`）

**接口归属**：分类接口并入商品域文件（分类是商品主数据、与商品域强耦合——商品列表筛选、商品抽屉就地新建、库存筛选三处共用）；归属判定流程见前端规则 §3。

新增类型与函数：

新增 `CategoryListQuery`（`keyword?: string` / `page: number` / `pageSize: number`，对应后端 `GetCategoriesPagedRequest`）与 `getCategoriesPaged(query)`（搜索 + 分页、创建时间正序；`GET /categories/paged` 经 `get<PagedResult<Category>>` 传 `params: query`）。

`getCategories`（全量）保留，商品页 / 商品抽屉下拉继续用。

## 5. 页面设计

### 5.1 分类管理页 `CategoriesView.vue`（改造）

参照 `ProductsView.vue` 搜索 + 分页模式：

- 页面头：仅标题「分类管理」。
- 工具条（无描边 `a-card` 内）两行：
  - 筛选行（`a-row wrap` + `a-col`）：关键词输入（placeholder「搜索分类名称」，`IconSearch` 前缀，回车生效）；末列「搜索」（primary，`loading=loading`）+「重置」（`loading=loading`）。
  - 操作行：右对齐「新增」（primary `size=small`，`IconPlus`，打开抽屉）+ 竖分隔 +「刷新」（`size=small`，`loading=loading`）。
- 表格（`:key=tableKey`，条件变化回第 1 页）：`row-key="id"`、服务端分页、`:loading=loading`、`:scroll="{ x: tableScrollX }"`；列：

| 列 | slot | 宽度 | 说明 |
|---|---|---:|---|
| 序号 | `seq` | 64 | 跨页连续：`(page-1)*pageSize + rowIndex + 1` |
| 分类名称 | `name` | 240 | 展示名称（`ellipsis + tooltip`） |
| 创建时间 | `createdAt` | 172 | `formatDateTime` |
| 操作 | `action` | 150 | 平铺 ≤3：编辑（主，`IconEdit`）、删除（危险 `status=danger`，`IconTrash` + `a-popconfirm`「确认删除该分类？已被商品引用的分类无法删除」） |

- 交互：
  - 搜索 / 重置 / 刷新 / 翻页 / 每页条数：`fetchSeq` 请求序号仲裁；`onSearch` 应用 `appliedKeyword` 回第 1 页；`onReset` 清空；`onPageSizeChange` 回第 1 页。
  - 新增 / 编辑：打开 `CategoryFormDrawer`（`mode=create/edit`），`@saved` 后 `fetchList`。新增成功后回第 1 页（便于看到新行）。
  - 删除：`deletingCategoryId` 行级 loading；成功 `Message.success('分类已删除')`、`fetchList`（当前页；若删空当前页回退到上一页）；40106 / 40400 由请求层统一提示。
  - 按钮 loading（前端规则 §4.6）：查询类 `loading`（表格 + 搜索 / 重置 / 刷新共用）；行内删除 `deletingCategoryId`。写 handler 防重入。

### 5.2 分类表单抽屉 `CategoryFormDrawer.vue`（新增）

参照 `ProductFormDrawer.vue` 的抽屉骨架（`a-drawer` 宽 480，`unmount-on-close`，`:footer=false` + 底部操作栏）：

- props：`visible`（v-model）、`mode: 'create' | 'edit'`、`editId?: string`、`editName?: string`（编辑回填用，分类无独立详情接口，父级传当前行名称避免额外请求）。
- emits：`update:visible`、`saved`。
- 表单字段：仅「分类名称」（`a-input`，1-20 字符，`required`，`max=20`，`show-word-limit`）。`mode=edit` 时 `watch visible` 将 `form.name` 回填为 `editName`（新增时为空）。
- 提交：`formRef.validate()` 通过后按 `mode` 调 `createCategory(name)` / `updateCategory(editId, name)`；成功 `Message.success`（「分类已创建」/「分类已更新」）、`emit('saved')`、关闭；`submitting` 防重入。
- 重名 40105 / 删除有引用 40106 由请求层统一 `Message.error` 提示。

### 5.3 路由与菜单（不变）

`router/index.ts` 已有 `categories` 路由（懒加载 `CategoriesView`，`requiresAuth`）；`AppLayout.vue`「基础档案」分组已有「分类管理」（`key=categories`，`IconTags`，「商品管理」之后）。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2；本节不重复改动。

## 6. e2e 设计（Playwright）

`frontend/e2e/category.spec.ts` 重写（搜索 + 分页 + 抽屉新增/编辑 + 删除拦截）：

1. 菜单进入分类管理页，渲染表头（分类名称 / 创建时间 / 操作）。
2. 新增分类：点「新增」→ 抽屉输入名称 → 提交 → 提示「分类已创建」→ 第 1 页列表可见新行；重名新增抽屉提交提示「分类名称已存在」（40105）。
3. 抽屉编辑：点行「编辑」→ 抽屉回填名称 → 改名提交 → 提示「分类已更新」→ 列表展示新名称；重名编辑提示 40105。
4. 删除：点行「删除」→ popconfirm 确认 → 提示「分类已删除」→ 行消失；被商品引用的分类删除被拦截（先建商品挂该分类，删该分类断言 40106 message 可见且行仍在）。
5. 搜索：输入命中关键词「搜索」→ 仅命中行可见；「重置」→ 恢复。
6. 分页：建 11 条同前缀数据后切 `pageSize=10` → 第 1 页 10 行（序号 1-10）、第 2 页 1 行（序号 11），验证切片正确与序号跨页连续。

`product.spec.ts`：入口跳转用例与 `createCategoryInDrawer` 行内新增保持不变（不回归）。

## 7. 不做的事

- 不改既有分类写接口（POST / PUT / DELETE）与全量查询 `GET /api/categories`。
- 不做分类排序 / 备注 / 多级 / 停用。
- 不引入跨页状态共享（Pinia / 事件总线）。

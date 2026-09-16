# 任务清单：表单与详情页样式参照（form-detail-showcase）

## 任务（初版：例 A + 例 B 两示例）

- [x] T1 前端规则落地：`frontend/RULE.mdc` 新增「表单与详情页约定」章节（形态决策 + 不共享约定）
- [x] T2 共享数据源 `src/composables/useOrderStore.ts`（模块级单例 + 订单种子数据 + upsert/remove/findById）
- [x] T3 例 A 列表页 `FormDrawerShowcaseView.vue`（参照 list-showcase 工具条/表格/分页 + 新增→抽屉、查看→详情、编辑→抽屉）
- [x] T4 例 A 抽屉表单 `OrderFormDrawer.vue`（create/edit 双模式、自定义底部操作栏 + 手动 validate 校验提交、回填）
- [x] T5 例 A 详情页 `FormDrawerDetailView.vue`（page-header + descriptions + 编辑/删除 + 404 空态）
- [x] T6 例 B 列表页 `FormPageShowcaseView.vue`（操作列/新增跳路由；底部例 A → 例 B 入口链接）
- [x] T7 例 B 表单页 `FormPageFormView.vue`（new/edit 共用、分组表单 + 明细子表格增删行 + 校验 + 提交跳详情）
- [x] T8 例 B 详情页 `FormPageDetailView.vue`（descriptions + 明细子表格 + 编辑/删除 + 404 空态）
- [x] T9 路由 +6 条子路由 + 侧边菜单「表单与详情示例」
- [x] T10 e2e `form-drawer-showcase.spec.ts`（抽屉新增/编辑/校验拦截/详情/404/删除）
- [x] T11 e2e `form-page-showcase.spec.ts`（独立表单页新增/编辑/明细行/校验拦截/详情/404/删除）
- [x] T12 验证：`npm run build` + `npm run lint` + `npm run test:e2e` 全通过（e2e 33/33）

## 变更：两示例合并为统一列表 `/form`（双形态入口）

- [x] T13 规格更新：`requirement.md` / `design.md` 改写为「单一列表 + 双形态入口 + 统一详情」
- [x] T14 统一列表页 `FormShowcaseView.vue`（工具条「新增·抽屉」primary +「新增·页面」次级；操作列 查看/编辑（dropdown 形态菜单：用抽屉/用页面）/删除）
- [x] T15 抽屉表单迁移至 `Showcase/OrderFormDrawer.vue`（行为不变）
- [x] T16 独立表单页 `FormPageFormView.vue` 适配新路由名（`formNew` / `formEdit`，提交跳 `formDetail`）
- [x] T17 统一详情页 `FormDetailView.vue`（descriptions 全字段 + 商品明细子表格 + 编辑走抽屉 + 404 空态）
- [x] T18 路由替换为 `/form`、`/form/new`、`/form/edit/:id`、`/form/detail/:id`；侧边菜单 `key="form"`；删除 4 个旧页面与 `FormDrawerShowcase/` 目录
- [x] T19 e2e 合并为 `form-showcase.spec.ts`（列表/抽屉双入口/编辑形态菜单/页面新增编辑/统一详情/404/删除），删除两个旧 spec
- [x] T20 验证：`npm run build` + `npm run lint` + `npm run test:e2e` 全通过（e2e 30/30）

## 完成定义

- 上述任务全部勾选，且 AGENTS.md §6 强制测试门槛通过（本功能纯前端，须跑 `npm run test:e2e` 且覆盖改动行为）。

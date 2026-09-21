---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：表单与详情页样式参照（form-detail-showcase）

## 任务

- [x] T1 前端规则落地：`frontend/RULE.mdc` 新增「表单与详情页约定」章节（形态决策 + 不共享约定）
- [x] T2 共享数据源 `src/composables/useOrderStore.ts`（模块级单例 + 订单种子数据 + upsert/remove/findById）
- [x] T3 统一列表页 `src/views/Showcase/FormShowcaseView.vue`（参照 list-showcase 工具条/表格/分页；工具条「新增·抽屉」primary +「新增·页面」次级；操作列 查看 / 编辑（dropdown 形态菜单：用抽屉 / 用页面）/ 删除）
- [x] T4 抽屉表单 `Showcase/OrderFormDrawer.vue`（create/edit 双模式、自定义底部操作栏 + 手动 validate 校验提交、回填）
- [x] T5 独立表单页 `FormPageFormView.vue`（new/edit 共用、分组表单 + 明细子表格增删行 + 校验 + 提交跳 `formDetail`；路由名 `formNew` / `formEdit`）
- [x] T6 统一详情页 `FormDetailView.vue`（descriptions 全字段 + 商品明细子表格 + 编辑走抽屉 + 404 空态）
- [x] T7 路由替换为 `/form`、`/form/new`、`/form/edit/:id`、`/form/detail/:id`；侧边菜单 `key="form"`；删除 4 个旧页面与 `FormDrawerShowcase/` 目录
- [x] T8 e2e `form-showcase.spec.ts`（列表 / 抽屉双形态入口 / 编辑形态菜单 / 页面新增编辑 / 统一详情 / 404 / 删除），并删除两个旧 spec
- [x] T9 验证：`npm run build` + `npm run lint` + `npm run test:e2e` 全通过

## 完成定义

- 上述任务全部勾选，且 AGENTS.md §6 强制测试门槛通过（本功能纯前端，须跑 `npm run test:e2e` 且覆盖改动行为）。

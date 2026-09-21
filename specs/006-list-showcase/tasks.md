---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：列表页样式参照（list-showcase）

## 任务

- [x] T1 新建 `frontend/src/views/Showcase/ListShowcaseView.vue`（静态数据 + 搜索/筛选/重置）
- [x] T2 表格增强：多选 + 批量删除、排序、列显示设置、空状态
- [x] T3 工具操作：新增（提示）、导出 CSV、刷新恢复
- [x] T4 分页（10/20/50，显示总数）与条件变化回第 1 页
- [x] T5 路由 `/list`（`AppLayout` 子路由，`requiresAuth`）+ 侧边菜单"列表示例"
- [x] T6 e2e `frontend/e2e/list-showcase.spec.ts`
- [x] T7 布局优化：操作区并入表格上方工具条 + 筛选区多列响应式栅格（新增角色筛选）+ e2e 角色筛选用例
- [x] T8 工具条视觉区分：工具条底部加分隔线与表格分开，操作行按钮改小号（size=small）
- [x] T9 操作行贴紧表格：分隔线下移至筛选行底部，操作行与表格间距缩至 8px
- [x] T10 操作行按钮分组：按主操作（新增）/ 数据操作（导出、批量删除）/ 视图操作（列设置、刷新）三组，组间竖分隔线
- [x] T11 操作行左右分组：主操作（新增/开单）统一靠左、其余操作靠右且同一行；采购/销售开单按钮从页面头移入操作行左组（页面头仅留标题）；全列表页对齐该约定，补 e2e 布局断言
- [x] T12 筛选行「搜索/重置」按钮补图标（`IconSearch` / `IconRestore`，`#icon` 插槽）：「重置」用 Tabler `IconRestore`（恢复初始态语义，与已归属「刷新」的 `IconRefresh` 区分），全列表页对齐该约定，e2e 图标断言用精确 class；**图标约定以本规格 `design.md` §0 为准**
- [x] T13 列设置补齐到销售开单 / 采购入库页（此前两页无列设置）：`columns` 由静态常量改为 computed 按 `visibleColumns` 拼列（§2.4），序号与操作列固定显示、不参与设置（`specs/011-action-column` §5），补 e2e 断言
- [x] T14 验证：`npm run build` + `npm run lint` + `npm run test:e2e` 全通过

## 完成定义

- 上述任务全部勾选，且 AGENTS.md §6 强制测试门槛通过。

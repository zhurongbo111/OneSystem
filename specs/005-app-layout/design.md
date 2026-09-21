---
created: 2026-09-10
updated: 2026-09-17
---

# 设计规格：全局页面布局（app-layout）

> **演进（erp-report）**：侧边菜单已由「示例页面 + 进销存」两组重构为多个顶级分组（示例页面 / 基础档案 / 采购 / 销售 / 库存 / 资金 / 报表 / 系统），分组与子项规划的唯一事实源见 `specs/025-erp-report/design.md` §0.2；自动展开逻辑由「分组 → 路由名集合」映射统一表达。现行为准见 `src/components/AppLayout.vue`。

## 1. 总体设计

在现有前端骨架上引入统一的全局布局组件 `AppLayout`：不引入新依赖、不改动接口层（`src/api/`）与后端。仅新增 1 个组件、重组路由为嵌套结构、收敛两个视图的自带头部，并新增 / 调整 e2e 用例。

## 2. 路由设计（`src/router/index.ts`）

采用嵌套路由，`AppLayout` 作为布局父路由（`name: 'layout'`）：

```
routes:
  - /login            → LoginView          （独立，不进布局，meta: { public: true }）
  - (AppLayout)        ← 布局父路由
      - path: ''       → HomeView            （name: home，meta: { requiresAuth: true }）
      - path: 'components' → ComponentShowcaseView （name: components，meta: { public: true }）
  - /:pathMatch(.*)*  → redirect: /
```

- 父路由 `component: () => import('@/components/AppLayout.vue')`，子路由懒加载（前端规则 §5）。
- 子路由 `home` 用空 `path: ''`，使最终 URL 仍为 `/`（保持现有 `/` 与守卫、e2e 的 URL 断言不变）。
- `/login` 仍独立在 `routes` 顶层，兜底重定向仍在最后。
- 守卫逻辑不变：`to.meta.requiresAuth && !isLoggedIn` → 跳登录页（带 `redirect`）；已登录访问 `login` → 跳 `home`。嵌套后 `meta` 仍从子路由记录取，行为一致。

## 3. 布局组件（`src/components/AppLayout.vue`）

结构（基于 Arco `a-layout`，样式仅用 Arco 组件 + `scoped` 少量布局样式 + Arco CSS 变量，前端规则 §4）：

```
a-layout (layout="has-sider", class=app-layout)
  a-layout-sider (v-model:collapsed, :collapsed-width=64, :width=208, collapsible, breakpoint=lg, :hide-trigger=false)
    顶部 logo 区（折叠时仅显示图标 / 缩写）
    a-menu (:selected-keys=当前路由名, :open-keys=openKeys, :collapsed=collapsed, @menu-item-click=路由跳转, @update:open-keys=同步 openKeys)
      a-menu-item key="home"        → 首页
      a-sub-menu key="showcase"     → 示例页面
        a-menu-item key="components"  → 组件示例
        a-menu-item key="list"          → 列表示例
        a-menu-item key="form"          → 表单与详情示例
  a-layout
    a-layout-header (class=layout-header)
      左侧：折叠触发按钮（MenuFoldOutlined / MenuUnfoldOutlined 图标按钮，与 sider 折叠联动）
      右侧：a-dropdown（trigger=click，利于 e2e 点击与可访问性）
        触发：a-avatar（取 displayName 首字）+ displayName
        下拉项（`a-doption`）：退出登录
    a-layout-content (class=layout-content)
      RouterView
```

- 菜单选中：`a-menu` 的 `selected-keys` 用 `computed` 绑定当前 `route.name`（`['home']` / `['components']` / `['list']` / `['form']` 等），实现路由 ↔ 菜单联动。
- 菜单点击：`@menu-item-click` 中 `router.push({ name: key })`（仅叶子项触发，子菜单不响应）。
- 子菜单展开：`a-menu` 默认 `vertical` 模式（Arco 合法 mode 为 `vertical` / `horizontal` / `pop` / `popButton`，无 `inline`；误传 `inline` 会使子菜单渲染为 hover 弹出层且 `open-keys` 失效），`open-keys` 绑定 `ref`，**初始为空数组（全部折叠）**；`watch(route.name, ..., { immediate: true })` 在初始化与路由变化时把「当前路由所属分组」补进 `openKeys`（只增不减，不移除用户手动展开的其他分组），`@update:open-keys` 同步用户手动展开 / 折叠，避免用户手动折叠后无法再展开。
- 折叠状态：`ref<boolean>`（默认 false），传给 `a-layout-sider` 的 `v-model:collapsed` 与 header 折叠按钮图标切换；不持久化。
- 顶部栏右侧：`a-dropdown` 触发元素为头像 + `displayName`（`auth.user` 为空时显示占位 "用户"）；下拉 `a-doption` / `a-menu` 仅一项"退出登录"。
- 退出登录：`auth.logout()` + `router.replace({ name: 'login' })`（复用现有 auth store 能力，前端规则 §7 的全局约定见 `AGENTS.md` §4.6）。
- 用户信息恢复：在布局 `onMounted` 调 `auth.fetchCurrentUser()`（从 `HomeView` 上移至此，保证任何受保护子页面进入都能恢复用户）。
- 内容区 `a-layout-content` 背景用 `var(--color-fill-2)`，内边距 `24px`，高度自适应。

## 4. 视图收敛

- `src/views/HomeView.vue`：
  - 移除 `a-layout` / `a-layout-header` / `header-right`（logo、用户名、"组件示例"、"退出登录"按钮），这些能力上移到 `AppLayout`。
  - 仅保留 `a-layout-content` 内的"欢迎"卡片与当前用户 `a-descriptions`（`a-spin` 加载态保留）。
  - 移除 `onMounted` 的 `fetchCurrentUser`（上移到布局）、`onLogout`、`goComponents` 及其样式。
  - 根节点改为内容容器（`div.home-page` 保留内边距交给布局 content，本页可简化为直接渲染卡片）。
- `src/views/Showcase/ComponentShowcaseView.vue`：
  - 移除顶部标题栏的"返回首页"按钮（导航由侧边菜单承担）。
  - 保留页面标题"Arco Design 组件示例"与 `a-tabs` 主体（页面标题可作为内容区顶部说明，保留 `h` 级标题供 e2e 断言）。

## 5. 技术决策

| 决策 | 理由 |
|---|---|
| `AppLayout` 放 `src/components/` 并作为路由父组件 | 符合前端规则 §2（复用组件放 components/）；布局是跨页面复用结构 |
| 用 `a-layout-sider` 内建 `collapsible` + `breakpoint` | Arco 原生能力，自带响应式折叠，不自造样式体系（前端规则 §4） |
| 菜单 `selected-keys` 绑定 `route.name` | 路由 ↔ 菜单单一数据源，避免维护 path 映射 |
| 子路由 `home` 用空 `path` | 保持 URL 为 `/`，不影响现有守卫与 e2e 的 `/` 断言 |
| 菜单静态配置 | 当前仅 2 项，动态化收益低；后续页面增多时再按 meta 生成（范围外已说明） |
| 不改接口层 / 后端 | 纯前端布局改造，最小化回归风险 |

## 6. e2e 设计（`frontend/e2e/app-layout.spec.ts`，并调整既有 spec）

- 依赖：前端 dev（5173）+ 后端 dev（5080，登录需要）。登录用例沿用 `login.spec.ts` 的 `beforeAll` 后端健康检查方式。
- `app-layout.spec.ts` 用例（需登录，复用登录流程）：
  1. 登录后进入首页，可见侧边菜单"首页"且为选中态（`arco-menu-selected` 可判）。
  2. 首页时子菜单默认折叠（子项不可见）；点击分组标题"示例页面"展开后点击"组件示例"跳转 `/components`，"组件示例"菜单项选中。
  3. 点击折叠按钮，侧边栏收起（菜单进入折叠态，`arco-layout-sider-collapsed` 可判）；再点展开恢复。
  4. 顶部栏用户下拉点击"退出登录"，跳转登录页。
  5. 直接访问子页面（如 `/products`）时，所属分组「基础档案」自动展开（子项可见），其余分组仍折叠。
- 既有 `login.spec.ts` 调整：
  - "登录成功进入首页并展示当前用户"：用户信息仍可见（`HomeView` 保留 descriptions），断言不变；如"管理员"在布局顶部栏与卡片都出现，取 `.first()`（已如此）。
  - "退出登录回到登录页"：退出入口从首页头部按钮改为布局顶部栏用户下拉项，需先 hover / 点击下拉触发元素再点击"退出登录"。
- 既有 `component-showcase.spec.ts` 调整：
  - "已登录首页点击「组件示例」跳转"：入口从首页头部按钮改为侧边菜单项，改为点击菜单"组件示例"。
  - 直接访问 `/components`、tab 切换用例不变（页面标题保留）。
- 既有 `list-showcase.spec.ts` / `form-showcase.spec.ts`（以及 `component-showcase.spec.ts`、进销存各 spec）：子菜单改为默认折叠后，点击子菜单项前需先展开所属分组。统一收敛到 e2e 公共 helper `frontend/e2e/helpers/menu.ts` 的 `clickMenuItem(page, name)`——内部按「菜单项 → 分组」映射，子项不可见时先点击分组标题（`.arco-menu-inline-header`）再点击子项；各 spec 用该函数替换原来的 `.arco-menu-item` 直接点击。

## 7. 验证门槛（`AGENTS.md` §6）

- 仅前端改动：`npm run build` + `npm run lint` + 前后端 dev 启动后 `npm run test:e2e` 全通过，方可勾选 `tasks.md`。

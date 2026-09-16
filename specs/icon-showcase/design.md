# 设计规格：图标示例（icon-showcase）

## 1. 总体设计

纯前端改动：安装 `@tabler/icons-vue`、`@lucide/vue` 依赖，在既有组件示例页 `src/views/Showcase/ComponentShowcaseView.vue` 的 `a-tabs` 中新增第 6 个 tab「图标」，分三段展示 Arco、Tabler、Lucide 三套图标的用法。不新增视图、不新增路由、不改接口层与后端。

## 2. 依赖

- Arco 图标：`@arco-design/web-vue` 自带，具名导入 `Icon*`（如 `IconHome`）。props：`size`（number|string，number 按 px）、`stroke-width`（number，**默认 4**，与 Tabler/Lucide 默认 2 不同）、`rotate`（number，角度）、`spin`（boolean，旋转动画）；**无 `color` prop**，SVG 用 `currentColor` 着色，颜色经 CSS `color` 控制（`a-space` 上 `:style="{ color }"`）。
- `@tabler/icons-vue`：Tabler 官方 Vue 图标组件封装。具名导入 `Icon*`（如 `IconHome`）；组件接受 `size`（px，number）、`stroke`（number/string）、`color` 等属性，透传 SVG 属性。
- `@lucide/vue`：Lucide 官方 Vue 图标组件封装（原 `lucide-vue-next` 已废弃并改名）。具名导入 PascalCase 图标（如 `House` / `Search`，无 `Icon` 前缀；注意新版 `Home` 已改名为 `House`）；`LucideProps` 含 `size?: number`、`strokeWidth?: number | string`、`color` 等。
- 三套图标组件均为 `FunctionalComponent`，同页共存无冲突（Arco 与 Tabler 同名图标如 `IconHome` 因导入来源不同，模板中按所在段使用）。

## 3. 页面设计（`src/views/Showcase/ComponentShowcaseView.vue`）

在现有 `a-tabs` 内、`导航` tab 之后新增 `<a-tab-pane key="icon" title="图标">`。tab 内分三段，各一段一个 `a-row` + 若干 `a-card`，段间以 `icon-lib-divider` 分组标题分隔，顺序：Arco → Tabler → Lucide。

### 3.1 Arco 段（段首，`@arco-design/web-vue`）

| 卡片 | 内容 |
|---|---|
| Arco 常用图标 | 图标网格（`IconHome` / `IconSearch` / `IconUser` / `IconSettings` / `IconNotification` 等 12 个），每项标注组件名 |
| 尺寸 size | 同一图标（`IconStar`）不同 `size`（16/24/32/48）对比 |
| 线宽 stroke-width | 同一图标（`IconStar`）不同 `:stroke-width`（1/2/3/4，默认 4）对比 |
| 颜色 color | 不同 `color`（Arco 主题色，CSS `color` 控制 `currentColor`）图标 |
| 旋转 rotate / 动画 spin（Arco 特有） | `IconPlayCircle` 不同 `rotate`（45/90/180）+ `IconRefresh` `:spin="true"` |
| 按钮内嵌图标 / 标签 / 菜单内嵌图标 | `a-button`、`a-tag`、`a-menu-item` 内嵌 Arco 图标 |

### 3.2 Tabler 段（分组标题「Tabler Icons（@tabler/icons-vue）」分隔）

| 卡片 | 内容 |
|---|---|
| Tabler 常用图标 | 图标网格（`IconHome` / `IconSearch` / `IconUser` / `IconBell` / `IconSettings` 等），每项标注组件名 |
| 尺寸 size | 同一图标不同 `size`（16/24/32/48）对比 |
| 线宽 stroke | 同一图标不同 `:stroke`（1/2/3）对比 |
| 颜色 color | 不同 `:color`（Arco 主题色）图标 |
| 按钮内嵌图标 / 标签 / 菜单内嵌图标 | `a-button`、`a-tag`、`a-menu-item` 内嵌 Tabler 图标 |

### 3.3 Lucide 段（分组标题「Lucide Icons（@lucide/vue）」分隔）

| 卡片 | 内容 |
|---|---|
| Lucide 常用图标 | 图标网格（`House` / `Search` / `User` / `Bell` / `Settings` 等），每项标注组件名 |
| 尺寸 size | 同一图标（`Star`）不同 `:size` 对比 |
| 线宽 stroke-width | 同一图标不同 `:stroke-width`（1/2/3）对比 |
| 颜色 color | 不同 `:color` 图标 |
| 按钮 / 标签内嵌图标 | `a-button`、`a-tag` 内嵌 Lucide 图标 |

演示数据以本地 `const` 声明，图标列表为 `{ name: string; icon: FunctionalComponent }[]`，尺寸/线宽/颜色数组三套共用（`arcoStrokeWidths` 因 Arco 默认 4 单列 `[1, 2, 3, 4]`）；复用 `icon-grid` / `icon-grid-item` / `icon-name` / `value-preview` 样式；`icon-lib-divider` 为分组分隔标题样式（Arco CSS 变量）。

## 4. 技术决策

| 决策 | 理由 |
|---|---|
| 新增「图标」tab 而非独立页面 | 与现有 5 分类同一页面组织，复用页面结构，范围最小 |
| 三套图标同一 tab 内分组展示 | 三套图标用途同类（UI 线性图标），集中对比便于选型；以分组标题分隔避免混淆 |
| Arco 图标段放最前 | 沿用既有页面段落顺序（Arco → Tabler → Lucide）；展示顺序不表达选型优先级，选型以业务语义为准并按前端规则 §4.7 的优先级取用（Tabler 优先） |
| Lucide 选用 `@lucide/vue` 而非 `lucide-vue-next` | `lucide-vue-next@1.0.0` 已被官方标记废弃并建议改用 `@lucide/vue` |
| 具名导入具体图标 | 符合项目按需导入约定，便于 tree-shaking，避免全量引入上千图标 |
| Arco 颜色示例用 CSS `color`（`a-space :style`） | Arco 图标组件无 `color` prop，SVG 为 `currentColor`，CSS 继承是官方着色的标准方式 |
| 图标网格复用 `a-col` + 自定义 grid 布局 | 三段一致，不自造样式体系 |
| 不改既有页面的 Arco 图标用法 | 与现有功能正交，范围最小化 |

## 5. e2e 设计（`frontend/e2e/component-showcase.spec.ts`）

- 在用例中点击「图标」tab（`.arco-tabs-tab-title`，`hasText: '图标'`），断言（`IconHome` / `IconStar` 等在多段重名，各段断言改用该段独有文本）：
  1. tab 激活（`.arco-tabs-tab-active` 含「图标」）。
  2. Arco 段：`Arco 常用图标` 卡片标题、Arco 段独有 `IconNotification`、Arco 特有 `旋转 rotate / 动画 spin` 卡片标题可见。
  3. Tabler 段：`Tabler 常用图标` 卡片标题、Tabler 段独有 `IconBell` 可见。
  4. Lucide 段：`Lucide 常用图标` 卡片标题、Lucide 独有图标名 `House`（`exact` 匹配）可见。
- 依赖：前端 dev（5173）；`public` 页面，不依赖后端。

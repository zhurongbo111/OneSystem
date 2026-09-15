# 设计规格：列表页样式参照（list-showcase）

> 纯前端参照页面，无后端、无接口。数据为页面内静态 Mock。

## 1. 路由与菜单

- 路由：`/list`，`name: 'list'`，`component: () => import('@/views/ListShowcaseView.vue')`，`meta: { requiresAuth: true }`，作为 `AppLayout` 子路由（`/` 父路由下）。
- 侧边菜单：在"组件示例"后追加"列表示例"菜单项（`key="list"`，图标 `IconUnorderedList` 或 `IconList`）。

## 2. 视图结构（`src/views/ListShowcaseView.vue`）

自上而下：

```
.page-header      标题（用户列表），仅标题
.table-card (a-card, bordered=false)
  .toolbar        工具条：筛选行（多列栅格）+ 操作行（主操作靠左 / 其余靠右），紧贴表格上方
  表格区          a-table（多选 + 排序 + 分页 + 列设置 + 空状态）
```

### 2.1 页面头（page-header）
- 仅 `<h1>` 页面标题"用户列表"，不再放置操作按钮（操作已并入表格上方工具条，见 2.2）。

### 2.2 工具条（.toolbar，表格卡片内、表格上方）
分两行，`margin-bottom` 约 12，均用 Arco 栅格 / 布局：

**筛选行**（`a-row`，`gutter: 16`，自动换行，窄屏多行排布）：
- `a-col :span=8`：`a-input` 搜索词（placeholder "搜索名称或邮箱"，`<IconSearch />` 前缀，`allow-clear`，宽度 100%）。
- `a-col :span=4`：`a-select` 状态筛选（选项：启用/禁用，`allow-clear`，宽度 100%）。
- `a-col :span=4`：`a-select` 角色筛选（选项：管理员/编辑/访客，`allow-clear`，宽度 100%）。
- `a-col :span=8`：`a-button type=primary` 搜索 + `a-button` 重置（间距 8，左对齐）。

**操作行**（`.toolbar-actions`，`flex` 左右分组（`justify-content: space-between`）、`gap: 8`、可换行，按钮统一 `size=small`；无主操作的只读页整行右对齐即可）：
- 左组 `.toolbar-actions__left`（`flex`，`gap: 8`）：主操作 新增（`a-button type=primary size=small`，`<IconPlus />`，点击 `Message.info('演示页面，暂未实现')`）。**主操作（新增/开单）统一靠左**，与右组其它按钮同一行；页面头不放任何创建按钮。
- 右组 `.toolbar-actions__right`（`flex`，`gap: 8`，组间以 `a-divider direction=vertical` 分隔）：数据操作 导出（`a-button size=small`，`<IconDownload />`，导出当前筛选结果为 CSV）、批量删除（`a-button status=danger size=small`，`<IconDelete />`，`:disabled="selectedKeys.length === 0"`，`a-popconfirm` 二次确认后移除）；视图操作 列设置（`a-dropdown`，`<IconSettings /> size=small`，内容为 `a-checkbox-group` 勾选要显示的列）、刷新（`a-button size=small`，`<IconRefresh />`，恢复初始数据）。

**分隔与贴紧**：分隔线下移到筛选行（`.toolbar-filter`）底部（`border-bottom: 1px solid var(--color-border)` + `padding-bottom` 约 12），用于区分"筛选"与"操作/表格"两个区域；操作行（`.toolbar-actions`）与表格之间不加线，仅 8px 间距，使操作按钮（尤其列设置/刷新）与表格尽量贴近。

> 说明 1：搜索/筛选采用"点搜索应用"模式（输入不实时过滤），重置还原默认，符合常见业务列表习惯，且 e2e 可稳定断言。
> 说明 2：工具条与表格同卡片，操作按钮与列表距离最近；筛选项按栅格多列排布，窗口变窄时栅格自动换行成多行，不挤压、不溢出。

### 2.3 表格（a-table）
- 数据源：`tableData = computed(...)`，基于**已应用**的搜索词 + 状态筛选 + 排序计算。
- 列定义（`columns` computed，依据"列显示设置"状态动态拼列）：
  | data-index | 标题 | 说明 |
  |---|---|---|
  | `name` | 名称 | `sorter` 前端排序；显示 |
  | `email` | 邮箱 | 显示 |
  | `role` | 角色 | `a-tag`（按角色着色）；显示 |
  | `status` | 状态 | `a-tag`（启用 success / 禁用 default）；显示 |
  | `createdAt` | 创建时间 | `sorter`；自定义插槽用 `dayjs`/`Intl` 格式化为 `YYYY-MM-DD`；显示 |
  | `action` | 操作 | 编辑（`a-button type=text`，`Message.info` 提示）/ 删除（`a-popconfirm`，确认后移除）；始终显示 |
- 序号：使用 `a-table` 内置 `row-key` + 表头"序号"列，通过 `#序号` 插槽按当前页偏移计算行号。
- 多选：`row-selection`（`type="checkbox"`），`v-model:selectedKeys`，`column-width` 适当，表头全选作用于**当前页**。
- 分页：`pagination`（前端），`pageSize=10`，`show-total`、`show-page-size`，`pageSizeOptions=[10,20,50]`。
- 排序：`sorter` 列点击触发，`sort-order` 由 Arco 管理；`tableData` 依据当前排序状态排序后再交给分页。
- 空状态：`#empty` 插槽自定义（无结果提示 + 重置按钮）。
- 行高亮/悬停由 Arco 默认提供。

### 2.4 列显示设置
- `visibleColumns = ref<string[]>([...全部列 data-index])`。
- 列设置下拉内 `a-checkbox-group` 选项来自列清单（排除固定显示的 `action`）。
- `columns` computed 依据 `visibleColumns` 过滤列。
- 不持久化（刷新/重进恢复默认）。

### 2.5 导出 CSV
- `exportCsv()`：取**当前已应用筛选 + 排序**的全量结果（不分页），按可见列拼接 CSV。
- 处理表头中文 BOM（`\uFEFF`）以保证 Excel 正确显示；拼接为 `Blob`，`URL.createObjectURL` 触发 `<a download="用户列表.csv">` 下载。
- 无可见数据时 `Message.warning('暂无可导出数据')`。

### 2.6 状态与数据
- 数据模型：
  ```ts
  interface UserRow {
    id: string
    name: string
    email: string
    role: 'admin' | 'editor' | 'viewer'
    status: 'active' | 'disabled'
    createdAt: string // ISO 日期
  }
  ```
- `initialData = ref<UserRow[]>([...约 25 条])`（跨 admin/editor/viewer，active/disabled 混合，创建时间分散，便于验证排序/筛选/分页）。
- `data = ref<UserRow[]>(clone(initialData))`，删除操作改 `data`；"刷新"重置 `data` 为 `clone(initialData)`。
- 搜索词/状态/角色筛选：输入态 `draft`（`keywordInput`/`statusInput`/`roleInput`）与已应用态（`appliedKeyword`/`appliedStatus`/`appliedRole`）分离，点"搜索"将 `draft` 赋给 `applied` 并重置到第 1 页。
- 过滤条件：`tableData` 按 `appliedKeyword`（名称/邮箱包含）+ `appliedStatus` + `appliedRole` 叠加过滤；`tableKey` 由三个已应用条件拼接（条件变化回第 1 页）。
- 删除（单行/批量）：`data.value = data.value.filter(...)`，并清理 `selectedKeys` 中已删除的键。

### 2.7 样式
- `scoped`；仅做布局（`page-header`/`toolbar` 的 flex 与栅格间距、卡片留白）。
- 筛选行用 `a-row`/`a-col`（栅格自动换行）；操作行 `flex + wrap` 左右分组（主操作靠左，其余靠右），仅视图操作（无主操作）的只读页整行右对齐。
- 颜色、圆角、边框、间距一律使用 Arco CSS 变量（`--color-*`、`--border-radius-*`、`padding` 等），不自造色板。
- 外层用 `a-card` 包裹表格区域，与 `app-content` 背景协调。

## 3. 交互细节

- 任一条件（搜索/筛选/重置/刷新）变化后，分页回到第 1 页。
- 排序不改变筛选结果集合，只改变顺序。
- 批量删除后 `Message.success` 提示删除条数。

## 4. e2e（`frontend/e2e/list-showcase.spec.ts`）

前置：登录后进入 `/list`。

- 页面加载后显示静态数据，表格行数 = 每页条数（10）。
- 输入关键词并点搜索 → 行数变化（命中 < 全量）；点重置 → 恢复 10 行。
- 状态筛选选"禁用"并搜索 → 行数变化。
- 角色筛选选"管理员"并搜索 → 行数变化（命中 5 条）。
- 勾选首行 + 点"批量删除" → 确认 → 对应行移除，表格更新。
- 切换每页 20 条 → 行数变化（≤ 总数）。
- 列设置隐藏"邮箱"列 → 表头不再出现"邮箱"。

> e2e 选择器优先使用 `getByRole`/`getByText`/`getByLabel`；必要处用 Arco 稳定类名（如 `.arco-table`）兜底。

## 5. 文件清单

| 文件 | 操作 |
|---|---|
| `frontend/src/views/ListShowcaseView.vue` | 新增 |
| `frontend/src/router/index.ts` | 修改（加 `/list` 子路由） |
| `frontend/src/components/AppLayout.vue` | 修改（加"列表示例"菜单项 + 图标 import） |
| `frontend/e2e/list-showcase.spec.ts` | 新增 |

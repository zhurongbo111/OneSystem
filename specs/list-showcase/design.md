# 设计规格：列表页样式参照（list-showcase）

> 纯前端参照页面，无后端、无接口。数据为页面内静态 Mock。
> **通用布局与交互约定**（页面头只放标题、工具条两行与左右分组、表格配置、列宽策略、请求序号仲裁、CSV BOM）的唯一事实源是**本规格 §0**（前端规则 `.codebuddy/rules/frontend/RULE.mdc` §5 只留判据与指针）；§1 起描述这个参照页面自身的内容。

## 0. 列表页通用约定正文（唯一事实源）

> 适用范围：所有表格类列表页。操作列细则见 `specs/action-column/design.md` §0；按钮 loading 见 `specs/button-loading/design.md` §0；图标选型见 `specs/icon-showcase/design.md` §0。

**参照实现**：所有表格类列表页以 `src/views/Showcase/ListShowcaseView.vue` 为标准参照实现；新增列表页优先复制其结构与交互模式再替换业务字段。

**页面布局**

- 页面头仅放标题；操作按钮不放页面头，统一并入表格卡片上方的工具条（避免操作离列表太远）。
- 工具条分两行，整体与表格同在一个无描边 `a-card` 内，紧贴表格：
  - 筛选行：`a-row`（`wrap`）+ `a-col` 栅格排多列，窄屏自动换行为多行；末列放「搜索」（primary，`<IconSearch />`）+「重置」（`<IconRestore />`）按钮，图标走 `#icon` 插槽（重置用 `IconRestore`（恢复初始态语义），不复用已归属「刷新」的 `IconRefresh`）；行底用边框线与操作行分隔。
  - 操作行：紧贴表格，左右分组（`justify-content: space-between`）；**主操作（新增 / 开单）统一靠左**，其余操作（数据操作 导出、批量删除 / 视图操作 列设置、刷新）靠右且与主操作同一行，组间用竖 `a-divider` 分隔；按钮统一 `size="small"`；无主操作的只读页（库存、登录日志）整行右对齐即可。页面头不放任何创建按钮（采购 / 销售开单入口同样在操作行左组）。
- 表格配置：`row-key` 必设；分页 `showTotal` + `showPageSize`（`pageSizeOptions: [10, 20, 50]`）；枚举字段（角色 / 状态等）用 `a-tag` 着色展示；操作列放表格末列，`a-button type="text"`，呈现细则见 `specs/action-column/design.md` §0。
- 列宽策略：每列都设 `width`，不设无宽度弹性列（唯一无宽度列会吸收全部剩余空间，宽屏下被撑到 400px+）；长文本列固定 `width` + `ellipsis: true, tooltip: true`；表格设 `:scroll="{ x: tableScrollX }"`（`tableScrollX` = 各列 `width` 之和）——总和小于容器时剩余空间按列宽比例分摊，窄屏横向滚动。操作列宽度按 `specs/action-column/design.md` §0 的列宽参考取值。

**交互模式**

- 筛选条件「输入态 / 已应用态」分离：只有点「搜索」（或回车）才写入已应用态，条件变化后清空勾选。
- 条件变化后回到第 1 页：已应用条件拼成字符串作 `:key` 重挂载表格。
- 查询动作（搜索 / 重置 / 刷新 / 翻页 / 每页条数）并发不可避免（HTTP 不串行、事件回调不阻塞、`void fetchList()` 不等待），列表请求必须用请求序号仲裁：`const seq = ++fetchSeq` 记序号，响应回来时 `seq !== fetchSeq` 则丢弃，`loading` 也只在最后发起的请求上复位；禁止用 `if (loading) return` 丢弃新点击。
- 查询类按钮的 loading 绑定见 `specs/button-loading/design.md` §0（与表格共用同一个 `loading`）。
- 删除（单行、批量）一律 `a-popconfirm` 确认；批量删除按钮在无勾选时 `:disabled`。
- 导出当前筛选结果为 CSV 时加 `\uFEFF` BOM 头（Excel 中文兼容）。

## 1. 路由与菜单

- 路由：`/list`，`name: 'list'`，`component: () => import('@/views/Showcase/ListShowcaseView.vue')`，`meta: { requiresAuth: true }`，作为 `AppLayout` 子路由（`/` 父路由下）。
- 侧边菜单：示例页面分组内「列表示例」菜单项（`key="list"`）。

## 2. 视图结构（`src/views/Showcase/ListShowcaseView.vue`）

自上而下（布局约定见本规格 §0「页面布局」，此处只列本页内容）：

```
.page-header      标题（用户列表），仅标题
.table-card (a-card, bordered=false)
  .toolbar        工具条：筛选行（多列栅格）+ 操作行（主操作靠左 / 其余靠右），紧贴表格上方
  表格区          a-table（多选 + 排序 + 分页 + 列设置 + 空状态）
```

### 2.1 本页工具条内容

- 筛选行：搜索词（placeholder「搜索名称或邮箱」，`IconSearch` 前缀，`allow-clear`）、状态筛选（启用 / 禁用）、角色筛选（管理员 / 编辑 / 访客），末列「搜索」（primary）+「重置」。
- 操作行左组：新增（primary，「演示页面，暂未实现」）。
- 操作行右组：导出（当前筛选结果为 CSV）、批量删除（danger，`:disabled="selectedKeys.length === 0"` + `a-popconfirm`）｜列设置（勾选显示列）、刷新（恢复初始数据）。
- 图标一律取 Tabler，选型优先级与映射见 `specs/icon-showcase/design.md` §0；同一操作全项目同一图标（重置用 `IconRestore`，不复用已归属「刷新」的 `IconRefresh`）。

### 2.2 列定义

- 数据源：`tableData = computed(...)`，基于**已应用**的搜索词 + 状态筛选 + 排序计算。

| data-index | 标题 | 说明 |
|---|---|---|
| `name` | 名称 | `sorter` 前端排序 |
| `email` | 邮箱 | 长文本列 `ellipsis + tooltip` |
| `role` | 角色 | `a-tag`（按角色着色） |
| `status` | 状态 | `a-tag`（启用 success / 禁用 default） |
| `createdAt` | 创建时间 | `sorter`；插槽格式化 `YYYY-MM-DD` |
| `action` | 操作 | 编辑（`type="text"`，`Message.info`）/ 删除（`a-popconfirm`，确认后移除）；始终显示、不参与列设置 |

- 序号列：`a-table` 内置 `row-key` + 表头「序号」列，按当前页偏移计算行号。
- 多选：`row-selection`（`type="checkbox"`，`v-model:selectedKeys`），表头全选作用于**当前页**。
- 其余表格配置（`row-key` 必设、分页 `showTotal` + `showPageSize` + `pageSizeOptions`、每列固定 `width` + `:scroll="{ x: tableScrollX }"`）见本规格 §0「表格配置 / 列宽策略」；本页 `pageSize = 10`。

### 2.3 列显示设置

- `visibleColumns = ref<string[]>([...全部列 data-index])`；下拉内 `a-checkbox-group` 选项排除固定显示的 `action`。
- `columns` computed 依据 `visibleColumns` 过滤列；不持久化（刷新 / 重进恢复默认）。

### 2.4 导出 CSV

- `exportCsv()`：取**当前已应用筛选 + 排序**的全量结果（不分页），按可见列拼接 CSV；表头加 BOM（见本规格 §0）；拼接 `Blob` → `URL.createObjectURL` → `<a download="用户列表.csv">`。
- 无可见数据时 `Message.warning('暂无可导出数据')`。

### 2.5 状态与数据

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

- `initialData = ref<UserRow[]>([...约 25 条])`（跨 admin/editor/viewer，active/disabled 混合，创建时间分散，便于验证排序 / 筛选 / 分页）。
- `data = ref<UserRow[]>(clone(initialData))`，删除操作改 `data`；「刷新」重置为 `clone(initialData)`。
- 筛选条件输入态（`keywordInput` / `statusInput` / `roleInput`）与已应用态（`applied*`）分离，点「搜索」应用并回第 1 页（模式见本规格 §0「交互模式」）。
- `tableData` 按 `appliedKeyword`（名称 / 邮箱包含）+ `appliedStatus` + `appliedRole` 叠加过滤；`tableKey` 由三个已应用条件拼接。
- 删除（单行 / 批量）：`data.value = data.value.filter(...)`，并清理 `selectedKeys` 中已删除的键。

### 2.6 样式

- `scoped`，仅做布局；颜色 / 圆角 / 边框 / 间距一律用 Arco CSS 变量，不自造色板（前端规则 §4）。
- 筛选行用 `a-row` / `a-col`（栅格自动换行）；操作行 `flex + wrap` 左右分组。

## 3. 交互细节（本页特有）

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
- 操作列形态（平铺 / 更多 / 危险样式）断言见 `specs/action-column/`。

> e2e 选择器优先使用 `getByRole`/`getByText`/`getByLabel`；必要处用 Arco 稳定类名（如 `.arco-table`）兜底。

## 5. 文件清单

| 文件 | 操作 |
|---|---|
| `frontend/src/views/Showcase/ListShowcaseView.vue` | 新增 |
| `frontend/src/router/index.ts` | 修改（加 `/list` 子路由） |
| `frontend/src/components/AppLayout.vue` | 修改（加"列表示例"菜单项 + 图标 import） |
| `frontend/e2e/list-showcase.spec.ts` | 新增 |

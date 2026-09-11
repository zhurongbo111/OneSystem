# 设计规格：列表操作列约定（action-column）

> 本规格为**纯前端展示约定**，不涉及后端、不涉及接口变更。约定内容落入前端规则 §5.2，参照实现为 `ListShowcaseView.vue`。

## 1. 适用范围

- 所有表格类列表页的操作列（`a-table` 末列，`slotName: 'action'`）。
- 页面工具条（页面级操作行）不适用：其分组与分隔约定已在前端规则 §5「列表页约定 · 页面布局」。

## 2. 操作数量约定（> 3 收纳进「更多」）

| 操作数 | 呈现 |
|---|---|
| ≤ 3 | 全部平铺 |
| > 3 | 保留前 3 个平铺（`a-space :size="4"`），其余收纳进行内「更多」下拉 |

- 平铺选取原则：**常用 / 主操作 + 危险操作**优先平铺；低频操作进「更多」。
- 「更多」下拉：`a-dropdown trigger="click"`，触发按钮 `a-button type="text" size="small"`，`#icon` 放 `IconMore`；菜单项用 `a-doption`（同样 `size` 小、`#icon` 插槽带图标，`@click` 触发）。
- 平铺操作与「更多」按钮之间沿用 `a-space :size="4"`，不加分隔符。

### 2.1 列宽与布局策略（防止列宽失衡 / 表头错位）

Arco `a-table` 默认 `table-layout: fixed`。**某一列未设 `width` 时会成为唯一弹性列，吸收全部剩余空间**（宽屏下被撑到 400px+）；反之**操作列 `width` 小于按钮组实际宽度时，`td` 内容溢出而表头仍按设定宽渲染，造成表头与内容错位**。两条都实测复现并修复如下：

1. **每列都设 `width`**：内容列（姓名/邮箱/手机号/时间等）均给固定 `width`，长文本列用 `ellipsis: true, tooltip: true` 截断，不设无宽度弹性列。
2. **操作列 `width` ≥ 按钮组实际宽度**：实测「编辑/详情/禁用」3 文本按钮（各 66px）+ 纯图标「更多」（28px）+ 间距 = **238px**（含 §5 密度 `padding: 0 8px`），取 **240**。
3. **`a-table :scroll="{ x: tableScrollX }"`**：`tableScrollX` = 各列 `width` 之和。列宽总和小于容器时，剩余空间按列宽比例**均匀分摊**到各列（不再单独撑爆邮箱列）；总和大于容器（窄屏）时启用横向滚动。
4. **操作列 `bodyCellClass: 'action-cell'`** + scoped `:deep(.action-cell) { white-space: nowrap }`：按钮组不折行，兜底防止换行导致行高异常。

> 参照 `UsersView.vue` / `ListShowcaseView.vue`：邮箱列 `width: 150`（`ellipsis`）、操作列 `width: 240`、表格 `:scroll="{ x: tableScrollX }"`。1600 视口实测：邮箱列 153px、操作列 245px、无横向滚动、表头与内容对齐。

## 3. 颜色约定（按操作类型固定）

行内按钮一律 `a-button type="text" size="small"`（`type="text"` 为 Arco 文本按钮，无背景、hover 变色），颜色语义固定：

| 语义 | 按钮属性 | 适用操作 |
|---|---|---|
| 主操作 | 默认（主题色，无 `type`/`status`） | 编辑 |
| 警示 | `status="warning"` | 禁用 / 停用（将可用对象置为不可用） |
| 危险 | `status="danger"` | 删除 / 移除（不可逆或破坏性操作） |
| 中性 | 默认（无 `type`/`status`） | 详情、查看、重置密码、启用 / 重新启用等 |

- 「启用 / 重新启用」是恢复操作，**不用** success 色：行内文本按钮无 success 语义位，且与「禁用（warning）」保持视觉对称，降低误读为"主操作"的风险。
- 危险 / 警示操作必须 `a-popconfirm` 二次确认（`type="warning"`，`content` 含对象名，如 `确认删除 {name}？`）。
- 收纳进「更多」的菜单项颜色由 `a-doption` 默认样式承担，**不**给菜单项文字染色；危险 / 警示语义由图标 + 二次确认承担。

## 4. 图标约定（Arco 图标固定映射）

平铺按钮与 `a-doption` 均通过 `#icon` 插槽挂图标；一个操作固定一个图标，不随页面变化：

| 操作 | 图标 |
|---|---|
| 编辑 | `IconEdit` |
| 详情 / 查看 | `IconEye` |
| 删除 | `IconDelete` |
| 启用（恢复启用） | `IconPlayCircle` |
| 禁用 / 停用 | `IconPoweroff` |
| 重置密码 | `IconLock` |
| 导出 | `IconDownload` |
| 更多（收纳触发按钮） | `IconMore` |
| 新增（仅工具条，参照） | `IconPlus` |
| 刷新（仅工具条，参照） | `IconRefresh` |

- 上表未覆盖的操作：选 Arco 语义最近图标，且同一图标在全项目内只对应一个操作（避免「图标 = 操作」的歧义）。
- Arco 图标包无钥匙 / 电源开图标：重置密码取 `IconLock`（密码语义），启用取 `IconPlayCircle`（恢复运行语义，与 `IconPoweroff` 成对）。
- 图标来自 `@arco-design/web-vue/es/icon`，按需具名导入（不整包引入）。

## 5. 呈现与交互要求

1. 按钮顺序：按**主操作 → 中性 → 警示 → 危险**排列（如 编辑 → 详情 → 重置密码 → 禁用 → 删除），「更多」触发按钮恒在末尾。
2. `a-popconfirm` 直接包裹平铺按钮；收纳进「更多」的危险 / 警示操作，`a-doption` 的 `@click` 里打开确认（`a-modal` 或行内二次 `a-popconfirm`），**禁止无确认直接执行**。
3. 行内写操作 loading 遵循 `specs/button-loading/`（前端规则 §4.6）：状态命名为 `xxingId`，平铺按钮 `:loading="xxingId === row.id"`；收纳进「更多」的写操作在确认入口同样判 `xxingId` 互斥，`a-doption :disabled="xxingId === row.id"`。
4. 操作列固定显示，不参与「列设置」勾选隐藏。
5. 行内同步动作（打开抽屉 / 路由跳详情）不置 loading（§4.6 同条款）。
6. 密度：Arco 文本按钮默认水平 padding 为 `0 15px`，相邻操作视觉间距过大。操作列按钮（含纯图标「更多」触发按钮）统一收窄为 `padding: 0 8px`，通过列表页 scoped 样式 `.row-actions :deep(.arco-btn-text), .row-actions :deep(.arco-btn-only-icon)` 实现（`a-space` 加 `class="row-actions"`）。

## 6. 参照实现

### 6.1 `ListShowcaseView.vue`（演示数据，覆盖两种形态）

操作列 4 个演示操作：

| 操作 | 平铺 | 颜色 | 图标 | 行为 |
|---|---|---|---|---|
| 详情 | 是 | 中性 | `IconEye` | `Message.info` 演示提示 |
| 编辑 | 是 | 主操作 | `IconEdit` | `Message.info` 演示提示 |
| 删除 | 是 | 危险 | `IconDelete` | `a-popconfirm` 确认后移除行 |
| 重置密码 | 否（更多） | 中性 | `IconLock` | `a-doption @click` → `Message.info` 演示提示 |

### 6.2 `UsersView.vue`（真实业务对齐）

操作列 4 个操作，平铺顺序：

| 操作 | 平铺 | 颜色 | 图标 | 行为（不变） |
|---|---|---|---|---|
| 编辑 | 是 | 主操作 | `IconEdit` | 打开编辑抽屉 |
| 详情 | 是 | 中性 | `IconEye` | 跳详情页 |
| 禁用 / 启用 | 是 | 警示（禁用态）/ 中性（启用态） | `IconPoweroff` / `IconPlayCircle` | `a-popconfirm` + `togglingId` 行内 loading |
| 重置密码 | 否（更多） | 中性 | `IconLock` | `a-doption @click` 打开重置密码模态 |

- 操作列 `width` 由 260 调为 220（平铺 3 短操作 + 更多按钮）。

## 7. 技术决策

| 决策 | 理由 |
|---|---|
| 阈值取 3 | 2 字中文操作按钮约 60–70px 宽（含图标与间距）：3 个平铺约 200px，4 个即超 260px，列宽收益消失且误点率上升；3 也是业界（Arco Pro、Ant Design 示例）常用阈值 |
| 收纳用「更多」下拉而非隐藏菜单 | 操作可发现性优先；隐藏菜单（`a-ellipsis` 等）降低可发现性，仅适合 5+ 操作且低频的场景，本项目暂无该场景 |
| 启用不用 success 色 | 行内文本按钮无 success 语义位；恢复操作视觉对称性更重要（见 §3） |
| 更多菜单项不染色 | Arco 菜单项不支持 `status` 文本染色；危险语义已由图标 + 二次确认承担，避免自造样式（前端规则 §4） |
| 约定落规则文件而非抽公共组件 | 列表页操作集合各异，`v-for` 数据驱动的抽象收益低于可读性损失；参照实现 + 规则约定 + e2e 兜底足够（与项目「参照实现」模式一致） |

## 8. e2e（`frontend/e2e/list-showcase.spec.ts`）

前置：登录后进入 `/list`。

- 操作列平铺区可见「详情」「编辑」「删除」三个文本按钮，且首行出现「更多」触发按钮。
- 点击「更多」→ 菜单出现含「重置密码」的菜单项；点击菜单项触发演示提示。
- 「删除」平铺按钮带危险样式（`.arco-btn-danger` 类名）；「禁用」类警示样式在用户管理页既有 e2e 中已覆盖行为，不在本规格新增。
- 既有删除用例（popconfirm 确认后行数 -1）保持通过，验证二次确认链路未被破坏。

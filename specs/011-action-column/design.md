---
created: 2026-09-11
updated: 2026-09-16
---

# 设计规格：列表操作列约定（action-column）

> 本规格为**纯前端展示约定**，不涉及后端、不涉及接口变更。
> **约定正文的唯一事实源是本规格 §0**（前端规则 `.codebuddy/rules/frontend/RULE.mdc` §5.2 只留判据与指针）；§1~§4 记录适用范围、技术决策、参照实现与 e2e。

## 0. 约定正文（唯一事实源）

> 适用范围：所有表格类列表页的操作列（`a-table` 末列，`slotName: 'action'`）；页面工具条（页面级操作行）不适用。列宽策略见 `specs/006-list-showcase/design.md` §0；图标来源优先级见 `specs/018-icon-showcase/design.md` §0；行内写操作 loading 见 `specs/010-button-loading/design.md` §0。

**数量：超过 3 个操作收纳进「更多」下拉**

| 操作数 | 呈现 |
|---|---|
| ≤ 3 | 全部平铺（`a-space :size="4"`） |
| > 3 | 保留前 3 个平铺，其余收纳进行内「更多」下拉（`a-dropdown trigger="click"` + `a-doption`，触发按钮 `type="text" size="small"` + `IconDotsVertical`） |

- 平铺选取：常用 / 主操作 + 危险操作优先平铺，低频操作进「更多」；「更多」触发按钮恒在末尾。
- 列宽参考：平铺 ≤ 3 个短操作 `150~220`；含「更多」时 `200~260`。
- 平铺按钮顺序：主操作 → 中性 → 完成 → 警示 → 危险（如单据列表 详情 → 标记已结算 / 改回未结算 → 作废）。

**颜色：按操作类型固定（行内按钮一律 `type="text" size="small"`）**

| 语义 | 按钮属性 | 适用操作 |
|---|---|---|
| 主操作 | 默认 | 编辑 |
| 警示 | `status="warning"` | 禁用 / 停用 |
| 危险 | `status="danger"` | 删除 / 移除 / 单据作废（不可逆） |
| 完成 | `status="success"` | 标记已结算（结算达成，与绿标签同色） |
| 中性 | 默认 | 详情、查看、重置密码、启用 / 重新启用 |
| 次要 | `.action-btn-secondary`（`var(--color-text-2)`） | 改回未结算（反向撤销，视觉降级） |

- 启用是恢复操作，不用 success 色；「改回未结算」降为**次要色**（次级文字灰），既不与「详情」同色，也不突出。结算切换两方向靠颜色（success / 次要）+ 图标（`IconCircleCheck` / `IconArrowBackUp`）+ 文案区分。
- 次要色用 scoped 样式覆盖 `.arco-btn-text` 颜色实现，**不用** `type="secondary"`（灰底实色按钮，在操作列里带背景块、视觉重量过大）。
- 危险 / 警示操作必须 `a-popconfirm` 二次确认。
- 「更多」菜单项不染色（Arco 菜单项无 `status` 语义位），危险语义由图标 + 二次确认承担；收纳进「更多」的危险 / 警示操作 `@click` 打开确认，禁止无确认直接执行。

**图标：一操作一图标（Tabler 图标，`#icon` 插槽，按需具名导入）**

| 操作 | 图标 | 操作 | 图标 |
|---|---|---|---|
| 编辑 | `IconEdit` | 重置密码 | `IconLock` |
| 详情 / 查看 | `IconEye` | 导出 | `IconDownload` |
| 删除 | `IconTrash` | 更多（收纳触发） | `IconDotsVertical` |
| 启用（恢复） | `IconPlayerPlay` | 禁用 / 停用 | `IconPower` |
| 作废（单据作废回冲） | `IconBan` | 结算切换：标记已结算 | `IconCircleCheck` |
| | | 结算切换：改回未结算 | `IconArrowBackUp` |

- 表外操作选 Tabler 语义最近图标，且同一图标全项目只对应一个操作。

**密度**：Arco 文本按钮默认 `padding: 0 15px`，操作列内过宽。操作列按钮（含纯图标「更多」触发按钮）统一 `padding: 0 8px`，列表页 scoped 样式实现（`a-space` 加 `class="row-actions"`）：

```css
.row-actions :deep(.arco-btn-text),
.row-actions :deep(.arco-btn-only-icon) {
  padding: 0 8px;
}
```

**其他**：操作列固定显示，不参与「列设置」勾选；行内写操作 loading 按 `specs/010-button-loading/design.md` §0（`xxingId`），收纳进「更多」的写操作在确认入口同样按 `xxingId` 互斥（`a-doption :disabled`）。

## 1. 适用范围

- 所有表格类列表页的操作列（`a-table` 末列，`slotName: 'action'`）。
- 页面工具条（页面级操作行）不适用：其分组与分隔约定见 `specs/006-list-showcase/design.md` §0「页面布局」。

## 2. 技术决策

| 决策 | 理由 |
|---|---|
| 阈值取 3 | 2 字中文操作按钮约 60–70px 宽（含图标与间距）：3 个平铺约 200px，4 个即超 260px，列宽收益消失且误点率上升；3 也是业界（Arco Pro、Ant Design 示例）常用阈值 |
| 收纳用「更多」下拉而非隐藏菜单 | 操作可发现性优先；隐藏菜单（`a-ellipsis` 等）降低可发现性，仅适合 5+ 操作且低频的场景，本项目暂无该场景 |
| 启用不用 success 色 | 行内文本按钮无 success 语义位；恢复操作与「禁用（warning）」的视觉对称性更重要 |
| 更多菜单项不染色 | Arco 菜单项不支持 `status` 文本染色；危险语义已由图标 + 二次确认承担，避免自造样式（前端规则 §4） |
| 约定落本规格 §0 而非抽公共组件 | 列表页操作集合各异，`v-for` 数据驱动的抽象收益低于可读性损失；参照实现 + 规格约定 + e2e 兜底足够（与项目「参照实现」模式一致） |
| 每列固定 `width` + 表格 `:scroll` | Arco `a-table` 默认 `table-layout: fixed`：唯一无宽度列会吸收全部剩余空间（宽屏被撑到 400px+）；操作列 `width` 小于按钮组实际宽度时 `td` 溢出而表头仍按设定宽渲染，造成表头与内容错位。实测 3 个文本按钮 + 纯图标「更多」合计 **238px**（含 `padding: 0 8px`），故操作列取 **240** |

## 3. 参照实现

### 3.1 `ListShowcaseView.vue`（演示数据，覆盖两种形态）

操作列 4 个演示操作：

| 操作 | 平铺 | 颜色 | 图标 | 行为 |
|---|---|---|---|---|
| 详情 | 是 | 中性 | `IconEye` | `Message.info` 演示提示 |
| 编辑 | 是 | 主操作 | `IconEdit` | `Message.info` 演示提示 |
| 删除 | 是 | 危险 | `IconTrash` | `a-popconfirm` 确认后移除行 |
| 重置密码 | 否（更多） | 中性 | `IconLock` | `a-doption @click` → `Message.info` 演示提示 |

### 3.2 `UsersView.vue`（真实业务对齐）

操作列 4 个操作，平铺顺序：

| 操作 | 平铺 | 颜色 | 图标 | 行为（不变） |
|---|---|---|---|---|
| 编辑 | 是 | 主操作 | `IconEdit` | 打开编辑抽屉 |
| 详情 | 是 | 中性 | `IconEye` | 跳详情页 |
| 禁用 / 启用 | 是 | 警示（禁用态）/ 中性（启用态） | `IconPower` / `IconPlayerPlay` | `a-popconfirm` + `togglingId` 行内 loading |
| 重置密码 | 否（更多） | 中性 | `IconLock` | `a-doption @click` 打开重置密码模态 |

- 列宽按前端规则 §5「列宽策略」设置：邮箱列 `width: 150` + `ellipsis`、操作列 `width: 240`、表格 `:scroll="{ x: tableScrollX }"`。1600 视口实测：邮箱列 153px、操作列 245px、无横向滚动、表头与内容对齐。

## 4. e2e（`frontend/e2e/list-showcase.spec.ts`）

前置：登录后进入 `/list`。

- 操作列平铺区可见「详情」「编辑」「删除」三个文本按钮，且首行出现「更多」触发按钮。
- 点击「更多」→ 菜单出现含「重置密码」的菜单项；点击菜单项触发演示提示。
- 「删除」平铺按钮带危险样式（`.arco-btn-status-danger` 类名；Arco 状态按钮类名统一为 `arco-btn-status-<status>`）；「禁用」类警示样式在用户管理页既有 e2e 中已覆盖行为，不在本规格新增。
- 既有删除用例（popconfirm 确认后行数 -1）保持通过，验证二次确认链路未被破坏。
- 「表头与内容对齐」断言：操作列 `th` 宽 ≥ 按钮组实际宽度，防回退。

# 需求规格：列表操作列约定（action-column）

## 1. 背景

列表页是本项目最高频的页面形态。各列表页的操作列（表格末列，行内操作）目前实现不一致：

- 按钮有无图标、图标是否统一，各行其是；
- 危险操作（删除 / 禁用）有的用红色、有的与普通操作同色，风险识别靠颜色但不统一；
- 操作数量多时（如用户管理 4 个操作）平铺导致列宽过宽、操作密度高、易误点；操作少时又无统一呈现基准。

需要沉淀一套**操作列统一约定**，使所有列表页的操作列在呈现、交互上可预期，并与列表页其他约定（工具条分组、`a-popconfirm` 二次确认、行内 loading）配套。

## 2. 目标

- 明确操作列的操作数量阈值：超过阈值时收纳进「更多」下拉，控制列宽与操作密度。
- 统一操作按钮的**颜色**语义（按操作类型固定）与**图标**映射（Tabler 图标，一操作一图标）。
- 约定正文落在本规格 `design.md` §0（前端规则 `.codebuddy/rules/frontend/RULE.mdc` §5.2 只留判据与指针），并以 `ListShowcaseView.vue` 为参照实现；用户管理列表同步对齐。
- 约定不改变既有交互语义：危险操作仍走 `a-popconfirm`，行内写操作 loading 仍按 `specs/button-loading/` 约定绑定。

## 3. 范围

### 3.1 本期包含

| 项 | 说明 |
|---|---|
| 数量约定 | 操作数 ≤ 3 平铺；> 3 时保留前 3 个，其余收纳进「更多」下拉（`a-dropdown` + `a-doption`） |
| 颜色约定 | 危险 / 警示 / 普通 / 主操作 四类固定按钮颜色语义 |
| 图标约定 | 常用操作 → Tabler 图标固定映射（编辑 / 详情 / 删除 / 启用 / 禁用 / 重置密码 / 导出 / 更多） |
| 呈现约定 | 行内按钮统一 `type="text" size="small"`；`a-popconfirm` 包裹方式 |
| 约定正文 | 本规格 `design.md` §0 操作列小节 |
| 参照对齐 | `ListShowcaseView.vue`（4 个演示操作，覆盖平铺 / 收纳两种形态） |
| 存量对齐 | `UsersView.vue` 操作列（编辑 / 详情 / 重置密码 / 禁用） |
| e2e | `list-showcase.spec.ts` 补操作列形态断言 |

### 3.2 本期不包含

- 不新建通用操作列组件（约定 + 参照实现即可，列表页直接按约定书写）。
- 不改变任何既有业务功能与接口。
- 不涉及工具条（页面级操作行）——其分组约定见 `specs/list-showcase/design.md` §0「页面布局」，不在本规格范围。

## 4. 验收标准

1. 规格 `design.md` §0「操作列约定」含数量阈值、颜色表、图标表、呈现要求，与参照实现一致。
2. `ListShowcaseView.vue` 操作列 4 个操作：平铺「编辑」（`IconEdit`）「详情」（`IconEye`）「删除」（danger + `IconTrash`），「更多」下拉内含「重置密码」（`IconLock`）；操作列不溢出、不挤压其他列。
3. `UsersView.vue` 操作列：平铺「编辑」（`IconEdit`）「详情」（`IconEye`）「禁用」（warning + `IconPower`，已禁用行显示「启用」+ `IconPlayerPlay`），「更多」下拉内含「重置密码」（`IconLock`）；既有编辑 / 详情 / 启停 / 重置密码功能行为不变。
4. 危险操作（删除 / 禁用）保持 `a-popconfirm` 二次确认，确认后行为与改动前一致。
5. 行内写操作 loading（`togglingId` 等）行为不变，收纳进下拉的写操作同样受行内 loading 约束（互斥、只转被点行）。
6. `npm run build`、`npm run lint` 通过；`npm run test:e2e` 中 `list-showcase.spec.ts`、`user-management.spec.ts` 全绿。

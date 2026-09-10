# 需求规格：组合式 API 分区书写规范

## 背景

前端已统一使用 Vue 3 `<script setup lang="ts">` 组合式 API，但现有文件内 `ref` / `computed` / `watch` / 函数 / 常量的书写位置无强制约束，各文件组织方式不一（有的混排、有的按功能块随意分段），可读性差、难以快速定位某类声明。

## 目标

- 定义 `<script setup>` 的**固定分区顺序**，所有组件与 composable 统一遵守。
- 用 ESLint 对可机检的部分做**强制兜底**（import 顺序、声明顺序、模板 ref 命名），违反即报错，优先支持 `--fix` 自动修复。
- 存量 10 个 `.vue` 文件 + `composables/` 按新规范对齐。

## 功能点

1. 规则文件（`.codebuddy/rules/frontend/RULE.mdc`）新增「组合式 API 分区书写规范」章节。
2. ESLint 接入 `eslint-plugin-perfectionist`，启用 import 排序、类型先于变量、script setup 声明顺序、模板 ref 命名规则。
3. 存量 `.vue` / composable 的 script 块按分区顺序重构，加分区注释。

## 验收标准

- [ ] 规则文件包含分区规范章节（11 区顺序 + 模板 ref 命名 + composable 顺序）。
- [ ] `npm run lint` 通过；**import 顺序**（vue 生态 → 第三方 → `@/` 内部）由 `perfectionist/sort-imports` 强制，乱序会报错。
- [ ] `npm run type-check` 通过。
- [ ] 全部 `.vue` 与 `composables/` 文件的 script 块符合分区顺序并带分区注释。
- [ ] `npm run test:e2e` 全量通过（行为不变，纯结构调整）。

> **lint 能力边界**（见 design.md §6）：perfectionist v4 无针对 `<script setup>` 顶层声明分区（ref/computed/watch/methods 顺序）与模板 ref 命名的规则，这两项由规则文档 + code review 把关；lint 强制的是 import 排序。

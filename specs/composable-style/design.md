# 设计规格：组合式 API 分区书写规范

> 规范的**唯一事实源是本规格 §0**（前端规则 `.codebuddy/rules/frontend/RULE.mdc` §4.5 只留判据与指针）；§1 起记录设计要点、lint 落地现状与风险。

## 0. 规范正文（唯一事实源）

**`<script setup>` 固定分区顺序**（自上而下，分区间空行分隔；只允许缺省，不允许乱序或穿插）：

| # | 分区 | 内容 |
|---:|---|---|
| 1 | imports | 全部 `import`（vue 生态 → 第三方 → 本地 `@/`，`type` 导入用 `type` 关键字） |
| 2 | types | 组件局部 `interface` / `type` |
| 3 | props/emits | `defineProps` / `defineEmits` / `defineOptions`（仅组件） |
| 4 | constants | 选项数组、映射表、配置对象、种子数据等纯 `const` |
| 5 | helpers | 私有纯函数（不依赖响应式），如 `cloneData` / `formatDate` / `emptyForm` |
| 6 | stores/composables | `useRouter` / `useRoute` / `useXxxStore()` / `useXxx()` 调用 |
| 7 | reactive state | `ref` / `reactive` / `shallowRef`（含模板 ref） |
| 8 | computed | `computed` 派生状态 |
| 9 | watch | `watch` / `watchEffect` |
| 10 | lifecycle | `onMounted` / `onUnmounted` 等 |
| 11 | methods | 业务函数（事件处理、提交、删除等） |

- 同一分区内「被引用者在前」：`computed` 依赖的 `ref` 在前、派生 `computed` 在后。
- 分区注释：`// —— reactive state ——` 形式；分区内声明 > 1 个或为 methods 区时必须写，单声明小分区可省略。
- **模板 ref 命名强制 `xxxRef`**（如 `formRef`）。
- **composable（`composables/*.ts`）**：`imports → types → constants（含模块级单例 ref，需注释）→ helpers → export function useXxx()`；函数体内同样遵守 reactive state → computed → watch → methods 相对顺序。
- lint 自动强制项：import 分组排序（`perfectionist/sort-imports`）与禁用 `any`（`@typescript-eslint/no-explicit-any`），见 `eslint.config.js`。
- 分区顺序、`types` 先于变量、ref 命名当前无 lint 规则覆盖，由 code review 把关（见 §2）。

## 1. 设计要点（规范未展开的补充）

- `props/emits` 紧跟 imports：让阅读者先看到组件契约，再看内部状态。
- 类型导出（`export interface` 等）放 composable / 独立模块，不放组件文件。
- 模块级单例 `ref`（如 `orders`）本质是"模块状态"，归入 composable 的 `constants` 分区末位，并用注释标明"模块级单例"。
- 空分区直接跳过，不留空注释占位；分区内原有的逐条 `/** ... */` 说明注释保留。

## 2. lint 落地现状（2026-09-16 核对）

`eslint.config.js` 当前**实际强制**的只有两项：

| 规则 | 配置 | 作用 |
|---|---|---|
| `perfectionist/sort-imports` | `type: natural, order: asc`；分组 external → internal(`@/`) → parent/sibling/index；`newlinesBetween: always` | import 顺序与分组 |
| `@typescript-eslint/no-explicit-any` | `error` | 禁用 `any`（对齐前端规则 §7） |

**未被 lint 覆盖**（由 code review 把关）：11 分区顺序、`types` 先于变量、模板 ref 命名。
曾设想的 `vue/template-ref-name` 规则并不存在，`perfectionist/sort-objects` 的 partition 映射方案也未落地——本规范的强制力主要来自规则文档 + review，不来自 lint。

## 3. 不做的事

- 不改动 `<template>` 与 `<style>` 结构。
- 不改动任何业务逻辑、数据流、接口调用。
- 不引入新的状态管理方案。

## 4. 风险

- 分区顺序、`types` 先于变量、ref 命名无 lint 兜底，仅靠文档 + code review，存在随迭代漂移的风险；若需要强约束，须引入自定义 ESLint 规则或独立校验脚本（另立规格，不在本期范围）。

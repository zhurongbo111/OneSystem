# 设计规格：组合式 API 分区书写规范

## 1. `<script setup>` 固定分区顺序

`<script setup lang="ts">` 内部自上而下按以下分区组织，**分区之间用空行分隔**，每区首行写 `// —— <分区名> ——` 注释（空区可省略，但声明必须落在对应分区内，禁止跨区穿插）：

| 序号 | 分区 | 内容 | 说明 |
|---:|---|---|---|
| 1 | `imports` | 所有 `import` | 由 perfectionist 排序：`vue` 生态 → 第三方 → 本地 `@/`，`type` 导入用 `type` 关键字 |
| 2 | `types` | `interface` / `type` 别名 | 组件局部数据模型、表单 State 等；类型导出放 composable |
| 3 | `props/emits` | `defineProps` / `defineEmits` / `defineOptions` | 仅组件文件有；紧跟 imports 之后，便于先了解契约 |
| 4 | `constants` | 模块级常量：选项数组、映射表、配置对象、种子数据 | `const` 纯数据，不含 `ref/computed` |
| 5 | `helpers` | 私有辅助函数（纯函数，不依赖响应式） | 如 `cloneData` / `formatDate` / `emptyForm` / `newKey` |
| 6 | `stores/composables` | `useRouter` / `useRoute` / `useXxxStore()` / `useXxx()` 调用 | 第三方注入与跨模块 composable 的调用结果 |
| 7 | `reactive state` | `ref` / `reactive` / `shallowRef` | 含模板 ref（命名必须 `xxxRef`，见 §3） |
| 8 | `computed` | `computed` | 派生只读状态 |
| 9 | `watch` | `watch` / `watchEffect` | 副作用观察 |
| 10 | `lifecycle` | `onMounted` / `onUnmounted` 等 | 生命周期钩子 |
| 11 | `methods` | 普通函数（事件处理、提交、删除等业务函数） | 可引用 7~10 区声明 |

### 顺序规则

- 严格 1 → 11，**只允许缺省，不允许乱序、不允许穿插**（例如禁止在 methods 之间夹一个 `ref`）。
- 同一分区内按「被引用者在前」排序：`computed` 依赖的 `ref` 在前、派生 `computed` 依赖基础 `computed` 在前。
- 空分区直接跳过，不留空注释占位。

### 分区注释格式

```
// —— reactive state ——
// —— computed ——
// —— methods ——
```

- 仅当该分区有多于 1 个声明、或为 `methods`（业务函数区）时必须写注释；单声明的小分区（如只有一个 `onMounted`）可省略注释。
- 分区内原有的逐条 `/** ... */` 说明注释保留，不受影响。

## 2. composable（`composables/*.ts`）分区顺序

普通 TS 模块，无 props，分区为：

1. `imports`
2. `types`（`export interface` 等）
3. `constants`（`export const` 选项/映射表）
4. `helpers`（模块私有纯函数、种子数据、模块级单例 `ref` 放此区并注释说明）
5. `hook body`：`export function useXxx()` 函数体内部同样遵守 reactive state → computed → watch → methods 的相对顺序。

> 说明：模块级单例 `ref`（如 `orders`）本质是"模块状态"，归入 `constants` 分区末位，用注释标明"模块级单例"。

## 3. 模板 ref 命名

- `<template>` 中 `ref="xxx"` 绑定的响应式变量**必须**命名为 `xxxRef`（如 `formRef`、`tableRef`），与 `eslint-plugin-perfectionist` 的 `vue/template-ref-name` 一致。
- 例外：第三方组件要求固定 ref 名的（如 Arco `a-form` 的 `formRef` 已满足）。

## 4. ESLint 强制项（perfectionist + vue）

| 规则 | 配置 | 目的 |
|---|---|---|
| `perfectionist/sort-imports` | `type: natural, order: asc`，分组 `[builtin, external, internal('@/'), parent, sibling, index]`，`newlinesBetween: always` | import 顺序统一 |
| `perfectionist/sort-objects` | 关闭（对象字面量顺序多为业务语义，不强排） | — |
| `perfectionist/sort-named-imports` | 跟随 sort-imports | 具名导入排序 |
| `perfectionist/sort-adjacent-siblings`（types） | `partitions: [{type: 'ts-type-alias'}, {type: 'ts-interface'}]` 先类型后变量 | types 区前置 |
| `vue/script-setup-uses-vars` 等既有 vue 规则 | 保持 | — |
| `perfectionist/sort-objects` for `<script setup>` 顶层声明 | 用自定义 `partition` 映射 11 区顺序 | **核心**：ref/computed/watch/函数 顺序强制 |

> 核心约束用 `perfectionist/sort-objects` 的 `partition` 参数对 `<script setup>` 顶层 `Statement/VariableDeclaration` 按「分区映射」排序，`ignoreComments: true`。若 `sort-objects` 对 setup 顶层语句支持不足，则退化为 `vue/define-macros-order` + `perfectionist/sort-adjacent-siblings` + 人工 + 规则文档兜底，并在 tasks 中记录。

## 5. 不做的事

- 不改动 `<template>` 与 `<style>` 结构。
- 不改动任何业务逻辑、数据流、接口调用。
- 不引入新的状态管理方案。

## 6. 风险

- `perfectionist` 对 `<script setup>` 顶层语句排序的支持有限，若无法精确到 11 区，则「分区顺序」主要靠规则文档 + code review 约束，lint 仅兜底 import 与 types 顺序。此为可接受降级，已在 tasks 中标注验证点。

# 设计规格：组合式 API 分区书写规范

> 规范的**唯一事实源**是前端规则 `.codebuddy/rules/frontend/RULE.mdc` §4.5（`<script setup>` 11 分区顺序、composable 分区顺序、模板 ref 命名、分区注释格式）。本规格只记录决策理由、lint 落地现状与风险，不重复规则正文。

## 1. 设计要点（规则未展开的补充）

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

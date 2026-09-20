<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <RouterView />
</template>

<style>
html,
body,
#app {
  height: 100%;
  margin: 0;
}

/* Tabler / Lucide 图标默认 24px，在 Arco 按钮与下拉项中收敛到 1em，与 Arco 自带图标同视觉重量 */
.arco-btn svg,
.arco-dropdown-option svg {
  width: 1em;
  height: 1em;
}

/* —— 单据打印视图公共版式（027-erp-export §0.3）：打印视图仅用类名，不重复写打印媒体查询 —— */
.print-view {
  min-height: 100vh;
  background: var(--color-fill-1);
}

.print-toolbar {
  display: flex;
  justify-content: flex-end;
  padding: 12px 16px;
  background: var(--color-bg-2);
}

.print-spin {
  display: block;
  width: 100%;
}

.print-page {
  max-width: 210mm;
  margin: 16px auto;
  padding: 24px 28px;
  background: var(--color-bg-2);
  border-radius: var(--border-radius-medium);
  color: var(--color-text-1);
}

.print-title {
  margin: 0 0 20px;
  font-size: 22px;
  font-weight: 600;
  text-align: center;
  letter-spacing: 4px;
}

.print-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 32px;
  margin-bottom: 16px;
  font-size: 14px;
}

.print-meta-label {
  color: var(--color-text-3);
}

.print-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  table-layout: fixed;
}

.print-table th,
.print-table td {
  padding: 6px 8px;
  border: 1px solid var(--color-border-2);
  word-break: break-all;
}

.print-table th {
  font-weight: 600;
  text-align: center;
  background: var(--color-fill-2);
}

.print-table th.is-right,
.print-table td.is-right {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.print-table th.is-center,
.print-table td.is-center {
  text-align: center;
}

.print-table tbody tr {
  break-inside: avoid;
}

.print-summary {
  display: flex;
  justify-content: flex-end;
  gap: 32px;
  margin-top: 12px;
  font-size: 14px;
  font-weight: 600;
}

.print-footer {
  display: flex;
  justify-content: space-between;
  margin-top: 24px;
  padding-top: 8px;
  border-top: 1px solid var(--color-border-2);
  font-size: 12px;
  color: var(--color-text-3);
}

@media print {
  .print-view {
    min-height: auto;
    background: #fff !important;
  }

  .print-toolbar {
    display: none !important;
  }

  .print-page {
    max-width: none;
    margin: 0;
    padding: 0;
    background: #fff !important;
    border-radius: 0;
  }

  .print-table th {
    background: #fff !important;
  }
}

@page {
  size: A4 portrait;
  margin: 12mm;
}
</style>

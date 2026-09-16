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
</style>

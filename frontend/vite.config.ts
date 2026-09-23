import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  // 启动时一次性预打包全部运行时依赖。若靠按需发现，dev 冷启动后某个页面首次
  // 引到未预打包的依赖会触发「重新预打包 + 整页 reload」，窗口期内模块请求返回
  // 504（Outdated Optimize Dep），懒加载 chunk 失败 → 路由首次导航中断 → 白屏，
  // e2e 表现为偶发的「未登录访问首页未跳转登录页」（URL 停在 /，页面纯白）。
  optimizeDeps: {
    include: [
      'vue',
      'vue-router',
      'pinia',
      'axios',
      '@arco-design/web-vue',
      '@arco-design/web-vue/es/icon',
      '@lucide/vue',
      '@tabler/icons-vue',
    ],
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // dev 联调：/api 转发到后端（见 specs/001-project-scaffold/design.md 3.6）
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})

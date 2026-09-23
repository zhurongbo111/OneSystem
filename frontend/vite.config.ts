import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

import vue from '@vitejs/plugin-vue'

// 启动时一次性预打包的运行时依赖：直接取 package.json 的 dependencies，
// 新增依赖自动纳入，无需手工维护列表。
const { dependencies } = JSON.parse(
  readFileSync(fileURLToPath(new URL('./package.json', import.meta.url)), 'utf8'),
) as { dependencies: Record<string, string> }

/**
 * dependencies 只含包名，补这里的是「根包名之外的子路径入口」。
 * 这类导入不会被包名覆盖，仍需显式声明，否则首次引到时同样会触发重新预打包。
 */
const SUBPATH_DEPS = ['@arco-design/web-vue/es/icon']

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  // 启动时一次性预打包全部运行时依赖。若靠按需发现，dev 冷启动后某个页面首次
  // 引到未预打包的依赖会触发「重新预打包 + 整页 reload」，窗口期内模块请求返回
  // 504（Outdated Optimize Dep），懒加载 chunk 失败 → 路由首次导航中断 → 白屏，
  // e2e 表现为偶发的「未登录访问首页未跳转登录页」（URL 停在 /，页面纯白）。
  optimizeDeps: {
    include: [...Object.keys(dependencies), ...SUBPATH_DEPS],
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

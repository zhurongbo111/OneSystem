import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'
import vueParser from 'vue-eslint-parser'

export default [
  { ignores: ['dist/**', 'node_modules/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  {
    // Vue 单文件组件：用 vue-eslint-parser 解析，TS 代码交给 typescript-eslint
    files: ['**/*.vue'],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tseslint.parser,
        extraFileExtensions: ['.vue'],
        sourceType: 'module',
      },
    },
  },
  {
    rules: {
      // JS 的 no-undef 对 TS/类型不友好，交给 TS 检查
      'no-undef': 'off',
      // 页面组件允许单词名（LoginView / HomeView 已多词，此处放宽）
      'vue/multi-word-component-names': 'off',
      // 项目约定：禁止 any
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
]

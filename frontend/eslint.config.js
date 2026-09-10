import perfectionist from 'eslint-plugin-perfectionist'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'
import vueParser from 'vue-eslint-parser'

import js from '@eslint/js'

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
    plugins: { perfectionist },
  },
  {
    rules: {
      // JS 的 no-undef 对 TS/类型不友好，交给 TS 检查
      'no-undef': 'off',
      // 页面组件允许单词名（LoginView / HomeView 已多词，此处放宽）
      'vue/multi-word-component-names': 'off',
      // 项目约定：禁止 any
      '@typescript-eslint/no-explicit-any': 'error',
      // 组合式 API 书写规范：import 顺序（vue 生态 → 第三方 → @/ 内部 → 相对路径）
      // 分区顺序（ref/computed/watch/methods）见 .codebuddy/rules/frontend/RULE.mdc §4.5，由 review 把关
      'perfectionist/sort-imports': [
        'error',
        {
          type: 'natural',
          order: 'asc',
          // @/ 别名归为 internal 组
          internalPattern: ['@/*'],
          groups: [
            ['external'],
            ['internal'],
            ['parent', 'sibling', 'index'],
          ],
          newlinesBetween: 'always',
          ignoreCase: true,
        },
      ],
    },
  },
]

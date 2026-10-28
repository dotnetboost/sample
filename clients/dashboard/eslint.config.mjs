// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

export default withNuxt({
  rules: {
    'vue/no-multiple-template-root': 'off',
    'vue/max-attributes-per-line': ['error', { singleline: 3 }]
  }
}, {
  // shadcn-vue's components, copied in by its CLI. `--fix` has put them in this repository's style,
  // but two rules would need their code rewritten rather than reformatted — the chart and calendar
  // parts type unovis' and reka-ui's callbacks as `any` — and a component kept close to the registry
  // is one `shadcn-vue diff` can still compare.
  files: ['app/components/ui/**'],
  rules: {
    '@typescript-eslint/no-explicit-any': 'off',
    '@typescript-eslint/no-unused-vars': 'off'
  }
})

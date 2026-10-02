/**
 * The active UI locale and message lookup, reachable from plain functions.
 *
 * `formatBytes`, `fieldLabel` and `problemMessage` are auto-imported helpers called straight from
 * templates and from other helpers. They are not composables, so there is no `setup()` scope for
 * them to reach `useI18n()` through, and threading a locale argument through every call site would
 * put the same expression in a dozen templates. `plugins/ui-language.ts` publishes both here once,
 * before anything renders, and the helpers read them.
 *
 * The ref matters as much as the value: these helpers are called inside `computed`s — the fact
 * list in a file's details, for one — and a plain module variable would leave those computeds with
 * no reason to re-run when the language changed. Reading `locale.value` inside the helper is what
 * makes "1.4MB" become "1.4 م.ب" on the same render as the labels around it.
 *
 * One reader per process, which is true of a client-rendered SPA and would not be true under SSR.
 */
const locale = ref('en')

/** vue-i18n's lookup, stored so the helpers below can reach it. Assigned once, by the plugin. */
let messages: { t: (key: string) => string, te: (key: string) => boolean } | null = null

/** Called by the plugin that keeps the UI locale in step with the reading language. */
export function publishUiLocale(
  code: string,
  lookup?: { t: (key: string) => string, te: (key: string) => boolean }
) {
  locale.value = code

  if (lookup) {
    messages = lookup
  }
}

/**
 * The locale tag handed to `Intl`.
 *
 * Arabic is pinned to Latin digits. Not a style preference: vue-i18n interpolates `{n}` as a plain
 * JavaScript number and has no numbering system to apply, so letting `Intl` choose Arabic-Indic
 * digits would print "١٬٤ م.ب" beside "3 منتجات" on the same card. One numbering system for the
 * whole dashboard, and it has to be the one the interpolated counts already use.
 */
export function intlLocale(): string {
  return locale.value === 'ar' ? 'ar-u-nu-latn' : locale.value
}

/**
 * One message by a key built at runtime, or `fallback` when no bundle has it.
 *
 * Only for keys that cannot be written out: the settings form addresses a group's text by the
 * group's route, so the key does not exist until the group does. Everywhere a key is known,
 * `$t` is used instead and `typedOptionsAndMessages` checks it against en.json.
 *
 * `fallback` is load-bearing twice over — once before the plugin has published anything, and once
 * for a key that is genuinely absent, which is what a settings property with no entry in the
 * bundles is. vue-i18n's `t` answers a missing key with the key itself, and
 * `settings.groups.catalog.fields.pageSize.label` on a form field is worse than `Page size`.
 */
export function uiText(key: string, fallback: string): string {
  // Read explicitly: on the branch where the key is missing, nothing else here touches a ref, and
  // a caller inside a `computed` still needs a reason to re-run when the language changes.
  void locale.value

  return messages?.te(key) ? messages.t(key) : fallback
}

/**
 * vue-i18n's own options, for the things a locale list cannot express.
 *
 * Plural rules are the whole reason this file exists. vue-i18n's built-in rule is English's —
 * one form and an everything-else form — and applying it to Arabic produces text no Arabic reader
 * would write: "2 لغات" where the language has a dual, and the same ending for 3 as for 30. The
 * counts here are small and on screen constantly (files in a role, languages still missing alt
 * text), so getting them wrong is not a corner case.
 */
export default defineI18nConfig(() => ({
  legacy: false,

  // A key missing from ar.json or fr.json renders English rather than its own name. Nothing should
  // be missing — the bundles are complete — but a half-translated label is readable and
  // `settings.groups.catalog.fields.pageSize.label` on screen is not.
  fallbackLocale: 'en',

  pluralRules: {
    /**
     * CLDR's six Arabic categories, in the order the messages are written:
     * `zero | one | two | few | many | other`.
     *
     * Every pluralised key in `ar.json` carries all six branches, because a rule that can return
     * 5 and a message that offers 2 would silently pick nothing.
     */
    ar: (choice: number) => {
      if (choice === 0) return 0
      if (choice === 1) return 1
      if (choice === 2) return 2

      const mod100 = choice % 100

      if (mod100 >= 3 && mod100 <= 10) return 3
      if (mod100 >= 11 && mod100 <= 99) return 4

      return 5
    },

    /**
     * French: `one | other`, where zero takes the singular — "0 produit", not "0 produits".
     * That one difference from English is the entire rule, and it is also the one vue-i18n's
     * default gets wrong, because its two-form rule sends 0 to the plural.
     */
    fr: (choice: number) => (choice === 0 || choice === 1 ? 0 : 1)
  }
}))

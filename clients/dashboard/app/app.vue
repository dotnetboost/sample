<script setup lang="ts">
/**
 * The shell's language and direction, set in one place.
 *
 * `dir` goes on `<html>` rather than on a wrapper element because modals and slideovers are
 * portalled to `document.body`: a direction scoped to a div inside the layout would leave every
 * dialog in the dashboard reading left-to-right while the page behind it read right-to-left.
 * `UApp` is told as well — reka-ui positions its popovers and dropdowns from that value rather
 * than from the DOM attribute.
 *
 * `lang` is the reading language, not the UI locale. They are usually the same, but when they are
 * not it is the content that a screen reader and a spell-checker are looking at.
 */
const { languages, loaded, direction, componentLocale } = useShellLocale()
const language = useLanguage()

/**
 * A remembered language can outlive the language itself.
 *
 * Deleting or deactivating the one you are reading in is allowed — only the default is protected —
 * and the choice survives a reload now, so without this the dashboard would come back every visit
 * with a selector showing a bare `ar`, chrome in Arabic, and content the API had quietly fallen
 * back to the default for. Falling back here is only making the control agree with what the API
 * already decided.
 *
 * `immediate` because the list is usually answered before this runs: a page suspends on its own
 * `useAsyncData`, and the shared `shell-languages` entry resolves during that wait. A watcher
 * without it waits for a change that already happened.
 */
watch(languages, (list) => {
  const active = list.filter(l => l.isActive)

  if (!active.length || active.some(l => l.code === language.value)) {
    return
  }

  language.value = active.find(l => l.isDefault)?.code ?? active[0]!.code
}, { immediate: true })

/**
 * The one writer. `app.vue` is mounted exactly once, which is what makes it the right place —
 * `useShellLocale` is called from three components, and this belongs with the single one.
 *
 * Guarded on the list having answered, so a reload cannot overwrite a known direction with the
 * placeholder `ltr` that stands in while the request is still out. `loaded` rather than the list
 * itself: it is never null now, and an empty list is an answer the shell can also receive.
 */
watch([language, direction], ([code, dir]) => {
  if (loaded.value) {
    rememberLanguage({ code, direction: dir })
  }
}, { immediate: true })

useHead({
  htmlAttrs: {
    lang: language,
    dir: direction
  }
})
</script>

<template>
  <UApp :locale="componentLocale" :dir="direction">
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>

/**
 * The reading language, restored before anything renders.
 *
 * `useLanguage` is `useState`, which lasts exactly as long as the tab does — picking Arabic and
 * reloading put the whole dashboard back into English, which is a strange thing for a tool you
 * work in all day to do.
 *
 * This runs as a plugin rather than inside `useLanguage` for two reasons. A plugin runs once,
 * before the app mounts, so the first paint is already in the remembered language instead of
 * flashing English on the way; and `useLanguage` is called from eight places, so a watcher living
 * in it would be eight watchers writing the same key.
 */
export default defineNuxtPlugin({
  name: 'reading-language',

  setup() {
    const remembered = readRememberedLanguage()

    if (remembered) {
      useLanguage().value = remembered.code
    }
  }
})

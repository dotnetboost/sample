/**
 * Who the dashboard is acting as, restored before anything renders and remembered when it changes.
 *
 * A plugin for the same two reasons `reading-language.ts` is one: it runs once before the app
 * mounts, so the first request a page makes already carries the name rather than 401-ing on the
 * Audit page and recovering a tick later; and `useActor` is read by every call `useApi` makes, so a
 * watcher living in it would be one watcher per caller all writing the same key.
 *
 * Both halves are here, unlike the language — whose direction is only known once
 * `GET api/languages` has answered, which is why `app.vue` writes that one. A name needs nothing
 * from the API to be worth remembering.
 */
export default defineNuxtPlugin({
  name: 'acting-as',

  setup() {
    const actor = useActor()
    const remembered = readRememberedActor()

    if (remembered) {
      actor.value = remembered
    }

    watch(actor, value => rememberActor(value.trim()))
  }
})

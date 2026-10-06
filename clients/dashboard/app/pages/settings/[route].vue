<script setup lang="ts">
/**
 * Any discovered settings group, drawn from its own schema.
 *
 * This is the page that makes discovery real: a `[SettingGroup]` class added in C# gets a tab from
 * `GET /api/settings` and a working form here, with no file of its own. `pages/settings/catalog.vue`
 * used to be exactly this with the route written in.
 *
 * A group needing more than its form — branding, which also has a logo and per-language text —
 * keeps a page of its own. Nuxt matches a static route before a dynamic one, so `branding.vue`
 * wins over this without either knowing about the other.
 */
const route = useRoute()
const { data: groups } = await useSettingGroups()

const group = computed(() =>
  groups.value?.find(g => g.route === route.params.route))
</script>

<template>
  <SettingsGroupForm v-if="group" :group="group" />

  <div v-else class="py-8 text-center text-sm text-muted">
    {{ $t('settings.unknownGroup', { route: route.params.route }) }}
  </div>
</template>

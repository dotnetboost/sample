<script setup lang="ts">
/**
 * Any discovered settings group, drawn from its own schema.
 *
 * This is the page that makes discovery real: a `[SettingGroup]` class added in C# gets a tab from
 * `GET /api/settings` and a working form here, with no file of its own. `pages/settings/catalog.vue`
 * used to be exactly this with the route written in.
 *
 * So did `branding.vue`, for longer, because branding has more than a form — per-language text and
 * a logo — and the logo's owner address was the one thing no endpoint published.
 * `BrandingProjector` answers with `owner` now, so `SettingsGroupForm` draws both of those from the
 * response and there is nothing left for a per-group page to add. Nuxt still matches a static route
 * before a dynamic one, so the escape hatch is there if a group ever needs it; none does.
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

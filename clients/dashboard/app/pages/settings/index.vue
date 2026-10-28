<script setup lang="ts">
/**
 * `/settings` has no content of its own: the tabs are discovered, so which group comes first is the
 * API's answer rather than this dashboard's. It sends the reader to the first one.
 *
 * A redirect rather than rendering the first group here, so the address bar names the group being
 * edited — a settings page you can link to and reload is worth more than one saved round trip.
 */
const { data: groups } = await useSettingGroups()

const first = computed(() => groups.value?.[0]?.route)

watchEffect(() => {
  if (first.value) navigateTo(groupPath(first.value), { replace: true })
})
</script>

<template>
  <div class="py-8 text-center text-sm text-muted-foreground">
    <!-- Only ever seen when the API reports no settings groups at all. -->
    {{ first ? '' : $t('settings.noGroups') }}
  </div>
</template>

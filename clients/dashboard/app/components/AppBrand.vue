<script setup lang="ts">
/**
 * The sidebar header: the store's own logo and name, not the sample's.
 *
 * Both come from the API and neither is hard-coded here, which is the point — the name is a
 * DotNetBoost.Settings value, the logo is a DotNetBoost.Media attachment, and changing either on
 * the Settings page is visible here on the next read. No redeploy, and no restart.
 *
 * Shared with the settings page through `useBranding`'s key, so a save or an attach there refreshes
 * this without the two components knowing about each other.
 */
defineProps<{
  collapsed?: boolean
}>()

const { data: branding } = await useBranding()

/**
 * The configured size, kept inside what the sidebar can actually take.
 *
 * `HeaderLogoSize` is an int the API will accept at any value — enforcing a range server-side would
 * mean installing the FluentValidation provider, which this sample does not. So the render clamps
 * instead: a careless 900 makes the logo large, not the layout broken.
 */
const logoSize = computed(() => {
  const configured = branding.value?.headerLogoSize ?? 28
  return Math.min(Math.max(configured, 16), 64)
})

// The resolved name where the reading language has one, the stored value otherwise — which is
// the default language's, and what every language falls back to.
const name = computed(() =>
  branding.value?.resolved?.storeName || branding.value?.storeName || 'DotNetBoost')
</script>

<template>
  <NuxtLink
    to="/"
    class="flex min-w-0 items-center gap-2 px-1.5 py-2 font-semibold text-highlighted"
    :title="name"
  >
    <img
      v-if="branding?.logo"
      :src="contentUrl(branding.logo.id)"
      :alt="branding.logo.altText ?? name"
      class="shrink-0 rounded object-contain"
      :style="{ width: `${logoSize}px`, height: `${logoSize}px` }"
    >
    <UIcon
      v-else
      name="i-lucide-boxes"
      class="size-5 shrink-0 text-primary"
    />

    <span v-if="!collapsed" class="min-w-0 truncate">
      <span class="block truncate">{{ name }}</span>
      <span class="block text-xs font-normal text-muted">{{ $t('brand.subtitle') }}</span>
    </span>
  </NuxtLink>
</template>

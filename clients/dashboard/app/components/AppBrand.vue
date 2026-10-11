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
 *
 * Until a store attaches one, the framework's own mark stands in — see `logoSrc`.
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

/**
 * The store's logo, or DotNetBoost's own until there is one.
 *
 * A placeholder rather than nothing, because this slot is never empty in practice: a fresh
 * database has no attachment, the Settings page is where you make one, and an empty square beside
 * the name reads as a failed image rather than as an invitation. `/logo.png` is served straight
 * out of `public/`, so it costs no round trip to the API and still draws when the API is down.
 *
 * It is the MARK alone — the D and the rocket — and not the full lockup: this is drawn at 28px by
 * default, where a wordmark is an illegible smudge, and the name is already rendered as text
 * beside it.
 *
 * Both are drawn at `headerLogoSize`, the same setting, so attaching a logo changes the picture
 * and not the layout.
 */
const logoSrc = computed(() =>
  branding.value?.logo ? contentUrl(branding.value.logo.id) : '/logo.png')

// The attachment's own alt text when it has one. The placeholder is the brand it stands for, and
// the link has no other accessible name once the sidebar is collapsed and the text is gone.
const logoAlt = computed(() => branding.value?.logo?.altText ?? name.value)
</script>

<template>
  <NuxtLink
    to="/"
    class="flex min-w-0 items-center gap-2 px-1.5 py-2 font-semibold text-highlighted"
    :title="name"
  >
    <img
      :src="logoSrc"
      :alt="logoAlt"
      class="shrink-0 rounded object-contain"
      :style="{ width: `${logoSize}px`, height: `${logoSize}px` }"
    >

    <span v-if="!collapsed" class="min-w-0 truncate">
      <span class="block truncate">{{ name }}</span>
      <span class="block text-xs font-normal text-muted">{{ $t('brand.subtitle') }}</span>
    </span>
  </NuxtLink>
</template>

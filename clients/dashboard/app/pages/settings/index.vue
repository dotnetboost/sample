<script setup lang="ts">
const group = findSettingsGroup('branding')!

/**
 * Branding's media, which is NOT part of the settings group.
 *
 * `BrandingSettings` holds text and the header logo size — the logo itself is an attachment on the
 * owner ("Branding", "branding"), an owner with no table behind it. So it comes from this
 * application's own endpoint rather than from `GET /api/settings/branding`, and it is not part of
 * the form's Save: an attach or a detach lands immediately, as a product's images do.
 *
 * Read through `useBranding`, which the sidebar header also uses — same key, one fetch. Refreshing
 * it after a save or an attach updates the header, without either component knowing the other
 * exists.
 */
const { data: branding, refresh: refreshBranding } = await useBranding()

/** Mirrors BrandingSettings.MediaOwnerName / .MediaOwnerId on the API. */
const ownerEntityName = 'Branding'
const ownerEntityId = 'branding'
</script>

<template>
  <SettingsGroupForm :group="group" @saved="refreshBranding">
    <template #extra>
      <SettingsBrandingText @saved="refreshBranding" />

      <UPageCard
        variant="subtle"
        class="mt-4 sm:mt-6"
        title="Media"
        description="Not a setting. The logo is a media attachment on an owner that has no table anywhere — which a foreign key could not express, and which the media library is perfectly happy with, because an owner is only ever two strings it never tries to resolve."
      >
        <div class="space-y-3">
          <EntityAttachments
            :entity-name="ownerEntityName"
            :entity-id="ownerEntityId"
            :images="branding?.images ?? []"
            @changed="refreshBranding"
          />

          <p class="text-xs text-muted">
            Outside this form's Save, and outside the group's ETag and audit trail — the three
            things storing a file id in a setting would have bought. Given up for a single source
            of truth: detaching the logo and the store having no logo are the same statement.
          </p>

          <p class="text-xs text-muted">
            Attach one and it appears in the sidebar header immediately, at the size this group's
            <strong>Header logo size</strong> names — one value from DotNetBoost.Settings sizing one
            file from DotNetBoost.Media, joined by nothing but this application.
          </p>
        </div>
      </UPageCard>
    </template>
  </SettingsGroupForm>
</template>

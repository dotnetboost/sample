<script setup lang="ts">
const group = findSettingsGroup('branding')!

const api = useApi()

/**
 * Branding's media, which is NOT part of the settings group.
 *
 * `BrandingSettings` holds text only — the logo is an attachment on the owner
 * ("Branding", "branding"), an owner with no table behind it. So it is read from this
 * application's own endpoint rather than from `GET /api/settings/branding`, and it is not part of
 * the form's Save: an attach or a detach lands immediately, as a product's images do.
 */
const { data: media, refresh: refreshMedia }
  = await useAsyncData('branding-media', () => api.branding.get())

/** Mirrors BrandingSettings.MediaOwnerName / .MediaOwnerId on the API. */
const ownerEntityName = 'Branding'
const ownerEntityId = 'branding'
</script>

<template>
  <SettingsGroupForm :group="group">
    <template #extra>
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
            :images="media?.images ?? []"
            @changed="refreshMedia"
          />

          <p class="text-xs text-muted">
            Outside this form's Save, and outside the group's ETag and audit trail — the three
            things storing a file id in a setting would have bought. Given up for a single source
            of truth: detaching the logo and the store having no logo are the same statement.
          </p>
        </div>
      </UPageCard>
    </template>
  </SettingsGroupForm>
</template>

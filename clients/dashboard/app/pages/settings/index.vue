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
        :title="$t('settings.media.title')"
        :description="$t('settings.media.description')"
      >
        <div class="space-y-3">
          <EntityAttachments
            :entity-name="ownerEntityName"
            :entity-id="ownerEntityId"
            :images="branding?.images ?? []"
            @changed="refreshBranding"
          />

          <p class="text-xs text-muted">
            {{ $t('settings.media.outsideSave') }}
          </p>

          <!--
            `i18n-t` because the sentence names another field on this very form, in bold, and that
            name is itself translated. Interpolating the field's own label keeps the two in step:
            rename it in the bundles and this paragraph follows.
          -->
          <i18n-t
            scope="global"
            keypath="settings.media.sizedBySetting"
            tag="p"
            class="text-xs text-muted"
          >
            <template #field>
              <strong>{{ fieldLabel(group, 'headerLogoSize') }}</strong>
            </template>
          </i18n-t>
        </div>
      </UPageCard>
    </template>
  </SettingsGroupForm>
</template>

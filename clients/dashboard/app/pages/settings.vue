<script setup lang="ts">
useHead({ title: 'Settings — DotNetBoost Sample' })

interface Branding { storeName: string, accentColor: string }
interface Catalog { pageSize: number, showProductsWithoutImages: boolean, currencySymbol: string }

const api = useApi()
const toast = useToast()

const { data: branding, refresh: refreshBranding }
  = await useAsyncData('settings-branding', () => api.settings.get<Branding>('branding'))

const { data: catalog, refresh: refreshCatalog }
  = await useAsyncData('settings-catalog', () => api.settings.get<Catalog>('catalog'))

/**
 * Branding's media, read from the application's own endpoint.
 *
 * It is NOT part of the settings group: the group holds text only, and the logo is an attachment
 * on the owner ("Branding", "branding") — an owner with no table behind it. So it is not part of
 * this form's Save either, exactly as a product's images are not part of the product's Save. An
 * attach or a detach is its own request, landed before the boxes redraw.
 */
const { data: media, refresh: refreshMedia }
  = await useAsyncData('settings-branding-media', () => api.branding.get())

/** Mirrors BrandingSettings.MediaOwnerName / .MediaOwnerId on the API. */
const ownerEntityName = 'Branding'
const ownerEntityId = 'branding'

const saving = ref(false)

async function saveBranding() {
  if (!branding.value) return

  saving.value = true
  try {
    await api.settings.set('branding', branding.value)
    await refreshBranding()
    toast.add({ title: 'Branding saved — no restart, no redeploy', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Save failed', description: String(error), color: 'error' })
  } finally {
    saving.value = false
  }
}

async function saveCatalog() {
  if (!catalog.value) return

  saving.value = true
  try {
    await api.settings.set('catalog', catalog.value)
    await refreshCatalog()
    toast.add({ title: 'Catalog settings saved', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Save failed', description: String(error), color: 'error' })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UDashboardPanel id="settings">
    <template #header>
      <UDashboardNavbar title="Settings">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <LanguageSelect />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UPageCard
        title="Runtime configuration"
        description="Served by MapSettingsEndpoints() from DotNetBoost.Settings.API. You write a plain C# class; the library stores it, caches it and gives you an endpoint to edit it. Changing a value here takes effect on the next read."
        variant="naked"
      />

      <UCard v-if="branding" class="shrink-0">
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="font-semibold">
              Branding
            </h2>
            <UBadge variant="subtle" color="neutral" size="sm">
              Settings × Media
            </UBadge>
          </div>
        </template>

        <div class="space-y-4">
          <UFormField label="Store name">
            <UInput v-model="branding.storeName" class="w-full" />
          </UFormField>

          <UFormField label="Accent colour">
            <div class="flex items-center gap-2">
              <UInput v-model="branding.accentColor" class="w-40" />
              <span
                class="size-8 rounded border border-default"
                :style="{ backgroundColor: branding.accentColor }"
              />
            </div>
          </UFormField>

          <div>
            <p class="mb-2 text-sm font-medium">
              Media
            </p>
            <EntityAttachments
              :entity-name="ownerEntityName"
              :entity-id="ownerEntityId"
              :images="media?.images ?? []"
              @changed="refreshMedia"
            />
            <p class="mt-2 text-xs text-muted">
              Not part of this form's Save — an attach or a detach lands immediately, as a
              product's images do. The branding settings group holds no file id at all, so there is
              nothing to drift out of step with.
            </p>
          </div>
        </div>

        <template #footer>
          <div class="flex items-center justify-between gap-4">
            <p class="text-xs text-muted">
              Two libraries, read independently. The settings group holds the text; the logo is a
              media attachment on an owner with no table behind it.
            </p>
            <UButton :loading="saving" label="Save" @click="saveBranding" />
          </div>
        </template>
      </UCard>

      <UCard v-if="catalog" class="shrink-0">
        <template #header>
          <h2 class="font-semibold">
            Catalog
          </h2>
        </template>

        <div class="space-y-4">
          <UFormField label="Page size">
            <UInput v-model.number="catalog.pageSize" type="number" class="w-32" />
          </UFormField>

          <UFormField label="Currency symbol">
            <UInput v-model="catalog.currencySymbol" class="w-32" />
          </UFormField>

          <UCheckbox
            v-model="catalog.showProductsWithoutImages"
            label="List products that have no image"
          />
        </div>

        <template #footer>
          <div class="flex justify-end">
            <UButton :loading="saving" label="Save" @click="saveCatalog" />
          </div>
        </template>
      </UCard>
    </template>
  </UDashboardPanel>
</template>

<script setup lang="ts">
import type { MediaFile } from '~/types'

useHead({ title: 'Settings — DotNetBoost Sample' })

interface Branding { logoFileId: string, storeName: string, accentColor: string }
interface Catalog { pageSize: number, showProductsWithoutImages: boolean, currencySymbol: string }

const api = useApi()
const toast = useToast()

const { data: branding, refresh: refreshBranding }
  = await useAsyncData('settings-branding', () => api.settings.get<Branding>('branding'))

const { data: catalog, refresh: refreshCatalog }
  = await useAsyncData('settings-catalog', () => api.settings.get<Catalog>('catalog'))

const { data: resolved, refresh: refreshResolved }
  = await useAsyncData('settings-resolved', () => api.branding.get())

const logo = ref<{ id: string, fileName: string } | null>(null)
watch(resolved, value => logo.value = value?.logo ?? null, { immediate: true })

const choosing = ref(false)

function chooseLogo(file: MediaFile) {
  logo.value = { id: file.id, fileName: file.fileName }
}

const saving = ref(false)

async function saveBranding() {
  if (!branding.value) return

  saving.value = true
  try {
    // The logo is picked from the media library, and only its id goes into the setting.
    await api.settings.set('branding', {
      ...branding.value,
      logoFileId: logo.value?.id ?? '00000000-0000-0000-0000-000000000000'
    })
    await Promise.all([refreshBranding(), refreshResolved()])
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

      <UCard v-if="branding">
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

          <UFormField
            label="Logo"
            help="Picked from the media library. Only the GUID is stored in the setting — the whole integration between the two libraries, and no attachment is involved: the logo belongs to the application, not to a row."
          >
            <div class="flex items-start gap-3">
              <div class="flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-dashed border-accented bg-elevated/40">
                <img
                  v-if="logo"
                  :src="contentUrl(logo.id)"
                  :alt="logo.fileName"
                  class="size-full object-cover"
                >
                <UIcon v-else name="i-lucide-image-plus" class="size-7 text-dimmed" />
              </div>

              <div class="min-w-0 flex-1 space-y-2">
                <p v-if="logo" class="truncate text-sm font-medium">
                  {{ logo.fileName }}
                </p>
                <p v-else class="text-sm text-muted">
                  Nothing selected.
                </p>

                <div class="flex flex-wrap gap-2">
                  <UButton
                    icon="i-lucide-images"
                    size="sm"
                    variant="subtle"
                    :label="logo ? 'Change' : 'Choose from library'"
                    @click="choosing = true"
                  />
                  <UButton
                    v-if="logo"
                    icon="i-lucide-x"
                    size="sm"
                    variant="ghost"
                    color="neutral"
                    label="Clear"
                    @click="logo = null"
                  />
                </div>
              </div>
            </div>
          </UFormField>

          <MediaSelectModal v-model:open="choosing" title="Choose a logo" @select="chooseLogo" />

          <div class="rounded bg-elevated p-3 font-mono text-xs">
            LogoFileId = {{ logo?.id ?? '00000000-0000-0000-0000-000000000000' }}
          </div>
        </div>

        <template #footer>
          <div class="flex items-center justify-between gap-4">
            <p class="text-xs text-muted">
              The settings library stores a GUID as it would store any value. Only this
              application knows it means a file.
            </p>
            <UButton :loading="saving" label="Save" @click="saveBranding" />
          </div>
        </template>
      </UCard>

      <UCard v-if="catalog">
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

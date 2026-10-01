<script setup lang="ts">
import type { EntityImage, MediaFile } from '~/types'

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

const logo = ref<EntityImage | null>(null)
watch(resolved, value => logo.value = value?.logo ?? null, { immediate: true })

const choosing = ref(false)

/**
 * A picked file, shaped like an attached one so the same box can draw it.
 *
 * `role` is the box's label, not a media role: nothing is attached here. The logo belongs to the
 * application rather than to a row, so the setting stores the id and the media library never
 * learns it is being used — which is also why clearing the box is just emptying a field, with no
 * detach to make.
 *
 * Alt text is resolved in the same step, because a logo needs one as much as any other image.
 */
async function chooseLogo(file: MediaFile) {
  const text = await api.mediaText.resolve([file.id])

  logo.value = {
    id: file.id,
    fileName: file.fileName,
    contentType: file.contentType,
    size: file.size,
    role: 'logo',
    sortOrder: 0,
    altText: text[file.id]?.altText ?? null,
    caption: text[file.id]?.caption ?? null,
    url: contentUrl(file.id)
  }
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

          <UFormField
            label="Logo"
            help="The same box the Catalog editor uses for a banner — but nothing is attached here. Only the GUID goes into the setting; the media library never learns the logo is in use."
          >
            <EntityRoleBox
              :image="logo"
              role="logo"
              highlight
              replaceable
              @pick="choosing = true"
              @detach="logo = null"
            />
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

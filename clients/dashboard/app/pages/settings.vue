<script setup lang="ts">
const api = useApi()
const toast = useToast()
const config = useRuntimeConfig()

interface Branding { logoFileId: string, storeName: string, accentColor: string }
interface Catalog { pageSize: number, showProductsWithoutImages: boolean, currencySymbol: string }

const { data: branding, refresh: refreshBranding }
  = await useAsyncData('settings-branding', () => api.settings.get<Branding>('branding'))

const { data: catalog, refresh: refreshCatalog }
  = await useAsyncData('settings-catalog', () => api.settings.get<Catalog>('catalog'))

const { data: resolved, refresh: refreshResolved }
  = await useAsyncData('settings-resolved', () => api.branding.get())

const saving = ref(false)
const uploadingLogo = ref(false)

async function saveBranding() {
  if (!branding.value) return
  saving.value = true
  try {
    await api.settings.set('branding', branding.value)
    await Promise.all([refreshBranding(), refreshResolved()])
    toast.add({ title: 'Branding saved — no redeploy', color: 'success' })
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

async function uploadLogo(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  if (file.size > Number(config.public.maxUploadBytes)) {
    toast.add({ title: 'File too large', color: 'error' })
    input.value = ''
    return
  }

  uploadingLogo.value = true
  try {
    await api.branding.uploadLogo(file)
    await Promise.all([refreshBranding(), refreshResolved()])
    toast.add({ title: 'Logo updated', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Upload failed', description: String(error), color: 'error' })
  } finally {
    uploadingLogo.value = false
    input.value = ''
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        Settings
      </h1>
      <p class="mt-1 text-muted">
        Served by <code>MapSettingsEndpoints()</code> from DotNetBoost.Settings.API. Changing a
        value here takes effect on the next read — no restart, no redeploy.
      </p>
    </div>

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

        <UFormField label="Logo" help="Uploaded to DotNetBoost.Media; only its id is stored in the setting.">
          <div class="flex items-center gap-3">
            <img
              v-if="resolved?.logo"
              :src="resolved.logo.url"
              alt="Current logo"
              class="size-12 rounded border border-default object-cover"
            >
            <UButton
              as="label"
              icon="i-lucide-upload"
              variant="subtle"
              :loading="uploadingLogo"
              label="Upload logo"
            >
              <input
                type="file"
                accept="image/*"
                class="hidden"
                @change="uploadLogo"
              >
            </UButton>
          </div>
        </UFormField>

        <div class="rounded bg-elevated p-3 font-mono text-xs">
          LogoFileId = {{ branding.logoFileId }}
        </div>
      </div>

      <template #footer>
        <div class="flex items-center justify-between gap-4">
          <p class="text-xs text-muted">
            The whole integration: a GUID the settings library stored, handed to the media library
            on read. No shared abstraction between them.
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
  </div>
</template>

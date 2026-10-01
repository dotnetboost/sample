<script setup lang="ts">
import type { MediaPage } from '~/types'

useHead({ title: 'Overview — DotNetBoost Sample' })

const api = useApi()

const { data: categories } = await useAsyncData('overview-categories', () => api.categories.list())
const { data: products } = await useAsyncData('overview-products', () => api.products.list())
const { data: languages } = await useAsyncData('overview-languages', () => api.multiLingual.languages())
// The media library's own endpoint, called directly — exactly as useMediaLibrary does.
const { data: library } = await useAsyncData('overview-media', () =>
  $fetch<MediaPage>('/api/media', { query: { take: 1 } }))

const withAltText = computed(() =>
  (products.value ?? []).flatMap(p => p.images).filter(i => i.altText).length)

const imageCount = computed(() =>
  (products.value ?? []).reduce((total, p) => total + p.images.length, 0))
</script>

<template>
  <UDashboardPanel id="overview">
    <template #header>
      <UDashboardNavbar title="Overview">
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
        title="Three libraries, zero references between them"
        description="This application installs DotNetBoost.Settings, DotNetBoost.Media and DotNetBoost.MultiLingual from NuGet. None depends on another, there is no shared kernel package, and no integration package pairs any two of them. Everything here is produced by application code composing them."
        variant="naked"
      />

      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <UPageCard
          icon="i-lucide-folder-tree"
          :title="String(categories?.length ?? 0)"
          description="Categories"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-package"
          :title="String(products?.length ?? 0)"
          description="Products"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-images"
          :title="String(library?.total ?? 0)"
          description="Files in the library"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-languages"
          :title="String(languages?.length ?? 0)"
          description="Languages"
          variant="subtle"
        />
      </div>

      <UCard class="shrink-0">
        <template #header>
          <h2 class="font-semibold">
            Where the composition happens
          </h2>
        </template>

        <div class="space-y-4 text-sm">
          <div>
            <p class="font-medium">
              Data/AppDbContext.cs
            </p>
            <p class="text-muted">
              One PostgreSQL database, one DbContext implementing three independent interfaces, and
              three independent <code>Apply*</code> calls. Remove a library and you delete its
              interface, its DbSets and its one line.
            </p>
          </div>

          <USeparator />

          <div>
            <p class="font-medium">
              Integration/MediaText.cs
            </p>
            <p class="text-muted">
              Per-language alt text for an uploaded file. Both libraries address a subject by
              <code>(entityName, entityId)</code>, so a media file's own GUID becomes the subject
              of translation rows. {{ withAltText }} of {{ imageCount }} attached images have alt
              text. No new table, no integration package.
            </p>
          </div>

          <USeparator />

          <div>
            <p class="font-medium">
              Settings/BrandingSettings.cs
            </p>
            <p class="text-muted">
              A setting whose value is a media file id. The settings library stores a GUID like any
              other value; the application passes it to the media library on read.
            </p>
          </div>
        </div>
      </UCard>

      <UAlert
        icon="i-lucide-languages"
        color="primary"
        variant="subtle"
        title="Switch the language in any page header"
        description="Every product name, description and image alt text is re-read in that language — Arabic flips the whole shell to RTL. The API resolves it from Accept-Language."
      />
    </template>
  </UDashboardPanel>
</template>

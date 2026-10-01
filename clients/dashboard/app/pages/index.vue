<script setup lang="ts">
const api = useApi()

const { data: categories } = await useAsyncData('overview-categories', () => api.catalog.categories())
const { data: products } = await useAsyncData('overview-products', () => api.catalog.products())
const { data: languages } = await useAsyncData('overview-languages', () => api.multiLingual.languages())

const imageCount = computed(() =>
  (products.value ?? []).reduce((total, p) => total + p.images.length, 0))

const withAltText = computed(() =>
  (products.value ?? []).flatMap(p => p.images).filter(i => i.altText).length)
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        Three libraries, zero references between them
      </h1>
      <p class="mt-1 max-w-3xl text-muted">
        This application installs DotNetBoost.Settings, DotNetBoost.Media and
        DotNetBoost.MultiLingual from NuGet. None of them depends on another, there is no shared
        kernel package, and no integration package pairs any two of them. Everything below is
        produced by application code composing them.
      </p>
    </div>

    <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <UPageCard icon="i-lucide-folder-tree" :title="String(categories?.length ?? 0)" description="Categories" />
      <UPageCard icon="i-lucide-package" :title="String(products?.length ?? 0)" description="Products" />
      <UPageCard icon="i-lucide-image" :title="String(imageCount)" description="Images attached" />
      <UPageCard icon="i-lucide-languages" :title="String(languages?.length ?? 0)" description="Languages" />
    </div>

    <UCard>
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
            <code>(entityName, entityId)</code>, so a media file's own GUID becomes the subject of
            translation rows. {{ withAltText }} of {{ imageCount }} images currently have alt text.
            No new table, no integration package.
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
      title="Switch the language in the header"
      description="Every product name, description and image alt text below is re-read in that language — Arabic flips the whole shell to RTL. The API resolves it from Accept-Language."
    />
  </div>
</template>

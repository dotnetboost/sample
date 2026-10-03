<script setup lang="ts">
import type { MediaPage } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.overview') }) })

const api = useApi()

const { data: categories } = await useAsyncData('overview-categories', () => api.categories.list())
/**
 * The overview counts across the catalogue rather than showing a page of it, so this is the one
 * caller that asks for its own page size instead of letting `CatalogSettings.PageSize` decide.
 *
 * `MaxPageSize` on the API caps it at 200, so the two image stats below describe at most that many
 * products — the headline count does not, because it reads `total`. And with
 * `showProductsWithoutImages` turned off, `total` is what the catalogue lists rather than what the
 * table holds, which is the number this panel is about.
 */
const { data: productPage } = await useAsyncData(
  'overview-products', () => api.products.list({ pageSize: 200 }))

const products = computed(() => productPage.value?.items ?? [])
const { data: languages } = await useAsyncData('overview-languages', () => api.multiLingual.languages())
// The media library's own endpoint, called directly — exactly as useMediaLibrary does.
const { data: library } = await useAsyncData('overview-media', () =>
  $fetch<MediaPage>('/api/media', { query: { take: 1 } }))

const withAltText = computed(() =>
  products.value.flatMap(p => p.images).filter(i => i.altText).length)

const imageCount = computed(() =>
  products.value.reduce((total, p) => total + p.images.length, 0))
</script>

<template>
  <UDashboardPanel id="overview">
    <template #header>
      <UDashboardNavbar :title="$t('nav.overview')">
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
        :title="$t('overview.intro.title')"
        :description="$t('overview.intro.description')"
        variant="naked"
      />

      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <UPageCard
          icon="i-lucide-folder-tree"
          :title="String(categories?.length ?? 0)"
          :description="$t('overview.stats.categories')"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-package"
          :title="String(productPage?.total ?? 0)"
          :description="$t('overview.stats.products')"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-images"
          :title="String(library?.total ?? 0)"
          :description="$t('overview.stats.files')"
          variant="subtle"
        />
        <UPageCard
          icon="i-lucide-languages"
          :title="String(languages?.length ?? 0)"
          :description="$t('overview.stats.languages')"
          variant="subtle"
        />
      </div>

      <UCard class="shrink-0">
        <template #header>
          <h2 class="font-semibold">
            {{ $t('overview.composition.title') }}
          </h2>
        </template>

        <!--
          `i18n-t` rather than `$t`, because these sentences have code spans inside them. The
          alternative is either splitting one sentence into three messages — which leaves a
          translator reordering fragments they cannot see the shape of — or rendering the message
          as HTML, which makes every bundle a place markup can arrive from.
        -->
        <div class="space-y-4 text-sm">
          <div>
            <p class="font-medium">
              Data/AppDbContext.cs
            </p>
            <i18n-t
              scope="global"
              keypath="overview.composition.dbContext"
              tag="p"
              class="text-muted"
            >
              <template #apply>
                <code>Apply*</code>
              </template>
            </i18n-t>
          </div>

          <USeparator />

          <div>
            <p class="font-medium">
              Integration/MediaText.cs
            </p>
            <i18n-t
              scope="global"
              keypath="overview.composition.mediaText"
              tag="p"
              class="text-muted"
            >
              <template #pair>
                <code>(entityName, entityId)</code>
              </template>
              <template #withAltText>
                {{ withAltText }}
              </template>
              <template #imageCount>
                {{ imageCount }}
              </template>
            </i18n-t>
          </div>

          <USeparator />

          <div>
            <p class="font-medium">
              Settings/BrandingSettings.cs
            </p>
            <p class="text-muted">
              {{ $t('overview.composition.branding') }}
            </p>
          </div>
        </div>
      </UCard>

      <UAlert
        icon="i-lucide-languages"
        color="primary"
        variant="subtle"
        :title="$t('overview.languageAlert.title')"
        :description="$t('overview.languageAlert.description')"
      />
    </template>
  </UDashboardPanel>
</template>

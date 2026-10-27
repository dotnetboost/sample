<script setup lang="ts">
import { ArrowUpRight, FolderTree, Images, Languages, Package } from '@lucide/vue'
import type { MediaPage } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.overview') }) })

const api = useApi()

const { data: categories } = await useAsyncData(
  'overview-categories', (_nuxtApp, { signal }) => api.categories.list({ signal }))
/**
 * The overview counts across the catalogue rather than showing a page of it, so this is the one
 * caller that asks for its own page size instead of letting `CatalogSettings.PageSize` decide.
 *
 * `MaxPageSize` on the API caps it at 200, so the two image stats below describe at most that many
 * products — the headline count does not, because it reads `total`.
 */
const { data: productPage } = await useAsyncData(
  'overview-products',
  (_nuxtApp, { signal }) => api.products.list({ pageSize: 200 }, { signal }))

const products = computed(() => productPage.value?.items ?? [])
// The one shared language list, so the count follows a language added on the Languages page.
const { languages } = useLanguageList()
// The media library's own endpoint, called directly — exactly as useMediaLibrary does.
const { data: library } = await useAsyncData('overview-media', (_nuxtApp, { signal }) =>
  $fetch<MediaPage>('/api/media', { query: { take: 1 }, signal }))

const withAltText = computed(() =>
  products.value.flatMap(p => p.images).filter(i => i.altText).length)

const imageCount = computed(() =>
  products.value.reduce((total, p) => total + p.images.length, 0))

const activeLanguages = computed(() => languages.value.filter(l => l.isActive))
const defaultLanguage = computed(() => languages.value.find(l => l.isDefault)?.code ?? '—')

/**
 * The four counts, as the `dashboard-01` block's section cards: the number, what it counts, one
 * line that says something about it, and the page it leads to.
 */
const stats = computed(() => [{
  label: t('overview.stats.categories'),
  value: categories.value?.length ?? 0,
  icon: FolderTree,
  hint: t('overview.stats.categoriesHint', {
    count: categories.value?.filter(c => c.banner).length ?? 0
  }),
  to: '/catalog'
}, {
  label: t('overview.stats.products'),
  value: productPage.value?.total ?? 0,
  icon: Package,
  hint: t('overview.stats.productsHint', {
    withAltText: withAltText.value,
    imageCount: imageCount.value
  }),
  to: '/catalog'
}, {
  label: t('overview.stats.files'),
  value: library.value?.total ?? 0,
  icon: Images,
  hint: t('overview.stats.filesHint'),
  to: '/media'
}, {
  label: t('overview.stats.languages'),
  value: languages.value.length,
  icon: Languages,
  hint: t('overview.stats.languagesHint', {
    active: activeLanguages.value.length,
    code: defaultLanguage.value
  }),
  to: '/languages'
}])
</script>

<template>
  <DashboardPage :title="$t('nav.overview')">
    <SectionIntro
      :title="$t('overview.intro.title')"
      :description="$t('overview.intro.description')"
    />

    <!-- The block's section cards: a faint wash of the accent rising from the bottom of each. -->
    <div
      class="grid grid-cols-1 gap-4 *:data-[slot=card]:bg-linear-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs @xl/main:grid-cols-2 @4xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
    >
      <Card v-for="stat in stats" :key="stat.label" class="@container/card">
        <CardHeader>
          <CardDescription>{{ stat.label }}</CardDescription>
          <CardTitle class="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            {{ stat.value }}
          </CardTitle>
          <CardAction>
            <span class="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <component :is="stat.icon" class="size-4" />
            </span>
          </CardAction>
        </CardHeader>
        <CardFooter class="flex items-center justify-between gap-2 text-sm">
          <span class="line-clamp-1 text-muted-foreground">{{ stat.hint }}</span>
          <Button
            as-child
            variant="ghost"
            size="icon-sm"
            class="shrink-0"
          >
            <NuxtLink :to="stat.to" :aria-label="`${$t('overview.stats.open')} ${stat.label}`">
              <ArrowUpRight class="rtl:-scale-x-100" />
            </NuxtLink>
          </Button>
        </CardFooter>
      </Card>
    </div>

    <OverviewActivityChart />

    <div class="grid gap-4 md:gap-6 @4xl/main:grid-cols-3">
      <Card class="@4xl/main:col-span-2">
        <CardHeader>
          <CardTitle>{{ $t('overview.composition.title') }}</CardTitle>
        </CardHeader>

        <!--
          `i18n-t` rather than `$t`, because these sentences have code spans inside them. The
          alternative is either splitting one sentence into three messages — which leaves a
          translator reordering fragments they cannot see the shape of — or rendering the message
          as HTML, which makes every bundle a place markup can arrive from.
        -->
        <CardContent class="space-y-4 text-sm">
          <div class="space-y-1">
            <p class="text-xs font-medium" dir="ltr">
              Infrastructure/Persistence/AppDbContext.cs
            </p>
            <i18n-t
              scope="global"
              keypath="overview.composition.dbContext"
              tag="p"
              class="text-muted-foreground"
            >
              <template #apply>
                <code class="rounded bg-muted px-1 py-0.5 text-xs text-foreground">Apply*</code>
              </template>
            </i18n-t>
          </div>

          <Separator />

          <div class="space-y-1">
            <p class="text-xs font-medium" dir="ltr">
              Application/Composition/MediaText.cs
            </p>
            <i18n-t
              scope="global"
              keypath="overview.composition.mediaText"
              tag="p"
              class="text-muted-foreground"
            >
              <template #pair>
                <code class="rounded bg-muted px-1 py-0.5 text-xs text-foreground">(entityName, entityId)</code>
              </template>
              <template #withAltText>
                {{ withAltText }}
              </template>
              <template #imageCount>
                {{ imageCount }}
              </template>
            </i18n-t>
          </div>

          <Separator />

          <div class="space-y-1">
            <p class="text-xs font-medium" dir="ltr">
              Application/Settings/BrandingSettings.cs
            </p>
            <p class="text-muted-foreground">
              {{ $t('overview.composition.branding') }}
            </p>
          </div>
        </CardContent>
      </Card>

      <Alert class="h-fit border-primary/30 bg-primary/5">
        <Languages class="text-primary" />
        <AlertTitle>{{ $t('overview.languageAlert.title') }}</AlertTitle>
        <AlertDescription>{{ $t('overview.languageAlert.description') }}</AlertDescription>
      </Alert>
    </div>
  </DashboardPage>
</template>

<script setup lang="ts">
import type { ChartConfig } from '~/components/ui/chart'
import type { AuditAction, AuditPage } from '~/types'
import { VisArea, VisAxis, VisLine, VisXYContainer } from '@unovis/vue'
import { ChartCrosshair, ChartTooltip, ChartTooltipContent, componentToString } from '~/components/ui/chart'

/**
 * What the audit trail recorded each day, by action — the `dashboard-01` block's interactive area
 * chart, drawn from DotNetBoost.Auditing instead of the block's made-up visitors.
 *
 * **One read, counted here.** The API has no per-day count, so this asks for the range's entries
 * in a single page and buckets them by day in the browser. A page is capped at the trail's
 * `maxPageSize`, so on a busy trail the chart describes the latest entries rather than all of them
 * — and it says so under the chart, with the API's own `total`, instead of quietly drawing a
 * shorter history as though it were the whole one.
 *
 * Days are the reader's local days, because "yesterday" means the reader's yesterday. The query's
 * lower bound is that local midnight converted to UTC, which is what the API expects.
 */
const { t } = useI18n()
const { maxPageSize } = useAuditEntities()

const ranges = { '7d': 7, '30d': 30, '90d': 90 } as const
type Range = keyof typeof ranges

const range = ref<Range>('30d')

/** Local midnight at the start of the first day in range. */
const start = computed(() => {
  const day = new Date()
  day.setHours(0, 0, 0, 0)
  day.setDate(day.getDate() - (ranges[range.value] - 1))
  return day
})

const { data: page, status } = await useAsyncData(
  'overview-activity',
  (_nuxtApp, { signal }) => $fetch<AuditPage>('/api/audit', {
    query: {
      // UTC with no zone designator — the same form useAuditTrail sends, read by the API as UTC.
      from: start.value.toISOString().slice(0, -1),
      take: maxPageSize.value || undefined,
      includeTotal: true
    },
    signal
  }),
  { watch: [range, maxPageSize] })

interface Day {
  date: number
  created: number
  updated: number
  deleted: number
}

const actionKeys: Record<AuditAction, keyof Omit<Day, 'date'>> = {
  Created: 'created',
  Updated: 'updated',
  Deleted: 'deleted'
}

const localDayKey = (date: Date) =>
  `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`

const days = computed<Day[]>(() => {
  const buckets = new Map<string, Day>()

  for (let i = 0; i < ranges[range.value]; i++) {
    const day = new Date(start.value)
    day.setDate(day.getDate() + i)
    buckets.set(localDayKey(day), { date: day.getTime(), created: 0, updated: 0, deleted: 0 })
  }

  for (const entry of page.value?.entries ?? []) {
    const bucket = buckets.get(localDayKey(new Date(entry.occurredAt)))
    if (bucket) bucket[actionKeys[entry.action]]++
  }

  return [...buckets.values()]
})

const shown = computed(() => page.value?.entries.length ?? 0)
const total = computed(() => page.value?.total ?? shown.value)

const config = computed(() => ({
  created: { label: t('audit.actions.created'), color: 'var(--chart-2)' },
  updated: { label: t('audit.actions.updated'), color: 'var(--primary)' },
  deleted: { label: t('audit.actions.deleted'), color: 'var(--destructive)' }
}) satisfies ChartConfig)

const series = ['created', 'updated', 'deleted'] as const

// Stacked, so each line is drawn at the running total of the series under it.
const stackedLines = series.map((_, i) => (d: Day) =>
  series.slice(0, i + 1).reduce((sum, key) => sum + d[key], 0))

const svgDefs = series.map(key => `
  <linearGradient id="fill-${key}" x1="0" y1="0" x2="0" y2="1">
    <stop offset="5%" stop-color="var(--color-${key})" stop-opacity="0.6" />
    <stop offset="95%" stop-color="var(--color-${key})" stop-opacity="0.05" />
  </linearGradient>`).join('')

const dayLabel = (value: number | Date) =>
  new Date(value).toLocaleDateString(intlLocale(), { month: 'short', day: 'numeric' })

const peak = computed(() => Math.max(1, ...days.value.map(stackedLines[2]!)))
</script>

<template>
  <Card class="@container/card pt-0">
    <CardHeader class="flex flex-wrap items-center gap-3 border-b py-5 sm:flex-row">
      <div class="grid flex-1 gap-1">
        <CardTitle>{{ $t('overview.activity.title') }}</CardTitle>
        <CardDescription>{{ $t('overview.activity.description') }}</CardDescription>
      </div>

      <ToggleGroup
        :model-value="range"
        type="single"
        variant="outline"
        size="sm"
        @update:model-value="value => { if (value) range = value as Range }"
      >
        <ToggleGroupItem
          v-for="(_, key) in ranges"
          :key="key"
          :value="key"
          class="px-3"
        >
          {{ $t(`overview.activity.ranges.${key}`) }}
        </ToggleGroupItem>
      </ToggleGroup>
    </CardHeader>

    <CardContent class="px-2 pt-4 sm:px-6 sm:pt-6">
      <!--
        `dir="ltr"`: time runs left to right on this axis in every language the dashboard ships,
        and an SVG does not mirror itself the way the page around it does.
      -->
      <div dir="ltr">
        <ChartContainer :config="config" class="aspect-auto h-64 w-full" cursor>
          <VisXYContainer
            :data="days"
            :svg-defs="svgDefs"
            :margin="{ left: -24, right: 8 }"
            :y-domain="[0, peak]"
          >
            <VisArea
              :x="(d: Day) => d.date"
              :y="series.map(key => (d: Day) => d[key])"
              :color="(_: Day, i: number) => `url(#fill-${series[i]})`"
              :opacity="0.8"
            />
            <VisLine
              :x="(d: Day) => d.date"
              :y="stackedLines"
              :color="(_: Day, i: number) => config[series[i]!].color"
              :line-width="1.5"
            />
            <VisAxis
              type="x"
              :x="(d: Day) => d.date"
              :tick-line="false"
              :domain-line="false"
              :grid-line="false"
              :num-ticks="6"
              :tick-format="dayLabel"
            />
            <VisAxis
              type="y"
              :num-ticks="3"
              :tick-line="false"
              :domain-line="false"
              :tick-format="(n: number) => Number.isInteger(n) ? String(n) : ''"
            />
            <ChartTooltip />
            <ChartCrosshair
              :x="(d: Day) => d.date"
              :y="stackedLines"
              :template="componentToString(config, ChartTooltipContent, { labelFormatter: dayLabel })"
              :color="(_: Day, i: number) => config[series[i % 3]!].color"
            />
          </VisXYContainer>

          <ChartLegendContent />
        </ChartContainer>
      </div>

      <p v-if="status === 'success' && !shown" class="pt-2 text-center text-sm text-muted-foreground">
        {{ $t('overview.activity.empty') }}
      </p>
      <p v-else-if="total > shown" class="pt-2 text-center text-xs text-muted-foreground">
        {{ $t('overview.activity.capped', { shown, total }) }}
      </p>
    </CardContent>
  </Card>
</template>

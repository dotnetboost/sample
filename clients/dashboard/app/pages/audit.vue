<script setup lang="ts">
import type { Component } from 'vue'
import { ChevronRight, GitCompareArrows, Pencil, PlugZap, Plus, RefreshCw, ScrollText, Trash2 } from '@lucide/vue'
import type { AuditAction, AuditEntry, AuditSort } from '~/types'

/**
 * DotNetBoost.Auditing's trail, as the one page in this dashboard that has to be signed in to.
 *
 * Two things about it are not like the other three libraries' pages, and both are the library's
 * own decisions rather than this sample's:
 *
 *   • **it needs a caller.** `MapAuditEndpoints()` requires an authenticated one by default — a
 *     trail says who did what and when, which is personal data about an organisation's staff — and
 *     the sample keeps that default instead of turning it off. The API's sample scheme signs every
 *     request in as one sample user, so the page is always readable here;
 *   • **translated text is not in it.** Name and Description carry `[AuditIgnore]`, so a save that
 *     only changed Arabic text adds no entry. The rules card says that in the model's own words,
 *     next to the trail, because the alternative is someone concluding the recording is broken.
 */
const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.audit') }) })

const {
  query, entries, entities, maxPageSize, schema,
  pending, page, total, isFiltered, loadError,
  setFilters, setPageSize, goTo, showEdit, reset, reload
} = useAuditTrail()

/**
 * How the rows on screen are ordered.
 *
 * Over this page only, and that is the API's limit rather than a choice: the trail is served newest
 * first with no parameter for another order. So "oldest first" is the oldest of these rows, and
 * page 1 is still the newest entries.
 */
const sort = ref<AuditSort>('newest')

const sortedEntries = computed(() => {
  const rows = [...entries.value]
  // Newest first is the order the API already answered in, so the tiebreak for every other sort.
  const byKey = (key: (entry: AuditEntry) => string) =>
    rows.sort((a, b) => key(a).localeCompare(key(b)) || b.occurredAt.localeCompare(a.occurredAt))

  switch (sort.value) {
    case 'oldest': return rows.reverse()
    case 'entity': return byKey(entry => `${entry.entityName}\u0000${entry.entityId}`)
    case 'action': return byKey(entry => entry.action)
    case 'actor': return byKey(entry => entry.actor ?? '\uffff')
    default: return rows
  }
})

/** Which rows are open, by entry id. */
const expanded = ref<Record<string, boolean>>({})

/** The action's badge: green for a row that appeared, the accent for a change, red for a removal. */
const actionStyles: Record<AuditAction, string> = {
  Created: 'bg-success/15 text-success',
  Updated: 'bg-primary/10 text-primary',
  Deleted: 'bg-destructive/10 text-destructive'
}

const actionIcons: Record<AuditAction, Component> = {
  Created: Plus,
  Updated: Pencil,
  Deleted: Trash2
}

/**
 * The action, in words. Computed rather than looked up by a key built from the value: a key
 * assembled at runtime is one `typedOptionsAndMessages` cannot check, and the three actions are
 * the whole set.
 */
const actionLabels = computed<Record<AuditAction, string>>(() => ({
  Created: t('audit.actions.created'),
  Updated: t('audit.actions.updated'),
  Deleted: t('audit.actions.deleted')
}))

/**
 * Filtering to one entity's key, from a row.
 *
 * The usual way into a trail is "what happened to this one row", and typing a key read off the
 * screen into the box above is the long way round.
 */
function showEntity(entry: AuditEntry) {
  setFilters({ entityName: entry.entityName, entityId: entry.entityId, correlationId: '' })
}

/** Paging closes whatever was open: a row id on one page means nothing on the next. */
async function go(move: () => unknown) {
  expanded.value = {}
  await move()
}

/** "1–25 of 140", or nothing for an empty trail. */
const range = computed(() => {
  if (!total.value) {
    return ''
  }

  const size = query.pageSize

  return t('audit.pager.range', {
    from: (page.value - 1) * size + 1,
    to: Math.min(page.value * size, total.value),
    total: total.value
  })
})
</script>

<template>
  <DashboardPage :title="$t('nav.audit')">
    <template #actions>
      <Button
        variant="ghost"
        size="icon-sm"
        :disabled="pending"
        :aria-label="$t('audit.reload')"
        @click="reload()"
      >
        <RefreshCw :class="{ 'animate-spin': pending }" />
      </Button>
    </template>

    <SectionIntro :title="$t('audit.title')" :description="$t('audit.card.description')">
      <Badge variant="outline" dir="ltr" class="font-mono">
        api/audit
      </Badge>
    </SectionIntro>

    <Alert v-if="loadError" variant="destructive">
      <PlugZap />
      <AlertTitle>{{ $t('audit.loadError') }}</AlertTitle>
      <AlertDescription>{{ loadError }}</AlertDescription>
      <AlertAction>
        <Button variant="outline" size="sm" @click="() => reload()">
          {{ $t('common.tryAgain') }}
        </Button>
      </AlertAction>
    </Alert>

    <template v-else>
      <AuditFilters
        v-model:sort="sort"
        :query="query"
        :entities="entities"
        :max-page-size="maxPageSize"
        :is-filtered="isFiltered"
        :pending="pending"
        @update="setFilters($event)"
        @page-size="setPageSize($event)"
        @reset="reset()"
      />

      <!-- Only with an entity filtered on: "what is recorded for Product" has no answer across
           entities, and one averaged over two would be a statement the model never made. -->
      <AuditEntityRules v-if="schema" :schema="schema" />

      <Alert v-if="query.correlationId" class="border-primary/30 bg-primary/5">
        <GitCompareArrows class="text-primary" />
        <AlertTitle>{{ $t('audit.editFilter.title') }}</AlertTitle>
        <AlertDescription>{{ $t('audit.editFilter.description') }}</AlertDescription>
        <AlertAction>
          <Button variant="outline" size="sm" @click="setFilters({ correlationId: '' })">
            {{ $t('common.clear') }}
          </Button>
        </AlertAction>
      </Alert>

      <Card class="py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead class="w-10 ps-2" />
              <TableHead>{{ $t('audit.columns.occurredAt') }}</TableHead>
              <TableHead>{{ $t('audit.columns.entity') }}</TableHead>
              <TableHead>{{ $t('audit.columns.action') }}</TableHead>
              <TableHead>{{ $t('audit.columns.actor') }}</TableHead>
              <TableHead class="pe-4">
                {{ $t('audit.columns.changes') }}
              </TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            <template v-if="pending && !entries.length">
              <TableRow v-for="n in 6" :key="n">
                <TableCell v-for="c in 6" :key="c" class="first:ps-2 last:pe-4">
                  <Skeleton class="h-5 w-full max-w-28" />
                </TableCell>
              </TableRow>
            </template>

            <TableEmpty v-else-if="!entries.length" :colspan="6">
              <Empty>
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <ScrollText />
                  </EmptyMedia>
                  <EmptyTitle>{{ isFiltered ? $t('audit.emptyFiltered') : $t('audit.empty') }}</EmptyTitle>
                </EmptyHeader>
              </Empty>
            </TableEmpty>

            <template v-for="entry in sortedEntries" v-else :key="entry.id">
              <TableRow :data-state="expanded[entry.id] ? 'selected' : undefined">
                <TableCell class="ps-2">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    :aria-label="$t('audit.expand')"
                    :aria-expanded="!!expanded[entry.id]"
                    @click="expanded[entry.id] = !expanded[entry.id]"
                  >
                    <ChevronRight
                      class="transition-transform rtl:rotate-180"
                      :class="{ 'rotate-90! ': expanded[entry.id] }"
                    />
                  </Button>
                </TableCell>

                <TableCell>
                  <!-- Relative, with the exact instant a hover away: "3 hours ago" is what a trail is
                       read with, and the timestamp is what it is quoted with. Both through
                       `utils/format.ts`, so both are in the UI locale. -->
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <span class="whitespace-nowrap font-medium">
                        {{ formatRelative(entry.occurredAt) }}
                      </span>
                    </TooltipTrigger>
                    <TooltipContent>{{ formatDate(entry.occurredAt) }}</TooltipContent>
                  </Tooltip>
                </TableCell>

                <TableCell>
                  <Button
                    variant="link"
                    class="h-auto p-0 font-mono"
                    :aria-label="$t('audit.filterToEntity', {
                      entity: entry.entityName, id: entry.entityId
                    })"
                    @click="showEntity(entry)"
                  >
                    <span dir="ltr">{{ entry.entityName }}&nbsp;{{ entry.entityId }}</span>
                  </Button>
                </TableCell>

                <TableCell>
                  <Badge :class="actionStyles[entry.action]">
                    <component :is="actionIcons[entry.action]" />
                    {{ actionLabels[entry.action] }}
                  </Badge>
                </TableCell>

                <TableCell>
                  <!--
                    A null actor is nobody — not "system". The library says so where the field is
                    documented, and this is the renderer that has to mean it: nothing was captured,
                    so the entry names no one, and the tooltip says that rather than leaving
                    "unknown" to be read as an account name.
                  -->
                  <Tooltip v-if="!entry.actor">
                    <TooltipTrigger as-child>
                      <span class="italic text-muted-foreground">{{ $t('audit.unknownActor') }}</span>
                    </TooltipTrigger>
                    <TooltipContent>{{ $t('audit.unknownActorTooltip') }}</TooltipContent>
                  </Tooltip>
                  <span v-else dir="ltr">{{ entry.actor }}</span>
                </TableCell>

                <TableCell class="pe-4">
                  <span class="whitespace-nowrap text-muted-foreground">
                    {{ $t('audit.changeCount', entry.changes.length) }}
                  </span>
                </TableCell>
              </TableRow>

              <TableRow v-if="expanded[entry.id]" class="hover:bg-transparent">
                <TableCell :colspan="6" class="bg-muted/30 p-0 whitespace-normal">
                  <AuditEntryChanges :entry="entry" @show-edit="go(() => showEdit($event))" />
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>
      </Card>

      <div v-if="total" class="flex flex-wrap items-center justify-between gap-3">
        <span class="text-sm text-muted-foreground">{{ range }}</span>

        <PagerControls
          v-if="total > query.pageSize"
          :page="page"
          :total="total"
          :items-per-page="query.pageSize"
          :disabled="pending"
          @update:page="go(() => goTo($event))"
        />
      </div>
    </template>
  </DashboardPage>
</template>

<script setup lang="ts">
import { GitCompareArrows, PlugZap, RefreshCw } from '@lucide/vue'
import type { AuditEntry } from '~/types'

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
 * Filtering to one entity's key, from a row: "what happened to this one row" is the usual way into
 * a trail, and typing a key read off the screen into the filter box is the long way round.
 */
function showEntity(entry: AuditEntry) {
  setFilters({ entityName: entry.entityName, entityId: entry.entityId, correlationId: '' })
}
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
      <Badge variant="outline" dir="ltr">
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
        :query="query"
        :entities="entities"
        :is-filtered="isFiltered"
        :pending="pending"
        @update="setFilters($event)"
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

      <AuditTrailTable
        :entries="entries"
        :pending="pending"
        :is-filtered="isFiltered"
        :page="page"
        :total="total"
        :page-size="query.pageSize"
        :max-page-size="maxPageSize"
        @go-to="goTo"
        @set-page-size="setPageSize"
        @show-entity="showEntity"
        @show-edit="showEdit"
      >
        <template #toolbar>
          <AuditActionTabs
            :model-value="query.action"
            @update:model-value="setFilters({ action: $event })"
          />
        </template>
      </AuditTrailTable>
    </template>
  </DashboardPage>
</template>

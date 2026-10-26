<script setup lang="ts">
import { GitCompareArrows } from '@lucide/vue'
import type { AuditEntry } from '~/types'

/**
 * What one entry says moved: a row per property, its value before and after, and the two flags a
 * renderer has to honour.
 *
 * **A redacted change is not a blank one.** `Redact` on a property means the values were withheld
 * rather than stored, so both arrive null — and a null drawn as an empty cell would read as "this
 * field was cleared", which is a different and wrong statement about somebody's data. `isRedacted`
 * is rendered as "not disclosed", with the reason beside it.
 *
 * **A deletion keeps the final values** in `oldValue` with nothing after them, which after the row
 * is gone is the only copy of what it said. An entry with no changes at all is legitimate — a read
 * that did not ask for them, or a deletion recorded without values — and says so rather than
 * drawing an empty table.
 */
const props = defineProps<{
  entry: AuditEntry
}>()

const emit = defineEmits<{
  showEdit: [correlationId: string]
}>()

/** `Created` has no before, `Deleted` has no after: the column is dropped rather than left empty. */
const showOld = computed(() => props.entry.action !== 'Created')
const showNew = computed(() => props.entry.action !== 'Deleted')
</script>

<template>
  <div class="flex flex-col gap-3 px-4 py-3 lg:ps-14">
    <div v-if="!entry.changes.length" class="text-sm text-muted-foreground">
      {{ $t('audit.noChanges') }}
    </div>

    <table v-else class="w-full text-sm">
      <thead class="text-muted-foreground">
        <tr>
          <th scope="col" class="w-1/4 pb-1 text-start font-medium">
            {{ $t('audit.changes.property') }}
          </th>
          <th v-if="showOld" scope="col" class="pb-1 text-start font-medium">
            {{ $t('audit.changes.oldValue') }}
          </th>
          <th v-if="showNew" scope="col" class="pb-1 text-start font-medium">
            {{ $t('audit.changes.newValue') }}
          </th>
        </tr>
      </thead>

      <tbody>
        <tr v-for="change in entry.changes" :key="change.propertyName" class="align-top">
          <!-- The property name is the C# one, which is how the model's own description spells it
               too — so it is quoted rather than translated, like every other identifier here. -->
          <th scope="row" class="py-1 pe-3 text-start font-normal">
            <span dir="ltr" class="font-mono font-medium">{{ change.propertyName }}</span>

            <Tooltip v-if="change.isTruncated">
              <TooltipTrigger as-child>
                <Badge class="ms-2 bg-warning/15 text-warning">
                  {{ $t('audit.truncated') }}
                </Badge>
              </TooltipTrigger>
              <TooltipContent>{{ $t('audit.truncatedTooltip') }}</TooltipContent>
            </Tooltip>
          </th>

          <td v-if="showOld" class="py-1 pe-3">
            <Tooltip v-if="change.isRedacted">
              <TooltipTrigger as-child>
                <span class="italic text-muted-foreground">{{ $t('audit.redacted') }}</span>
              </TooltipTrigger>
              <TooltipContent>{{ $t('audit.redactedTooltip') }}</TooltipContent>
            </Tooltip>
            <!-- `dir="auto"`: a recorded value can be Arabic text in an otherwise English page,
                 or the reverse. -->
            <span v-else-if="change.oldValue !== null" dir="auto" class="break-words">
              {{ change.oldValue }}
            </span>
            <span v-else class="text-muted-foreground/70">{{ $t('common.empty') }}</span>
          </td>

          <td v-if="showNew" class="py-1">
            <Tooltip v-if="change.isRedacted">
              <TooltipTrigger as-child>
                <span class="italic text-muted-foreground">{{ $t('audit.redacted') }}</span>
              </TooltipTrigger>
              <TooltipContent>{{ $t('audit.redactedTooltip') }}</TooltipContent>
            </Tooltip>
            <span v-else-if="change.newValue !== null" dir="auto" class="break-words">
              {{ change.newValue }}
            </span>
            <span v-else class="text-muted-foreground/70">{{ $t('common.empty') }}</span>
          </td>
        </tr>
      </tbody>
    </table>

    <dl class="flex flex-wrap items-center gap-x-6 gap-y-1 text-xs text-muted-foreground">
      <div class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.id') }}</dt>
        <dd dir="ltr" class="font-mono text-foreground">
          {{ entry.id }}
        </dd>
      </div>

      <div class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.occurredAt') }}</dt>
        <dd class="text-foreground">
          {{ formatDate(entry.occurredAt) }}
        </dd>
      </div>

      <!-- Passed through as the string the application stored: the library never looked inside it,
           and neither does this. -->
      <div v-if="entry.metadata" class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.metadata') }}</dt>
        <dd dir="auto" class="font-mono text-foreground">
          {{ entry.metadata }}
        </dd>
      </div>

      <!-- One edit in this application is not always one entry: the API records every entity a
           save touches, and one save can touch several. The correlation id is one per save, which
           is what says they belong together. -->
      <div v-if="entry.correlationId" class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.correlationId') }}</dt>
        <dd dir="ltr" class="font-mono text-foreground">
          {{ entry.correlationId }}
        </dd>
        <Button size="xs" variant="secondary" @click="emit('showEdit', entry.correlationId)">
          <GitCompareArrows />
          {{ $t('audit.entry.showEdit') }}
        </Button>
      </div>
    </dl>
  </div>
</template>

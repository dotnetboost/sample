<script setup lang="ts">
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
  <div class="flex flex-col gap-3 py-1">
    <div v-if="!entry.changes.length" class="text-sm text-muted">
      {{ $t('audit.noChanges') }}
    </div>

    <table v-else class="w-full text-sm">
      <thead class="text-muted">
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
            <span dir="ltr" class="font-mono text-highlighted">{{ change.propertyName }}</span>

            <UTooltip v-if="change.isTruncated" :text="$t('audit.truncatedTooltip')">
              <UBadge
                color="warning"
                variant="subtle"
                size="sm"
                class="ms-2"
                :label="$t('audit.truncated')"
              />
            </UTooltip>
          </th>

          <td v-if="showOld" class="py-1 pe-3">
            <UTooltip v-if="change.isRedacted" :text="$t('audit.redactedTooltip')">
              <span class="italic text-dimmed">{{ $t('audit.redacted') }}</span>
            </UTooltip>
            <!-- `dir="auto"`: a recorded value can be Arabic text in an otherwise English page,
                 or the reverse. -->
            <span v-else-if="change.oldValue !== null" dir="auto" class="break-words">
              {{ change.oldValue }}
            </span>
            <span v-else class="text-dimmed">{{ $t('common.empty') }}</span>
          </td>

          <td v-if="showNew" class="py-1">
            <UTooltip v-if="change.isRedacted" :text="$t('audit.redactedTooltip')">
              <span class="italic text-dimmed">{{ $t('audit.redacted') }}</span>
            </UTooltip>
            <span v-else-if="change.newValue !== null" dir="auto" class="break-words">
              {{ change.newValue }}
            </span>
            <span v-else class="text-dimmed">{{ $t('common.empty') }}</span>
          </td>
        </tr>
      </tbody>
    </table>

    <dl class="flex flex-wrap items-center gap-x-6 gap-y-1 text-xs text-muted">
      <div class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.id') }}</dt>
        <dd dir="ltr" class="font-mono text-default">
          {{ entry.id }}
        </dd>
      </div>

      <div class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.occurredAt') }}</dt>
        <dd class="text-default">
          {{ formatDate(entry.occurredAt) }}
        </dd>
      </div>

      <!-- Passed through as the string the application stored: the library never looked inside it,
           and neither does this. -->
      <div v-if="entry.metadata" class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.metadata') }}</dt>
        <dd dir="auto" class="font-mono text-default">
          {{ entry.metadata }}
        </dd>
      </div>

      <!-- One edit in this application is not always one entry: creating a product with text in
           three languages writes the product's entry and, in a scope of its own, the creation
           trail's. The correlation id is what says they belong together. -->
      <div v-if="entry.correlationId" class="flex items-center gap-2">
        <dt>{{ $t('audit.entry.correlationId') }}</dt>
        <dd dir="ltr" class="font-mono text-default">
          {{ entry.correlationId }}
        </dd>
        <UButton
          size="xs"
          color="neutral"
          variant="subtle"
          icon="i-lucide-git-compare-arrows"
          :label="$t('audit.entry.showEdit')"
          @click="emit('showEdit', entry.correlationId)"
        />
      </div>
    </dl>
  </div>
</template>

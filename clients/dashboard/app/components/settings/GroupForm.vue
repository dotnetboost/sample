<script setup lang="ts">
import type { SettingField, SettingGroup } from '~/types'

const props = defineProps<{
  group: SettingGroup
}>()

const emit = defineEmits<{
  /** The group was written. Lets a tab react to its own values changing — the branding tab
      refreshes the header, which shows the store name and the logo size. */
  saved: []
}>()

const {
  keys, state, pending, saving, dirty, loadError, problem, errors, conflict,
  load, save, reset, retryOnLatest
} = useSettingsGroup(props.group)

async function submit() {
  if (await save()) emit('saved')
}

async function reapply() {
  if (await retryOnLatest()) emit('saved')
}

const revealed = reactive<Record<string, boolean>>({})

function field(key: string): SettingField {
  return props.group.fields[key] ?? {}
}

function label(key: string) {
  return field(key).label ?? humanizeKey(key)
}
</script>

<template>
  <UForm
    :id="`settings-${group.route}`"
    :state="state"
    @submit="submit"
  >
    <UPageCard
      :title="group.label"
      variant="naked"
      orientation="horizontal"
      class="mb-4"
    >
      <template #description>
        <p>{{ group.description }}</p>

        <UBadge
          color="neutral"
          variant="subtle"
          size="sm"
          class="mt-2 font-mono"
          :label="`api/settings/${group.route}`"
        />
      </template>

      <div class="flex items-center gap-2 lg:ms-auto">
        <UButton
          label="Reset"
          color="neutral"
          variant="ghost"
          :disabled="!dirty || saving"
          @click="reset"
        />
        <UButton
          :form="`settings-${group.route}`"
          label="Save changes"
          color="neutral"
          type="submit"
          :loading="saving"
          :disabled="!dirty"
        />
      </div>
    </UPageCard>

    <UAlert
      v-if="loadError"
      color="error"
      variant="subtle"
      icon="i-lucide-plug-zap"
      :title="`Could not load ${group.name}`"
      :description="loadError"
      class="mb-4"
      :actions="[{ label: 'Try again', color: 'neutral', variant: 'outline', onClick: load }]"
    />

    <UAlert
      v-else-if="conflict"
      color="warning"
      variant="subtle"
      icon="i-lucide-git-merge"
      title="These settings changed while you were editing"
      :description="problem"
      class="mb-4"
      :actions="[
        { label: 'Re-apply my changes', color: 'warning', variant: 'solid', loading: saving, onClick: reapply },
        { label: 'Discard mine and reload', color: 'neutral', variant: 'outline', onClick: load }
      ]"
    />

    <UAlert
      v-else-if="problem"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      :description="problem"
      class="mb-4"
    />

    <UPageCard variant="subtle">
      <div v-if="pending" class="flex flex-col gap-6">
        <USkeleton v-for="n in 4" :key="n" class="h-9 w-full" />
      </div>

      <template v-else>
        <template v-for="(key, index) in keys" :key="key">
          <USeparator v-if="index > 0" />

          <UFormField
            :name="key"
            :label="label(key)"
            :description="field(key).description"
            :error="errors[key]"
            class="flex max-sm:flex-col justify-between items-start gap-4"
          >
            <template #label>
              <span class="flex items-center gap-1.5">
                {{ label(key) }}
                <UBadge
                  v-if="field(key).sensitive"
                  color="warning"
                  variant="subtle"
                  size="sm"
                  label="sensitive"
                />
              </span>
            </template>

            <USwitch
              v-if="typeof state[key] === 'boolean'"
              v-model="state[key]"
            />

            <UInputNumber
              v-else-if="typeof state[key] === 'number'"
              v-model="state[key]"
              :min="field(key).min"
              :max="field(key).max"
              :step="field(key).step"
              class="w-full sm:w-56"
            />

            <UInput
              v-else-if="field(key).sensitive"
              v-model="state[key]"
              :type="revealed[key] ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="Not set"
              class="w-full sm:w-72"
              :ui="{ trailing: 'pe-1' }"
            >
              <template #trailing>
                <UButton
                  color="neutral"
                  variant="link"
                  size="sm"
                  :icon="revealed[key] ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="revealed[key] ? `Hide ${label(key)}` : `Show ${label(key)}`"
                  @click="revealed[key] = !revealed[key]"
                />
              </template>
            </UInput>

            <UInput
              v-else
              v-model="state[key]"
              :type="field(key).type ?? 'text'"
              autocomplete="off"
              class="w-full sm:w-72"
            />
          </UFormField>
        </template>
      </template>
    </UPageCard>

    <!--
      Anything the group has that is not a setting. The form is generated from the values the API
      returns, so it cannot know about branding's logo — that is a media attachment, not a property
      of the group. The branding tab fills this; the catalog tab leaves it empty.
    -->
    <slot name="extra" />
  </UForm>
</template>

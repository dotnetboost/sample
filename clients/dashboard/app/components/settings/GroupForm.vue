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

const { defaultLanguage } = useLanguages()
const { t } = useI18n()

/**
 * What a translated field's badge says. The settings row holds the default language's value, so
 * the badge names it — otherwise the box looks like it edits whatever you are reading in.
 */
const defaultLanguageLabel = computed(() =>
  defaultLanguage.value
    ? t('settings.form.defaultLanguageBadge', { code: defaultLanguage.value.code })
    : t('settings.form.defaultLanguageBadgeUnknown'))

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

/**
 * The group's schema, from `GET /api/settings/{route}/schema`.
 *
 * This is what makes the form generated from the C# class rather than from a guess about the shape
 * of a response: the property list, each one's type and default, which are `[Sensitive]`, and the
 * constraints the registered validators enforce.
 */
const api = useApi()
const { data: schema } = await useAsyncData(
  () => `settings-schema-${props.group.route}`,
  () => api.settings.schema(props.group.route),
  { watch: [() => props.group.route] })

const described = computed(() =>
  Object.fromEntries((schema.value?.properties ?? []).map(p => [p.name, p])))

/**
 * How one property is rendered: what the SERVER says about it, plus the two things only a client
 * can know.
 *
 * `min`, `max`, `maxLength`, `pattern` and `sensitive` all come from the API — the bounds used to
 * be hardcoded here and matched nothing in C#. What stays client-side is deliberate:
 *
 *   • `step` is a control's behaviour, not a rule. An integer steps by one; nothing server-side
 *     has an opinion.
 *   • `type` picks the input — `color` for an accent colour. The schema publishes the `pattern`
 *     that validates it, but which widget to draw is presentation.
 *   • `translated` mirrors `[MultiLingual]`, which DotNetBoost.Settings cannot publish: it is
 *     another library's attribute, and the settings engine has never heard of it. This is the one
 *     flag the registry still has to carry, and it is the sample's whole subject in miniature.
 */
function field(key: string): SettingField {
  const own = props.group.fields[key] ?? {}
  const described_ = described.value[key]

  if (!described_) return own

  const c = described_.constraints ?? {}

  return {
    ...own,
    sensitive: described_.sensitive || own.sensitive,
    min: typeof c.min === 'number' ? c.min : undefined,
    max: typeof c.max === 'number' ? c.max : undefined,
    step: own.step ?? (described_.type === 'Int32' ? 1 : undefined)
  }
}

/**
 * The property's label and help text, from the bundles, by group route and property name.
 *
 * A property the bundles say nothing about — one just added to the C# class — still renders: the
 * label falls back to its humanised name and the description to nothing. That is the same promise
 * the generated form makes about the controls themselves.
 */
function label(key: string) {
  return fieldLabel(props.group, key)
}

function description(key: string) {
  return fieldDescription(props.group, key) || undefined
}
</script>

<template>
  <UForm
    :id="`settings-${group.route}`"
    :state="state"
    @submit="submit"
  >
    <UPageCard
      :title="groupLabel(group)"
      variant="naked"
      orientation="horizontal"
      class="mb-4"
    >
      <template #description>
        <p>{{ groupDescription(group) }}</p>

        <UBadge
          color="neutral"
          variant="subtle"
          size="sm"
          dir="ltr"
          class="mt-2 font-mono"
          :label="`api/settings/${group.route}`"
        />
      </template>

      <div class="flex items-center gap-2 lg:ms-auto">
        <UButton
          :label="$t('common.reset')"
          color="neutral"
          variant="ghost"
          :disabled="!dirty || saving"
          @click="reset"
        />
        <UButton
          :form="`settings-${group.route}`"
          :label="$t('common.saveChanges')"
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
      :title="$t('settings.form.loadError', { name: group.name })"
      :description="loadError"
      class="mb-4"
      :actions="[{ label: $t('common.tryAgain'), color: 'neutral', variant: 'outline', onClick: load }]"
    />

    <UAlert
      v-else-if="conflict"
      color="warning"
      variant="subtle"
      icon="i-lucide-git-merge"
      :title="$t('settings.form.conflict.title')"
      :description="problem"
      class="mb-4"
      :actions="[
        { label: $t('settings.form.conflict.reapply'), color: 'warning', variant: 'solid', loading: saving, onClick: reapply },
        { label: $t('settings.form.conflict.discard'), color: 'neutral', variant: 'outline', onClick: load }
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

      <!-- Label over a full-width control, as the catalogue editors do it — rather than the
           label-left/control-right split the Settings dashboard uses, so a field here looks like a
           field anywhere else in this dashboard. -->
      <div v-else class="space-y-4">
        <template v-for="key in keys" :key="key">
          <UFormField
            :name="key"
            :label="label(key)"
            :description="description(key)"
            :error="errors[key]"
          >
            <template #label>
              <span class="flex items-center gap-1.5">
                {{ label(key) }}
                <UBadge
                  v-if="field(key).sensitive"
                  color="warning"
                  variant="subtle"
                  size="sm"
                  :label="$t('settings.form.sensitiveBadge')"
                />
                <UBadge
                  v-if="field(key).translated"
                  color="info"
                  variant="subtle"
                  size="sm"
                  :label="defaultLanguageLabel"
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
              class="w-full"
            />

            <!--
              `dir="auto"` on every box that holds a value rather than a label.

              The shell flips to RTL for Arabic, and the bidi algorithm then lays out a value with
              no strong direction of its own from the right: `#512BD4` renders as `512BD4#`, and a
              file called `321106641.png` as `png.321106641`. Neither is a translation problem —
              both are correct Unicode for text the browser was told is Arabic. `auto` tells it to
              decide per value from the first strongly-directional character, so a store name in
              Arabic still reads right-to-left and a hex colour still reads left-to-right.
            -->
            <UInput
              v-else-if="field(key).sensitive"
              v-model="state[key]"
              dir="auto"
              :type="revealed[key] ? 'text' : 'password'"
              autocomplete="new-password"
              :placeholder="$t('settings.form.notSet')"
              class="w-full"
              :ui="{ trailing: 'pe-1' }"
            >
              <template #trailing>
                <UButton
                  color="neutral"
                  variant="link"
                  size="sm"
                  :icon="revealed[key] ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="revealed[key]
                    ? $t('settings.form.hide', { label: label(key) })
                    : $t('settings.form.reveal', { label: label(key) })"
                  @click="revealed[key] = !revealed[key]"
                />
              </template>
            </UInput>

            <UInput
              v-else
              v-model="state[key]"
              dir="auto"
              :type="field(key).type ?? 'text'"
              autocomplete="off"
              class="w-full"
            />
          </UFormField>
        </template>
      </div>
    </UPageCard>

    <!--
      Anything the group has that is not a setting. The form is generated from the values the API
      returns, so it cannot know about branding's logo — that is a media attachment, not a property
      of the group. The branding tab fills this; the catalog tab leaves it empty.
    -->
    <slot name="extra" />
  </UForm>
</template>

<script setup lang="ts">
import type { EntityImage, MediaOwner, SettingField, SettingGroup } from '~/types'

const props = defineProps<{
  group: SettingGroup
}>()

const emit = defineEmits<{
  /** The group was written. Lets a tab react to its own values changing — the branding tab
      refreshes the header, which shows the store name and the logo size. */
  saved: []
}>()

const { defaultLanguage } = useLanguageList()
const { t } = useI18n()

/**
 * What a translated field's badge says. The settings row holds the default language's value, so
 * the badge names it — otherwise the box looks like it edits whatever you are reading in.
 */
const defaultLanguageLabel = computed(() =>
  defaultLanguage.value
    ? t('settings.form.defaultLanguageBadge', { code: defaultLanguage.value.code })
    : t('settings.form.defaultLanguageBadgeUnknown'))

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
  (_nuxtApp, { signal }) => api.settings.schema(props.group.route, { signal }),
  { watch: [() => props.group.route] })

const described = computed(() =>
  Object.fromEntries((schema.value?.properties ?? []).map(p => [p.name, p])))

/**
 * The group's own property names, which is what the form draws boxes for.
 *
 * From the schema rather than from the keys of the values response: a projector may add keys that
 * belong to the representation and not to the group — branding answers with its logo, a media
 * attachment that is not a setting — and an input must not appear for those.
 */
const propertyNames = computed(() => (schema.value?.properties ?? []).map(p => p.name))

const {
  keys, state, values, pending, saving, dirty, loadError, problem, errors, conflict,
  load, save, reset, retryOnLatest, refreshValues
} = useSettingsGroup(props.group, propertyNames)

/**
 * Whether this group has text in other languages — from its schema, not from its route.
 *
 * `MultiLingualSchemaContributor` publishes `translated` for a property carrying `[MultiLingual]`,
 * so a group gains the per-language card by declaring the attribute in C# and nothing else.
 */
const translatedFields = computed(() =>
  (schema.value?.properties ?? []).filter(p => p.constraints?.translated === true))

/**
 * Where this group's media and translations are filed, if it has any — from `owner` on its own
 * response, which a projector publishes.
 *
 * This is what replaced `pages/settings/branding.vue`. That page existed because the two strings
 * had to be written down somewhere, and nothing the API published told a client what they were:
 * the group list answers with the CLR class name, and `[MediaEntity]` pins the stored owner name
 * away from it on purpose. With the address in the response, every group draws itself here —
 * `EntityAttachments` asks `GET /api/media/roles/{entityName}` for its boxes, and a group with no
 * roles declared gets `200 []` and shows none.
 */
const owner = computed(() => {
  const published = values.value?.owner

  return published
    && typeof published === 'object'
    && 'entityName' in published
    ? published as unknown as MediaOwner
    : null
})

/** Everything attached to that owner, as the same response carries it. */
const images = computed(() =>
  (Array.isArray(values.value?.images) ? values.value.images : []) as unknown as EntityImage[])

/**
 * After a write, re-read anything sharing this group's key.
 *
 * `useBranding` fetches under the group's own route, so saving branding or attaching its logo
 * updates the sidebar header without this component knowing the header exists. For a group
 * nothing else watches, it refreshes nothing and costs nothing.
 */
function refreshGroup() {
  refreshNuxtData(props.group.route)
}

/**
 * An attach, a detach or a reorder landed.
 *
 * Both halves are needed and they refresh different things. `refreshValues()` re-reads the group,
 * which is where the boxes below get their images — `refreshNuxtData` cannot do it, because
 * `values` is a plain ref inside `useSettingsGroup` and not a `useAsyncData` entry. That was the
 * bug this function exists to fix: the attach succeeded, the header updated, and the box went on
 * showing the file that had just been replaced.
 */
async function mediaChanged() {
  await refreshValues()
  refreshGroup()
}

async function submit() {
  if (await save()) {
    refreshGroup()
    emit('saved')
  }
}

async function reapply() {
  if (await retryOnLatest()) {
    refreshGroup()
    emit('saved')
  }
}

const revealed = reactive<Record<string, boolean>>({})

/**
 * How one property is rendered — entirely from what the API says about it.
 *
 * `min`, `max`, `sensitive` and `translated` all come from
 * `GET /api/settings/{route}/schema`, which merges every registered contributor: the bounds and
 * `required` from the group's FluentValidation rules, and `translated` from the application's own
 * `MultiLingualSchemaContributor`, which publishes `[MultiLingual]` — an attribute
 * DotNetBoost.Settings cannot know about because it belongs to another library.
 *
 * The bounds and that flag were both literals in `utils/settings.ts` until recently, matching
 * nothing in C#. `step` is the only thing still decided here, and it is not a rule: an integer
 * steps by one, and no server has an opinion about that.
 */
function field(key: string): SettingField {
  const described = schemaOf(key)

  if (!described) return {}

  const c = described.constraints ?? {}

  return {
    sensitive: described.sensitive,
    translated: c.translated === true,
    min: typeof c.min === 'number' ? c.min : undefined,
    max: typeof c.max === 'number' ? c.max : undefined,
    step: described.type === 'Int32' ? 1 : undefined
  }
}

function schemaOf(key: string) {
  return described.value[key]
}

/**
 * The property's label and help text, from the bundles, by group route and property name.
 *
 * A property the bundles say nothing about — one just added to the C# class — still renders: the
 * label falls back to its humanised name and the description to nothing. That is the same promise
 * the generated form makes about the controls themselves.
 */
function label(key: string) {
  return fieldLabel(props.group.route, key)
}

function description(key: string) {
  return fieldDescription(props.group.route, key) || undefined
}
</script>

<template>
  <UForm
    :id="`settings-${group.route}`"
    :state="state"
    @submit="submit"
  >
    <UPageCard
      :title="groupLabel(group.route, group.name)"
      variant="naked"
      orientation="horizontal"
      class="mb-4"
    >
      <template #description>
        <p>{{ groupDescription(group.route) }}</p>

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
    <!--
      The two halves a settings group can have beyond its form, both decided by what the server
      sent rather than by which page rendered this. `branding.vue` used to be this markup with the
      route and the owner address written into it.
    -->
    <SettingsGroupText
      v-if="translatedFields.length && owner"
      :route="group.route"
      :owner="owner"
      :translated="translatedFields"
      @saved="refreshGroup(); emit('saved')"
    />

    <UPageCard
      v-if="owner"
      variant="subtle"
      class="mt-4 sm:mt-6"
      :title="$t('settings.media.title')"
      :description="$t('settings.media.description')"
    >
      <div class="space-y-3">
        <EntityAttachments
          :entity-name="owner.entityName"
          :entity-id="owner.entityId"
          :images="images"
          @changed="mediaChanged"
        />

        <p class="text-xs text-muted">
          {{ $t('settings.media.outsideSave') }}
        </p>
      </div>
    </UPageCard>

    <slot name="extra" />
  </UForm>
</template>

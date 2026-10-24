<script setup lang="ts">
/**
 * Who the dashboard is acting as, in the sidebar footer beside the appearance menu.
 *
 * The name goes out as `X-Sample-Actor` on every call `useApi` makes, which is what makes the
 * catalogue's own writes attributable and the Audit page readable at all — the audit endpoints
 * require an authenticated caller, DotNetBoost.Auditing's own default, and this sample keeps it.
 *
 * **It asks rather than assuming.** The dashboard knows nobody, so there is no default name here
 * and nothing is invented: acting as nobody is the starting state, catalogue changes made that way
 * are recorded with no actor, and the trail says `unknown` rather than naming a person who does
 * not exist. Inventing a plausible-looking identity would put it in the audit trail of every
 * change anyone makes, which is the one place a plausible lie does real damage.
 *
 * **And it does not pretend to be a sign-in.** The API trusts the header outright, so the help
 * text says so where someone typing a name will read it, and the control is a text box rather than
 * a padlock and a password field dressed up as security.
 */
const actor = useActor()

const open = ref(false)

defineProps<{
  collapsed?: boolean
}>()

/** The box's own value, applied on submit — so a half-typed name is not sent as a header. */
const draft = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    draft.value = actor.value
  }
})

function apply() {
  actor.value = draft.value.trim()
  open.value = false
}

function clear() {
  actor.value = ''
  draft.value = ''
  open.value = false
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'center', collisionPadding: 12 }">
    <UButton
      :icon="actor ? 'i-lucide-user-round' : 'i-lucide-user-round-x'"
      :label="collapsed ? undefined : (actor || $t('actingAs.nobody'))"
      :trailing-icon="collapsed ? undefined : 'i-lucide-chevrons-up-down'"
      color="neutral"
      variant="ghost"
      block
      :square="collapsed"
      class="data-[state=open]:bg-elevated"
      :aria-label="$t('actingAs.trigger')"
      :ui="{ label: 'truncate', trailingIcon: 'text-dimmed' }"
    />

    <template #content>
      <form class="flex w-72 flex-col gap-3 p-3" @submit.prevent="apply">
        <UFormField
          name="actor"
          :label="$t('actingAs.label')"
          :description="$t('actingAs.help')"
        >
          <!-- `dir="ltr"`: an actor is an identifier the API stores verbatim — a login or an
               address — and it is the same string on a right-to-left page. -->
          <UInput
            v-model="draft"
            dir="ltr"
            autocomplete="off"
            icon="i-lucide-user-round"
            :placeholder="$t('actingAs.placeholder')"
            class="w-full font-mono"
          />
        </UFormField>

        <UAlert
          color="warning"
          variant="subtle"
          icon="i-lucide-shield-alert"
          :title="$t('actingAs.notAuthentication.title')"
          :description="$t('actingAs.notAuthentication.description')"
          :ui="{ title: 'text-xs', description: 'text-xs' }"
        />

        <div class="flex justify-end gap-2">
          <UButton
            v-if="actor"
            :label="$t('actingAs.clear')"
            color="neutral"
            variant="ghost"
            @click="clear"
          />
          <UButton
            type="submit"
            :label="$t('actingAs.apply')"
            :disabled="draft.trim() === actor"
          />
        </div>
      </form>
    </template>
  </UPopover>
</template>

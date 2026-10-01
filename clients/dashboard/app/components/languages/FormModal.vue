<script setup lang="ts">
import type { FormError } from '@nuxt/ui'
import type { Language, TextDirection } from '~/types'

/**
 * Adds a language, or edits one when `language` is given. The code is the language's identity, so
 * it can only be chosen on the way in.
 */
const props = defineProps<{
  language?: Language
}>()

const open = defineModel<boolean>('open', { default: false })

const form = useTemplateRef('form')

const { languages, create, update, busy } = useLanguages()

type Direction = TextDirection | 'auto'

const state = reactive({
  code: '',
  name: '',
  direction: 'auto' as Direction,
  isActive: true,
  isDefault: false
})

const editing = computed(() => !!props.language)
const first = computed(() => !editing.value && languages.value.length === 0)

const directions = [
  { label: 'From the code', value: 'auto' },
  { label: 'Left to right', value: 'ltr' },
  { label: 'Right to left', value: 'rtl' }
]

watch(open, (isOpen) => {
  if (!isOpen) {
    return
  }

  Object.assign(state, props.language
    ? {
        code: props.language.code,
        name: props.language.name,
        direction: props.language.direction,
        isActive: props.language.isActive,
        isDefault: props.language.isDefault
      }
    : { code: '', name: '', direction: 'auto', isActive: true, isDefault: false })
})

// The API validates too, and its word is final. These only save a round trip for the obvious.
function validate(values: typeof state): FormError[] {
  const errors: FormError[] = []

  if (!values.code.trim()) {
    errors.push({ name: 'code', message: 'Required.' })
  }

  if (!values.name.trim()) {
    errors.push({ name: 'name', message: 'Required.' })
  } else if (values.name.trim().length > 128) {
    errors.push({ name: 'name', message: 'At most 128 characters.' })
  }

  return errors
}

/** Enter submits, except mid-composition, where it belongs to the input method. */
function onEnter(event: KeyboardEvent) {
  if (event.isComposing) {
    return
  }

  event.preventDefault()
  form.value?.submit()
}

async function submit() {
  const direction = state.direction === 'auto' ? undefined : state.direction

  const saved = props.language
    ? await update(props.language.code, {
        name: state.name.trim(),
        direction,
        isActive: state.isActive
      })
    : await create({
        code: state.code.trim(),
        name: state.name.trim(),
        direction,
        isActive: state.isActive,
        isDefault: state.isDefault
      })

  if (saved) {
    open.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="editing ? `Edit ${language?.name}` : 'Add a language'"
    :description="editing
      ? 'The code is the language\'s identity and cannot change; everything written in it is keyed by it.'
      : 'A new language starts with no translations. Existing content falls back to the default until each row is filled in.'"
  >
    <template #body>
      <!-- The submit button sits in the modal footer, outside the form, so Enter is wired up here. -->
      <UForm
        id="language-form"
        ref="form"
        :state="state"
        :validate="validate"
        class="flex flex-col gap-4"
        @submit="submit"
        @keydown.enter="onEnter"
      >
        <UFormField
          name="code"
          label="Code"
          description="ISO 639-1, optionally with a region: en, ar, pt-BR."
        >
          <UInput
            v-model="state.code"
            placeholder="es"
            :disabled="editing"
            autocomplete="off"
            class="w-full font-mono"
          />
        </UFormField>

        <UFormField
          name="name"
          label="Name"
          description="Shown in the language picker, written in the language itself."
        >
          <UInput
            v-model="state.name"
            :dir="state.direction === 'auto' ? 'auto' : state.direction"
            placeholder="Español"
            autocomplete="off"
            class="w-full"
          />
        </UFormField>

        <UFormField
          name="direction"
          label="Direction"
          :description="editing
            ? 'Changing this flips the shell for everyone reading in this language.'
            : 'Left as “from the code”, the API takes it from the runtime\'s culture data — ar, he, fa and ur come back right to left.'"
        >
          <USelect
            v-model="state.direction"
            :items="directions"
            value-key="value"
            class="w-full"
          />
        </UFormField>

        <USeparator />

        <UFormField
          name="isActive"
          label="Active"
          description="When off, content is no longer served in it. The default language cannot be deactivated."
          class="flex items-center justify-between gap-4"
        >
          <USwitch v-model="state.isActive" :disabled="language?.isDefault" />
        </UFormField>

        <UFormField
          v-if="!editing"
          name="isDefault"
          label="Make it the default"
          :description="first
            ? 'The first language created is always the default — there is nothing to fall back to otherwise.'
            : 'Demotes whichever language holds the role now.'"
          class="flex items-center justify-between gap-4"
        >
          <USwitch
            :model-value="first || state.isDefault"
            :disabled="first"
            @update:model-value="state.isDefault = $event"
          />
        </UFormField>

        <!-- Lets Enter submit, since the visible button is outside the form. -->
        <button
          type="submit"
          class="sr-only"
          tabindex="-1"
          aria-hidden="true"
        />
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
        <UButton
          form="language-form"
          type="submit"
          :label="editing ? 'Save' : 'Add language'"
          :loading="busy !== null"
        />
      </div>
    </template>
  </UModal>
</template>

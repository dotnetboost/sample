<script setup lang="ts">
import type { Language, TextDirection } from '~/types'

/**
 * Adds a language, or edits one when `language` is given. The code is the language's identity, so
 * it can only be chosen on the way in.
 */
const props = defineProps<{
  language?: Language
}>()

const open = defineModel<boolean>('open', { default: false })

const { languages, create, update, busy } = useLanguages()
const { t } = useI18n()

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

const directions = computed(() => [
  { label: t('languages.form.direction.auto'), value: 'auto' },
  { label: t('languages.form.direction.ltr'), value: 'ltr' },
  { label: t('languages.form.direction.rtl'), value: 'rtl' }
])

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

  errors.code = errors.name = undefined
})

/** The field-level messages from the last submit, by field. Cleared as soon as the dialog reopens. */
const errors = reactive<{ code?: string, name?: string }>({})

// The API validates too, and its word is final. These only save a round trip for the obvious.
function validate() {
  errors.code = state.code.trim() ? undefined : t('languages.form.validation.required')

  errors.name = !state.name.trim()
    ? t('languages.form.validation.required')
    : state.name.trim().length > 128 ? t('languages.form.validation.nameTooLong') : undefined

  return !errors.code && !errors.name
}

/**
 * The buttons are inside the `<form>` this time, so Enter submits natively — and a browser does not
 * submit on the Enter that commits an input method's composition, which the old handler had to
 * check for by hand.
 */
async function submit() {
  if (!validate()) {
    return
  }

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
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-lg">
      <form class="grid gap-6" novalidate @submit.prevent="submit">
        <DialogHeader>
          <DialogTitle>
            {{ editing
              ? $t('languages.form.titleEdit', { name: language?.name ?? '' })
              : $t('languages.form.titleAdd') }}
          </DialogTitle>
          <DialogDescription>
            {{ editing ? $t('languages.form.descriptionEdit') : $t('languages.form.descriptionAdd') }}
          </DialogDescription>
        </DialogHeader>

        <FieldGroup class="gap-5">
          <Field :data-invalid="!!errors.code || undefined">
            <FieldLabel for="language-code">
              {{ $t('languages.form.code.label') }}
            </FieldLabel>
            <Input
              id="language-code"
              v-model="state.code"
              dir="ltr"
              :placeholder="$t('languages.form.code.placeholder')"
              :disabled="editing"
              :aria-invalid="!!errors.code || undefined"
              autocomplete="off"
              class="font-mono"
            />
            <FieldDescription>{{ $t('languages.form.code.description') }}</FieldDescription>
            <FieldError v-if="errors.code">
              {{ errors.code }}
            </FieldError>
          </Field>

          <Field :data-invalid="!!errors.name || undefined">
            <FieldLabel for="language-name">
              {{ $t('languages.form.name.label') }}
            </FieldLabel>
            <Input
              id="language-name"
              v-model="state.name"
              :dir="state.direction === 'auto' ? 'auto' : state.direction"
              :placeholder="$t('languages.form.name.placeholder')"
              :aria-invalid="!!errors.name || undefined"
              autocomplete="off"
            />
            <FieldDescription>{{ $t('languages.form.name.description') }}</FieldDescription>
            <FieldError v-if="errors.name">
              {{ errors.name }}
            </FieldError>
          </Field>

          <Field>
            <FieldLabel for="language-direction">
              {{ $t('languages.form.direction.label') }}
            </FieldLabel>
            <Select v-model="state.direction">
              <SelectTrigger id="language-direction" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="item in directions" :key="item.value" :value="item.value">
                  {{ item.label }}
                </SelectItem>
              </SelectContent>
            </Select>
            <FieldDescription>
              {{ editing
                ? $t('languages.form.direction.descriptionEdit')
                : $t('languages.form.direction.descriptionNew') }}
            </FieldDescription>
          </Field>

          <FieldSeparator />

          <Field orientation="horizontal">
            <FieldContent>
              <FieldLabel for="language-active">
                {{ $t('languages.form.active.label') }}
              </FieldLabel>
              <FieldDescription>{{ $t('languages.form.active.description') }}</FieldDescription>
            </FieldContent>
            <Switch id="language-active" v-model="state.isActive" :disabled="language?.isDefault" />
          </Field>

          <Field v-if="!editing" orientation="horizontal">
            <FieldContent>
              <FieldLabel for="language-default">
                {{ $t('languages.form.makeDefault.label') }}
              </FieldLabel>
              <FieldDescription>
                {{ first
                  ? $t('languages.form.makeDefault.descriptionFirst')
                  : $t('languages.form.makeDefault.descriptionOther') }}
              </FieldDescription>
            </FieldContent>
            <Switch
              id="language-default"
              :model-value="first || state.isDefault"
              :disabled="first"
              @update:model-value="state.isDefault = $event"
            />
          </Field>
        </FieldGroup>

        <DialogFooter>
          <Button type="button" variant="ghost" @click="open = false">
            {{ $t('common.cancel') }}
          </Button>
          <Button type="submit" :disabled="busy !== null">
            <Spinner v-if="busy !== null" />
            {{ editing ? $t('common.save') : $t('languages.add') }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>

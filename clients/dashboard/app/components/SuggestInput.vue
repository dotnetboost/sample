<script setup lang="ts">
import type { Component } from 'vue'
import { Check, Plus, X } from '@lucide/vue'
import {
  ComboboxAnchor,
  ComboboxContent,
  ComboboxInput,
  ComboboxItem,
  ComboboxItemIndicator,
  ComboboxPortal,
  ComboboxRoot,
  ComboboxViewport,
  useFilter
} from 'reka-ui'

/**
 * A text box that suggests values it has seen, and takes one it has not — what `UInputMenu` with
 * `create-item` was under Nuxt UI. shadcn-vue's combobox is a pick-from-a-list control; the media
 * filters need a free one, because their options are only the categories and tags on the pages
 * loaded so far, and the one you want may be on a page you have not opened.
 *
 * Built on reka-ui's combobox, which is what shadcn-vue's own is built on, and styled to match its
 * input group and popover. Typing shows the matches and, when the text is not one of them, a "use
 * this" row at the top; Enter takes whichever row is highlighted.
 */
const props = defineProps<{
  options: string[]
  placeholder?: string
  icon?: Component
}>()

const model = defineModel<string>({ required: true })

const text = ref(model.value)

watch(model, (value) => {
  text.value = value
})

const { contains } = useFilter({ sensitivity: 'base' })

const matches = computed(() => text.value
  ? props.options.filter(option => contains(option, text.value))
  : props.options)

/** The typed text, offered as a value of its own when no option is already exactly it. */
const typed = computed(() => {
  const value = text.value.trim()
  return value && !props.options.some(option => option.toLocaleLowerCase() === value.toLocaleLowerCase())
    ? value
    : ''
})

function choose(value: unknown) {
  model.value = typeof value === 'string' ? value : ''
  text.value = model.value
}
</script>

<template>
  <ComboboxRoot
    :model-value="model || undefined"
    ignore-filter
    open-on-focus
    :reset-search-term-on-blur="false"
    @update:model-value="choose"
  >
    <ComboboxAnchor as-child>
      <InputGroup>
        <InputGroupAddon v-if="icon">
          <component :is="icon" />
        </InputGroupAddon>

        <ComboboxInput
          v-model="text"
          data-slot="input-group-control"
          dir="auto"
          :placeholder="placeholder"
          class="h-full min-w-0 flex-1 bg-transparent px-2.5 text-sm outline-none placeholder:text-muted-foreground"
        />

        <InputGroupAddon v-if="model" align="inline-end">
          <InputGroupButton
            size="icon-xs"
            :aria-label="$t('common.clear')"
            @click="choose('')"
          >
            <X />
          </InputGroupButton>
        </InputGroupAddon>
      </InputGroup>
    </ComboboxAnchor>

    <ComboboxPortal>
      <!-- Nothing to suggest and nothing typed: no empty popover hanging under the box. -->
      <ComboboxContent
        v-if="typed || matches.length"
        position="popper"
        :side-offset="4"
        class="z-50 max-h-72 w-(--reka-combobox-trigger-width) min-w-40 overflow-hidden rounded-md bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
      >
        <ComboboxViewport class="p-1">
          <ComboboxItem
            v-if="typed"
            :value="typed"
            class="relative flex cursor-default items-center gap-2 rounded-sm py-1.5 ps-2 pe-8 text-sm outline-hidden select-none data-highlighted:bg-accent data-highlighted:text-accent-foreground"
          >
            <Plus class="size-4 text-muted-foreground" />
            <span dir="auto" class="truncate">{{ typed }}</span>
          </ComboboxItem>

          <ComboboxItem
            v-for="option in matches"
            :key="option"
            :value="option"
            class="relative flex cursor-default items-center gap-2 rounded-sm py-1.5 ps-2 pe-8 text-sm outline-hidden select-none data-highlighted:bg-accent data-highlighted:text-accent-foreground"
          >
            <span dir="auto" class="truncate">{{ option }}</span>
            <ComboboxItemIndicator class="absolute end-2 flex size-4 items-center justify-center">
              <Check class="size-4" />
            </ComboboxItemIndicator>
          </ComboboxItem>
        </ComboboxViewport>
      </ComboboxContent>
    </ComboboxPortal>
  </ComboboxRoot>
</template>

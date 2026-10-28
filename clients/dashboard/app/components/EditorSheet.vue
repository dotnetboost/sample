<script setup lang="ts">
/**
 * The panel an editor slides in on — what `USlideover` was under Nuxt UI: a title and a sentence,
 * a body that scrolls on its own, and a footer that stays put with the buttons in it.
 *
 * It opens from the far side of the reading direction, the side the content flows towards. A
 * sheet's `side` is physical, so it is chosen here from the direction rather than written as
 * `right` and left wrong in Arabic.
 */
withDefaults(defineProps<{
  title: string
  description?: string
  /** Tailwind max width, for an editor that needs more room than a form column. */
  width?: string
}>(), {
  description: undefined,
  width: 'sm:max-w-xl'
})

const open = defineModel<boolean>('open', { default: false })

const { direction } = useShellLocale()
</script>

<template>
  <Sheet v-model:open="open">
    <SheetContent
      :side="direction === 'rtl' ? 'left' : 'right'"
      :class="['w-full gap-0 p-0', width]"
    >
      <SheetHeader class="border-b p-6">
        <SheetTitle>{{ title }}</SheetTitle>
        <SheetDescription v-if="description">
          {{ description }}
        </SheetDescription>
      </SheetHeader>

      <div class="min-h-0 flex-1 overflow-y-auto p-6">
        <slot />
      </div>

      <SheetFooter v-if="$slots.footer" class="flex-row justify-end border-t p-4">
        <slot name="footer" />
      </SheetFooter>
    </SheetContent>
  </Sheet>
</template>

<script setup lang="ts">
import { EllipsisVertical, Pencil, Trash2 } from '@lucide/vue'

/** A catalogue row's menu. The cell holding it stops the click, or Delete would open the editor. */
defineProps<{
  deleteDisabled?: boolean
}>()

const emit = defineEmits<{
  edit: []
  delete: []
}>()
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
        <EllipsisVertical />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end">
      <DropdownMenuItem @select="emit('edit')">
        <Pencil />
        {{ $t('common.edit') }}
      </DropdownMenuItem>
      <DropdownMenuSeparator />
      <DropdownMenuItem variant="destructive" :disabled="deleteDisabled" @select="emit('delete')">
        <Trash2 />
        {{ $t('common.delete') }}
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>

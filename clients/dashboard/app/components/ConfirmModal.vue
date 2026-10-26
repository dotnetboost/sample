<script setup lang="ts">
/** A yes/no question before an action that cannot be undone — shadcn-vue's alert dialog. */
defineProps<{
  title: string
  description: string
  confirmLabel: string
  loading?: boolean
}>()

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  confirm: []
}>()
</script>

<template>
  <AlertDialog v-model:open="open">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ title }}</AlertDialogTitle>
        <AlertDialogDescription>{{ description }}</AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <AlertDialogCancel>{{ $t('common.cancel') }}</AlertDialogCancel>
        <!--
          A plain button, not `AlertDialogAction`: the action closes the dialog the moment it is
          clicked, and this one has to stay open with its spinner until the API has answered — a
          refused delete is reported while the question is still on screen.
        -->
        <Button variant="destructive" :disabled="loading" @click="emit('confirm')">
          <Spinner v-if="loading" />
          {{ confirmLabel }}
        </Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>

<script setup lang="ts">
const group = findSettingsGroup('catalog')!
</script>

<template>
  <SettingsGroupForm :group="group" />
</template>

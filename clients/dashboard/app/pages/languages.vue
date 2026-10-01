<script setup lang="ts">
const api = useApi()
const toast = useToast()

const { data: languages, refresh } = await useAsyncData('languages-page', () => api.multiLingual.languages())

const adding = ref(false)
const form = ref({ code: '', name: '', direction: 'Ltr' })

async function add() {
  adding.value = true
  try {
    await api.multiLingual.create(form.value)
    await refresh()
    form.value = { code: '', name: '', direction: 'Ltr' }
    toast.add({ title: 'Language added', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Could not add language', description: String(error), color: 'error' })
  } finally {
    adding.value = false
  }
}

const directionLabel = (value: number | string) => isRtl(value) ? 'RTL' : 'LTR'
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        Languages
      </h1>
      <p class="mt-1 text-muted">
        Served by <code>MapLanguageEndpoints()</code> from DotNetBoost.MultiLingual.API — a
        library endpoint, not one this application wrote.
      </p>
    </div>

    <UCard>
      <div class="divide-y divide-default">
        <div
          v-for="language in languages"
          :key="language.code"
          class="flex items-center gap-3 py-3 first:pt-0 last:pb-0"
        >
          <UBadge variant="subtle">
            {{ language.code }}
          </UBadge>
          <span class="flex-1 font-medium">{{ language.name }}</span>
          <UBadge color="neutral" variant="subtle" size="sm">
            {{ directionLabel(language.direction) }}
          </UBadge>
          <UBadge
            v-if="language.isDefault"
            color="primary"
            variant="subtle"
            size="sm"
          >
            default
          </UBadge>
          <UBadge
            v-if="!language.isActive"
            color="warning"
            variant="subtle"
            size="sm"
          >
            inactive
          </UBadge>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Add a language
        </h2>
      </template>

      <div class="flex flex-wrap items-end gap-3">
        <UFormField label="Code" class="w-28">
          <UInput v-model="form.code" placeholder="de" />
        </UFormField>
        <UFormField label="Name" class="w-48">
          <UInput v-model="form.name" placeholder="Deutsch" />
        </UFormField>
        <UFormField label="Direction" class="w-32">
          <USelect
            v-model="form.direction"
            :items="[{ label: 'LTR', value: 'Ltr' }, { label: 'RTL', value: 'Rtl' }]"
            value-key="value"
          />
        </UFormField>
        <UButton
          :loading="adding"
          :disabled="!form.code || !form.name"
          label="Add"
          @click="add"
        />
      </div>

      <template #footer>
        <p class="text-xs text-muted">
          A new language starts with no translations: existing content falls back to the default
          language until each row is filled in.
        </p>
      </template>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import { Database, EllipsisVertical, Eraser, Languages, Pencil, PilcrowLeft, PilcrowRight, PlugZap, Plus, RefreshCw, Star, Trash2 } from '@lucide/vue'
import type { Language } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.languages') }) })

const { languages, pending, loadError, busy, refresh, isBase, update, setDefault, remove, purge } = useLanguages()

const formOpen = ref(false)
const editing = ref<Language>()

/** The destructive action awaiting confirmation. */
const confirming = ref<{ kind: 'delete' | 'purge', language: Language } | null>(null)

function add() {
  editing.value = undefined
  formOpen.value = true
}

function edit(language: Language) {
  editing.value = language
  formOpen.value = true
}

function toggleActive(language: Language, isActive: boolean) {
  // PUT is a full replacement, so the name and direction go back as they are.
  update(language.code, { name: language.name, direction: language.direction, isActive })
}

const confirmation = computed(() => {
  const target = confirming.value

  if (!target) {
    return { title: '', description: '', label: '' }
  }

  return target.kind === 'purge'
    ? {
        title: t('languages.confirmPurge.title', { name: target.language.name }),
        // The base-language sentence is a second message rather than a branch inside the first:
        // a translator should not have to assemble one paragraph out of a conditional.
        description: [
          t('languages.confirmPurge.description', { code: target.language.code }),
          isBase(target.language.code) ? t('languages.confirmPurge.baseNote') : ''
        ].filter(Boolean).join(' '),
        label: t('languages.confirmPurge.confirm')
      }
    : {
        title: t('languages.confirmDelete.title', { name: target.language.name }),
        description: t('languages.confirmDelete.description'),
        label: t('common.delete')
      }
})

async function onConfirm() {
  const target = confirming.value

  if (!target) {
    return
  }

  await (target.kind === 'purge' ? purge(target.language.code) : remove(target.language.code))
  confirming.value = null
}
</script>

<template>
  <DashboardPage :title="$t('nav.languages')">
    <template #actions>
      <Button
        variant="ghost"
        size="icon-sm"
        :disabled="pending"
        :aria-label="$t('languages.reload')"
        @click="() => refresh()"
      >
        <RefreshCw :class="{ 'animate-spin': pending }" />
      </Button>
      <Button size="sm" @click="add">
        <Plus />
        {{ $t('languages.add') }}
      </Button>
    </template>

    <SectionIntro :title="$t('languages.title')" :description="$t('languages.card.description')">
      <Badge variant="outline" dir="ltr" class="font-mono">
        api/languages
      </Badge>
    </SectionIntro>

    <Alert v-if="loadError" variant="destructive">
      <PlugZap />
      <AlertTitle>{{ $t('languages.loadError') }}</AlertTitle>
      <AlertDescription>{{ loadError }}</AlertDescription>
      <AlertAction>
        <Button variant="outline" size="sm" @click="() => refresh()">
          {{ $t('common.tryAgain') }}
        </Button>
      </AlertAction>
    </Alert>

    <Card v-else class="py-0">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead class="ps-4">
              {{ $t('languages.columns.code') }}
            </TableHead>
            <TableHead>{{ $t('languages.columns.name') }}</TableHead>
            <TableHead>{{ $t('languages.columns.direction') }}</TableHead>
            <TableHead>{{ $t('languages.columns.role') }}</TableHead>
            <TableHead>{{ $t('languages.columns.active') }}</TableHead>
            <TableHead class="w-12 pe-4">
              <span class="sr-only">{{ $t('common.actions') }}</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="pending && !languages.length">
            <TableRow v-for="n in 3" :key="n">
              <TableCell v-for="c in 6" :key="c" class="first:ps-4 last:pe-4">
                <Skeleton class="h-5 w-full max-w-24" />
              </TableCell>
            </TableRow>
          </template>

          <TableEmpty v-else-if="!languages.length" :colspan="6">
            <Empty>
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <Languages />
                </EmptyMedia>
                <EmptyTitle>{{ $t('languages.empty') }}</EmptyTitle>
              </EmptyHeader>
            </Empty>
          </TableEmpty>

          <TableRow v-for="language in languages" v-else :key="language.code">
            <TableCell class="ps-4">
              <!-- `dir="ltr"`: a language tag is an identifier, even on a right-to-left page. -->
              <span dir="ltr" class="font-mono font-medium">{{ language.code }}</span>
            </TableCell>

            <TableCell>
              <span :dir="language.direction" class="font-medium">{{ language.name }}</span>
            </TableCell>

            <TableCell>
              <Badge variant="outline" dir="ltr" class="font-mono">
                <PilcrowLeft v-if="language.direction === 'rtl'" />
                <PilcrowRight v-else />
                {{ language.direction }}
              </Badge>
            </TableCell>

            <TableCell>
              <div class="flex flex-wrap gap-1">
                <Badge v-if="language.isDefault" class="bg-primary/10 text-primary">
                  <Star />
                  {{ $t('languages.badge.default') }}
                </Badge>
                <Tooltip v-if="isBase(language.code)">
                  <TooltipTrigger as-child>
                    <Badge variant="secondary">
                      <Database />
                      {{ $t('languages.badge.base') }}
                    </Badge>
                  </TooltipTrigger>
                  <TooltipContent>{{ $t('languages.badge.baseTooltip') }}</TooltipContent>
                </Tooltip>
              </div>
            </TableCell>

            <TableCell>
              <div class="flex items-center gap-2">
                <Switch
                  :model-value="language.isActive"
                  :disabled="busy !== null"
                  :aria-label="language.isActive
                    ? $t('languages.deactivate', { name: language.name })
                    : $t('languages.activate', { name: language.name })"
                  @update:model-value="toggleActive(language, $event)"
                />
                <Spinner v-if="busy === language.code" class="size-3.5 text-muted-foreground" />
              </div>
            </TableCell>

            <TableCell class="pe-4 text-end">
              <DropdownMenu>
                <DropdownMenuTrigger as-child>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    :aria-label="$t('languages.actionsFor', { name: language.name })"
                  >
                    <EllipsisVertical />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem @select="edit(language)">
                    <Pencil />
                    {{ $t('languages.actions.edit') }}
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    :disabled="language.isDefault || !language.isActive"
                    @select="setDefault(language.code)"
                  >
                    <Star />
                    {{ $t('languages.actions.makeDefault') }}
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    variant="destructive"
                    @select="confirming = { kind: 'purge', language }"
                  >
                    <Eraser />
                    {{ $t('languages.actions.purge') }}
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    variant="destructive"
                    :disabled="language.isDefault"
                    @select="confirming = { kind: 'delete', language }"
                  >
                    <Trash2 />
                    {{ $t('languages.actions.delete') }}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </Card>

    <LanguagesFormModal v-model:open="formOpen" :language="editing" />

    <ConfirmModal
      :open="confirming !== null"
      :title="confirmation.title"
      :description="confirmation.description"
      :confirm-label="confirmation.label"
      :loading="busy !== null"
      @update:open="!$event && (confirming = null)"
      @confirm="onConfirm"
    />
  </DashboardPage>
</template>

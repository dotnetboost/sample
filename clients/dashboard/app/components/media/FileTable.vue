<script setup lang="ts">
import { Check, Download, EllipsisVertical, ExternalLink, Folder, Link, PanelRightOpen, Trash2 } from '@lucide/vue'
import type { useMediaFileTable } from '~/composables/useMediaFileTable'
import type { MediaFile } from '~/types'

/**
 * The media library as a list: the same files as the grid, one row each, in a table drawn like
 * the Catalog and Audit ones — a framed, shaded, sticky header, sortable columns, and a footer slot
 * for the browser's pager.
 *
 * Named `FileTable` rather than `Table`: inside a file called Table.vue a bare `<Table>` is Vue's
 * implicit self-reference, and the table would render itself.
 *
 * A row opens the file's details wherever it is clicked, as the grid's thumbnail does; the name is
 * a real button for the keyboard, and the menu cell stops the click. In the picker (`selectable`)
 * the end of the row is the Attach button instead, and the row itself opens nothing — attaching is
 * the one thing the picker is for.
 */
defineProps<{
  table: ReturnType<typeof useMediaFileTable>['table']
  labels: Record<string, string>
  selectable?: boolean
  attachedIds?: string[]
  /** Id of the file an action is running on. */
  busy?: string | null
}>()

const emit = defineEmits<{
  open: [file: MediaFile]
  select: [file: MediaFile]
  remove: [file: MediaFile]
}>()
</script>

<template>
  <TableFrame>
    <Table>
      <TableHeader class="sticky-head">
        <TableRow>
          <SortableHead :column="table.getColumn('fileName')!" :label="labels.fileName!" class="ps-4" />
          <TableHead v-if="table.getColumn('kind')!.getIsVisible()" class="hidden md:table-cell">
            {{ labels.kind }}
          </TableHead>
          <TableHead v-if="table.getColumn('category')!.getIsVisible()" class="hidden lg:table-cell">
            {{ labels.category }}
          </TableHead>
          <SortableHead
            v-if="table.getColumn('size')!.getIsVisible()"
            :column="table.getColumn('size')!"
            :label="labels.size!"
            align="end"
          />
          <SortableHead
            v-if="table.getColumn('createdAt')!.getIsVisible()"
            :column="table.getColumn('createdAt')!"
            :label="labels.createdAt!"
            class="hidden sm:table-cell"
          />
          <TableHead class="w-12 pe-4">
            <span class="sr-only">{{ $t('common.actions') }}</span>
          </TableHead>
        </TableRow>
      </TableHeader>

      <TableBody>
        <TableRow
          v-for="row in table.getRowModel().rows"
          :key="row.id"
          :class="{ 'cursor-pointer': !selectable }"
          @click="!selectable && emit('open', row.original)"
        >
          <TableCell class="ps-4">
            <div class="flex min-w-0 items-center gap-3">
              <MediaThumbnail :file="row.original" size="sm" class="size-10 shrink-0 rounded-md border" />
              <div class="min-w-0 max-w-40 sm:max-w-64 lg:max-w-md">
                <!-- `dir="auto"`: an uploaded name is the uploader's, not ours. -->
                <button
                  type="button"
                  dir="auto"
                  class="block max-w-full truncate text-start font-medium outline-none hover:underline focus-visible:underline"
                  @click.stop="emit('open', row.original)"
                >
                  {{ row.original.fileName }}
                </button>
                <p v-if="row.original.tags.length" dir="auto" class="truncate text-xs text-muted-foreground">
                  {{ row.original.tags.join(' · ') }}
                </p>
              </div>
            </div>
          </TableCell>

          <TableCell v-if="table.getColumn('kind')!.getIsVisible()" class="hidden md:table-cell">
            <!-- A media type is an identifier: always left-to-right. -->
            <span class="inline-flex items-center gap-1.5 text-muted-foreground" dir="ltr">
              <component :is="iconFor(row.original.contentType)" class="size-3.5 shrink-0" />
              {{ row.original.contentType }}
            </span>
          </TableCell>

          <TableCell v-if="table.getColumn('category')!.getIsVisible()" class="hidden lg:table-cell">
            <Badge v-if="row.original.category" variant="outline" class="text-muted-foreground">
              <Folder />
              <span dir="auto">{{ row.original.category }}</span>
            </Badge>
            <span v-else class="text-muted-foreground/60">—</span>
          </TableCell>

          <TableCell v-if="table.getColumn('size')!.getIsVisible()" class="text-end tabular-nums text-muted-foreground">
            {{ formatBytes(row.original.size) }}
          </TableCell>

          <TableCell v-if="table.getColumn('createdAt')!.getIsVisible()" class="hidden text-muted-foreground sm:table-cell">
            <Tooltip>
              <TooltipTrigger as-child>
                <span class="whitespace-nowrap">{{ formatRelative(row.original.createdAt) }}</span>
              </TooltipTrigger>
              <TooltipContent>{{ formatDate(row.original.createdAt) }}</TooltipContent>
            </Tooltip>
          </TableCell>

          <TableCell class="pe-4 text-end" @click.stop>
            <template v-if="selectable">
              <Button
                v-if="attachedIds?.includes(row.original.id)"
                size="sm"
                variant="secondary"
                disabled
              >
                <Check />
                {{ $t('media.card.attached') }}
              </Button>
              <Button
                v-else
                size="sm"
                :disabled="busy === row.original.id"
                @click="emit('select', row.original)"
              >
                <Spinner v-if="busy === row.original.id" />
                <Link v-else />
                {{ $t('media.card.attach') }}
              </Button>
            </template>

            <DropdownMenu v-else>
              <DropdownMenuTrigger as-child>
                <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
                  <EllipsisVertical />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem @select="emit('open', row.original)">
                  <PanelRightOpen class="rtl:-scale-x-100" />
                  {{ $t('media.list.details') }}
                </DropdownMenuItem>
                <DropdownMenuItem as-child>
                  <a :href="contentUrl(row.original.id)" target="_blank" rel="noopener">
                    <ExternalLink />
                    {{ $t('common.open') }}
                  </a>
                </DropdownMenuItem>
                <DropdownMenuItem as-child>
                  <a :href="contentUrl(row.original.id, true)">
                    <Download />
                    {{ $t('common.download') }}
                  </a>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" @select="emit('remove', row.original)">
                  <Trash2 />
                  {{ $t('common.delete') }}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <slot name="footer" />
  </TableFrame>
</template>

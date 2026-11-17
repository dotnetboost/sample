import type { Component } from 'vue'
import { ArrowUpDown, Link, Link2Off, Replace, Unlink } from '@lucide/vue'
import type { AuditEntry } from '~/types'

/** What `MediaAuditParticipant` writes as `"media"` in an entry's metadata. */
export type MediaOperation = 'Attach' | 'Detach' | 'Replace' | 'Reorder'

/** A media entry's operation, and whether its detach came from the file being deleted. */
export interface MediaOperationInfo {
  operation: MediaOperation
  fileDeleted: boolean
}

const operations: readonly MediaOperation[] = ['Attach', 'Detach', 'Replace', 'Reorder']

/**
 * The media operation an entry records, read from its metadata — or `null` for every entry that is
 * not one.
 *
 * The metadata is this application's own convention, not the auditing library's: the API's
 * `MediaAuditParticipant` writes `{"media":"Attach","fileId":"…"}` and its kin on the owner's
 * entry. Anything else — no metadata, metadata that is not JSON, JSON without a known `media` —
 * is not a media entry, and is left to be shown as the string it is.
 */
export function mediaOperationOf(entry: Pick<AuditEntry, 'metadata'>): MediaOperationInfo | null {
  if (!entry.metadata) return null

  let parsed: unknown
  try {
    parsed = JSON.parse(entry.metadata)
  } catch {
    return null
  }

  if (!parsed || typeof parsed !== 'object') return null

  const { media, reason } = parsed as { media?: unknown, reason?: unknown }

  if (typeof media !== 'string' || !operations.includes(media as MediaOperation)) return null

  return { operation: media as MediaOperation, fileDeleted: reason === 'FileDeleted' }
}

const icons: Record<MediaOperation, Component> = {
  Attach: Link,
  Detach: Unlink,
  Replace,
  Reorder: ArrowUpDown
}

/**
 * How the Audit page draws a media operation: its icon and its name. The names are computed rather
 * than looked up by a key built from the value, for the reason `useAuditActions` gives.
 */
export function useMediaOperations() {
  const { t } = useI18n()

  const labels = computed<Record<MediaOperation, string>>(() => ({
    Attach: t('audit.media.attach'),
    Detach: t('audit.media.detach'),
    Replace: t('audit.media.replace'),
    Reorder: t('audit.media.reorder')
  }))

  /**
   * A detach because the file itself was deleted says so, rather than reading as a manual one —
   * in full where there is room, and by its own icon where there is not.
   */
  function label(info: MediaOperationInfo, short = false) {
    return info.fileDeleted && !short ? t('audit.media.detachFileDeleted') : labels.value[info.operation]
  }

  function icon(info: MediaOperationInfo): Component {
    return info.fileDeleted ? Link2Off : icons[info.operation]
  }

  return { icon, label }
}

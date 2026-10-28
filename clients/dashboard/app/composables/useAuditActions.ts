import type { Component } from 'vue'
import { Pencil, Plus, Trash2 } from '@lucide/vue'
import type { AuditAction } from '~/types'

/** The three, in the order a record's life runs. */
const actions: AuditAction[] = ['Created', 'Updated', 'Deleted']

const icons: Record<AuditAction, Component> = {
  Created: Plus,
  Updated: Pencil,
  Deleted: Trash2
}

/**
 * The icon's colour, inside an outline badge as the `dashboard-01` block draws a status: green
 * for a row that appeared, the accent for a change, red for a removal. On the icon alone, so a
 * column of them reads as words with a marker each rather than a wall of coloured pills.
 */
const styles: Record<AuditAction, string> = {
  Created: 'text-success',
  Updated: 'text-primary',
  Deleted: 'text-destructive'
}

/**
 * How the Audit page draws an action: its icon, its colour and its name, in one place for the
 * tabs over the table and the badge in each row.
 *
 * The names are computed rather than looked up by a key built from the value: a key assembled at
 * runtime is one `typedOptionsAndMessages` cannot check, and the three actions are the whole set.
 */
export function useAuditActions() {
  const { t } = useI18n()

  const labels = computed<Record<AuditAction, string>>(() => ({
    Created: t('audit.actions.created'),
    Updated: t('audit.actions.updated'),
    Deleted: t('audit.actions.deleted')
  }))

  return { actions, icons, styles, labels }
}

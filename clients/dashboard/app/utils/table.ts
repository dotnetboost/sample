import type { Updater } from '@tanstack/vue-table'

/** TanStack hands state changes over as a value or as a function of the old one. */
export function applyUpdater<T>(next: Updater<T>, current: T): T {
  return typeof next === 'function' ? (next as (old: T) => T)(current) : next
}

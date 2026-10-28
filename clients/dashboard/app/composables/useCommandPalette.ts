/**
 * Whether the command palette is open. Shared state rather than a prop, because it is opened from
 * two places that do not know about each other: the sidebar's search button, and ⌘K anywhere — see
 * `CommandPalette.vue`.
 */
export function useCommandPalette() {
  return useState('command-palette-open', () => false)
}

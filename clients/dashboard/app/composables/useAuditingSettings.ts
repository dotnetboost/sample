/** The `auditing` settings group, as `GET api/settings/auditing` returns it. */
export interface AuditingSettings {
  /** Whether the API's retention service deletes entries older than `retentionDays`. */
  pruneEnabled: boolean
  /** How old an entry must be, in days, before a prune deletes it. */
  retentionDays: number
  /** How often a prune runs while `pruneEnabled` is on. */
  pruneIntervalHours: number
}

/**
 * The key the Audit page and the settings tab share. It is the group's route, which is what
 * `SettingsGroupForm` refreshes after a save — so turning pruning on updates the Audit page's
 * retention badge without either knowing about the other.
 */
export const AUDITING_SETTINGS_KEY = 'auditing'

/**
 * The trail's retention as the API actually applies it, fetched once and shared.
 *
 * Needed beside the entity schema because the two describe different things. The schema's
 * `retentionDays` is DotNetBoost.Auditing's own `o.Retention`, which this application leaves
 * unset: the option is read once at startup and could not follow an edit. What deletes entries
 * here is `AuditRetentionService`, reading this group — so without it the Audit page would say
 * "kept indefinitely" about a trail that is being pruned.
 */
export function useAuditingSettings() {
  const api = useApi()

  return useAsyncData<AuditingSettings>(
    AUDITING_SETTINGS_KEY,
    (_nuxtApp, { signal }) => api.settings.get<AuditingSettings>('auditing', { signal }))
}

/**
 * Whether a language reads right-to-left.
 *
 * The API serialises `TextDirection` as a lowercase string ("ltr" / "rtl"), but a different
 * serializer configuration could send the enum's number instead, so both are accepted. Comparing
 * case-sensitively against "Rtl" — the C# member name — silently never matches, which is exactly
 * the bug this helper exists to stop recurring.
 */
export function isRtl(direction: number | string | undefined | null): boolean {
  if (typeof direction === 'number') return direction === 1
  return String(direction ?? '').toLowerCase() === 'rtl'
}

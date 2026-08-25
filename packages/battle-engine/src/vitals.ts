/**
 * The gauges a fighting thing carries, and how they are read as fractions.
 *
 * A unit has hit points and morale; a structure has hit points. Every one of
 * those is a current against a maximum, and "what fraction is left" was computed
 * in eleven places with four different behaviours: seven divided plainly, one
 * guarded a maximum of zero, two clamped to 0..1, and one did both.
 *
 * The disagreement had already reached the screen. A structure type whose
 * `maxHp` is zero drew a full condition bar under the generic art, which guards
 * the division, and a `NaN`-wide one under the campaign's, which does not — the
 * same picture, two answers. And the campaign's roster refuses a stored ratio
 * outside 0..1, so the clamp is required on the way out and was written at the
 * call site rather than owned anywhere.
 *
 * A leaf module on purpose: `state.ts` builds a unit's morale and `morale.ts`
 * spends it, and `morale.ts` already imports `state.ts`, so the one fact they
 * share cannot live in either.
 */

/**
 * The morale a unit has when its type declares none of its own.
 *
 * Written as a bare `100` in four places across two packages, one of them the
 * campaign bridge projecting a stored ratio back onto a unit — so changing the
 * engine's default would have left the campaign scaling against the old one.
 */
export const DEFAULT_MAX_MORALE = 100;

/**
 * A gauge read as a fraction of itself, 0..1.
 *
 * A maximum of zero reads as whole rather than as `NaN`: nothing that cannot be
 * damaged is damaged. This is deliberately *not* the right function for "how big
 * was this hit compared to the target" — a 200 point strike on an 80 point unit
 * is two and a half times its size, and morale damage is scaled by exactly that,
 * so clamping it there would silently cap the blow.
 */
export const gaugeRatio = (current: number, maximum: number): number =>
  maximum > 0 ? Math.max(0, Math.min(1, current / maximum)) : 1;

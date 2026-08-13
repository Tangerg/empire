import { isWeaponReady } from './combat';
import { executeCombatPlan, forecastCombatPlan, type CombatPlanRules } from './combat-plan';
import { hostileActionAllowed } from './engagement';
import { player, unitAtCoord } from './state';
import { UnitDepartureHandlers } from './unit-departure';
import { SpellCastEntity } from './domain/spell-cast';
import { DomainInvariantError, IllegalActionError } from './domain/errors';
import type { CastRefusal, Coord, GameEvent, GameState, PendingCast, PlayerId, Unit, WeaponDef } from './types';

/**
 * Charge time.
 *
 * A weapon with `castTurns > 0` is committed now and resolved later: the aim
 * point and the launch tile freeze at commit time, so the shot keeps the
 * geometry it was fired with while the battlefield moves under it. A target
 * may walk out of the tile — and an ally may walk into it.
 *
 * Delay is counted in **actor turns** (`state.actorTurns`) rather than in a
 * policy-specific tick, so one content pack means the same thing under either
 * turn-order family: "two turns from now" is two entitlements to act, whether
 * those belong to whole sides or to single units.
 *
 * While a cast is charging its caster is locked: the battle lifecycle does not
 * ready it, which is what makes charge time a cost rather than a free delay.
 */

/** Everything resolving a due cast needs; `BattleRuleServices` satisfies it. */
export type CastingRules = CombatPlanRules;

/**
 * Casts still being sustained, i.e. whose caster is still on the field.
 *
 * A departing caster drops its charge immediately, so this filter is a safety
 * net rather than the mechanism: it keeps state that arrived from elsewhere —
 * a save, a fixture, a mid-refactor bug — from showing one reader a charge that
 * another reader has already discounted.
 */
export function activeCasts(state: GameState): PendingCast[] {
  return state.pendingCasts.filter((cast) =>
    state.units.some((unit) => unit.id === cast.caster));
}

/** The cast this unit is sustaining, if any. A unit sustains at most one. */
export function castOf(state: GameState, unitId: number): PendingCast | undefined {
  return state.pendingCasts.find((cast) => cast.caster === unitId);
}

/** True while the unit is charging, and therefore not free to act. */
export function isCharging(state: GameState, unit: Unit): boolean {
  return castOf(state, unit.id) !== undefined;
}

/** Sustained casts a viewer should be warned about. */
export function hostileCastsAgainst(state: GameState, viewer: PlayerId): PendingCast[] {
  return activeCasts(state).filter((cast) => cast.owner !== viewer);
}

export interface CastDeclaration {
  caster: Unit;
  ability: string;
  weapon: WeaponDef;
  /** Tile the strike is aimed at. */
  target: Coord;
  /** Tile the strike is launched from — the end of the caster's move. */
  origin: Coord;
}

/** Commits a strike that will land later. */
export function beginCast(state: GameState, declaration: CastDeclaration, emit: (event: GameEvent) => void): PendingCast {
  const { caster, weapon, target, origin } = declaration;
  // Asking to charge an instant weapon is a caller defect, not a refused order.
  if (weapon.castTurns <= 0) {
    throw new DomainInvariantError(`weapon "${weapon.id}" resolves immediately and cannot be charged`);
  }
  if (castOf(state, caster.id)) throw new IllegalActionError('该单位已经在咏唱中');

  const cast: PendingCast = {
    caster: caster.id,
    owner: caster.owner,
    ability: declaration.ability,
    weapon: weapon.id,
    target: { ...target },
    origin: { ...origin },
    declaredAt: state.actorTurns,
    resolveAt: state.actorTurns + Math.max(1, Math.round(weapon.castTurns)),
  };
  state.pendingCasts.push(cast);
  emit({
    type: 'castBegan',
    unit: caster.id,
    weapon: weapon.id,
    at: { ...target },
    turns: new SpellCastEntity(cast).duration,
  });
  return cast;
}

/** Drops the cast a departing unit was sustaining. */
export function cancelCastOf(
  state: GameState,
  unitId: number,
  emit: (event: GameEvent) => void,
): void {
  const cast = castOf(state, unitId);
  if (!cast) return;
  state.pendingCasts = state.pendingCasts.filter((candidate) => candidate !== cast);
  emit({
    type: 'castCancelled',
    unit: cast.caster,
    weapon: cast.weapon,
    at: { ...cast.target },
    reason: 'casterLost',
  });
}

/** A departing caster drops its charge the moment it leaves, not a turn later. */
UnitDepartureHandlers.register({
  id: 'casting.cancel',
  handle: ({ state, unit, emit }) => cancelCastOf(state, unit.id, emit),
});

/** Resolves every cast that has come due. */
export function resolveDueCasts(
  rules: CastingRules,
  state: GameState,
  emit: (event: GameEvent) => void,
): void {
  const due = state.pendingCasts.filter((cast) => new SpellCastEntity(cast).isDueAt(state.actorTurns));
  state.pendingCasts = state.pendingCasts.filter((cast) => !due.includes(cast));
  // Oldest first, so simultaneous casts resolve in commit order rather than in
  // whatever order the array happens to hold.
  for (const cast of [...due].sort((left, right) => left.declaredAt - right.declaredAt)) {
    resolveOne(rules, state, cast, emit);
  }
}

/**
 * Why a cast can fail to land. Charge time opens a window in which the world
 * changes, so resolution asks its preconditions rather than assuming the ones
 * that held at commit time still hold — an unmet precondition must fizzle the
 * cast, never abort the turn that happened to trigger the sweep.
 */
function refusal(rules: CastingRules, state: GameState, cast: PendingCast): CastRefusal | null {
  const caster = state.units.find((unit) => unit.id === cast.caster);
  // The caster may have fallen to a strike resolved earlier in this same sweep.
  if (!caster) return 'casterLost';
  if (!isWeaponReady(rules, caster, cast.weapon, player(state, caster.owner))) return 'weaponUnavailable';
  if (!hostileActionAllowed(state, caster.owner, cast.origin, cast.target, 'attack')) return 'targetProtected';
  // A single-target strike needs something on the tile; an area strike still
  // falls on it and splashes whoever stayed nearby.
  const weapon = rules.content.weapons.get(cast.weapon);
  if (weapon.area === 'single' && !unitAtCoord(state, cast.target)) return 'targetVacated';
  return null;
}

function resolveOne(
  rules: CastingRules,
  state: GameState,
  cast: PendingCast,
  emit: (event: GameEvent) => void,
): void {
  const refused = refusal(rules, state, cast);
  if (refused) {
    emit({
      type: 'castCancelled',
      unit: cast.caster,
      weapon: cast.weapon,
      at: { ...cast.target },
      reason: refused,
    });
    return;
  }
  const caster = state.units.find((unit) => unit.id === cast.caster)!;
  emit({ type: 'castResolved', unit: caster.id, weapon: cast.weapon, at: { ...cast.target } });
  const plan = forecastCombatPlan(rules, state, caster, cast.target, {
    from: cast.origin,
    weapon: cast.weapon,
  });
  executeCombatPlan(rules, state, plan, emit);
}

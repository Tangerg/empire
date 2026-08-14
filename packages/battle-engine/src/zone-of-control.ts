import { bestReactiveStrike, consumeWeapon, type CombatRules } from './combat';
import { resolveDamage, type DamageRules } from './damage';
import { UnitEntity } from './domain/unit-entity';
import { boardOf } from './domain/board';
import { reactionOf } from './reactions';
import { areEnemies } from './state';
import type { ContentCatalog } from './content-pack';
import type { GridRegistry } from './tactical-grid';
import type { Coord, GameEvent, GameState, Unit } from './types';

/**
 * Zone of control.
 *
 * A unit holds the ground around it. Walking into an enemy's zone ends the
 * move — you cannot stroll past a spearman to reach the archer behind him —
 * and slipping back out of one invites a parting shot. Together the two turn a
 * battle line into something that has to be broken rather than walked around,
 * which is most of the difference between a skirmish and a tactics game.
 *
 * Off unless `rules.zoneOfControl` says otherwise, so every existing battle
 * plays exactly as before. `UnitDef.zoneOfControl` tunes the radius per unit
 * type; 0 is for the units that hold no ground at all — scouts, artillery,
 * anything whose business is not standing in the way.
 */

const DEFAULT_CONTROL_RADIUS = 1;

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface ControlZoneRules {
  readonly content: ContentCatalog;
  readonly grids: GridRegistry;
}

/**
 * Radius a unit projects, already accounting for the battle's rules.
 *
 * The rule decides whether zones exist at all and the content decides how far
 * each unit type reaches — the same split as `moraleEnabled` and a unit's
 * morale profile. A pack cannot switch a rule on by describing its units.
 */
export function controlRadius(content: ContentCatalog, state: GameState, unit: Unit): number {
  if (!state.rules.zoneOfControl) return 0;
  return Math.max(0, Math.round(content.units.get(unit.type).zoneOfControl ?? DEFAULT_CONTROL_RADIUS));
}

function controls(rules: ControlZoneRules, state: GameState, unit: Unit, at: Coord): boolean {
  const radius = controlRadius(rules.content, state, unit);
  return radius > 0 && boardOf(rules, state).distance(unit, at) <= radius;
}

/**
 * Tile indices held by the enemies of `unit`.
 *
 * A set rather than a predicate because the mover's whole reachable area is
 * tested against it — pathfinding asks thousands of times per move field and
 * should not re-scan the army for every tile.
 */
export function hostileControlZone(
  rules: ControlZoneRules,
  state: GameState,
  unit: Unit,
): Set<number> {
  const zone = new Set<number>();
  if (!state.rules.zoneOfControl) return zone;
  const board = boardOf(rules, state);
  for (const other of state.units) {
    if (!areEnemies(state, other.owner, unit.owner)) continue;
    const radius = controlRadius(rules.content, state, other);
    if (radius <= 0) continue;
    for (const index of board.ringIndices(other, 1, radius)) zone.add(index);
  }
  return zone;
}

/**
 * Enemies that `unit` disengages from by moving `from` → `to`.
 *
 * Sliding along a battle line provokes nothing; it is leaving a zone that
 * costs. A stance that has given up its riposte has given up this too, and a
 * unit only ever gets one reaction per round to spend on it.
 */
export function disengagedControllers(
  rules: CombatRules,
  state: GameState,
  unit: Unit,
  from: Coord,
  to: Coord,
): Unit[] {
  if (!state.rules.zoneOfControl) return [];
  return state.units.filter((other) =>
    areEnemies(state, other.owner, unit.owner) &&
    reactionOf(rules, other.reaction).retaliates &&
    new UnitEntity(other).canReact(state.turn) &&
    controls(rules, state, other, from) &&
    !controls(rules, state, other, to));
}

/** A parting shot is free, so it is not a full attack. */
const PARTING_SHOT_MULTIPLIER = 0.7;

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface ZoneOfControlRules extends CombatRules, DamageRules, ControlZoneRules {}

/**
 * Resolves every parting shot provoked by a voluntary move.
 *
 * Forced movement does not provoke: a unit that was thrown out of a zone did
 * not choose to disengage. The mover may fall here, so callers must ask
 * whether it is still on the field before commanding it further.
 */
export function resolvePartingShots(
  rules: ZoneOfControlRules,
  state: GameState,
  unit: Unit,
  from: Coord,
  to: Coord,
  emit: (event: GameEvent) => void,
): void {
  for (const controller of disengagedControllers(rules, state, unit, from, to)) {
    if (!state.units.some((candidate) => candidate.id === unit.id)) return;
    const stance = reactionOf(rules, controller.reaction);
    const strike = bestReactiveStrike(
      rules,
      state,
      { unit: controller, at: controller },
      { unit, at: from },
      stance,
    );
    if (!strike) continue;
    new UnitEntity(controller).consumeReaction(state.turn);
    consumeWeapon(rules, state, controller, strike.weapon.id, emit);
    const damage = Math.max(1, Math.round(strike.damage.damage * PARTING_SHOT_MULTIPLIER));
    // Damage and nothing else, on purpose. A parting shot is a punishment for
    // disengaging, not an engagement: it does not carry the weapon's rider, so
    // walking past a poisoner is not a way to poison an army, and it teaches
    // its owner nothing, so a chokepoint is not a rank farm. That is why this
    // is a bare `resolveDamage` and not the `land` that combat proper uses —
    // the difference is a rule, not an omission.
    resolveDamage(rules, state, {
      unit: unit.id,
      amount: damage,
      report: (blow) => ({
        type: 'partingShot',
        attacker: controller.id,
        defender: unit.id,
        weapon: strike.weapon.id,
        at: { ...from },
        damage: blow.amount,
        killed: blow.killed,
      }),
    }, emit);
  }
}

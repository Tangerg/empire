import { BattleAggregate, type UnitDamageResult } from './domain/index';
import { mournFallen, sufferDamageShock, type MoraleRules } from './morale';
import { announceUnitFall } from './unit-departure';
import type { Coord, GameEvent, GameState } from './types';

/**
 * Port declared by this module; `BattleRuleServices` satisfies it. Everything
 * a blow implies is reachable from here, which is the point: a caller passes
 * its ruleset and gets the whole aftermath.
 */
export type DamageRules = MoraleRules;

export interface DamageRequest {
  /** Who takes the blow. Already gone is a legal answer, not an error. */
  readonly unit: number;
  readonly amount: number;
  /** Damage that may wound but never kill: bleeding, exhaustion, a warning shot. */
  readonly nonlethal?: boolean;
  /**
   * The caller's own account of the blow — the `attack`, `counter`,
   * `collisionDamage` or `statusTick` line it wants in the log. It is emitted
   * after the hit lands and before anything that follows from it, so the log
   * always reads cause before consequence.
   */
  readonly report: (blow: UnitDamageResult) => GameEvent;
}

export interface DamageOutcome {
  /** False when there was nobody left to hit; nothing was emitted. */
  readonly landed: boolean;
  readonly amount: number;
  readonly hpAfter: number;
  readonly killed: boolean;
  /**
   * The unit is no longer on the battlefield — it died, or it broke and ran.
   *
   * This is the question callers actually have before touching the target
   * again. Asking `killed` instead is the mistake that keeps being made: a
   * unit routed by the morale shock of the very same blow is just as gone.
   */
  readonly leftField: boolean;
  readonly at: Coord;
}

const NOTHING_TO_HIT: DamageOutcome = {
  landed: false,
  amount: 0,
  hpAfter: 0,
  killed: false,
  leftField: true,
  at: { x: -1, y: -1 },
};

function survivableAmount(hp: number, request: DamageRequest): number {
  const requested = Math.max(0, Math.round(request.amount));
  return request.nonlethal ? Math.min(requested, Math.max(0, hp - 1)) : requested;
}

/**
 * A blow and everything the rules make of it, in one fixed order.
 *
 * The caller owns the account of the blow itself and nothing else. What
 * follows from *any* damage — the death, the corpse, the passengers lost with
 * a transport, the consequences other subsystems attach to a departure, the
 * morale shock on the victim and on everyone who watched it fall — belongs to
 * the rules, not to the weapon that happened to deal it.
 *
 * Before this existed the aftermath was six lines copied into five call sites
 * in three different orders, each one edit away from forgetting a step. Two of
 * them also had to guard, by hand, against a target that an earlier hit in the
 * same volley had already routed. That guard lives here now: damage aimed at
 * a unit that has left is simply an outcome that did not land.
 */
export function resolveDamage(
  rules: DamageRules,
  state: GameState,
  request: DamageRequest,
  emit: (event: GameEvent) => void,
): DamageOutcome {
  const target = state.units.find((candidate) => candidate.id === request.unit);
  if (!target) return NOTHING_TO_HIT;

  const at = { x: target.x, y: target.y };
  const amount = survivableAmount(target.hp, request);
  if (amount <= 0) {
    return { landed: true, amount: 0, hpAfter: target.hp, killed: false, leftField: false, at };
  }

  const blow = new BattleAggregate(state, rules.content).damageUnit(target.id, amount);
  emit(request.report(blow));
  if (blow.fall) {
    announceUnitFall(rules, state, blow.fall, emit);
    mournFallen(rules, state, target, blow.at, emit);
  } else {
    sufferDamageShock(rules, state, target, blow.amount, emit);
  }
  return {
    landed: true,
    amount: blow.amount,
    hpAfter: blow.hpAfter,
    killed: blow.killed,
    leftField: !state.units.some((candidate) => candidate.id === target.id),
    at: blow.at,
  };
}

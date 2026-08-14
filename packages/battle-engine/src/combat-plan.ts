import {
  computeDamage,
  canReachWithWeapon,
  consumeWeapon,
  forecast,
  forecastStructure,
  primaryWeapon,
  isWeaponReady,
  unitWeapons,
  type CombatForecast,
  type CombatRules,
  type DamageBreakdown,
  type StructureCombatForecast,
} from './combat';

import { DomainInvariantError, UnitEntity } from './domain/index';
import { sameCoord } from './grid';
import { boardOf } from './domain/board';
import { type WeaponAreaRules } from './weapon-area';
import {
  awardCombatProgress,
  awardDamageTakenMomentum,
  awardRankProgress,
  changeMomentum,
  type RankProgressionPolicy,
} from './progression';
import { type WeaponHitEffectHandlerRegistry } from './hit-effects';
import { areAllies, areEnemies, requireUnit, unitAt } from './state';
import { damageStructure, structureAt } from './structures';
import type {
  Coord,
  GameEvent,
  GameState,
  StructureState,
  Unit,
  WeaponDef,
  WeaponId,
  WeaponHitEffect,
} from './types';
import { type ContentCatalog } from './content-pack';
import { resolveDamage, type DamageOutcome, type DamageRequest, type DamageRules } from './damage';
import { hostileActionAllowed } from './engagement';

export interface PlannedUnitHit {
  target: number;
  at: Coord;
  primary: boolean;
  protectedUnit?: number;
  damage: DamageBreakdown;
  hpBefore: number;
  hpAfter: number;
  killed: boolean;
  effects: WeaponHitEffect[];
}

export interface PlannedStructureHit {
  target: string;
  at: Coord;
  primary: boolean;
  forecast: StructureCombatForecast;
}

export interface PlannedSupportAttack {
  attacker: number;
  target: number;
  weapon: WeaponId;
  damage: DamageBreakdown;
  hpBefore: number;
  hpAfter: number;
  killed: boolean;
  effects: WeaponHitEffect[];
}

/**
 * Immutable result of target expansion and combat forecasting.
 *
 * Execution consumes this exact plan, so UI, AI, replay and resolution cannot
 * disagree about which entities an area weapon affects.
 */
export interface CombatPlan {
  attacker: number;
  weapon: WeaponId;
  from: Coord;
  aimedAt: Coord;
  area: WeaponDef['area'];
  affectedCells: Coord[];
  primaryUnit: CombatForecast | null;
  primaryStructure: StructureCombatForecast | null;
  unitHits: PlannedUnitHit[];
  structureHits: PlannedStructureHit[];
  supportAttack: PlannedSupportAttack | null;
}

/**
 * Execution needs everything forecasting needs, plus the effect and growth
 * policies. Declared as a consumer port; `BattleRuleServices` satisfies it.
 */
export interface CombatPlanRules extends CombatRules, DamageRules, WeaponAreaRules {
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly progression: RankProgressionPolicy;
}

const SUPPORT_ATTACK_MULTIPLIER = 0.6;

function supportDamage(damage: DamageBreakdown): DamageBreakdown {
  return damage.and({
    id: 'reaction.support-attack',
    label: '援护攻击',
    source: 'reaction',
    stage: 'final',
    operation: 'multiply',
    value: SUPPORT_ATTACK_MULTIPLIER,
  });
}

function planSupportAttack(
  rules: CombatRules,
  state: GameState,
  attacker: Unit,
  attackerAt: Coord,
  defender: Unit,
  defenderHp: number,
): PlannedSupportAttack | null {
  const content = rules.content;
  const board = boardOf(rules, state);
  const candidates = state.units
    .filter((candidate) =>
      candidate.id !== attacker.id &&
      areAllies(state, candidate.owner, attacker.owner) &&
      candidate.reaction === 'support' &&
      new UnitEntity(candidate).canReact(state.turn) &&
      board.distance(candidate, attackerAt) === 1)
    .flatMap((supporter) => unitWeapons(supporter, content)
      .filter((weapon) =>
        weapon.canCounter &&
        isWeaponReady(rules, supporter, weapon, state.players.find((p) => p.id === supporter.owner)) &&
        canReachWithWeapon(board, weapon, supporter, defender))
      .map((weapon) => ({
        supporter,
        weapon,
        damage: supportDamage(computeDamage(rules, state, supporter, defender, {
          attackerAt: supporter,
          defenderAt: defender,
          weapon: weapon.id,
        })),
      })))
    .sort((left, right) =>
      right.damage.damage - left.damage.damage ||
      left.supporter.id - right.supporter.id ||
      left.weapon.id.localeCompare(right.weapon.id));
  const selected = candidates[0];
  if (!selected) return null;
  const hpAfter = Math.max(0, defenderHp - selected.damage.damage);
  return {
    attacker: selected.supporter.id,
    target: defender.id,
    weapon: selected.weapon.id,
    damage: selected.damage,
    hpBefore: defenderHp,
    hpAfter,
    killed: hpAfter <= 0,
    effects: hpAfter <= 0 ? [] : selected.weapon.hitEffects,
  };
}

function hostileStructure(state: GameState, attacker: Unit, at: Coord, content: ContentCatalog): StructureState | null {
  const structure = structureAt(state, at.x, at.y);
  if (!structure || !content.structures.get(structure.type).targetable) return null;
  return structure.owner === 0 || areEnemies(state, structure.owner, attacker.owner) ? structure : null;
}

/** Where the strike comes from and which profile is used. */
export interface CombatPlanOptions {
  /** Tile the attacker fires from; defaults to its current tile. */
  from?: Coord;
  weapon?: WeaponId;
}

/**
 * Forecasts the strike aimed at `aimedAt`.
 *
 * The three preconditions below are the caller's to establish — every caller
 * reaches this through `abilityTargets`, which already applied them — so
 * failing one is a defect rather than a refusal, and says so with the type.
 */
export function forecastCombatPlan(
  rules: CombatRules & WeaponAreaRules,
  state: GameState,
  attacker: Unit,
  aimedAt: Coord,
  options: CombatPlanOptions = {},
): CombatPlan {
  const content = rules.content;
  const from = options.from ?? { x: attacker.x, y: attacker.y };
  const resolvedWeaponId = options.weapon ?? primaryWeapon(attacker, content).id;
  if (!hostileActionAllowed(state, attacker.owner, from, aimedAt, 'attack')) {
    throw new DomainInvariantError('combat plan target is protected by an engagement rule');
  }
  const weapon = content.weapons.get(resolvedWeaponId);
  const primaryTarget = unitAt(state, aimedAt);
  const primaryStructureTarget = hostileStructure(state, attacker, aimedAt, content);
  // An area weapon may land on a tile whose occupant left — that is the whole
  // point of charge time, and every step below already copes with a null
  // primary. A single-target weapon aimed at nothing has nothing to resolve.
  if (!primaryTarget && !primaryStructureTarget && rules.areaShapes.get(weapon.area).needsOccupant) {
    throw new DomainInvariantError('combat plan requires a hostile primary target');
  }
  if (primaryTarget && !areEnemies(state, primaryTarget.owner, attacker.owner)) {
    throw new DomainInvariantError('combat plan cannot target an allied unit');
  }

  const primaryUnit = primaryTarget
    ? forecast(rules, state, attacker, primaryTarget, { attackFrom: from, weapon: resolvedWeaponId })
    : null;
  const primaryStructure = primaryStructureTarget
    ? forecastStructure(rules, state, attacker, primaryStructureTarget, { weapon: resolvedWeaponId })
    : null;
  const affectedCells = rules.areaShapes.coverage(boardOf(rules, state), from, aimedAt, weapon.area);
  const unitHits: PlannedUnitHit[] = [];
  const structureHits: PlannedStructureHit[] = [];
  const excludedUnits = new Set<number>();

  if (primaryUnit && primaryTarget) {
    const recipient = requireUnit(state, primaryUnit.damageRecipient);
    excludedUnits.add(primaryTarget.id);
    excludedUnits.add(recipient.id);
    unitHits.push({
      target: recipient.id,
      at: { x: recipient.x, y: recipient.y },
      primary: true,
      protectedUnit: primaryUnit.interceptor ? primaryTarget.id : undefined,
      damage: primaryUnit.strike,
      hpBefore: recipient.hp,
      hpAfter: primaryUnit.recipientHpAfter,
      killed: primaryUnit.recipientDies,
      effects: primaryUnit.recipientDies ? [] : weapon.hitEffects,
    });
  }
  if (primaryStructure && primaryStructureTarget) {
    structureHits.push({
      target: primaryStructureTarget.id,
      at: { x: primaryStructureTarget.x, y: primaryStructureTarget.y },
      primary: true,
      forecast: primaryStructure,
    });
  }

  for (const cell of affectedCells) {
    if (sameCoord(cell, aimedAt)) continue;
    const unit = unitAt(state, cell);
    if (unit && areEnemies(state, unit.owner, attacker.owner) && !excludedUnits.has(unit.id)) {
      const damage = computeDamage(rules, state, attacker, unit, {
        attackerAt: from,
        defenderAt: cell,
        weapon: resolvedWeaponId,
      });
      unitHits.push({
        target: unit.id,
        at: { ...cell },
        primary: false,
        damage,
        hpBefore: unit.hp,
        hpAfter: Math.max(0, unit.hp - damage.damage),
        killed: damage.damage >= unit.hp,
        effects: damage.damage >= unit.hp ? [] : weapon.hitEffects,
      });
      excludedUnits.add(unit.id);
    }
    const structure = hostileStructure(state, attacker, cell, content);
    if (structure && structure.id !== primaryStructureTarget?.id) {
      structureHits.push({
        target: structure.id,
        at: { ...cell },
        primary: false,
        forecast: forecastStructure(rules, state, attacker, structure, { weapon: resolvedWeaponId }),
      });
    }
  }

  const supportAttack = primaryUnit && primaryTarget && !primaryUnit.defenderDies
    ? planSupportAttack(rules, state, attacker, from, primaryTarget, primaryUnit.defenderHpAfter)
    : null;

  return {
    attacker: attacker.id,
    weapon: resolvedWeaponId,
    from: { ...from },
    aimedAt: { ...aimedAt },
    area: weapon.area,
    affectedCells,
    primaryUnit,
    primaryStructure,
    unitHits,
    structureHits,
    supportAttack,
  };
}

/** One unit striking another: the blow itself, and the rider it carries. */
interface Blow {
  readonly striker: Unit;
  readonly target: number;
  readonly amount: number;
  /** What the weapon does beyond damage. Never applied to a corpse. */
  readonly effects: readonly WeaponHitEffect[];
  readonly report: DamageRequest['report'];
}

/**
 * A blow between two units, and everything combat makes of it.
 *
 * The volley, the riposte and the ally's covering shot are one act performed by
 * different people, and they were written out three times. The copies had
 * drifted: only one of them re-checked that the target was still standing after
 * a hit effect resolved, so a shove into a cliff credited the corpse with the
 * dash of momentum a survivor earns for taking a hit, and announced it — a
 * `resourceChanged` for a unit that had already left the field. Only one of
 * them noticed a blow that never landed, so a counter aimed at an attacker who
 * had already gone still taught its owner something.
 */
function land(
  rules: CombatPlanRules,
  state: GameState,
  blow: Blow,
  emit: (event: GameEvent) => void,
): DamageOutcome {
  // A prior hit in the same volley can rout a later recipient through morale
  // shock. The immutable plan still describes the aimed area; `resolveDamage`
  // treats a recipient who has already left as a blow that did not land.
  const outcome = resolveDamage(rules, state, {
    unit: blow.target,
    amount: blow.amount,
    report: blow.report,
  }, emit);
  if (!outcome.landed) return outcome;
  awardCombatProgress(rules, blow.striker, outcome.amount, outcome.killed, emit);
  if (outcome.leftField) return outcome;

  if (blow.effects.length > 0) {
    rules.hitEffects.apply(rules, state, blow.striker, requireUnit(state, blow.target), [...blow.effects], emit);
  }
  // A hit effect can finish what the blow started — a shove into a cliff.
  const survivor = state.units.find((unit) => unit.id === blow.target);
  if (survivor) awardDamageTakenMomentum(rules.resources, survivor, emit);
  return outcome;
}

function applyStructureHit(
  rules: CombatPlanRules,
  state: GameState,
  attacker: Unit,
  hit: PlannedStructureHit,
  emit: (event: GameEvent) => void,
): void {
  const { content } = rules;
  emit({
    type: hit.primary ? 'attackStructure' : 'areaAttackStructure',
    attacker: attacker.id,
    structure: hit.target,
    weapon: hit.forecast.weapon,
    damage: hit.forecast.damage,
    destroyed: hit.forecast.destroyed,
  });
  const damage = damageStructure(content, state, hit.target, hit.forecast.rawDamage, emit);
  awardCombatProgress(rules, attacker, damage, hit.forecast.destroyed, emit);
}

/** Commits an already forecast plan in one fixed, tested phase order. */
export function executeCombatPlan(
  rules: CombatPlanRules,
  state: GameState,
  plan: CombatPlan,
  emit: (event: GameEvent) => void,
): void {
  const { content, resources } = rules;
  const attacker = requireUnit(state, plan.attacker);
  consumeWeapon(rules, state, attacker, plan.weapon, emit);

  if (plan.primaryUnit?.reaction) {
    const reaction = plan.primaryUnit.reaction;
    const reactor = state.units.find((candidate) => candidate.id === reaction.unit);
    if (reactor) {
      new UnitEntity(reactor).consumeReaction(state.turn);
      if (reaction.stance === 'support') awardRankProgress(rules, reactor, 20, emit);
    }
    emit({
      type: 'reactionTriggered',
      unit: reaction.unit,
      stance: reaction.stance,
      protectedUnit: reaction.protectedUnit,
    });
  }

  let unitKilled = false;
  for (const hit of plan.unitHits) {
    const outcome = land(rules, state, {
      striker: attacker,
      target: hit.target,
      amount: hit.damage.damage,
      effects: hit.effects,
      report: (blow) => ({
        type: hit.primary ? 'attack' : 'areaAttack',
        attacker: attacker.id,
        defender: hit.target,
        protectedUnit: hit.protectedUnit,
        weapon: hit.damage.weapon,
        damage: blow.amount,
        killed: blow.killed,
      }),
    }, emit);
    unitKilled = outcome.killed || unitKilled;
  }
  for (const hit of plan.structureHits) applyStructureHit(rules, state, attacker, hit, emit);
  changeMomentum(
    resources,
    attacker,
    unitKilled || plan.structureHits.some((hit) => hit.forecast.destroyed) ? 10 : 5,
    emit,
  );

  const counter = plan.primaryUnit?.counter;
  const defenderId = plan.primaryUnit?.defender;
  if (counter && defenderId !== undefined && state.units.some((unit) => unit.id === defenderId)) {
    const defender = requireUnit(state, defenderId);
    consumeWeapon(rules, state, defender, counter.weapon, emit);
    const outcome = land(rules, state, {
      striker: defender,
      target: attacker.id,
      amount: counter.damage,
      effects: content.weapons.get(counter.weapon).hitEffects,
      report: (blow) => ({
        type: 'counter',
        attacker: defender.id,
        defender: attacker.id,
        weapon: counter.weapon,
        damage: blow.amount,
        killed: blow.killed,
      }),
    }, emit);
    changeMomentum(resources, defender, outcome.killed ? 10 : 5, emit);
  }

  const support = plan.supportAttack;
  if (!support) return;
  const supporter = state.units.find((unit) => unit.id === support.attacker);
  const target = state.units.find((unit) => unit.id === support.target);
  if (!supporter || !target) return;
  new UnitEntity(supporter).consumeReaction(state.turn);
  consumeWeapon(rules, state, supporter, support.weapon, emit);
  const outcome = land(rules, state, {
    striker: supporter,
    target: target.id,
    amount: support.damage.damage,
    effects: support.effects,
    report: (blow) => ({
      type: 'supportAttack',
      attacker: supporter.id,
      defender: target.id,
      weapon: support.weapon,
      damage: blow.amount,
      killed: blow.killed,
    }),
  }, emit);
  changeMomentum(resources, supporter, outcome.killed ? 10 : 5, emit);
}

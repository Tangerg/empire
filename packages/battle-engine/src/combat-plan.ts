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

import { BattleAggregate, UnitEntity } from './domain/index';
import { dist, idx, inBounds, lineBetween, ring, sameCoord } from './grid';
import {
  awardCombatProgress,
  awardDamageTakenMomentum,
  awardRankProgress,
  changeMomentum,
  type RankProgressionPolicy,
} from './progression';
import { type WeaponHitEffectHandlerRegistry } from './hit-effects';
import { areAllies, areEnemies, requireUnit, unitAtCoord } from './state';
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
import { resolveMoraleAfterDamage } from './morale';
import { announceUnitDeparture, announceUnitFall, type UnitDepartureRules } from './unit-departure';
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
export interface CombatPlanRules extends CombatRules, UnitDepartureRules {
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly progression: RankProgressionPolicy;
}

const SUPPORT_ATTACK_MULTIPLIER = 0.6;

function supportDamage(damage: DamageBreakdown): DamageBreakdown {
  const modifier = {
    id: 'reaction.support-attack',
    label: '援护攻击',
    source: 'reaction' as const,
    stage: 'final' as const,
    operation: 'multiply' as const,
    value: SUPPORT_ATTACK_MULTIPLIER,
  };
  return {
    ...damage,
    reactionMultiplier: SUPPORT_ATTACK_MULTIPLIER,
    modifiers: [...damage.modifiers, modifier],
    damage: Math.max(1, Math.round(damage.damage * SUPPORT_ATTACK_MULTIPLIER)),
  };
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
  const candidates = state.units
    .filter((candidate) =>
      candidate.id !== attacker.id &&
      areAllies(state, candidate.owner, attacker.owner) &&
      candidate.reaction === 'support' &&
      candidate.reactionUsedRound !== state.turn &&
      dist(candidate, attackerAt) === 1)
    .flatMap((supporter) => unitWeapons(supporter, content)
      .filter((weapon) =>
        weapon.canCounter &&
        isWeaponReady(rules, supporter, weapon, state.players.find((p) => p.id === supporter.owner)) &&
        canReachWithWeapon(weapon, supporter, defender))
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

/** Deterministic cells affected after a legal primary target has been chosen. */
export function weaponAreaCells(
  state: GameState,
  from: Coord,
  aimedAt: Coord,
  weapon: WeaponDef,
): Coord[] {
  let cells: Coord[];
  switch (weapon.area) {
    case 'single':
      cells = [{ ...aimedAt }];
      break;
    case 'cross1':
      cells = ring(state.map, aimedAt, 0, 1);
      break;
    case 'ring1': {
      cells = [];
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const x = aimedAt.x + dx;
          const y = aimedAt.y + dy;
          if (inBounds(state.map, x, y)) cells.push({ x, y });
        }
      }
      break;
    }
    case 'line':
      cells = lineBetween(from, aimedAt).slice(1);
      break;
  }
  const unique = new Map<number, Coord>();
  for (const cell of cells) unique.set(idx(state.map, cell.x, cell.y), cell);
  return [...unique.values()];
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

export function forecastCombatPlan(
  rules: CombatRules,
  state: GameState,
  attacker: Unit,
  aimedAt: Coord,
  options: CombatPlanOptions = {},
): CombatPlan {
  const content = rules.content;
  const from = options.from ?? { x: attacker.x, y: attacker.y };
  const resolvedWeaponId = options.weapon ?? primaryWeapon(attacker, content).id;
  if (!hostileActionAllowed(state, attacker.owner, from, aimedAt, 'attack')) {
    throw new Error('combat plan target is protected by an engagement rule');
  }
  const weapon = content.weapons.get(resolvedWeaponId);
  const primaryTarget = unitAtCoord(state, aimedAt);
  const primaryStructureTarget = hostileStructure(state, attacker, aimedAt, content);
  // An area weapon may land on a tile whose occupant left — that is the whole
  // point of charge time, and every step below already copes with a null
  // primary. A single-target weapon aimed at nothing has nothing to resolve.
  if (!primaryTarget && !primaryStructureTarget && weapon.area === 'single') {
    throw new Error('combat plan requires a hostile primary target');
  }
  if (primaryTarget && !areEnemies(state, primaryTarget.owner, attacker.owner)) {
    throw new Error('combat plan cannot target an allied unit');
  }

  const primaryUnit = primaryTarget
    ? forecast(rules, state, attacker, primaryTarget, { attackFrom: from, weapon: resolvedWeaponId })
    : null;
  const primaryStructure = primaryStructureTarget
    ? forecastStructure(rules, state, attacker, primaryStructureTarget, { weapon: resolvedWeaponId })
    : null;
  const affectedCells = weaponAreaCells(state, from, aimedAt, weapon);
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
    const unit = unitAtCoord(state, cell);
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

function applyUnitHit(
  rules: CombatPlanRules,
  state: GameState,
  attacker: Unit,
  hit: PlannedUnitHit,
  emit: (event: GameEvent) => void,
): boolean {
  const { content, resources, progression, hitEffects } = rules;
  // A prior hit in the same area plan can rout a later recipient through
  // morale shock. The immutable plan still describes the aimed area, but a
  // unit that has already left the battlefield is no longer a legal mutation
  // target and must not make the remaining resolution fail.
  const target = state.units.find((unit) => unit.id === hit.target);
  if (!target) return false;
  const result = new BattleAggregate(state, content).damageUnit(hit.target, hit.damage.damage);
  emit({
    type: hit.primary ? 'attack' : 'areaAttack',
    attacker: attacker.id,
    defender: hit.target,
    protectedUnit: hit.protectedUnit,
    weapon: hit.damage.weapon,
    damage: result.amount,
    killed: result.killed,
  });
  if (result.fall) announceUnitFall(rules, state, result.fall, emit);
  awardCombatProgress(attacker, result.amount, result.killed, emit, progression, content);
  if (!result.killed && hit.effects.length > 0) {
    hitEffects.apply(rules, state, attacker, requireUnit(state, hit.target), hit.effects, emit);
  }
  if (!result.killed && resolveMoraleAfterDamage(state, target, result.amount, false, result.at, emit, content)) {
    announceUnitDeparture(rules, state, target, emit);
  }
  if (result.killed) resolveMoraleAfterDamage(state, target, result.amount, true, result.at, emit, content);
  if (!result.killed) awardDamageTakenMomentum(target, emit, resources);
  return result.killed;
}

function applyStructureHit(
  rules: CombatPlanRules,
  state: GameState,
  attacker: Unit,
  hit: PlannedStructureHit,
  emit: (event: GameEvent) => void,
): void {
  const { content, progression } = rules;
  emit({
    type: hit.primary ? 'attackStructure' : 'areaAttackStructure',
    attacker: attacker.id,
    structure: hit.target,
    weapon: hit.forecast.weapon,
    damage: hit.forecast.damage,
    destroyed: hit.forecast.destroyed,
  });
  const damage = damageStructure(state, hit.target, hit.forecast.rawDamage, content, emit);
  awardCombatProgress(attacker, damage, hit.forecast.destroyed, emit, progression, content);
}

/** Commits an already forecast plan in one fixed, tested phase order. */
export function executeCombatPlan(
  rules: CombatPlanRules,
  state: GameState,
  plan: CombatPlan,
  emit: (event: GameEvent) => void,
): void {
  const { content, resources, progression, hitEffects } = rules;
  const attacker = requireUnit(state, plan.attacker);
  consumeWeapon(rules, state, attacker, plan.weapon, emit);

  if (plan.primaryUnit?.reaction) {
    const reaction = plan.primaryUnit.reaction;
    const reactor = state.units.find((candidate) => candidate.id === reaction.unit);
    if (reactor) {
      new UnitEntity(reactor).consumeReaction(state.turn);
      if (reaction.stance === 'support') awardRankProgress(reactor, 20, emit, progression, content);
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
    unitKilled = applyUnitHit(rules, state, attacker, hit, emit) || unitKilled;
  }
  for (const hit of plan.structureHits) applyStructureHit(rules, state, attacker, hit, emit);
  changeMomentum(
    attacker,
    unitKilled || plan.structureHits.some((hit) => hit.forecast.destroyed) ? 10 : 5,
    emit,
    resources,
  );

  const counter = plan.primaryUnit?.counter;
  const defenderId = plan.primaryUnit?.defender;
  if (counter && defenderId !== undefined && state.units.some((unit) => unit.id === defenderId)) {
    const defender = requireUnit(state, defenderId);
    consumeWeapon(rules, state, defender, counter.weapon, emit);
    const result = new BattleAggregate(state, content).damageUnit(attacker.id, counter.damage);
    emit({
      type: 'counter',
      attacker: defender.id,
      defender: attacker.id,
      weapon: counter.weapon,
      damage: result.amount,
      killed: result.killed,
    });
    if (result.fall) announceUnitFall(rules, state, result.fall, emit);
    else {
      hitEffects.apply(rules, state, defender, requireUnit(state, attacker.id), content.weapons.get(counter.weapon).hitEffects, emit);
      awardDamageTakenMomentum(attacker, emit, resources);
    }
    if (!result.killed && resolveMoraleAfterDamage(state, attacker, result.amount, false, result.at, emit, content)) {
      announceUnitDeparture(rules, state, attacker, emit);
    }
    if (result.killed) resolveMoraleAfterDamage(state, attacker, result.amount, true, result.at, emit, content);
    awardCombatProgress(defender, result.amount, result.killed, emit, progression, content);
    changeMomentum(defender, result.killed ? 10 : 5, emit, resources);
  }

  const support = plan.supportAttack;
  if (!support) return;
  const supporter = state.units.find((unit) => unit.id === support.attacker);
  const target = state.units.find((unit) => unit.id === support.target);
  if (!supporter || !target) return;
  new UnitEntity(supporter).consumeReaction(state.turn);
  consumeWeapon(rules, state, supporter, support.weapon, emit);
  const result = new BattleAggregate(state, content).damageUnit(target.id, support.damage.damage);
  emit({
    type: 'supportAttack',
    attacker: supporter.id,
    defender: target.id,
    weapon: support.weapon,
    damage: result.amount,
    killed: result.killed,
  });
  if (result.fall) announceUnitFall(rules, state, result.fall, emit);
  else {
    hitEffects.apply(rules, state, supporter, requireUnit(state, target.id), support.effects, emit);
    awardDamageTakenMomentum(target, emit, resources);
  }
  if (!result.killed && resolveMoraleAfterDamage(state, target, result.amount, false, result.at, emit, content)) {
    announceUnitDeparture(rules, state, target, emit);
  }
  if (result.killed) resolveMoraleAfterDamage(state, target, result.amount, true, result.at, emit, content);
  awardCombatProgress(supporter, result.amount, result.killed, emit, progression, content);
  changeMomentum(supporter, result.killed ? 10 : 5, emit, resources);
}

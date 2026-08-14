import { boardOf, type Board } from './domain/board';
import { combinedStatusModifiers } from './statuses';
import { Battlefield } from './domain/battlefield';
import { commanderAuraFor } from './commanders';
import { areAllies } from './state';
import { player } from './state';
import { UnitEntity } from './domain/unit-entity';
import { DomainInvariantError } from './domain/errors';
import { attackerStrength, type CombatModifier, type CombatModifierPipeline } from './combat-modifiers';
import type { Coord, GameEvent, GameState, PlayerState, ReactionStance, StructureState, Unit, WeaponDef, WeaponId } from './types';
import {
  type BattleResourceSystem,
  canAffordTransactions,
} from './resources';
import type { ContentCatalog } from './content-pack';
import { reactionOf, type ReactionBehavior, type ReactionRules } from './reactions';
import type { GridRules } from './tactical-grid';

export { MAX_MITIGATION } from './combat-modifiers';

/**
 * Ports declared by this module, not imposed on it.
 *
 * Dependency inversion in the consumer's direction: combat states the narrow
 * capability set it needs, and the composition-level `BattleRuleServices`
 * satisfies it structurally without either side importing the other.
 */
export interface WeaponRules extends GridRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
}

export interface CombatRules extends WeaponRules, ReactionRules, GridRules {
  readonly combatModifiers: CombatModifierPipeline;
}

/**
 * One strike, and why it came out at that number.
 *
 * The explanation is the modifier chain — ordered, labelled, and open to any
 * provider a rule plugin registers. It used to be stated twice: nine further
 * fields carried `effectiveness`, `strength`, `terrainDefense` and the rest,
 * each re-derived from that same chain by matching a modifier id string. Two
 * representations of one fact, and the second could only ever describe the
 * contributions the core happened to know the names of.
 */
export class DamageBreakdown {
  constructor(
    readonly weapon: WeaponId,
    readonly damageType: WeaponDef['damageType'],
    readonly base: number,
    /** Fraction of the raw number absorbed before it landed, 0..1. */
    readonly mitigation: number,
    /** Ordered explanation used by HUDs, logs, balance tools and mods. */
    readonly modifiers: readonly CombatModifier[],
    readonly damage: number,
  ) {}

  /**
   * What one named contribution was worth. A contribution nobody made is the
   * neutral value for its stage, so a caller never has to know whether the
   * modifier was registered.
   */
  factorOf(id: string, absent = 1): number {
    return this.modifiers.find((modifier) => modifier.id === id)?.value ?? absent;
  }

  /** Product of every contribution whose id shares a prefix, e.g. one family. */
  familyFactor(idPrefix: string): number {
    return this.modifiers
      .filter((modifier) => modifier.id.startsWith(idPrefix))
      .reduce((product, modifier) => product * modifier.value, 1);
  }

  familyLabels(idPrefix: string): string[] {
    return this.modifiers.filter((modifier) => modifier.id.startsWith(idPrefix)).map((modifier) => modifier.label);
  }

  /** The same strike, with one more contribution folded in at the end. */
  and(modifier: CombatModifier): DamageBreakdown {
    return new DamageBreakdown(
      this.weapon,
      this.damageType,
      this.base,
      this.mitigation,
      [...this.modifiers, modifier],
      Math.max(1, Math.round(this.damage * modifier.value)),
    );
  }
}

export interface CombatForecast {
  attacker: number;
  defender: number;
  /** Damage the attacker deals. */
  strike: DamageBreakdown;
  /** Unit that actually receives the strike; differs when support intercepts. */
  damageRecipient: number;
  interceptor: number | null;
  /** Stance that changed the exchange, if any. Open: stances are content. */
  reaction: { unit: number; stance: ReactionStance; protectedUnit?: number } | null;
  recipientHpAfter: number;
  recipientDies: boolean;
  defenderHpAfter: number;
  defenderDies: boolean;
  /** Retaliation, present only when the defender survives and can reach back. */
  counter: DamageBreakdown | null;
  attackerHpAfter: number;
  attackerDies: boolean;
}

export interface StructureCombatForecast {
  attacker: number;
  structure: string;
  weapon: WeaponId;
  base: number;
  strength: number;
  statusAttackMultiplier: number;
  commanderAttackMultiplier: number;
  targetBonusMultiplier: number;
  targetBonusReasons: string[];
  structureDefense: number;
  rawDamage: number;
  damage: number;
  hpAfter: number;
  destroyed: boolean;
}

/** Where a strike happens. Absent coordinates default to the units' tiles. */
export interface StrikeGeometry {
  attackerAt?: Coord;
  defenderAt?: Coord;
  weapon?: WeaponId;
}

export interface ForecastOptions {
  /** Tile the attacker strikes from; defaults to its current tile. */
  attackFrom?: Coord;
  weapon?: WeaponId;
}

export interface StructureAttackOptions {
  weapon?: WeaponId;
}



export function unitWeapons(content: ContentCatalog, unit: Unit): WeaponDef[] {
  return content.units.get(unit.type).weapons.map((id) => content.weapons.get(id));
}

/** Can this unit fire a weapon it is already holding? Ammunition, cooldown, upkeep. */
export function isWeaponReady(
  rules: WeaponRules,
  unit: Unit,
  weapon: WeaponDef,
  owner?: PlayerState,
): boolean {
  if (!rules.content.units.get(unit.type).weapons.includes(weapon.id)) return false;
  const context = { player: owner, unit, weapon: weapon.id };
  return new UnitEntity(unit).canUseWeapon(weapon) &&
    canAffordTransactions(rules.resources, weapon.resourceRequirements, context) &&
    canAffordTransactions(rules.resources, weapon.resourceCosts, context);
}

export function primaryWeapon(content: ContentCatalog, unit: Unit): WeaponDef {
  const weapon = unitWeapons(content, unit)[0];
  if (!weapon) throw new DomainInvariantError(`${content.units.get(unit.type).name} has no weapon`);
  return weapon;
}

/**
 * The weapon, if this unit both carries it and can fire it right now.
 *
 * Asking and committing are different acts, so they are different functions.
 * One function that answered "no" by throwing forced every menu, every AI
 * enumeration and every legality check to wrap it in `try/catch` and return
 * false — which also swallowed a unit whose weapon list names a weapon the
 * content never defined, turning a typo in a pack into "this unit can never
 * attack" instead of an error anyone could find.
 */
export function readyWeapon(
  rules: WeaponRules,
  unit: Unit,
  id: WeaponId,
  owner?: PlayerState,
): WeaponDef | null {
  if (!rules.content.units.get(unit.type).weapons.includes(id)) return null;
  // Past this point the unit claims the weapon, so an unknown id is a content
  // defect and the registry is right to say so out loud.
  const definition = rules.content.weapons.get(id);
  return isWeaponReady(rules, unit, definition, owner) ? definition : null;
}

/** The same question, asked by a caller that has already established the answer. */
export function requireReadyWeapon(
  rules: WeaponRules,
  unit: Unit,
  id: WeaponId,
  owner?: PlayerState,
): WeaponDef {
  const weapon = readyWeapon(rules, unit, id, owner);
  if (!weapon) {
    throw new DomainInvariantError(
      `${rules.content.units.get(unit.type).name} cannot fire weapon "${id}" right now`,
    );
  }
  return weapon;
}

/** Commits one successful strike. Forecasting never mutates this state. */
export function consumeWeapon(
  rules: WeaponRules,
  state: GameState,
  unit: Unit,
  id: WeaponId,
  emit: (event: GameEvent) => void = () => {},
): void {
  const owner = player(state, unit.owner);
  const weapon = requireReadyWeapon(rules, unit, id, owner);
  const transactionContext = { player: owner, unit, weapon: weapon.id };
  for (const cost of weapon.resourceCosts) {
    const subject = rules.resources.subjectFor(transactionContext, cost);
    const amount = rules.resources.spend(cost.resource, subject, cost.amount);
    rules.resources.announce(subject, cost.resource, -amount, emit);
  }
  new UnitEntity(unit).commitWeaponCooldown(weapon);
}

function weaponTargetBonus(weapon: WeaponDef, tags: string[]): { multiplier: number; reasons: string[] } {
  const matches = weapon.bonuses.filter((bonus) => tags.includes(bonus.targetTag));
  return {
    multiplier: matches.reduce((value, bonus) => value * bonus.multiplier, 1),
    reasons: matches.map((bonus) => bonus.reason),
  };
}

/**
 * Deterministic damage — the HUD forecast is the truth.
 *
 *   damage = attack x effectiveness x (0.5 + 0.5 x hp%) x (1 - mitigation)
 */
export function computeDamage(
  rules: CombatRules,
  state: GameState,
  attacker: Unit,
  defender: Unit,
  geometry: StrikeGeometry = {},
): DamageBreakdown {
  const content = rules.content;
  const attackerAt = geometry.attackerAt ?? { x: attacker.x, y: attacker.y };
  const defenderAt = geometry.defenderAt ?? { x: defender.x, y: defender.y };
  const resolvedWeaponId = geometry.weapon ?? primaryWeapon(content, attacker).id;
  const weapon = requireReadyWeapon(rules, attacker, resolvedWeaponId, player(state, attacker.owner));
  const base = weapon.power;
  const battlefield = new Battlefield(state, content);
  const result = rules.combatModifiers.evaluate(base, {
    rules,
    board: boardOf(rules, state),
    state,
    attacker,
    attackerAt,
    defender,
    defenderAt,
    weapon,
    content,
    battlefield,
  });

  return new DamageBreakdown(
    weapon.id,
    weapon.damageType,
    base,
    result.mitigation,
    result.modifiers,
    result.damage,
  );
}

export function forecastStructure(
  rules: WeaponRules,
  state: GameState,
  attacker: Unit,
  structure: StructureState,
  options: StructureAttackOptions = {},
): StructureCombatForecast {
  const content = rules.content;
  const resolvedWeaponId = options.weapon ?? primaryWeapon(content, attacker).id;
  const weapon = requireReadyWeapon(rules, attacker, resolvedWeaponId, player(state, attacker.owner));
  const def = content.structures.get(structure.type);
  const statusAttackMultiplier = combinedStatusModifiers(content, attacker).attackMultiplier;
  const commanderAttackMultiplier = commanderAuraFor(rules, state, attacker).attackMultiplier;
  const strength = attackerStrength(content, attacker);
  const targetBonus = weaponTargetBonus(weapon, def.tags);
  const rawDamage = Math.max(
    1,
    Math.round(
      weapon.power * strength * statusAttackMultiplier * commanderAttackMultiplier * targetBonus.multiplier,
    ),
  );
  const damage = Math.min(structure.hp, Math.max(1, Math.round(rawDamage * (1 - def.defense))));
  const hpAfter = Math.max(0, structure.hp - damage);
  return {
    attacker: attacker.id,
    structure: structure.id,
    weapon: weapon.id,
    base: weapon.power,
    strength,
    statusAttackMultiplier,
    commanderAttackMultiplier,
    targetBonusMultiplier: targetBonus.multiplier,
    targetBonusReasons: targetBonus.reasons,
    structureDefense: def.defense,
    rawDamage,
    damage,
    hpAfter,
    destroyed: hpAfter <= 0,
  };
}

export function canReachWithWeapon(board: Board, weapon: WeaponDef, from: Coord, target: Coord): boolean {
  const d = board.distance(from, target);
  return d >= weapon.minRange && d <= weapon.maxRange;
}

/** A combatant and the tile it fights from, which are not always its own. */
export interface Combatant {
  readonly unit: Unit;
  readonly at: Coord;
}

export interface ReactiveStrike {
  readonly weapon: WeaponDef;
  readonly damage: DamageBreakdown;
}

/**
 * The strongest blow `reactor` can land on `target` without spending its turn.
 *
 * The riposte and the parting shot are the same question — what does this unit
 * still have ready, and does it reach from where it stands — so they ask it
 * once. The stance comes in whole rather than as the one flag this needs: a
 * stance that hoards its resources hoards them for every kind of reaction.
 */
export function bestReactiveStrike(
  rules: CombatRules,
  state: GameState,
  reactor: Combatant,
  target: Combatant,
  stance: ReactionBehavior,
): ReactiveStrike | null {
  const owner = player(state, reactor.unit.owner);
  const candidates = unitWeapons(rules.content, reactor.unit)
    .filter((weapon) => {
      if (stance.conservesResources && (weapon.resourceCosts.length > 0 || weapon.cooldown > 0)) return false;
      return weapon.canCounter &&
        isWeaponReady(rules, reactor.unit, weapon, owner) &&
        canReachWithWeapon(boardOf(rules, state), weapon, reactor.at, target.at);
    })
    .map((weapon) => ({
      weapon,
      damage: computeDamage(rules, state, reactor.unit, target.unit, {
        attackerAt: reactor.at,
        defenderAt: target.at,
        weapon: weapon.id,
      }),
    }))
    .sort((a, b) => b.damage.damage - a.damage.damage || a.weapon.id.localeCompare(b.weapon.id));
  return candidates[0] ?? null;
}

/** The ally that steps in front of this defender, if any stance offers to. */
function interceptorFor(rules: CombatRules, state: GameState, defender: Unit): Unit | null {
  const board = boardOf(rules, state);
  return (
    state.units
      .filter(
        (candidate) =>
          candidate.id !== defender.id &&
          areAllies(state, candidate.owner, defender.owner) &&
          reactionOf(rules, candidate.reaction).intercepts &&
          new UnitEntity(candidate).canReact(state.turn) &&
          board.distance(candidate, defender) === 1,
      )
      .sort((a, b) => b.hp - a.hp || a.id - b.id)[0] ?? null
  );
}

function applyReactionMultiplier(
  damage: DamageBreakdown,
  behavior: ReactionBehavior,
): DamageBreakdown {
  return damage.and({
    id: `reaction.${behavior.id}`,
    label: behavior.name,
    source: 'reaction',
    stage: 'final',
    operation: 'multiply',
    value: behavior.incomingMultiplier,
  });
}

/**
 * Full exchange preview. Retaliation is derived purely from range coverage:
 * an archer hitting at distance 2 takes nothing back from a swordsman, but is
 * countered when it shoots from an adjacent tile. Siege units (minRange 2) can
 * never be countered by melee, and never counter themselves.
 */
export function forecast(
  rules: CombatRules,
  state: GameState,
  attacker: Unit,
  defender: Unit,
  options: ForecastOptions = {},
): CombatForecast {
  const attackFrom = options.attackFrom ?? { x: attacker.x, y: attacker.y };
  const resolvedWeaponId = options.weapon ?? primaryWeapon(rules.content, attacker).id;
  const defenderAt = { x: defender.x, y: defender.y };
  const interceptor = interceptorFor(rules, state, defender);
  const recipient = interceptor ?? defender;
  let strike = computeDamage(rules, state, attacker, recipient, {
    attackerAt: attackFrom,
    defenderAt: recipient,
    weapon: resolvedWeaponId,
  });
  const stance = reactionOf(rules, defender.reaction);
  let reaction: CombatForecast['reaction'] = null;
  if (interceptor) {
    reaction = {
      unit: interceptor.id,
      stance: interceptor.reaction,
      protectedUnit: defender.id,
    };
  } else if (stance.incomingMultiplier !== 1 && new UnitEntity(defender).canReact(state.turn)) {
    strike = applyReactionMultiplier(strike, stance);
    reaction = { unit: defender.id, stance: stance.id };
  }

  const recipientHpAfter = Math.max(0, recipient.hp - strike.damage);
  const recipientDies = recipientHpAfter <= 0;
  const defenderHpAfter = recipient.id === defender.id ? recipientHpAfter : defender.hp;
  const defenderDies = defenderHpAfter <= 0;

  let counter: DamageBreakdown | null = null;
  let attackerHpAfter = attacker.hp;

  if (!defenderDies && stance.retaliates && state.rules.counterAttack) {
    const counterSource: Unit = { ...defender, hp: defenderHpAfter };
    const counterTarget: Unit = {
      ...attacker,
      facing: boardOf(rules, state).grid.toward(attackFrom, defenderAt),
    };
    const candidate = bestReactiveStrike(
      rules,
      state,
      { unit: counterSource, at: defenderAt },
      { unit: counterTarget, at: attackFrom },
      stance,
    );
    if (candidate) {
      counter = candidate.damage;
      attackerHpAfter = Math.max(0, attacker.hp - counter.damage);
    }
  }

  return {
    attacker: attacker.id,
    defender: defender.id,
    strike,
    damageRecipient: recipient.id,
    interceptor: interceptor?.id ?? null,
    reaction,
    recipientHpAfter,
    recipientDies,
    defenderHpAfter,
    defenderDies,
    counter,
    attackerHpAfter,
    attackerDies: attackerHpAfter <= 0,
  };
}

/** Default amount restored by a support-healing ability. */
export function healAmount(content: ContentCatalog, source: Unit, target: Unit): number {
  const def = content.units.get(target.type);
  const power = Number(source.meta.healPower ?? 30);
  return Math.min(power, def.maxHp - target.hp);
}

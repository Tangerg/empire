import { dist } from './grid';
import { directionToward } from './spatial';
import { combinedStatusModifiers } from './statuses';
import { Battlefield } from './domain/battlefield';
import { commanderAuraFor } from './commanders';
import { areAllies } from './state';
import { player } from './state';
import { UnitEntity } from './domain/unit-entity';
import { DomainInvariantError } from './domain/errors';
import { type CombatModifier, type CombatModifierPipeline } from './combat-modifiers';
import type { Coord, GameEvent, GameState, PlayerState, ReactionStance, StructureState, Unit, WeaponDef, WeaponId } from './types';
import {
  type BattleResourceSystem,
  canAffordTransactions,
  transactionSubject,
} from './resources';
import type { ContentCatalog } from './content-pack';
import { reactionOf, type ReactionBehavior, type ReactionRules } from './reactions';

export { MAX_MITIGATION } from './combat-modifiers';

/**
 * Ports declared by this module, not imposed on it.
 *
 * Dependency inversion in the consumer's direction: combat states the narrow
 * capability set it needs, and the composition-level `BattleRuleServices`
 * satisfies it structurally without either side importing the other.
 */
export interface WeaponRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
}

export interface CombatRules extends WeaponRules, ReactionRules {
  readonly combatModifiers: CombatModifierPipeline;
}

export interface DamageBreakdown {
  weapon: WeaponId;
  damageType: WeaponDef['damageType'];
  base: number;
  /** Damage-type vs armour multiplier. */
  effectiveness: number;
  targetBonusMultiplier: number;
  targetBonusReasons: string[];
  /** Attacker's strength scaled by its remaining HP (0.5 .. 1.0). */
  strength: number;
  terrainDefense: number
  unitDefense: number;
  statusAttackMultiplier: number;
  commanderAttackMultiplier: number;
  commanderDefenseDelta: number;
  reactionMultiplier: number;
  mitigation: number;
  /** Ordered explanation used by HUDs, logs, balance tools and mods. */
  modifiers: CombatModifier[];
  damage: number;
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

const hpRatio = (content: ContentCatalog, u: Unit) => u.hp / content.units.get(u.type).maxHp;

export function unitWeapons(unit: Unit, content: ContentCatalog): WeaponDef[] {
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

export function primaryWeapon(unit: Unit, content: ContentCatalog): WeaponDef {
  const weapon = unitWeapons(unit, content)[0];
  if (!weapon) throw new Error(`${content.units.get(unit.type).name} has no weapon`);
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
    const subject = transactionSubject(cost, transactionContext);
    const amount = rules.resources.spend(cost.resource, subject, cost.amount);
    const current = rules.resources.balance(cost.resource, subject);
    if (current === null) continue;
    const eventSubject = subject.kind === 'player'
      ? { kind: 'player' as const, id: subject.player.id }
      : subject.kind === 'unit'
        ? { kind: 'unit' as const, id: subject.unit.id }
        : { kind: 'weapon' as const, unit: subject.unit.id, weapon: subject.weapon };
    emit({
      type: 'resourceChanged',
      resource: cost.resource,
      subject: eventSubject,
      amount: -amount,
      current,
    });
  }
  new UnitEntity(unit).commitWeaponCooldown(weapon);
}

export function terrainDefenseAt(state: GameState, c: Coord, content: ContentCatalog): number {
  return new Battlefield(state, content).cell(c).defense;
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
  const resolvedWeaponId = geometry.weapon ?? primaryWeapon(attacker, content).id;
  const weapon = requireReadyWeapon(rules, attacker, resolvedWeaponId, player(state, attacker.owner));
  const base = weapon.power;
  const battlefield = new Battlefield(state, content);
  const result = rules.combatModifiers.evaluate(base, {
    state: state,
    attacker,
    attackerAt,
    defender,
    defenderAt,
    weapon,
    content,
    battlefield,
  });
  const modifier = (id: string) => result.modifiers.find((candidate) => candidate.id === id);
  const tagModifiers = result.modifiers.filter((candidate) => candidate.id.startsWith('weapon.target-tag.'));
  const unitDefenseModifier = modifier('defense.unit');

  return {
    weapon: weapon.id,
    damageType: weapon.damageType,
    base,
    effectiveness: modifier('matchup.effectiveness')?.value ?? 1,
    targetBonusMultiplier: tagModifiers.reduce((value, entry) => value * entry.value, 1),
    targetBonusReasons: tagModifiers.map((entry) => entry.label),
    strength: modifier('unit.hp-strength')?.value ?? 1,
    terrainDefense: modifier('defense.terrain')?.value ?? 0,
    unitDefense: unitDefenseModifier?.value ?? 0,
    statusAttackMultiplier: modifier('status.attack')?.value ?? 1,
    commanderAttackMultiplier: modifier('commander.attack')?.value ?? 1,
    commanderDefenseDelta: Number(unitDefenseModifier?.details?.commanderDelta ?? 0),
    reactionMultiplier: 1,
    mitigation: result.mitigation,
    modifiers: result.modifiers,
    damage: result.damage,
  };
}

export function forecastStructure(
  rules: WeaponRules,
  state: GameState,
  attacker: Unit,
  structure: StructureState,
  options: StructureAttackOptions = {},
): StructureCombatForecast {
  const content = rules.content;
  const resolvedWeaponId = options.weapon ?? primaryWeapon(attacker, content).id;
  const weapon = requireReadyWeapon(rules, attacker, resolvedWeaponId, player(state, attacker.owner));
  const def = content.structures.get(structure.type);
  const statusAttackMultiplier = combinedStatusModifiers(attacker, content).attackMultiplier;
  const commanderAttackMultiplier = commanderAuraFor(state, attacker).attackMultiplier;
  const strength = 0.5 + 0.5 * hpRatio(content, attacker);
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

/** Can `unit`, standing at `from`, reach `target` with its weapon? */
export function canReach(unit: Unit, from: Coord, target: Coord, content: ContentCatalog): boolean {
  return canReachWithWeapon(primaryWeapon(unit, content), from, target);
}

export function canReachWithWeapon(weapon: WeaponDef, from: Coord, target: Coord): boolean {
  const d = dist(from, target);
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
  const candidates = unitWeapons(reactor.unit, rules.content)
    .filter((weapon) => {
      if (stance.conservesResources && (weapon.resourceCosts.length > 0 || weapon.cooldown > 0)) return false;
      return weapon.canCounter &&
        isWeaponReady(rules, reactor.unit, weapon, owner) &&
        canReachWithWeapon(weapon, reactor.at, target.at);
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
function interceptorFor(rules: ReactionRules, state: GameState, defender: Unit): Unit | null {
  return (
    state.units
      .filter(
        (candidate) =>
          candidate.id !== defender.id &&
          areAllies(state, candidate.owner, defender.owner) &&
          reactionOf(rules, candidate.reaction).intercepts &&
          new UnitEntity(candidate).canReact(state.turn) &&
          dist(candidate, defender) === 1,
      )
      .sort((a, b) => b.hp - a.hp || a.id - b.id)[0] ?? null
  );
}

function applyReactionMultiplier(
  damage: DamageBreakdown,
  behavior: ReactionBehavior,
): DamageBreakdown {
  const multiplier = behavior.incomingMultiplier;
  const reactionModifier: CombatModifier = {
    id: `reaction.${behavior.id}`,
    label: behavior.name,
    source: 'reaction',
    stage: 'final',
    operation: 'multiply',
    value: multiplier,
  };
  return {
    ...damage,
    reactionMultiplier: multiplier,
    modifiers: [...damage.modifiers, reactionModifier],
    damage: Math.max(1, Math.round(damage.damage * multiplier)),
  };
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
  const resolvedWeaponId = options.weapon ?? primaryWeapon(attacker, rules.content).id;
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
    const counterTarget: Unit = { ...attacker, facing: directionToward(attackFrom, defenderAt) };
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
export function healAmount(source: Unit, target: Unit, content: ContentCatalog): number {
  const def = content.units.get(target.type);
  const power = Number(source.meta.healPower ?? 30);
  return Math.min(power, def.maxHp - target.hp);
}

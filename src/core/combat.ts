import { Terrains } from './data/terrain';
import { EFFECTIVENESS, unitDef } from './data/units';
import { dist, idx } from './grid';
import type { Coord, GameState, Unit } from './types';

export const MAX_MITIGATION = 0.6;

export interface DamageBreakdown {
  base: number;
  /** Damage-type vs armour multiplier. */
  effectiveness: number;
  /** Attacker's strength scaled by its remaining HP (0.5 .. 1.0). */
  strength: number;
  terrainDefense: number
  unitDefense: number;
  mitigation: number;
  damage: number;
}

export interface CombatForecast {
  attacker: number;
  defender: number;
  /** Damage the attacker deals. */
  strike: DamageBreakdown;
  defenderHpAfter: number;
  defenderDies: boolean;
  /** Retaliation, present only when the defender survives and can reach back. */
  counter: DamageBreakdown | null;
  attackerHpAfter: number;
  attackerDies: boolean;
}

const hpRatio = (u: Unit) => u.hp / unitDef(u.type).maxHp;

export function terrainDefenseAt(s: GameState, c: Coord): number {
  return Terrains.get(s.map.tiles[idx(s.map, c.x, c.y)]).defense;
}

/**
 * Deterministic damage — the HUD forecast is the truth. `rules.damageVariance`
 * exists as an escape hatch for future modes but is 0 by default.
 *
 *   damage = attack x effectiveness x (0.5 + 0.5 x hp%) x (1 - mitigation)
 */
export function computeDamage(
  s: GameState,
  attacker: Unit,
  defender: Unit,
  defenderAt: Coord = { x: defender.x, y: defender.y },
): DamageBreakdown {
  const atk = unitDef(attacker.type);
  const dfn = unitDef(defender.type);

  const base = atk.attack;
  const effectiveness = EFFECTIVENESS[atk.damageType][dfn.armorClass];
  const strength = 0.5 + 0.5 * hpRatio(attacker);
  const terrainDefense = terrainDefenseAt(s, defenderAt);
  const unitDefense = dfn.defense;
  const mitigation = Math.min(MAX_MITIGATION, terrainDefense + unitDefense);

  const raw = base * effectiveness * strength * (1 - mitigation);
  const damage = Math.max(1, Math.round(raw));

  return { base, effectiveness, strength, terrainDefense, unitDefense, mitigation, damage };
}

/** Can `unit`, standing at `from`, reach `target` with its weapon? */
export function canReach(unit: Unit, from: Coord, target: Coord): boolean {
  const def = unitDef(unit.type);
  const d = dist(from, target);
  return d >= def.minRange && d <= def.maxRange;
}

/**
 * Full exchange preview. Retaliation is derived purely from range coverage:
 * an archer hitting at distance 2 takes nothing back from a swordsman, but is
 * countered when it shoots from an adjacent tile. Siege units (minRange 2) can
 * never be countered by melee, and never counter themselves.
 */
export function forecast(
  s: GameState,
  attacker: Unit,
  defender: Unit,
  attackFrom: Coord = { x: attacker.x, y: attacker.y },
): CombatForecast {
  const defenderAt = { x: defender.x, y: defender.y };
  const strike = computeDamage(s, attacker, defender, defenderAt);
  const defenderHpAfter = Math.max(0, defender.hp - strike.damage);
  const defenderDies = defenderHpAfter <= 0;

  let counter: DamageBreakdown | null = null;
  let attackerHpAfter = attacker.hp;

  if (!defenderDies && s.rules.counterAttack && canReach(defender, defenderAt, attackFrom)) {
    const wounded: Unit = { ...defender, hp: defenderHpAfter };
    counter = computeDamage(s, wounded, attacker, attackFrom);
    attackerHpAfter = Math.max(0, attacker.hp - counter.damage);
  }

  return {
    attacker: attacker.id,
    defender: defender.id,
    strike,
    defenderHpAfter,
    defenderDies,
    counter,
    attackerHpAfter,
    attackerDies: attackerHpAfter <= 0,
  };
}

/** How much HP a cleric-style ability restores. */
export function healAmount(source: Unit, target: Unit): number {
  const def = unitDef(target.type);
  const power = Number(source.meta.healPower ?? 30);
  return Math.min(power, def.maxHp - target.hp);
}

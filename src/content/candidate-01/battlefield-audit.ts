import type { LevelData, LevelUnit } from '../../core/types';

export interface DeploymentSpan {
  width: number;
  height: number;
  area: number;
}

export interface BattlefieldAudit {
  id: string;
  width: number;
  height: number;
  cells: number;
  playerUnits: number;
  enemyUnits: number;
  reinforcements: number;
  playerSpan: DeploymentSpan;
  enemySpan: DeploymentSpan;
  occupiedSectors: number;
  closestContact: number;
  initialDensity: number;
}

function span(units: readonly LevelUnit[]): DeploymentSpan {
  if (units.length === 0) return { width: 0, height: 0, area: 0 };
  const xs = units.map((unit) => unit.x);
  const ys = units.map((unit) => unit.y);
  const width = Math.max(...xs) - Math.min(...xs) + 1;
  const height = Math.max(...ys) - Math.min(...ys) + 1;
  return { width, height, area: width * height };
}

function reinforcementCount(level: LevelData): number {
  return (level.scenario?.triggers ?? []).reduce((total, trigger) => total + trigger.effects.reduce((subtotal, effect) => (
    subtotal + (effect.type === 'spawnUnits' && effect.reason === 'reinforcement' ? effect.units.length : 0)
  ), 0), 0) ?? 0;
}

function occupiedSectors(level: LevelData, units: readonly LevelUnit[]): number {
  const sectors = new Set<string>();
  for (const unit of units) {
    const column = Math.min(2, Math.floor(unit.x * 3 / level.width));
    const row = Math.min(2, Math.floor(unit.y * 3 / level.height));
    sectors.add(`${column},${row}`);
  }
  return sectors.size;
}

function closestContact(player: readonly LevelUnit[], enemy: readonly LevelUnit[]): number {
  let closest = Number.POSITIVE_INFINITY;
  for (const ally of player) {
    for (const foe of enemy) closest = Math.min(closest, Math.abs(ally.x - foe.x) + Math.abs(ally.y - foe.y));
  }
  return Number.isFinite(closest) ? closest : 0;
}

/**
 * Produces stable spatial metrics for campaign level review. These are design
 * diagnostics, not combat rules, so the battle kernel remains unaware of map
 * pacing conventions used by this campaign.
 */
export function auditBattlefield(level: LevelData): BattlefieldAudit {
  const player = level.units.filter((unit) => unit.owner === 1);
  const enemy = level.units.filter((unit) => unit.owner === 2);
  const combatants = [...player, ...enemy];
  return {
    id: level.id,
    width: level.width,
    height: level.height,
    cells: level.width * level.height,
    playerUnits: player.length,
    enemyUnits: enemy.length,
    reinforcements: reinforcementCount(level),
    playerSpan: span(player),
    enemySpan: span(enemy),
    occupiedSectors: occupiedSectors(level, combatants),
    closestContact: closestContact(player, enemy),
    initialDensity: combatants.length / (level.width * level.height),
  };
}

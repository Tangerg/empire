import { DomainInvariantError } from './domain/errors';
import { sameCoord } from './grid';
import type { Coord, GameState, PlayerId, ZoneEngagementRule } from './types';

function appliesTo(rule: ZoneEngagementRule, player: PlayerId): boolean {
  return !rule.players?.length || rule.players.includes(player);
}

/** How harshly a zone's truce judges an act: only blows, or any hostility. */
export type EngagementKind = 'attack' | 'hostile-action';

/**
 * Central hostile-action policy. Abilities use this instead of knowing why a
 * tile is protected (truce, hospital, sanctuary, boarding corridor, ...).
 */
export function hostileActionAllowed(
  state: GameState,
  actor: PlayerId,
  from: Coord,
  target: Coord,
  kind: EngagementKind = 'attack',
): boolean {
  return !state.scenario.engagementRules.some((rule) => {
    if (!appliesTo(rule, actor)) return false;
    if (rule.mode === 'no-attacks' && kind !== 'attack') return false;
    const cells = state.scenario.zones[rule.zone] ?? [];
    return cells.some((cell) => sameCoord(cell, from) || sameCoord(cell, target));
  });
}

export function addEngagementRule(state: GameState, rule: ZoneEngagementRule): void {
  if (!state.scenario.zones[rule.zone]) throw new DomainInvariantError(`unknown scenario zone "${rule.zone}"`);
  if (state.scenario.engagementRules.some((candidate) => candidate.id === rule.id)) {
    throw new DomainInvariantError(`engagement rule already exists: "${rule.id}"`);
  }
  state.scenario.engagementRules.push({ ...rule, players: rule.players?.slice() });
}

export function removeEngagementRule(state: GameState, id: string): void {
  const index = state.scenario.engagementRules.findIndex((rule) => rule.id === id);
  if (index >= 0) state.scenario.engagementRules.splice(index, 1);
}

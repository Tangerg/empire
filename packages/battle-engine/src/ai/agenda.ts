import { buildAiMissionIntent, type AiMissionIntent, type AiObjectiveAdvisorRegistry } from '../ai-objectives';
import { type ObjectiveHandlerRegistry } from '../objective-system';
import { areEnemies } from '../state';
import type { ContentCatalog } from '../content-pack';
import type { Coord, GameState, PlayerId } from '../types';

/**
 * What a side is playing for this turn, read once from the board.
 *
 * Named an agenda rather than "objectives" on purpose: the objective *system*
 * owns the formal win conditions, and this is the planner's reading of them
 * plus the plain territorial facts no objective states — which keeps has-a-goal
 * and wants-that-tile from sharing one word.
 */
export interface AiAgenda {
  /** Tiles worth walking to: enemy/neutral capturables, weighted. */
  readonly captureTargets: readonly { at: Coord; weight: number }[];
  readonly enemyHqs: readonly Coord[];
  readonly myHqs: readonly Coord[];
  /** The formal objectives, translated into pulls and priorities. */
  readonly mission: AiMissionIntent;
}

export function readAgenda(
  state: GameState,
  side: PlayerId,
  advisors: AiObjectiveAdvisorRegistry,
  objectives: ObjectiveHandlerRegistry,
  content: ContentCatalog,
): AiAgenda {
  const captureTargets: { at: Coord; weight: number }[] = [];
  const enemyHqs: Coord[] = [];
  const myHqs: Coord[] = [];
  for (let tile = 0; tile < state.map.tiles.length; tile++) {
    const terrain = content.terrains.get(state.map.tiles[tile]);
    if (!terrain.capturable) continue;
    const at = { x: tile % state.map.width, y: Math.floor(tile / state.map.width) };
    const owner = state.map.owners[tile];
    if (terrain.hq) {
      if (owner === side) myHqs.push(at);
      else if (areEnemies(state, owner, side)) enemyHqs.push(at);
    }
    if (owner !== side) {
      const weight = terrain.hq ? 6 : terrain.produces.length > 0 ? 3 : 2;
      captureTargets.push({ at, weight });
    }
  }
  return {
    captureTargets,
    enemyHqs,
    myHqs,
    mission: buildAiMissionIntent(state, side, advisors, objectives, content),
  };
}

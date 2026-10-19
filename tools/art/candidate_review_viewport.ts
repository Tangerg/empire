import { TacticalGrids, type GameMap } from '@empire/battle-engine';
import { createSceneViewport, TILE, type SceneViewport } from '@empire/game-ui';
import { candidate01SceneProfile } from '../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';

/** Candidate art diagnostics share the runtime grid, tile unit, and scene insets. */
export function candidateReviewViewport(map: Pick<GameMap, 'width' | 'height'>): SceneViewport {
  return createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, TILE, candidate01SceneProfile());
}

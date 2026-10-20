import { writeFileSync } from 'node:fs';
import { mapFromLevel, TacticalGrids } from '../../../packages/battle-engine/src/index';
import { createTestCatalog } from '../../../packages/test-content/src/index';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../../../packages/story-candidate-01/src/index';
import { candidate01MapSceneryLayers, candidate01SceneProfile } from '../../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { createSceneViewport } from '../../../packages/game-ui/src/art/scene-viewport';
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const fields = CANDIDATE_01_LEVELS.map(level => {
  const map = mapFromLevel(content, level);
  const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
  const layers = candidate01MapSceneryLayers({ content, levelId: level.id, map, viewport });
  const connectors = layers.ground.flatMap(piece => {
    const match = piece.markup.match(/class="(candidate-ground-route(?:-edge)?)"/);
    return match ? [{ x: piece.x ?? 0, y: piece.y ?? 0, role: match[1], markup: piece.markup }] : [];
  });
  return { id: level.id, width: map.width, height: map.height, tiles: map.tiles, connectors };
});
writeFileSync(process.argv[2], JSON.stringify({ fields }));
console.log(`Executed source connector projections: ${fields.length} levels, ${fields.reduce((sum, field) => sum + field.connectors.length, 0)} ordered cells.`);

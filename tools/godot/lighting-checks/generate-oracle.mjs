import { writeFileSync } from 'node:fs';
import { mapFromLevel, TacticalGrids } from '../../../packages/battle-engine/src/index';
import { createTestCatalog } from '../../../packages/test-content/src/index';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../../../packages/story-candidate-01/src/index';
import { candidate01MapSceneryLayers, candidate01SceneProfile } from '../../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { CANDIDATE_01_BOARD_STYLE } from '../../../packages/story-candidate-01/src/presentation/candidate-01-board-style';
import { createSceneViewport } from '../../../packages/game-ui/src/art/scene-viewport';
import { runtimeUnitPicture } from '../../../packages/game-ui/src/art/runtime-raster';
import { drawableBody } from '../../../packages/game-ui/src/art/board-surface';
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const fields = CANDIDATE_01_LEVELS.map(level => {
  const map = mapFromLevel(content, level);
  const before = JSON.stringify(map);
  const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
  const layers = candidate01MapSceneryLayers({ content, levelId: level.id, map, viewport });
  if (JSON.stringify(map) !== before) throw new Error('Source lighting changed map');
  return { id: level.id, width: map.width, height: map.height, tiles: map.tiles,
    hearths: layers.underUnits.find(piece => piece.markup.includes('id="c01-hearth-'))?.markup ?? '',
    sunlight: layers.underUnits.find(piece => piece.markup.includes('id="c01-field-sun"'))?.markup ?? '' };
});
const output = process.env.EMPIRE_LIGHTING_ORACLE;
if (!output) throw new Error('Set EMPIRE_LIGHTING_ORACLE');
const unitBody = runtimeUnitPicture({ href: 'source-contract-only', frameWidth: 32, frameHeight: 32, frameCount: 4, anchor: { x: 16, y: 31 }, idleFrame: 0, walkFrames: [1, 2], attackFrame: 3 }, '#3465ab').body;
writeFileSync(output, JSON.stringify({ unitBody, fields, style: CANDIDATE_01_BOARD_STYLE, relief: drawableBody({ body: '<path/>', relief: true }) }));
console.log(`Source lighting oracle: ${fields.length} canonical fields`);

import { writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { mapFromLevel, TacticalGrids } from '../../../packages/battle-engine/src/index';
import { createTestCatalog } from '../../../packages/test-content/src/index';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../../../packages/story-candidate-01/src/index';
import { CANDIDATE_01_ART } from '../../../packages/story-candidate-01/src/presentation/index';
import { candidate01EnvironmentScene } from '../../../packages/story-candidate-01/src/presentation/candidate-01-environment';
import { candidate01SceneFrameMarkup, candidate01SceneProfile } from '../../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { createSceneViewport } from '../../../packages/game-ui/src/art/scene-viewport';
import { battlefieldFeaturePieces } from '../../../packages/game-ui/src/art/battlefield-layer';
import { squareLayout } from '../../../packages/game-ui/src/art/board-decorations';
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const fields = CANDIDATE_01_LEVELS.map(level => {
  const map = mapFromLevel(content, level);
  const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
  const sourceFrames = candidate01EnvironmentScene(level.id)?.framePlacements;
  return { id: level.id, width: map.width, height: map.height,
    authored: sourceFrames !== undefined,
    ...(sourceFrames === undefined ? {} : { framePlacements: sourceFrames.map(({ id, contact, scale, flip }) =>
      ({ id, contact: [...contact], scale, flip })) }),
    foreground: candidate01SceneFrameMarkup({ content, levelId: level.id, map, viewport }).foreground };
});
const map = mapFromLevel(content, CANDIDATE_01_LEVELS[0]);
const covers = [[], [{ at: { x: 2, y: 3 }, sides: {} }],
  [{ at: { x: 2, y: 3 }, sides: { north: 'half' } }],
  [{ at: { x: 2, y: 3 }, sides: { north: 'half', south: 'full' } }],
  [{ at: { x: 1, y: 1 }, sides: { east: 'full' } }, { at: { x: 4, y: 5 }, sides: { west: 'half' } }],
  [{ at: { x: 0, y: 0 }, sides: { north: null, south: '' } }], []
].map(directionalCover => {
  const current = { ...map, directionalCover };
  const before = JSON.stringify(current);
  const pieces = battlefieldFeaturePieces({ art: CANDIDATE_01_ART, layout: squareLayout, levelId: CANDIDATE_01_LEVELS[0].id }, current)
    .filter(piece => piece.markup.includes('candidate-battle-prop'));
  if (before !== JSON.stringify(current)) throw new Error('Source cover mutated map');
  return { directionalCover, pieces };
});
const payload = JSON.stringify({ fields, covers });
writeFileSync(process.env.EMPIRE_SCENERY_ORACLE, process.env.EMPIRE_SCENERY_ORACLE.endsWith(".gz") ? gzipSync(payload, { mtime: 0 }) : payload);
console.log(`Source scenery oracle: ${fields.length} actual level frames; ${covers.length} live cover changes`);

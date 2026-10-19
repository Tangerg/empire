/** Isolated real-level art evidence; does not mutate gameplay or runtime assets. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { TacticalGrids, idx, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { boardPiecesMarkup, boardPictureMarkup, createSceneViewport } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK } from '../../packages/story-candidate-01/src/index';
import { candidate01Level } from '../../packages/story-candidate-01/src/levels';
import { candidate01MapSceneryLayers, candidate01SceneProfile } from '../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { candidate01TerrainMarkup, candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';
const output = resolve(process.env.CALIBRATION_OUTPUT ?? '/workspace/shared/empire-art-rebuild/calibration-v4/actual-scene');
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
const layers = candidate01MapSceneryLayers({ content, levelId: 'c01-02', map, viewport });
const buildings = [];
for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
  const markup = candidate01TerrainMarkup(map.tiles[idx(map, x, y)]!, { x, y, linked: { n: false, e: false, s: false, w: false } });
  if (markup) buildings.push({ x: x * 32, y: y * 32, markup });
}
const units = level.units.map(unit => {
  const picture = candidate01UnitPicture(unit.unit, '#718b96');
  return { x: unit.x * 32, y: unit.y * 32, markup: picture ? boardPictureMarkup(picture) : '' };
});
mkdirSync(output, { recursive: true });
writeFileSync(resolve(output, 'pieces.json'), JSON.stringify({ kind: 'actual-renderer-pieces-art-study-not-gameplay', levelId: 'c01-02', map, level, layers, buildings, units, presentationSubstitutions: ['Buildings use normal state without owner banner for shape study; original owner data retained', 'Unit team is neutral blue-gray; idle frames only', 'No CSS lighting, shadows, animation or camera'], sourceFit: { inset: 3, bleed: .5 } }, null, 2));
const body = boardPiecesMarkup([...layers.ground, ...layers.underUnits, ...buildings, ...units, ...layers.overUnits]);
writeFileSync(resolve(output, 'actual-scene.svg'), `<svg xmlns="http://www.w3.org/2000/svg" width="${map.width * 32}" height="${map.height * 32}" viewBox="0 0 ${map.width * 32} ${map.height * 32}">${body}</svg>`);
console.log(output);

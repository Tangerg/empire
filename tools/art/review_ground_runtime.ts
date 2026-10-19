/** Offline, full-field evidence from the real candidate BoardPieces. */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { idx, mapFromLevel } from '@empire/battle-engine';
import { boardPictureMarkup, boardPiecesMarkup, TILE } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '@empire/story-candidate-01';
import { candidate01MapSceneryLayers } from '../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { candidate01TerrainMarkup, candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';
import { CANDIDATE_01_FIELD_MATERIAL, candidate01FieldMaterialPieces } from '../../packages/story-candidate-01/src/presentation/candidate-01-field-material';
import { candidateReviewViewport } from './candidate_review_viewport';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.GROUND_REVIEW_OUTPUT ?? 'docs/art-rebuild-20261019/approved-v5/ground-runtime');
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const before = JSON.stringify(map);
const viewport = candidateReviewViewport(map);
const layers = candidate01MapSceneryLayers({ content, levelId: level.id, map, viewport });
const colors: Readonly<Record<number, string>> = { 1: '#4d86b8', 2: '#a8493f', 3: '#998c6e' };
const buildings = [];
for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
  const tile = idx(map, x, y);
  const markup = candidate01TerrainMarkup(map.tiles[tile], {
    x, y, ownerColor: colors[map.owners[tile]], linked: { n: false, e: false, s: false, w: false },
  });
  if (markup) buildings.push({ x: x * TILE, y: y * TILE, markup });
}
const units = level.units.map((unit) => {
  const picture = candidate01UnitPicture(unit.unit, colors[unit.owner] ?? '#998c6e');
  if (!picture) throw new Error(`missing unit picture: ${unit.unit}`);
  return { x: unit.x * TILE, y: unit.y * TILE, markup: boardPictureMarkup(picture) };
});
const width = map.width * TILE;
const height = map.height * TILE;
const field = candidate01FieldMaterialPieces(width, height);
const world = boardPiecesMarkup(field);
const upper = [...layers.ground.slice(field.length), ...layers.underUnits, ...buildings, ...units, ...layers.overUnits];
const integration = boardPiecesMarkup([...field, ...upper]);
// A requested comparison only: enlarge the same renderer field about the same
// world origin. The runtime manifest and every overlying picture stay unchanged.
const largerField = candidate01FieldMaterialPieces(width / 2, height / 2).map((piece) => ({
  x: piece.x * 2, y: piece.y * 2, markup: `<g transform="scale(2)">${piece.markup}</g>`,
}));
const largerWorld = boardPiecesMarkup(largerField);
const largerIntegration = boardPiecesMarkup([...largerField, ...upper]);
if (JSON.stringify(map) !== before) throw new Error('ground review changed the logical map');
const local = (markup: string) => markup.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g,
  (_, path: string) => `href="${relative(output, resolve(root, `.${path}`))}"`);
const svg = (body: string, box: readonly number[]) => `<svg xmlns="http://www.w3.org/2000/svg"`
  + ` width="${box[2]}" height="${box[3]}" viewBox="${box.join(' ')}">${local(body)}</svg>`;
mkdirSync(output, { recursive: true });
for (const [name, body, box] of [
  ['field-full', world, [0, 0, width, height]],
  ['integration-full', integration, [0, 0, width, height]],
  ['field-seam-crop', world, [448, 64, 256, 256]],
  ['integration-seam-crop', integration, [448, 64, 256, 256]],
  ['field-period-1024-diagnostic', largerWorld, [0, 0, width, height]],
  ['integration-period-1024-diagnostic', largerIntegration, [0, 0, width, height]],
  ['field-period-1024-crop-diagnostic', largerWorld, [448, 64, 256, 256]],
  ['integration-period-1024-crop-diagnostic', largerIntegration, [448, 64, 256, 256]],
] as const) writeFileSync(resolve(output, `${name}.svg`), svg(body, box));
writeFileSync(resolve(output, 'runtime-provenance.json'), JSON.stringify({
  kind: 'offline real BoardPieces diagnostic; not a live gameplay screenshot',
  levelId: level.id, mapSize: [map.width, map.height], logicalExtent: [width, height],
  material: CANDIDATE_01_FIELD_MATERIAL, fieldPieces: field,
  layerCounts: Object.fromEntries(Object.entries(layers).map(([key, value]) => [key, value.length])),
  buildingCount: buildings.length, unitCount: units.length,
  mapSha256: createHash('sha256').update(before).digest('hex'), gameplayTopologyChanged: false,
  provenance: 'candidate01MapSceneryLayers + candidate01TerrainMarkup + candidate01UnitPicture; no SVG material substitutions',
  cropWorldBox: [448, 64, 256, 256],
  samplingComparison: { approvedRuntimePeriod: [512, 512], diagnosticOnlyPeriod: [1024, 1024],
    worldOrigin: [0, 0], method: 'uniform 2x placement scale of the actual field BoardPieces; identical source and upper layers',
    status: 'not selected or approved for runtime', diagnosticRasterDensity: '1024px source over 1024 logical units; 2x diagnostic is an enlarged view' },
  limitations: ['Static initial unit poses, no animation, browser or GPU playback',
    'Explicit diagnostic blue/red owner colors; no HUD, camera effects or CSS lighting',
    'Legacy shore, water, road, non-base surfaces and decals remain visible; only base meadow changed'],
}, null, 2) + '\n');
console.log(output);

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
import { CANDIDATE_01_WATER_MATERIAL } from '../../packages/story-candidate-01/src/presentation/candidate-01-water-material';
import { CANDIDATE_01_MAP_STRUCTURE_ART } from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
import { candidate01Asset } from '../../packages/story-candidate-01/src/presentation/candidate-01-assets';
import { candidateReviewViewport } from './candidate_review_viewport';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.GROUND_REVIEW_OUTPUT ?? `docs/art-rebuild-20261019/approved-v5/ground-runtime-${CANDIDATE_01_FIELD_MATERIAL.logicalSize[0]}`);
const waterReview = process.env.GROUND_REVIEW_LAYER === 'water';
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const before = JSON.stringify(map);
const viewport = candidateReviewViewport(map);
const layers = candidate01MapSceneryLayers({ content, levelId: level.id, map, viewport });
const colors: Readonly<Record<number, string>> = { 1: '#4d86b8', 2: '#a8493f', 3: '#998c6e' };
const buildings = [];
const buildingRegistrations = [];
for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
  const tile = idx(map, x, y);
  const markup = candidate01TerrainMarkup(map.tiles[tile], {
    x, y, ownerColor: colors[map.owners[tile]], linked: { n: false, e: false, s: false, w: false },
  });
  if (markup) {
    buildings.push({ x: x * TILE, y: y * TILE, markup });
    const topic = CANDIDATE_01_MAP_STRUCTURE_ART[map.tiles[tile]]!;
    const record = candidate01Asset(topic);
    buildingRegistrations.push({ topic, cell: [x,y], owner: map.owners[tile],
      state: colors[map.owners[tile]] ? 'captured' : 'normal',
      source: record.png, frameSize: [record.frameWidth,record.frameHeight], anchor: record.anchor,
      presentationScale: record.presentationScale ?? 1, footprint: record.footprint, worldFoot: [x*TILE+16,y*TILE+31] });
  }
}
const units = [...level.units].sort((a,b) => a.y-b.y || a.x-b.x).map((unit) => {
  const picture = candidate01UnitPicture(unit.unit, colors[unit.owner] ?? '#998c6e');
  if (!picture) throw new Error(`missing unit picture: ${unit.unit}`);
  return { x: unit.x * TILE, y: unit.y * TILE, markup: boardPictureMarkup(picture) };
});
const width = map.width * TILE;
const height = map.height * TILE;
const field = candidate01FieldMaterialPieces(width, height);
const sampled = waterReview ? layers.ground.filter((piece) => piece.markup.includes('data-runtime-raster="area-water"')) : field;
const world = boardPiecesMarkup(sampled);
const crop = waterReview ? [288, 96, 128, 256] : [448, 64, 256, 256];
const upper = [...layers.ground.slice(field.length), ...buildings, ...layers.underUnits, ...units, ...layers.overUnits];
const integration = boardPiecesMarkup([...field, ...upper]);
// Preserve the 512-unit/native-2x alternative without changing source pixels.
// Only this diagnostic changes placement scale; the selected runtime stays broad.
const alternativeScale = CANDIDATE_01_FIELD_MATERIAL.rasterSize[0] / CANDIDATE_01_FIELD_MATERIAL.logicalSize[0];
const alternativeField = candidate01FieldMaterialPieces(width / alternativeScale, height / alternativeScale).map((piece) => ({
  x: piece.x * alternativeScale, y: piece.y * alternativeScale,
  markup: `<g transform="scale(${alternativeScale})">${piece.markup}</g>`,
}));
const alternativeWorld = boardPiecesMarkup(alternativeField);
const alternativeIntegration = boardPiecesMarkup([...alternativeField, ...upper]);
if (JSON.stringify(map) !== before) throw new Error('ground review changed the logical map');
const local = (markup: string) => markup.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g,
  (_, path: string) => `href="${relative(output, resolve(root, `.${path}`))}"`);
const svg = (body: string, box: readonly number[]) => `<svg xmlns="http://www.w3.org/2000/svg"`
  + ` width="${box[2]}" height="${box[3]}" viewBox="${box.join(' ')}">${local(body)}</svg>`;
mkdirSync(output, { recursive: true });
for (const [name, body, box] of [
  ['field-full', world, [0, 0, width, height]],
  ['integration-full', integration, [0, 0, width, height]],
  ['field-seam-crop', world, crop],
  ['integration-seam-crop', integration, crop],
  ['field-high-density-alternative', alternativeWorld, [0, 0, width, height]],
  ['integration-high-density-alternative', alternativeIntegration, [0, 0, width, height]],
  ['field-high-density-crop-alternative', alternativeWorld, [448, 64, 256, 256]],
  ['integration-high-density-crop-alternative', alternativeIntegration, [448, 64, 256, 256]],
] as const) {
  if (!waterReview || !name.includes('alternative')) writeFileSync(resolve(output, `${name}.svg`), svg(body, box));
}
writeFileSync(resolve(output, 'runtime-provenance.json'), JSON.stringify({
  kind: 'offline real BoardPieces diagnostic; not a live gameplay screenshot',
  levelId: level.id, mapSize: [map.width, map.height], logicalExtent: [width, height],
  material: waterReview ? CANDIDATE_01_WATER_MATERIAL : CANDIDATE_01_FIELD_MATERIAL, fieldPieces: sampled,
  waterAndBridgeCells: map.tiles.flatMap((id, index) => id === 'water' || id === 'bridge' ? [[index % map.width, Math.floor(index / map.width)]] : []),
  layerCounts: Object.fromEntries(Object.entries(layers).map(([key, value]) => [key, value.length])),
  buildingCount: buildings.length, unitCount: units.length, buildingRegistrations,
  mapSha256: createHash('sha256').update(before).digest('hex'), gameplayTopologyChanged: false,
  provenance: 'candidate01MapSceneryLayers + candidate01TerrainMarkup + candidate01UnitPicture; no SVG material substitutions',
  cropWorldBox: crop,
  samplingComparison: waterReview ? { selectedRuntimePeriod: CANDIDATE_01_WATER_MATERIAL.logicalSize,
    status: process.env.GROUND_REVIEW_STATUS ?? '512 selected at native2x; 128 ribbon/plane repeat evidence retained separately' } : { selectedRuntimePeriod: CANDIDATE_01_FIELD_MATERIAL.logicalSize,
    highDensityAlternativePeriod: CANDIDATE_01_FIELD_MATERIAL.rasterSize, worldOrigin: [0, 0],
    selectedPixelsPerWorldUnit: CANDIDATE_01_FIELD_MATERIAL.rasterPixelsPerWorldUnit,
    alternativePixelsPerWorldUnit: [2, 2],
    method: 'uniform placement scale of actual field BoardPieces; identical source and upper layers',
    status: '1024 selected at 1 source pixel/unit; 512 alternative preserves 2 source pixels/unit',
    densityWarning: 'Display 2x of the selected broad field is enlarged existing art, not new native2x source detail' },
  limitations: ['Static initial unit poses, no animation, browser or GPU playback',
    'Explicit diagnostic blue/red owner colors; no HUD, camera effects or CSS lighting',
    'Legacy shore, road, crossings, non-base surfaces and decals remain visible; buildings may overlap'],
}, null, 2) + '\n');
console.log(output);

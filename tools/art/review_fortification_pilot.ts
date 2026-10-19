/** Offline actual c01-13 BoardView diagnostic; no production binding is installed. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { ArtDirection, BoardView, SvgBoardSurface, type BoardLayer, type BoardPiece, type SceneLayers } from '@empire/game-ui';
import { candidateReviewViewport } from './candidate_review_viewport';
import { wallTopology, wallPilotPiece, wallGeometry, supportedWall, WALL_SOURCES, type Cell } from './fortification_pilot';

const root = resolve(import.meta.dirname, '../..');
const density = process.env.WALL_PILOT_DENSITY === '16' ? 16 : 4;
const output = resolve(process.env.WALL_PILOT_OUTPUT ?? '/tmp/empire-fortification-pilot');
// Sparse engineering checkouts may read already committed images from the full checkout.
const assetRoot = resolve(process.env.WALL_PILOT_ASSET_ROOT ?? root);
mkdirSync(output, { recursive: true });
const hash = (value: string | Uint8Array) => createHash('sha256').update(value).digest('hex');
const jsonHash = (value: unknown) => hash(JSON.stringify(value));
const sources = new Map<string, string>();
let captureSources = new Map<string, string>();
const serialize = (markup: string) => markup.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
  const file = resolve(assetRoot, `.${path}`);
  const digest = sources.get(path.slice(1)) ?? hash(readFileSync(file));
  sources.set(path.slice(1), digest);
  captureSources.set(path.slice(1), digest);
  return `href="${relative(output, file)}"`;
});
const writeSvg = (name: string, markup: string) => writeFileSync(resolve(output, `${name}.svg`), serialize(markup));
const writeJson = (name: string, value: unknown) => writeFileSync(resolve(output, `${name}.json`), JSON.stringify(value, null, 2) + '\n');
const svg = (viewBox: string, width: number, height: number, body: string) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}" width="${width}" height="${height}">${body}</svg>`;
const piecesMarkup = (pieces: readonly BoardPiece[]) => pieces.map(p => `<g transform="translate(${p.x} ${p.y})">${p.markup}</g>`).join('');

// Context extends beyond both ends of these open windows. Unsupported end states
// are neither rendered nor silently substituted by a straight.
const fixtures = [
  { name: 'open-ns', cells: Array.from({ length: 7 }, (_, y) => [1, y] as Cell), box: [0, 48, 96, 96] },
  { name: 'open-ew', cells: Array.from({ length: 7 }, (_, x) => [x, 1] as Cell), box: [60, 0, 96, 80] },
  { name: 'open-double', cells: Array.from({ length: 7 }, (_, y) => [[1, y] as Cell, [2, y] as Cell]).flat(), box: [0, 48, 128, 96] },
];
for (const fixture of fixtures) {
  const topology = wallTopology(fixture.cells);
  const selected = topology.cells.filter(supportedWall);
  const [x, y, w, h] = fixture.box;
  for (const topOnly of [false, true]) {
    writeSvg(fixture.name + (topOnly ? '-top' : ''), svg(`${x} ${y} ${w} ${h}`, w, h,
      piecesMarkup(selected.map(cell => wallPilotPiece(cell, WALL_SOURCES, topOnly, density)))));
  }
  writeJson(fixture.name, { ...fixture, topology, displayed: selected.map(cell => ({ ...cell, geometry: wallGeometry(cell, density) })),
    boundary: 'open diagnostic window into actual occupancy; end states outside window are not implemented' });
}

if (process.env.WALL_PILOT_FIXTURES_ONLY === '1') { console.log(output); process.exit(0); }

const level = shippedLevel('c01-13');
const state = engine.createState(level);
const stateBefore = JSON.stringify(state);
const levelBefore = JSON.stringify(level);
const cells = state.map.tiles.flatMap((terrain, index) => terrain === 'wall' ? [[index % state.map.width, Math.floor(index / state.map.width)] as Cell] : []);
const topology = wallTopology(cells);
const viewport = candidateReviewViewport(state.map);
for (const topOnly of [false, true]) writeSvg('actual-pilot' + (topOnly ? '-top' : ''), svg(`0 0 ${state.map.width * 32} ${state.map.height * 32}`, state.map.width * 32, state.map.height * 32, piecesMarkup(topology.cells.filter(supportedWall).map(cell => wallPilotPiece(cell, WALL_SOURCES, topOnly, density)))));
const original = CANDIDATE_01_ART.presentation;
const replaced: Cell[] = [];
const partial = new ArtDirection(CANDIDATE_01_ART.providers, {
  ...original,
  sceneLayers(context): SceneLayers {
    const layers = original.sceneLayers(context);
    if (context.levelId !== 'c01-13' || context.editing) return layers;
    const matches = new Map<string, number>();
    const underUnits = layers.underUnits.map(piece => {
      const cell = topology.cells.find(({ at: [x, y] }) => Math.abs(piece.x - (x + .5) * 32) < 1e-8
        && Math.abs(piece.y - (y + 1.18) * 32) < 1e-8 && piece.markup.includes('candidate-environment-prop'));
      if (!cell) return piece;
      const key = cell.at.join(',');
      matches.set(key, (matches.get(key) ?? 0) + 1);
      if (!supportedWall(cell)) return piece;
      replaced.push(cell.at);
      return wallPilotPiece(cell, WALL_SOURCES, false, density);
    });
    if (matches.size !== 68 || [...matches.values()].some(n => n !== 1)) throw new Error('did not identify exactly one existing scenery prop on each actual wall cell');
    return { ...layers, underUnits };
  },
});
const appCss = readFileSync(resolve(root, 'packages/game-ui/src/styles/app.css'), 'utf8');
const environmentManifest = JSON.parse(readFileSync(resolve(root, 'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json'), 'utf8'));
const crossings = environmentManifest.atlases.find((a: { id: string }) => a.id === 'crossings-fortifications');
const captures: Record<string, unknown>[] = [];
for (const [name, art] of [['before', CANDIDATE_01_ART], ['partial', partial]] as const) {
  captureSources = new Map();
  const layers = new Map<BoardLayer, readonly BoardPiece[]>();
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art, renderer(scene) {
    const surface = new SvgBoardSurface(scene);
    const setLayer = surface.setLayer.bind(surface);
    surface.setLayer = (layer, pieces) => { layers.set(layer, pieces); setLayer(layer, pieces); };
    return surface;
  } }, state, { onTileClick() {}, onTileEnter() {}, onLeave() {}, onSecondary() {}, onScale() {} });
  board.render(emptyOverlay());
  const element = board.el as SVGSVGElement;
  const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
  style.textContent = appCss;
  element.prepend(style);
  const [,, width, height] = element.getAttribute('viewBox')!.split(' ').map(Number);
  element.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  element.setAttribute('width', String(width)); element.setAttribute('height', String(height));
  element.removeAttribute('style');
  const actors = element.querySelectorAll('.layer-units > .unit');
  const buildings = element.querySelectorAll('[data-candidate-art="structure"]');
  if (actors.length !== 24 || buildings.length !== 18) throw new Error(`incomplete c01-13: ${actors.length} actors, ${buildings.length} structures`);
  writeSvg(`${name}-full`, element.outerHTML);
  const annotations = topology.cells.map(cell => {
    const pilot = name === 'partial' && supportedWall(cell);
    const [x, y] = cell.at;
    return `<g transform="translate(${viewport.originX + x * 32} ${viewport.originY + y * 32})"><rect width="32" height="32" fill="none" stroke="${pilot ? '#72f3ed' : '#ffa64b'}" stroke-width=".7" stroke-dasharray="${pilot ? 'none' : '3 2'}"/><text x="2" y="30" font-size="7" font-family="sans-serif" fill="white" stroke="#101010" stroke-width="2" paint-order="stroke">${pilot ? 'P' : 'L'}${cell.mask}</text></g>`;
  }).join('');
  writeSvg(`${name}-full-labeled`, svg(`0 0 ${width} ${height + 54}`, width, height + 54,
    `<rect width="${width}" height="${height + 54}" fill="#202624"/><text x="16" y="21" fill="white" font-family="sans-serif" font-size="16">${name === 'partial' ? 'DIAGNOSTIC ONLY: 27 PILOT CELLS / 41 LEGACY CELLS' : 'PRODUCTION BASELINE: ALL 68 CELLS LEGACY'}</text><text x="16" y="42" fill="#ddd" font-family="sans-serif" font-size="12">24 actors / 18 buildings | cyan P = pilot mask; orange L = unchanged legacy mask | same camera and source images</text><g transform="translate(0 54)">${element.outerHTML}${annotations}</g>`));
  const wallBindings = topology.cells.map(cell => {
    const pilot = name === 'partial' && supportedWall(cell);
    const pieces = [...layers.values()].flat();
    const piece = pieces.find(p => pilot ? p.markup.includes(`data-wall-cell="${cell.at.join(',')}"`)
      : Math.abs(p.x - (cell.at[0] + .5) * 32) < 1e-8 && Math.abs(p.y - (cell.at[1] + 1.18) * 32) < 1e-8 && p.markup.includes('candidate-environment-prop'));
    if (!piece) throw new Error('missing real wall binding');
    const container = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    container.innerHTML = piece.markup;
    const image = container.querySelector('image')!;
    const index = pilot ? null : Math.round(-Number(image.getAttribute('x')) / crossings.cellWidth)
      + Math.round(-Number(image.getAttribute('y')) / crossings.cellHeight) * crossings.columns;
    return { cell: cell.at, mask: cell.mask, status: pilot ? 'pilot' : 'legacy', sourceIdentity: pilot ? `material-mask-${cell.mask}` : crossings.cells[index!].id,
      x: piece.x, y: piece.y, foot: piece.foot, markupSha256: hash(piece.markup) };
  });
  captures.push({ name, wallBindings, viewport: [width, height], actors: actors.length, buildings: buildings.length,
    actorMarkupSha256: jsonHash([...actors].map(a => a.outerHTML)), buildingMarkupSha256: jsonHash([...buildings].map(a => a.outerHTML)),
    layerHashes: Object.fromEntries([...layers].map(([layer, pieces]) => [layer, jsonHash(pieces)])),
    stateSha256: hash(JSON.stringify(state)), sourcePngHashes: Object.fromEntries([...captureSources].filter(([path]) => path.endsWith('.png'))) });
  board.dispose();
}
if (replaced.length !== 27) throw new Error(`expected 27 pilot cells, got ${replaced.length}`);
if (JSON.stringify(state) !== stateBefore || JSON.stringify(level) !== levelBefore) throw new Error('diagnostic changed gameplay');
const editorContext = { content, levelId: level.id, map: state.map, viewport, editing: true };
if (jsonHash(partial.presentation.sceneLayers(editorContext)) !== jsonHash(original.sceneLayers(editorContext))) throw new Error('diagnostic changed editor presentation');
writeJson('actual-topology', { levelId: level.id, ...topology, maskCounts: Object.fromEntries([...new Set(topology.cells.map(c => c.mask))].sort((a, b) => a - b).map(mask => [mask, topology.cells.filter(c => c.mask === mask).length])),
  replaced, legacy: topology.cells.filter(cell => !supportedWall(cell)).map(cell => cell.at) });
writeJson('provenance', { checkpoint: process.env.SCENE_REVIEW_CHECKPOINT ?? 'current worktree', kind: 'offline actual BoardView/SvgBoardSurface diagnostic; not live GPU/browser',
  projectionSources: Object.fromEntries(['tools/art/fortification_pilot.ts', 'tools/art/review_fortification_pilot.ts', 'packages/story-candidate-01/src/levels.ts', 'packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts', 'packages/game-ui/src/styles/app.css'].map(path => [path, hash(readFileSync(resolve(root, path)))])),
  actorCells: state.units.map(unit => ({ id: unit.id, key: unit.key, type: unit.type, cell: [unit.x, unit.y] })),
  productionActivated: false, sourcePixelsEdited: false, sourceRepeat: density === 16, cellPitch: 32, capLift: [-4, -16],
  sourcePixelsPerWorldUnit: density, topSourceCore: [115, 115, 1139, 1139], faceVWindow: [627 - density * 16, 627 + density * 16], facePhysicalHeight: 32,
  stateUnchanged: true, levelUnchanged: true, editorUnchanged: true, levelSha256: hash(levelBefore), captures, sources: Object.fromEntries(sources),
  limitations: ['Only masks 5/10/7/13 are implemented; remaining 41 c01-13 wall cells are unchanged legacy',
    density === 4 ? 'No repeat; component UV fits one 1024px core' : '64-world-pixel core repeat; native core edges differ, no seamless claim', 'North/west sides are back-facing under the declared lift; state7 has full top and no camera-facing side',
    'Material clipping establishes geometry only; final stone scale and visual relief require review'] });
console.log(output);

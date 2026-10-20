/** Read-only full-scene rural scale audit; multipliers are diagnostic, not catalog edits. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { BoardView, SvgBoardSurface, CELL_FOOT, TILE, type BoardLayer, type BoardPiece } from '@empire/game-ui';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { CANDIDATE_01_ENVIRONMENT } from '../../packages/story-candidate-01/src/presentation/candidate-01-environment';
const root = resolve(import.meta.dirname, '../..');
const output = resolve('docs/art-rebuild-20261019/approved-v5/rural-scale-audit');
mkdirSync(output, { recursive: true });
const atlas = CANDIDATE_01_ENVIRONMENT.atlas('rural-life');
const wanted = new Set(['livestock-pen-small', 'stone-well', 'wheat-drying-rack', 'roadside-waystation']);
function rural(markup: string) {
  const container = document.createElementNS('http://www.w3.org/2000/svg', 'g');
  container.innerHTML = markup;
  const image = container.querySelector('image');
  if (!image || !image.getAttribute('href')!.includes('/rural-life')) return null;
  const index = -Number(image.getAttribute('x')) / atlas.cellWidth - Number(image.getAttribute('y')) / atlas.cellHeight * atlas.columns;
  const cell = atlas.cells?.find((cell, i) => (cell.index ?? i) === index);
  if (!cell || !wanted.has(cell.id)) return null;
  const prop = container.querySelector('.candidate-environment-prop')!;
  const transform = prop.getAttribute('transform')!;
  const scale = Math.abs(Number(/scale\((-?[\d.]+)/.exec(transform)![1]));
  return { id: cell.id, index, anchor: cell.anchor, footprint: cell.footprint,
    presentationScale: cell.presentationScale ?? 1, scale, transform };
}
const sources = new Map<string, string>();
for (const path of [
  'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json',
  'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/scenes/c01-01.scene.json',
  'packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts',
]) sources.set(path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex'));
const renders = [];
for (const [levelId, penFactor] of [['c01-02', 1], ['c01-02', 1.5], ['c01-02', 1.8], ['c01-01', 1]] as const) {
  const level = shippedLevel(levelId);
  const state = engine.createState(level);
  const before = JSON.stringify(state);
  const layers = new Map<BoardLayer, readonly BoardPiece[]>();
  let origin = [0, 0];
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
    renderer: scene => {
      origin = [scene.originX, scene.originY];
      const surface = new SvgBoardSurface(scene);
      const setLayer = surface.setLayer.bind(surface);
      surface.setLayer = (layer, pieces) => {
        layers.set(layer, pieces);
        setLayer(layer, pieces.map(piece => penFactor !== 1 && rural(piece.markup)?.id === 'livestock-pen-small'
          ? { ...piece, markup: `<g transform="scale(${penFactor})">${piece.markup}</g>` } : piece));
      };
      return surface;
    } }, state, { onTileClick() {}, onTileEnter() {}, onLeave() {}, onSecondary() {}, onScale() {} });
  board.render(emptyOverlay());
  const svg = board.el as SVGSVGElement;
  const [, , width, height] = svg.getAttribute('viewBox')!.split(' ').map(Number);
  svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg'); svg.setAttribute('width', String(width)); svg.setAttribute('height', String(height)); svg.removeAttribute('style');
  const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
  const stylesPath = 'packages/game-ui/src/styles/app.css';
  const styles = readFileSync(resolve(root, stylesPath), 'utf8'); style.textContent = styles; svg.prepend(style);
  sources.set(stylesPath, createHash('sha256').update(styles).digest('hex'));
  const filename = `${levelId}-${penFactor === 1 ? 'actual' : `pen-factor-${penFactor}`}`;
  writeFileSync(resolve(output, `${filename}.svg`), svg.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path) => {
    const file = resolve(root, `.${path}`);
    sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
    return `href="${relative(output, file)}"`;
  }));
  const props = [...layers].flatMap(([layer, pieces]) => pieces.flatMap((piece, index) => {
    const info = rural(piece.markup);
    return info ? [{ ...info, sourceId: `${layer}:${index}`, worldFoot: [piece.x + (piece.foot?.x ?? 0), piece.y + (piece.foot?.y ?? 0)],
      screenFoot: [origin[0] + piece.x + (piece.foot?.x ?? 0), origin[1] + piece.y + (piece.foot?.y ?? 0)] }] : [];
  }));
  const actors = state.units.map(unit => ({ id: unit.id, key: unit.key, type: unit.type, cell: [unit.x, unit.y],
    worldFoot: [unit.x * TILE + CELL_FOOT.x, unit.y * TILE + CELL_FOOT.y] }));
  const buildings = (layers.get('terrain') ?? []).flatMap((piece, index) => piece.foot ? [{ sourceId: `terrain:${index}`,
    sourceImage: /<image[^>]+href="[^"]*\/([^/"?]+)(?:\?[^"]*)?"/.exec(piece.markup)?.[1],
    worldFoot: [piece.x + piece.foot.x, piece.y + piece.foot.y] }] : []);
  if (svg.querySelectorAll('.layer-units > .unit').length !== state.units.length) throw new Error('lost an actor');
  if (JSON.stringify(state) !== before) throw new Error('audit changed battle state');
  renders.push({ filename, levelId, penFactor, kind: penFactor === 1 ? 'actual production BoardView' : 'diagnostic pen-only foot-pivot scale multiplier; no production changes',
    viewport: [0, 0, width, height], props, actors, buildings,
    sourceLayerCounts: Object.fromEntries([...layers].map(([layer, pieces]) => [layer, pieces.length])),
    stateSha256: createHash('sha256').update(before).digest('hex') });
  board.dispose();
}
writeFileSync(resolve(output, 'audit-provenance.json'), JSON.stringify({ checkpoint: '36555c4', renders, sources: Object.fromEntries(sources),
  gameplayChanged: false, productionFilesChanged: false, omissions: [], browserPlayback: false }, null, 2) + '\n');
console.log(output);

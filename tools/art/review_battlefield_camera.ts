/** Offline production BoardView/camera projections, never claimed as browser QA. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { BoardView, SvgBoardSurface, TILE } from '@empire/game-ui';
import { BattlefieldCamera } from '../../packages/game-ui/src/ui/battlefield-camera';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.CAMERA_REVIEW_OUTPUT ?? '/tmp/empire-battlefield-camera-review');
mkdirSync(output, { recursive: true });
const width = 1440;
const height = 900;
const level = shippedLevel('c01-02');
const state = engine.createState(level);
const before = JSON.stringify(state);
const sources = new Map<string, string>();
const sourceText = (path: string) => {
  const text = readFileSync(resolve(root, path), 'utf8');
  sources.set(path, createHash('sha256').update(text).digest('hex'));
  return text;
};
const battleCss = sourceText('packages/game-ui/src/styles/battle.css');
const appCss = sourceText('packages/game-ui/src/styles/app.css');
for (const path of [
  'packages/game-ui/src/ui/board.ts',
  'packages/game-ui/src/ui/game.ts',
  'packages/game-ui/src/ui/battlefield-camera.ts',
  'packages/game-ui/src/art/letterbox.ts',
  'packages/story-candidate-01/src/levels.ts',
  'packages/story-candidate-01/src/terrain.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-playable-shape.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-square-mask.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-road-corridor.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-river-material.ts',
  'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/scenes/c01-02.scene.json',
  'tools/art/render_ground_review.py',
  'tools/art/svg_viewports.py',
]) sourceText(path);
const hudBand = (name: string) => {
  const value = battleCss.match(new RegExp(`--hud-${name}: (\\d+)px;`))?.[1];
  if (!value) throw new Error(`missing production HUD band ${name}`);
  return Number(value);
};
const padding = {
  top: hudBand('crown'),
  right: hudBand('aside') + hudBand('gutter') * 2,
  bottom: hudBand('dock'),
  left: hudBand('flank') + hudBand('gutter') * 2,
};
const board = new BoardView({
  content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
  renderer: (scene) => new SvgBoardSurface(scene),
}, state, {
  onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {},
  onSecondary: () => {}, onScale: () => {}, onPan: () => {},
});
board.render(emptyOverlay());
const svg = board.el as SVGSVGElement;
const sceneWidth = Number.parseFloat(svg.style.width);
const sceneHeight = Number.parseFloat(svg.style.height);
const viewport = document.createElement('div');
viewport.style.padding = `${padding.top}px ${padding.right}px ${padding.bottom}px ${padding.left}px`;
viewport.append(svg);
document.body.append(viewport);
(globalThis as Record<string, unknown>).getComputedStyle = window.getComputedStyle.bind(window);
Object.defineProperties(viewport, {
  clientWidth: { value: width }, clientHeight: { value: height },
  scrollWidth: { value: Math.max(width, sceneWidth + padding.left + padding.right) },
  scrollHeight: { value: Math.max(height, sceneHeight + padding.top + padding.bottom) },
});
const origin = {
  x: padding.left + Math.max(0, (width - padding.left - padding.right - sceneWidth) / 2),
  y: padding.top + Math.max(0, (height - padding.top - padding.bottom - sceneHeight) / 2),
};
viewport.scrollTo = (options?: ScrollToOptions | number, _y?: number) => {
  if (typeof options !== 'object') throw new Error('expected camera scroll options');
  viewport.scrollLeft = options.left ?? viewport.scrollLeft;
  viewport.scrollTop = options.top ?? viewport.scrollTop;
};
viewport.getBoundingClientRect = () => ({ left: 0, top: 0 } as DOMRect);
svg.getBoundingClientRect = () => ({
  left: origin.x - viewport.scrollLeft, top: origin.y - viewport.scrollTop,
  width: sceneWidth, height: sceneHeight,
} as DOMRect);
const camera = new BattlefieldCamera(viewport, svg);
const human = state.players.find((player) => player.controller === 'human')!;
const opening = state.units.filter((unit) => unit.owner === human.id && unit.hp > 0).map((unit) => board.pointAt(unit));
const openingFocus = {
  x: opening.reduce((sum, point) => sum + point.x, 0) / opening.length,
  y: opening.reduce((sum, point) => sum + point.y, 0) / opening.length,
};
const bridgeCells = state.map.tiles.flatMap((id, index) =>
  id === 'bridge'
    ? [{ x: index % level.width, y: Math.floor(index / level.width) }] : []);
if (bridgeCells.length === 0) throw new Error('expected authored crossings');
const middleBridge = [...bridgeCells].sort((a, b) => a.y - b.y || a.x - b.x)[Math.floor(bridgeCells.length / 2)];
const easternFocus = board.pointAt({ x: Math.min(level.width - 1, middleBridge.x + 5), y: middleBridge.y });
const captures = [];
for (const [name, focus] of [['opening', openingFocus], ['eastern-crossing', easternFocus]] as const) {
  if (name === 'opening') camera.centerOn(focus, 'instant');
  else {
    const from = camera.position;
    camera.pan({ x: from.x - focus.x, y: from.y - focus.y });
  }
  if (board.zoomLevel !== 1.25 || TILE * board.zoomLevel !== 40) throw new Error('camera capture changed actor scale');
  const copy = svg.cloneNode(true) as SVGSVGElement;
  copy.removeAttribute('style');
  copy.setAttribute('width', String(sceneWidth));
  copy.setAttribute('height', String(sceneHeight));
  copy.setAttribute('x', String(origin.x - viewport.scrollLeft));
  copy.setAttribute('y', String(origin.y - viewport.scrollTop));
  const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
  style.textContent = appCss;
  copy.prepend(style);
  const picture = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  picture.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  picture.setAttribute('width', String(width));
  picture.setAttribute('height', String(height));
  picture.setAttribute('viewBox', `0 0 ${width} ${height}`);
  // The offline viewport flattener replaces nested SVGs with groups. Keep the
  // board's inherited CSS context on the outer SVG, where it cannot be lost.
  for (const name of ['class', 'shape-rendering', 'text-rendering']) {
    const value = copy.getAttribute(name);
    if (value) picture.setAttribute(name, value);
  }
  picture.innerHTML = `<rect width="${width}" height="${height}" fill="#140e0a"/>`;
  picture.append(copy);
  const markup = picture.outerHTML.replace(/href="(\/(?:packages\/|@fs\/)[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
    const file = path.startsWith('/@fs/') ? resolve(path.slice(4)) : resolve(root, `.${path}`);
    sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
    return `href="${relative(output, file)}"`;
  });
  writeFileSync(resolve(output, `${name}.svg`), markup);
  captures.push({
    name, viewport: [width, height], zoom: board.zoomLevel, pixelsPerCell: TILE * board.zoomLevel,
    requestedFocus: focus, actualFocus: camera.position, scroll: [viewport.scrollLeft, viewport.scrollTop],
    boardOffset: [origin.x - viewport.scrollLeft, origin.y - viewport.scrollTop],
    scenePixels: [sceneWidth, sceneHeight], actorsInSource: copy.querySelectorAll('.layer-units > .unit').length,
    svgSha256: createHash('sha256').update(markup).digest('hex'),
  });
}
if (JSON.stringify(state) !== before) throw new Error('camera capture changed gameplay state');
board.dispose();
writeFileSync(resolve(output, 'camera-provenance.json'), JSON.stringify({
  kind: 'Offline actual BoardView/SvgBoardSurface + BattlefieldCamera projection, not browser or GPU QA',
  level: level.id, mapSize: [level.width, level.height], padding, captures,
  stateSha256: createHash('sha256').update(before).digest('hex'), sources: Object.fromEntries(sources),
  limitations: ['Layout box metrics emulate the production CSS at 1440x900; no browser layout or native input exercised',
    'HUD not rasterized; its real padding bands are retained for camera centering',
    'Initial animation poses only; source scene objects are not omitted or repositioned'],
  reproduction: [
    'CAMERA_REVIEW_OUTPUT=/tmp/empire-battlefield-camera-review npx vite-node tools/art/review_battlefield_camera.ts',
    "PYTHONPATH=tools/art python -c \"from pathlib import Path; from render_ground_review import rasterize; rasterize(Path('/tmp/empire-battlefield-camera-review'), densities=(1,))\"",
  ],
}, null, 2) + '\n');
console.log(output);

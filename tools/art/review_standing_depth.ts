/** Actual BoardView SVG snapshots, offline only; both variants keep every source piece. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { BoardDepthOrder, BoardView, CELL_FOOT, SvgBoardSurface, TILE, type BoardLayer, type BoardPiece } from '@empire/game-ui';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.STANDING_REVIEW_OUTPUT ?? 'docs/art-rebuild-20261019/approved-v5/standing-depth-review');
const edgeComparison = process.env.STANDING_REVIEW_COMPARISON === 'edges';
mkdirSync(output, { recursive: true });
const level = shippedLevel('c01-02');
const sources = new Map<string, string>();
const appStylesPath = 'packages/game-ui/src/styles/app.css';
const appStyles = readFileSync(resolve(root, appStylesPath), 'utf8');
sources.set(appStylesPath, createHash('sha256').update(appStyles).digest('hex'));
const renders = [];
for (const standing of [false, true]) {
  const depthEnabled = edgeComparison || standing;
  const state = engine.createState(level);
  const before = JSON.stringify(state);
  const layers = new Map<BoardLayer, readonly BoardPiece[]>();
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
    renderer: (scene) => {
      const surface = new SvgBoardSurface(scene);
      const setLayer = surface.setLayer.bind(surface);
      surface.setLayer = (layer, pieces) => {
        const shown = edgeComparison && !standing
          ? pieces.filter((piece) => !piece.markup.includes('candidate-ground-edge')) : pieces;
        layers.set(layer, shown);
        // Edge comparison changes only ground patches. Default depth comparison
        // restores the former fixed layers with identical art and contacts.
        setLayer(layer, depthEnabled ? shown : shown.map(({ foot: _foot, ...piece }) => piece));
      };
      return surface;
    },
  }, state, { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {}, onPan: () => {} });
  board.render(emptyOverlay());
  const svg = board.el as SVGSVGElement;
  // The renderer is mounted inside app.css in production. Keep its actual
  // facing rule in this standalone evidence, rather than exporting bare classes.
  const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
  style.textContent = appStyles;
  svg.prepend(style);
  const [,, width, height] = svg.getAttribute('viewBox')!.split(' ').map(Number);
  svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  svg.setAttribute('width', String(width));
  svg.setAttribute('height', String(height));
  svg.removeAttribute('style');
  const unitCount = svg.querySelectorAll('.layer-units > .unit').length;
  const buildingCount = svg.querySelectorAll('[data-candidate-art="structure"]').length;
  if (unitCount !== 20 || buildingCount !== 9) throw new Error(`incomplete c01-02 snapshot: ${unitCount} actors, ${buildingCount} buildings`);
  const filename = edgeComparison ? `runtime-${standing ? 'with' : 'without'}-edges-full.svg`
    : standing ? 'runtime-standing-full.svg' : 'runtime-legacy-order-full.svg';
  const actors = state.units.map((unit) => ({ id: `actor:${unit.id}`, actorId: unit.id, key: unit.key,
    type: unit.type, cell: [unit.x, unit.y], facing: unit.facing,
    origin: [unit.x * TILE, unit.y * TILE], worldFoot: [unit.x * TILE + CELL_FOOT.x, unit.y * TILE + CELL_FOOT.y] }));
  const order = new BoardDepthOrder<object>();
  for (const [layer, pieces] of layers) order.replaceLayer(layer, pieces.map((piece, index) => {
    const { foot: _foot, ...legacyPiece } = piece;
    return {
      piece: depthEnabled ? piece : legacyPiece,
      drawing: { id: `${layer}:${index}`, layer, index, origin: [piece.x, piece.y],
        ...(piece.foot ? { worldFoot: [piece.x + piece.foot.x, piece.y + piece.foot.y] } : {}) },
    };
  }));
  for (const actor of actors) order.placeUnit(actor.actorId, actor, actor.origin[0], actor.origin[1]);
  const standingDrawOrder = order.ordered('units').map((entry, index) => ({ ...entry, displayIndex: index }));
  if (standingDrawOrder.length !== svg.querySelector('.layer-units')!.children.length) {
    throw new Error('recorded draw order differs from actual surface');
  }
  const markup = svg.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
    const file = resolve(root, `.${path}`);
    sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
    return `href="${relative(output, file)}"`;
  });
  writeFileSync(resolve(output, filename), markup);
  if (JSON.stringify(state) !== before) throw new Error('depth review changed battle state');
  renders.push({ filename, viewport: [0, 0, width, height], unitCount, buildingCount, actors, standingDrawOrder,
    edgeDecorationCount: svg.querySelectorAll('.candidate-ground-edge').length,
    sourceLayerCounts: Object.fromEntries([...layers].map(([layer, pieces]) => [layer, pieces.length])),
    standingSources: [...layers].flatMap(([layer, pieces]) => pieces.flatMap((piece, index) => piece.foot
      ? [{ layer, index, origin: [piece.x, piece.y], worldFoot: [piece.x + piece.foot.x, piece.y + piece.foot.y] }] : [])),
    displayedLayerCounts: Object.fromEntries([...svg.querySelectorAll('.board-world > .layer')]
      .map((layer) => [layer.getAttribute('class')!.replace('layer layer-', ''), layer.children.length])),
    stateSha256: createHash('sha256').update(before).digest('hex') });
  board.dispose();
}
writeFileSync(resolve(output, 'runtime-scene-provenance.json'), JSON.stringify({
  kind: 'offline actual BoardView/SvgBoardSurface snapshot; not live browser or GPU playback',
  levelId: level.id, renders, sources: Object.fromEntries(sources),
  omissions: [], gameplayChanged: false, comparison: edgeComparison
    ? 'same complete production viewport and shared footpoint ordering; only the fourteen authored ground-edge overlays differ'
    : 'same art and entire scene; legacy fixed-layer order versus footpoint order',
  limitations: ['Initial poses only; no browser or GPU pixel verification',
    'Real front canopies can occlude rear actors; no selective fading or removal is applied',
    'Scene framing is the production viewport; the extra field-only diagnostic crops at logical map boundaries'],
}, null, 2) + '\n');
console.log(output);

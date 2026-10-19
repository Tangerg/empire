/** Complete c01-01 BoardView/SvgBoardSurface evidence, with actual actor bindings and no omissions. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { CANDIDATE_01_UNIT_ART } from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
import { BoardDepthOrder, BoardView, CELL_FOOT, SvgBoardSurface, TILE, type BoardLayer, type BoardPiece } from '@empire/game-ui';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.STANDING_REVIEW_OUTPUT ?? '/tmp/empire-gray-flag-scene-review');
mkdirSync(output, { recursive: true });
const level = shippedLevel('c01-01');
const sources = new Map<string, string>();
const appStylesPath = 'packages/game-ui/src/styles/app.css';
const appStyles = readFileSync(resolve(root, appStylesPath), 'utf8');
sources.set(appStylesPath, createHash('sha256').update(appStyles).digest('hex'));
const environmentPath = 'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json';
const manifest = JSON.parse(readFileSync(resolve(root, environmentPath), 'utf8'));
for (const path of [environmentPath,
  'packages/story-candidate-01/src/levels.ts',
  'packages/story-candidate-01/src/campaign.ts',
  'packages/story-candidate-01/src/presentation/index.ts',
  'packages/game-ui/src/art/battlefield-layer.ts',
  'packages/game-ui/src/ui/board.ts',
  'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/scenes/c01-01.scene.json',
  'packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-road-material.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-road-corridor.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-road-corridor.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-environment.ts',
  'packages/story-candidate-01/src/presentation/candidate-01-world-material.ts',
  'packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-road-material-v2.json',
  'packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts',
]) sources.set(path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex'));
const renders = [];
{
  const state = engine.createState(level);
  const before = JSON.stringify(state);
  const layers = new Map<BoardLayer, readonly BoardPiece[]>();
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
    renderer: (scene) => {
      const surface = new SvgBoardSurface(scene);
      const setLayer = surface.setLayer.bind(surface);
      surface.setLayer = (layer, pieces) => {
        layers.set(layer, pieces);
        setLayer(layer, pieces);
      };
      return surface;
    },
  }, state, { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} });
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
  if (unitCount !== 19 || buildingCount !== 4) throw new Error(`incomplete c01-01 snapshot: ${unitCount} actors, ${buildingCount} buildings`);
  const filename = 'runtime-standing-full.svg';
  const actors = state.units.map((unit) => ({ id: `actor:${unit.id}`, actorId: unit.id, key: unit.key,
    type: unit.type, cell: [unit.x, unit.y], facing: unit.facing,
    origin: [unit.x * TILE, unit.y * TILE], worldFoot: [unit.x * TILE + CELL_FOOT.x, unit.y * TILE + CELL_FOOT.y] }));
  const order = new BoardDepthOrder<{ id: string }>();
  for (const [layer, pieces] of layers) order.replaceLayer(layer, pieces.map((piece, index) => {
    return {
      piece,
      drawing: { id: `${layer}:${index}`, layer, index, origin: [piece.x, piece.y],
        ...(piece.foot ? { worldFoot: [piece.x + piece.foot.x, piece.y + piece.foot.y] } : {}) },
    };
  }));
  for (const actor of actors) order.placeUnit(actor.actorId, actor, actor.origin[0], actor.origin[1]);
  const standingDrawOrder = order.ordered('units').map((entry, index) => ({ ...entry, displayIndex: index }));
  if (standingDrawOrder.length !== svg.querySelector('.layer-units')!.children.length) {
    throw new Error('recorded draw order differs from actual surface');
  }
  const actorBindings = actors.map((actor) => {
    const displayIndex = standingDrawOrder.findIndex((entry) => entry.id === actor.id);
    const element = svg.querySelector('.layer-units')!.children[displayIndex];
    const runtimeImage = element.querySelector('image')!.getAttribute('href')!.split('?')[0];
    return { ...actor, topicId: CANDIDATE_01_UNIT_ART[actor.type], runtimeImage,
      usesApprovedV5: runtimeImage.includes('/runtime-approved-v5/') };
  });
  const markup = svg.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
    const file = resolve(root, `.${path}`);
    sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
    return `href="${relative(output, file)}"`;
  });
  writeFileSync(resolve(output, filename), markup);
  if (JSON.stringify(state) !== before) throw new Error('depth review changed battle state');
  const namedPieces = [...layers].flatMap(([layer, pieces]) => pieces.flatMap((piece, index) => {
    const container = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    container.innerHTML = piece.markup;
    const image = container.querySelector('image');
    if (!image) return [];
    const href = image.getAttribute('href')!;
    const filename = href.split('/').pop()!.split('?')[0];
    const atlas = manifest.atlases.find((a: { png2x: string }) => a.png2x.split('/').pop() === filename);
    const prop = container.querySelector('.candidate-environment-prop, .candidate-scenery-topic, [data-candidate-art="structure"]');
    if (!prop) return [];
    const cellIndex = atlas ? Math.round(-Number(image.getAttribute('x')) / atlas.cellWidth)
      + Math.round(-Number(image.getAttribute('y')) / atlas.cellHeight) * atlas.columns : null;
    const cell = atlas?.cells?.[cellIndex!];
    return [{ sourceId: `${layer}:${index}`, sourceImage: filename, atlas: atlas?.id, cellIndex,
      cellId: cell?.id, anchor: cell?.anchor, footprint: cell?.footprint,
      transform: prop.getAttribute('transform'), opacity: prop.getAttribute('opacity') ?? '1',
      origin: [piece.x, piece.y], ...(piece.foot ? { worldFoot: [piece.x + piece.foot.x, piece.y + piece.foot.y] } : {}) }];
  }));
  renders.push({ filename, namedPieces, viewport: [0, 0, width, height], unitCount, buildingCount, actors: actorBindings, standingDrawOrder,
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
  checkpoint: process.env.SCENE_REVIEW_CHECKPOINT ?? 'current worktree; exact input hashes recorded', omissions: [], captureMutatesGameplay: false, comparison: 'none; complete actual production scene',
  limitations: ['Initial poses only; no browser or GPU pixel verification',
    'Real front canopies can occlude rear actors; no selective fading or removal is applied'],
}, null, 2) + '\n');
console.log(output);

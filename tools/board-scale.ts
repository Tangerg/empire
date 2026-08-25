/**
 * What one board costs to draw, as the field grows.
 *
 * The renderer is about to be given a second backend, and the case for it is a
 * number nobody has yet written down. `AGENTS.md` says not to optimize from
 * intuition alone: measure the path, then implement the simplest change the
 * evidence supports. This measures the path.
 *
 * What it reports, per layer:
 *
 * - **pictures** — placed drawings in the layer. One tile, one prop, one unit.
 * - **nodes** — every element under it. This is the term that decides SVG paint
 *   cost, and the one a batched backend collapses.
 * - **distinct** — how many of those pieces are *different* pictures. A texture
 *   cache pays exactly this, once, instead of pieces × every frame; the ratio is
 *   the whole argument for baking markup into textures.
 *
 * The board is grown by tiling a shipped level, so the terrain mix, the unit
 * density and the art are the campaign's own rather than a synthetic best case.
 *
 *   npx vite-node tools/board-scale.ts
 */
import { JSDOM } from 'jsdom';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  type LevelData,
  type LevelTileOwner,
  type LevelUnit,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK, ANCIENT_EMPIRES_LEVELS } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

const dom = new JSDOM('<!doctype html><div id="app"></div>');
const globals = globalThis as Record<string, unknown>;
globals.window = dom.window;
globals.document = dom.window.document;
globals.SVGElement = dom.window.SVGElement;
globals.MutationObserver = dom.window.MutationObserver;
globals.DOMParser = dom.window.DOMParser;
globals.Node = dom.window.Node;
globals.Element = dom.window.Element;
globals.requestAnimationFrame = (callback: (time: number) => void) => setTimeout(() => callback(0), 0);
globals.cancelAnimationFrame = (handle: number) => clearTimeout(handle as unknown as NodeJS.Timeout);

const { BoardView, emptyOverlay, svgBoardSurface, GENERIC_ART, BOARD_LAYERS } = await import('@empire/game-ui');

const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });
const grid = engine.rules.grids.get('square4');

/**
 * The same level, repeated across a bigger field.
 *
 * Ids are the reason this is not a plain copy. Structures, composites and
 * commanders are named, and a named thing cannot appear twice, so those stay as
 * the one set the base level shipped — which also keeps its victory conditions
 * resolvable. Units are anonymous past the first tile for the same reason.
 */
function tiled(base: LevelData, nx: number, ny: number): LevelData {
  const width = base.width * nx;
  const height = base.height * ny;
  const terrain = Array.from({ length: height }, (_, y) => base.terrain[y % base.height].repeat(nx));
  const elevation = base.elevation && Array.from({ length: width * height }, (_, cell) => {
    const x = cell % width;
    const y = Math.floor(cell / width);
    return base.elevation![(y % base.height) * base.width + (x % base.width)];
  });

  const units: LevelUnit[] = [];
  const owners: LevelTileOwner[] = [];
  for (let ty = 0; ty < ny; ty++) {
    for (let tx = 0; tx < nx; tx++) {
      const dx = tx * base.width;
      const dy = ty * base.height;
      const first = tx === 0 && ty === 0;
      for (const unit of base.units) {
        units.push({ ...unit, key: first ? unit.key : undefined, x: unit.x + dx, y: unit.y + dy });
      }
      for (const owner of base.owners) owners.push({ ...owner, x: owner.x + dx, y: owner.y + dy });
    }
  }

  return { ...base, width, height, terrain, elevation, units, owners, deployment: undefined };
}

interface LayerCost {
  layer: string;
  pieces: number;
  nodes: number;
  distinct: number;
}

/**
 * What one drawn picture is, for counting purposes.
 *
 * An element that carries a translate, and is not inside another one. That rule
 * is the board's own convention — everything placed on the field is a group at a
 * cell's origin or centre — and it is the only definition that finds the tiles
 * rather than the container the layer's markup happens to be wrapped in.
 *
 * Line work has no translate and is therefore not a picture. That is the honest
 * answer: a grid is geometry, and geometry is what a GPU draws without a texture
 * at all.
 */
const isPlaced = (element: Element): boolean =>
  (element.getAttribute('transform') ?? '').includes('translate(');

/**
 * The same picture at a different place is one texture, so the placement is not
 * part of what a picture *is*.
 *
 * A `data-unit="7"` handle used to be stripped here as well. It was the only
 * mention of that attribute anywhere outside the two places that wrote it — a
 * label kept for a human reading a renderer diff, which this probe had to undo to
 * measure anything. It is gone from the markup instead.
 */
const pictureIdentity = (element: Element): string =>
  element.outerHTML.replace(/ transform="[^"]*"/g, '');

function placedPictures(host: Element): string[] {
  const found: string[] = [];
  const walk = (element: Element): void => {
    for (const child of element.children) {
      if (isPlaced(child)) found.push(pictureIdentity(child));
      else walk(child);
    }
  };
  walk(host);
  return found;
}

function layerCosts(root: Element): LayerCost[] {
  const costs: LayerCost[] = [];
  for (const layer of BOARD_LAYERS) {
    const host = root.querySelector(`.layer-${layer}`);
    if (!host) throw new Error(`board has no ${layer} layer`);
    const pictures = placedPictures(host);
    costs.push({
      layer,
      pieces: pictures.length,
      nodes: host.querySelectorAll('*').length,
      distinct: new Set(pictures).size,
    });
  }
  return costs;
}

const pad = (value: string | number, width: number) => String(value).padStart(width);

function measure(label: string, base: LevelData, art: unknown, nx: number, ny: number): void {
  const level = tiled(base, nx, ny);
  const state = engine.createState(level);
  const started = performance.now();
  const board = new BoardView({ content, grid, art: art as never, renderer: svgBoardSurface }, state, {
    onTileClick: () => {},
    onTileEnter: () => {},
    onLeave: () => {},
    onSecondary: () => {},
    onScale: () => {},
  });
  board.render(emptyOverlay());
  const built = performance.now() - started;

  const root = board.el as Element;
  const costs = layerCosts(root);
  const cells = level.width * level.height;
  const nodes = root.querySelectorAll('*').length;

  const redrawn = performance.now();
  board.render(emptyOverlay());
  const again = performance.now() - redrawn;

  console.log(
    `${label}  ${pad(`${level.width}×${level.height}`, 7)}  cells ${pad(cells.toLocaleString(), 6)}`
    + `  units ${pad(level.units.length, 4)}  nodes ${pad(nodes.toLocaleString(), 7)}`
    + `  ${pad((nodes / cells).toFixed(1), 5)}/cell`
    + `  build ${pad(built.toFixed(0), 5)}ms  redraw ${pad(again.toFixed(0), 4)}ms`,
  );
  for (const cost of costs) {
    if (!cost.nodes) continue;
    const reuse = cost.distinct ? `${(cost.pieces / cost.distinct).toFixed(1)}:1` : 'line work';
    console.log(
      `      ${cost.layer.padEnd(11)} pictures ${pad(cost.pieces.toLocaleString(), 6)}`
      + `  nodes ${pad(cost.nodes.toLocaleString(), 7)}`
      + `  distinct ${pad(cost.distinct.toLocaleString(), 6)}`
      + `  ${pad(reuse, 9)}`,
    );
  }
  board.dispose();
}

const HEAVIEST = CANDIDATE_01_LEVELS.find((level) => level.id === 'c01-16')!;
const PAINTED = CANDIDATE_01_LEVELS.find((level) => level.id === 'c01-01')!;
const GENERIC = ANCIENT_EMPIRES_LEVELS[ANCIENT_EMPIRES_LEVELS.length - 1];

console.log(`themed art · ${HEAVIEST.name} tiled`);
for (const [nx, ny] of [[1, 1], [2, 2], [3, 3]]) measure('  ', HEAVIEST, CANDIDATE_01_ART, nx, ny);

console.log(`\npainted scene · ${PAINTED.name} tiled`);
for (const [nx, ny] of [[1, 1], [3, 3]]) measure('  ', PAINTED, CANDIDATE_01_ART, nx, ny);

console.log(`\ngeneric art · ${GENERIC.name} tiled`);
for (const [nx, ny] of [[1, 1], [3, 3]]) measure('  ', GENERIC, GENERIC_ART, nx, ny);

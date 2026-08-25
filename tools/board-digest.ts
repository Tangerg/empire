/**
 * What the board draws, for every shipped level, as one text file.
 *
 * The safety net for changing the renderer. A board is 5,575 SVG nodes of scenery,
 * tiles, sprites and overlays, and no test asserts most of it — so extracting a
 * renderer port under it could quietly move a thousand things by a pixel and every
 * test would still pass. This dumps the mounted markup instead: run it before the
 * change, run it after, and diff.
 *
 * Deliberately a probe rather than a test. It pins the current picture exactly,
 * which is the right thing to hold still during a refactor and the wrong thing to
 * assert forever — a fixture this size fails on every intended change and teaches
 * nobody anything.
 *
 *   npx vite-node tools/board-digest.ts > /tmp/board-before.txt
 */
import { JSDOM } from 'jsdom';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  idx,
  type LevelData,
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

const { BoardView, emptyOverlay, svgBoardSurface, GENERIC_ART } = await import('@empire/game-ui');

const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });
const grid = engine.rules.grids.get('square4');

/** An overlay with something in every set, so no layer is dumped empty. */
function busyOverlay(level: LevelData) {
  const overlay = emptyOverlay();
  const map = { width: level.width, height: level.height };
  const cells = [0, 1, 2, 3, 4, 5].map((n) => idx(map as never, n % level.width, Math.floor(n / level.width)));
  overlay.move = new Set(cells.slice(0, 3));
  overlay.attack = new Set(cells.slice(3, 4));
  overlay.heal = new Set(cells.slice(4, 5));
  overlay.threat = new Set(cells.slice(5, 6));
  overlay.controlled = new Set(cells.slice(0, 1));
  overlay.incoming = new Map([[cells[1], 2]]);
  overlay.path = [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }];
  overlay.selected = { x: 1, y: 1 };
  overlay.cursor = { x: 2, y: 1 };
  return overlay;
}

const levels: [string, LevelData][] = [
  ...ANCIENT_EMPIRES_LEVELS.map((level) => [`generic/${level.id}`, level] as [string, LevelData]),
  ...CANDIDATE_01_LEVELS.map((level) => [`themed/${level.id}`, level] as [string, LevelData]),
];

const lines: string[] = [];
for (const [label, level] of levels) {
  // Both arts over every level: the generic look and the painted one are different
  // renderers' worth of markup, and both have to hold still.
  const art = label.startsWith('themed') ? CANDIDATE_01_ART : GENERIC_ART;
  const board = new BoardView({ content, grid, art, renderer: svgBoardSurface }, engine.createState(level), {
    onTileClick: () => {},
    onTileEnter: () => {},
    onLeave: () => {},
    onSecondary: () => {},
    onScale: () => {},
  });
  board.render(busyOverlay(level));
  board.setZoom(1.75);
  lines.push(`### ${label}`);
  lines.push(board.el.outerHTML);
  board.dispose();
}
console.log(lines.join('\n'));

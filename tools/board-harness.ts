/**
 * The board the rulers measure, mounted once and described three ways.
 *
 * `board-digest`, `board-flat` and `board-scale` are the instruments that prove a
 * renderer change moved nothing. All three had their own copy of the same
 * twenty-five lines: the JSDOM globals, the catalog and its three packs, the
 * engine, the grid, the busy overlay, the shipped level list, and the five no-op
 * handlers a `BoardView` needs.
 *
 * Which made the instruments themselves the risk. A ruler is only worth its
 * reading if the two runs measured the same board, and `board-digest` and
 * `board-flat` are meant to be read against each other — one pins the markup, the
 * other resolves the placement. Edit the overlay in one and they describe
 * different boards, and nothing says so.
 *
 * The DOM has to exist before `@empire/game-ui` is evaluated, which is why the
 * import of it is dynamic and why this module owns it. A tool importing this one
 * gets the globals installed as a precondition of the import.
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
import type { ArtDirection, BoardOverlay, BoardView } from '@empire/game-ui';

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

const gameUi = await import('@empire/game-ui');
export const { BOARD_LAYERS, emptyOverlay, GENERIC_ART } = gameUi;
export { CANDIDATE_01_ART };

const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });
const grid = engine.rules.grids.get('square4');

export { content, engine };

/** One shipped level and the art it ships under. */
export interface ShippedBoard {
  readonly label: string;
  readonly level: LevelData;
  readonly art: ArtDirection;
}

/**
 * Every shipped level under the art it ships with, and the campaign's levels
 * under the generic look as well.
 *
 * Both arts over every level: the painted one and the plain one are different
 * renderers' worth of markup, and both have to hold still.
 */
export const SHIPPED_BOARDS: readonly ShippedBoard[] = [
  ...ANCIENT_EMPIRES_LEVELS.map((level) => ({
    label: `generic/${level.id}`,
    level,
    art: GENERIC_ART,
  })),
  ...CANDIDATE_01_LEVELS.map((level) => ({
    label: `themed/${level.id}`,
    level,
    art: CANDIDATE_01_ART,
  })),
];

/** One shipped campaign level by id, or a refusal naming it. */
export function shippedLevel(id: string): LevelData {
  const level = [...CANDIDATE_01_LEVELS, ...ANCIENT_EMPIRES_LEVELS].find((each) => each.id === id);
  if (!level) throw new Error(`no shipped level "${id}"`);
  return level;
}

/** An overlay with something in every set, so no layer is dumped empty. */
export function busyOverlay(level: LevelData): BoardOverlay {
  const overlay = emptyOverlay();
  const map = { width: level.width, height: level.height };
  const cells = [0, 1, 2, 3, 4, 5].map((n) => idx(map, n % level.width, Math.floor(n / level.width)));
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

/** A mounted board over one level. The caller renders it and disposes it. */
export function mountBoard(level: LevelData, art: ArtDirection): BoardView {
  return new gameUi.BoardView(
    { content, grid, art, renderer: gameUi.svgBoardSurface },
    engine.createState(level),
    {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
      onScale: () => {},
    },
  );
}

/**
 * The zoom every dump is taken at.
 *
 * Not 1: a scale factor that is never applied is a code path the rulers never
 * look at, and the board's own layout maths runs through it.
 */
const DUMP_ZOOM = 1.75;

/**
 * Every shipped board, mounted with the busy overlay, described by one reading.
 *
 * The reading is the only thing that differs between `board-digest` and
 * `board-flat`; everything above it is what they must not differ about.
 */
export function describeEveryShippedBoard(
  describe: (root: Element, out: string[]) => void,
): string {
  const lines: string[] = [];
  for (const { label, level, art } of SHIPPED_BOARDS) {
    const board = mountBoard(level, art);
    board.render(busyOverlay(level));
    board.setZoom(DUMP_ZOOM);
    lines.push(`### ${label}`);
    describe(board.el as Element, lines);
    board.dispose();
  }
  return lines.join('\n');
}

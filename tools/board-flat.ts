/**
 * Every shipped board as a flat list of drawn things at absolute coordinates.
 *
 * `board-digest.ts` pins the markup exactly, which is the right net for most
 * changes and the wrong one for moving a translate out of a picture and onto its
 * wrapper: the picture is identical and every line of the dump differs. This
 * resolves placement instead. A group that carries nothing but a transform is
 * structure and is not reported; everything else is reported at the coordinates it
 * actually lands on.
 *
 * So a wrapper appearing or disappearing is invisible here, and a thing moving by
 * a pixel is not. Nesting is not reported either, for the same reason: indenting by
 * depth would make one added group shift every line below it. Run it on both sides
 * of a renderer refactor and diff.
 *
 *   npx vite-node tools/board-flat.ts > /tmp/flat-before.txt
 */
import { JSDOM } from 'jsdom';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  createState,
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

/** Coordinate attributes, and which axis each one moves along. */
const ALONG_X = ['x', 'cx', 'x1', 'x2'];
const ALONG_Y = ['y', 'cy', 'y1', 'y2'];

interface Transform {
  dx: number;
  dy: number;
  /** Whatever is not a leading translate — a rotate, a scale — kept verbatim. */
  rest: string;
}

function readTransform(value: string): Transform {
  const ops = [...value.matchAll(/([a-zA-Z]+)\(([^)]*)\)/g)];
  let dx = 0;
  let dy = 0;
  const rest: string[] = [];
  for (const [, name, args] of ops) {
    const numbers = args.trim().split(/[\s,]+/).map(Number);
    // Only leading translates fold into a position; once anything else has been
    // seen, the rest of the chain is kept as written so it cannot be misread.
    if (name === 'translate' && rest.length === 0) {
      dx += numbers[0] ?? 0;
      dy += numbers[1] ?? 0;
    } else {
      rest.push(`${name}(${numbers.join(' ')})`);
    }
  }
  return { dx, dy, rest: rest.join(' ') };
}

const shift = (value: string, by: number): string => {
  const number = Number(value);
  return Number.isFinite(number) ? (number + by).toFixed(2) : value;
};

/** A points list moved bodily, so a polygon reads the same wherever it was drawn. */
const shiftPoints = (value: string, dx: number, dy: number): string =>
  value.trim().split(/\s+/).map((pair) => {
    const [x, y] = pair.split(',');
    return `${shift(x, dx)},${shift(y ?? '0', dy)}`;
  }).join(' ');

/**
 * An element's attributes, with any coordinate among them resolved to absolute.
 *
 * `sited` says the position now lives in the attributes, so the caller must not
 * also print the offset — printing both makes `@0,0 x=162` and `@160,0 x=162`
 * look like two different places when they are the same one.
 */
function describe(element: Element, dx: number, dy: number): { attrs: string; sited: boolean } {
  const parts: string[] = [];
  let sited = false;
  for (const attribute of [...element.attributes].sort((a, b) => a.name.localeCompare(b.name))) {
    if (attribute.name === 'transform') continue;
    if (ALONG_X.includes(attribute.name)) {
      parts.push(`${attribute.name}=${shift(attribute.value, dx)}`);
      sited = true;
    } else if (ALONG_Y.includes(attribute.name)) {
      parts.push(`${attribute.name}=${shift(attribute.value, dy)}`);
      sited = true;
    } else if (attribute.name === 'points') {
      parts.push(`points=${shiftPoints(attribute.value, dx, dy)}`);
      sited = true;
    } else {
      parts.push(`${attribute.name}=${attribute.value}`);
    }
  }
  return { attrs: parts.join(' '), sited };
}

/**
 * Walk the tree, folding placement into the things placed.
 *
 * A nested `<svg>` opens its own coordinate system, so it is reported at where it
 * lands and its content is left exactly as written rather than resolved against
 * the outer offsets.
 */
function flatten(root: Element, out: string[], dx = 0, dy = 0): void {
  for (const child of [...root.children]) {
    const own = readTransform(child.getAttribute('transform') ?? '');
    const x = dx + own.dx;
    const y = dy + own.dy;
    const tag = child.tagName;
    const { attrs, sited } = describe(child, x, y);
    // A group has no geometry of its own: where it sits reaches the picture only
    // through its children, and those are already resolved. So a group is reported
    // without a position — otherwise moving a translate from the children onto the
    // group they share reads as the group having moved.
    const at = sited || tag === 'g' ? '' : `@${x.toFixed(2)},${y.toFixed(2)} `;

    if (tag === 'svg') {
      out.push(`svg ${at}${own.rest} ${attrs}`);
      out.push(child.innerHTML.replace(/\s+/g, ' ').trim());
      continue;
    }
    // A group with nothing but a transform is structure, not a drawing. Reporting
    // it would make a wrapper's arrival look like a change to the picture.
    const structural = tag === 'g' && attrs === '' && own.rest === '';
    if (!structural) {
      const text = child.children.length === 0 ? (child.textContent ?? '').trim() : '';
      out.push(`${tag} ${at}${own.rest} ${attrs}` + (text ? ` "${text}"` : ''));
    }
    flatten(child, out, x, y);
  }
}

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
  const art = label.startsWith('themed') ? CANDIDATE_01_ART : GENERIC_ART;
  const board = new BoardView({ content, grid, art, renderer: svgBoardSurface }, createState(content, level), {
    onTileClick: () => {},
    onTileEnter: () => {},
    onLeave: () => {},
    onSecondary: () => {},
    onScale: () => {},
  });
  board.render(busyOverlay(level));
  board.setZoom(1.75);
  lines.push(`### ${label}`);
  flatten(board.el as Element, lines);
  board.dispose();
}
console.log(lines.join('\n'));

/** Four-state diagnostic only. Nothing in an application imports this material pilot. */
import type { BoardPiece } from '@empire/game-ui';

export type Cell = readonly [number, number];
export const PILOT_MASKS = [5, 10, 7, 13] as const;
export const WALL_PITCH = 32;
export const CAP_LIFT = [-4, -16] as const;
export const MATERIAL_CORE = [115, 115, 1139, 1139] as const;
const MATERIAL_ROOT = '/packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/fortification-materials';
export const WALL_SOURCES = {
  top: `${MATERIAL_ROOT}/stone-top-material/master.png`,
  face: `${MATERIAL_ROOT}/stone-wall-face-material/master.png`,
} as const;
const directions = [[0, -1, 1], [1, 0, 2], [0, 1, 4], [-1, 0, 8]] as const;
const key = ([x, y]: Cell) => `${x},${y}`;

export interface WallCell {
  readonly at: Cell;
  readonly mask: number;
  readonly component: number;
  readonly uvOrigin: Cell;
  readonly faceJoins: { readonly east: readonly [boolean, boolean]; readonly south: readonly [boolean, boolean] };
}
export interface WallTopology {
  readonly cells: readonly WallCell[];
  readonly components: readonly (readonly Cell[])[];
  readonly connections: number;
  readonly exposedEdges: number;
}

/** Occupancy is supplied by the actual map consumer, never inferred from a picture. */
export function wallTopology(cells: readonly Cell[]): WallTopology {
  const occupied = new Map<string, Cell>();
  for (const cell of cells) {
    if (!cell.every(Number.isInteger)) throw new Error('wall coordinates must be integers');
    if (occupied.has(key(cell))) throw new Error(`duplicate wall cell ${key(cell)}`);
    occupied.set(key(cell), cell);
  }
  const components: Cell[][] = [];
  const membership = new Map<string, number>();
  for (const cell of cells) {
    if (membership.has(key(cell))) continue;
    const component: Cell[] = [cell];
    membership.set(key(cell), components.length);
    for (let index = 0; index < component.length; index++) {
      const [x, y] = component[index];
      for (const [dx, dy] of directions) {
        const next = occupied.get(key([x + dx, y + dy]));
        if (!next || membership.has(key(next))) continue;
        membership.set(key(next), components.length);
        component.push(next);
      }
    }
    components.push(component);
  }
  const origins = components.map(component => [Math.min(...component.map(c => c[0])), Math.min(...component.map(c => c[1]))] as Cell);
  let connections = 0;
  const result = cells.map(at => {
    const mask = directions.reduce((value, [dx, dy, bit]) => value | (occupied.has(key([at[0] + dx, at[1] + dy])) ? bit : 0), 0);
    connections += Number(Boolean(mask & 2)) + Number(Boolean(mask & 4));
    const component = membership.get(key(at))!;
    return { at, mask, component, uvOrigin: origins[component] };
  });
  const indexed = new Map(result.map(cell => [key(cell.at), cell]));
  const joined = (x: number, y: number, exteriorBit: number) => {
    const other = indexed.get(key([x, y]));
    return Boolean(other && (PILOT_MASKS as readonly number[]).includes(other.mask) && !(other.mask & exteriorBit));
  };
  return { cells: result.map(cell => {
    const [x, y] = cell.at;
    return { ...cell, faceJoins: { east: [joined(x, y - 1, 2), joined(x, y + 1, 2)] as const,
      south: [joined(x - 1, y, 4), joined(x + 1, y, 4)] as const } };
  }), components, connections, exposedEdges: cells.length * 4 - connections * 2 };
}

export function supportedWall(cell: WallCell): boolean {
  return (PILOT_MASKS as readonly number[]).includes(cell.mask);
}

export function wallGeometry(cell: WallCell, density: 4 | 16 = 4) {
  if (!supportedWall(cell)) throw new Error(`wall pilot has no mask ${cell.mask}; retain explicitly marked legacy art`);
  const [x, y] = cell.at;
  const [ox, oy] = cell.uvOrigin;
  const span = WALL_PITCH * density;
  const [left, top, right, bottom] = MATERIAL_CORE;
  const [lx, ly] = CAP_LIFT;
  const n = WALL_PITCH;
  const topSource = [left + (x - ox) * span, top + (y - oy) * span, span, span] as const;
  if (density === 4 && (topSource[0] < left || topSource[1] < top || topSource[0] + span > right || topSource[1] + span > bottom)) {
    throw new Error('wall component exceeds the single-source core; repetition is not authorized');
  }
  // Butt-clipped diagonal planes produce an alpha seam when composited. Extend
  // their identical UV sample only into another implemented, collinear exterior
  // face. The union of painted planes is unchanged; no ground overhang is added.
  const [en, es] = cell.faceJoins.east.map(joined => joined ? 4 : 0);
  const [sw, se] = cell.faceJoins.south.map(joined => joined ? 4 : 0);
  return {
    paintEast: cell.mask & 2 ? [] : [[n + lx, ly - en], [n + lx, n + ly + es], [n, n + es], [n, -en]] as readonly Cell[],
    paintSouth: cell.mask & 4 ? [] : [[lx - sw, n + ly], [n + lx + se, n + ly], [n + se, n], [-sw, n]] as readonly Cell[],
    top: [[lx, ly], [n + lx, ly], [n + lx, n + ly], [lx, n + ly]] as readonly Cell[],
    east: cell.mask & 2 ? [] : [[n + lx, ly], [n + lx, n + ly], [n, n], [n, 0]] as readonly Cell[],
    south: cell.mask & 4 ? [] : [[lx, n + ly], [n + lx, n + ly], [n, n], [0, n]] as readonly Cell[],
    topSource,
    eastSource: [left + (y - oy) * span, 627 - span / 2, span, span] as const,
    southSource: [left + (x - ox) * span, 627 - span / 2, span, span] as const,
  };
}

/** Flat material stock is projected onto planes; no whole isometric sprite is rotated. */
export function wallPilotPiece(cell: WallCell, sources: { top: string; face: string } = WALL_SOURCES, topOnly = false, density: 4 | 16 = 4): BoardPiece {
  const geometry = wallGeometry(cell, density);
  const id = `wall-pilot-${cell.at[0]}-${cell.at[1]}`;
  const parts: string[] = [];
  const [lx, ly] = CAP_LIFT;
  const vx = -lx / (WALL_PITCH * density), vy = -ly / (WALL_PITCH * density);
  const plane = (name: string, points: readonly Cell[], href: string, matrix: readonly number[], window: readonly number[]) => {
    if (!points.length) return;
    let texture = `<image href="${href}" width="1254" height="1254"/>`;
    if (density === 16) {
      // This comparison deliberately repeats the unchanged stock core. It is not
      // claimed seamless: exact edge differences remain visible and measured.
      const [u, v, w, h] = window;
      const margin = 4 * density;
      const pattern = `${id}-${name}-stock`;
      texture = `<defs><pattern id="${pattern}" x="115" y="115" width="1024" height="1024" patternUnits="userSpaceOnUse" viewBox="115 115 1024 1024"><image href="${href}" width="1254" height="1254"/></pattern></defs><rect x="${u - margin}" y="${v - margin}" width="${w + margin * 2}" height="${h + margin * 2}" fill="url(#${pattern})"/>`;
    }
    parts.push(`<g data-wall-plane="${name}"><defs><clipPath id="${id}-${name}" clipPathUnits="userSpaceOnUse"><polygon points="${points.map(p => p.join(',')).join(' ')}"/></clipPath></defs><g clip-path="url(#${id}-${name})"><g transform="matrix(${matrix.join(' ')})">${texture}</g></g></g>`);
  };
  if (!topOnly) {
    const [eu, ev] = geometry.eastSource;
    // U follows the cardinal wall axis. 32 physical height units project to (4,16).
    plane('east', geometry.paintEast, sources.face, [0, 1 / density, vx, vy, WALL_PITCH + lx - ev * vx, ly - eu / density - ev * vy], geometry.eastSource);
    const [su, sv] = geometry.southSource;
    plane('south', geometry.paintSouth, sources.face, [1 / density, 0, vx, vy, lx - su / density - sv * vx, WALL_PITCH + ly - sv * vy], geometry.southSource);
  }
  const [u, v] = geometry.topSource;
  plane('top', geometry.top, sources.top, [1 / density, 0, 0, 1 / density, lx - u / density, ly - v / density], geometry.topSource);
  return {
    x: cell.at[0] * WALL_PITCH,
    y: cell.at[1] * WALL_PITCH,
    foot: { x: WALL_PITCH / 2, y: WALL_PITCH },
    markup: `<g data-wall-pilot-mask="${cell.mask}" data-wall-cell="${cell.at.join(',')}">${parts.join('')}</g>`,
  };
}

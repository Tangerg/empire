import { inBounds } from '@empire/battle-engine';
import { TILE } from '@empire/game-ui';
import type { FixedNumbers } from './candidate-01-documents';

type Point = readonly [number, number];
export interface CandidateRoadCorridorProfile {
  readonly width: number;
  readonly maxTangentCut: number;
  readonly maxPlainOverhang: number;
  readonly protectedCells: readonly FixedNumbers<2>[];
}

const directions: readonly Point[] = [[-1, 0], [0, -1], [0, 1], [1, 0]];
const key = ([x, y]: Point) => `${x},${y}`;
const compare = (a: Point, b: Point) => a[0] - b[0] || a[1] - b[1];
const center = ([x, y]: Point): Point => [(x + .5) * TILE, (y + .5) * TILE];
const collinear = (a: Point, b: Point, c: Point) =>
  (b[0] - a[0]) * (c[1] - b[1]) === (b[1] - a[1]) * (c[0] - b[0]);

/** Painted cells own nodes; catalog route semantics own their cardinal connections. */
export function candidate01RoadGraph(width: number, height: number, painted: (x: number, y: number) => boolean,
  connected: (x: number, y: number) => boolean = painted,
) {
  if (![width, height].every(value => Number.isInteger(value) && value > 0)) {
    throw new Error('candidate-01: road map dimensions must be positive integers');
  }
  const cells: Point[] = [];
  for (let x = 0; x < width; x++) for (let y = 0; y < height; y++) if (painted(x, y)) cells.push([x, y]);
  const occupied = new Set(cells.map(key));
  const neighbors = (p: Point): Point[] => connected(p[0], p[1]) ? directions
    .map(([dx, dy]): Point => [p[0] + dx, p[1] + dy]).filter(p => occupied.has(key(p)) && connected(p[0], p[1])) : [];
  const visited = new Set<string>();
  const edge = (a: Point, b: Point) => compare(a, b) < 0 ? `${key(a)}:${key(b)}` : `${key(b)}:${key(a)}`;
  const chains: Point[][] = [];
  // Junctions/endpoints first; only disconnected all-degree-two cycles remain.
  const starts = [...cells].sort((a, b) => Number(neighbors(a).length === 2) - Number(neighbors(b).length === 2) || compare(a, b));
  for (const start of starts) for (const next of neighbors(start)) {
    if (visited.has(edge(start, next))) continue;
    const chain = [start, next];
    visited.add(edge(start, next));
    let current = next;
    while (neighbors(current).length === 2) {
      const previous = chain[chain.length - 2];
      const after = neighbors(current).find(p => key(p) !== key(previous));
      if (after === undefined || visited.has(edge(current, after))) break;
      visited.add(edge(current, after));
      chain.push(after);
      current = after;
    }
    chains.push(chain);
  }
  return { width, height, cells, chains, occupied, neighbors, connected };
}

function roundedChain(chain: readonly Point[], cut: number): string {
  const closed = key(chain[0]) === key(chain[chain.length - 1]);
  const points = (closed ? chain.slice(0, -1) : chain).map(center);
  const simple = points.filter((point, i) => !closed && (i === 0 || i === points.length - 1)
    || !collinear(points[(i + points.length - 1) % points.length], point, points[(i + 1) % points.length]));
  const corner = (i: number) => {
    const a = simple[(i + simple.length - 1) % simple.length], b = simple[i], c = simple[(i + 1) % simple.length];
    const incoming = Math.hypot(b[0] - a[0], b[1] - a[1]);
    const outgoing = Math.hypot(c[0] - b[0], c[1] - b[1]);
    const radius = Math.min(cut, incoming / 2, outgoing / 2);
    const start: Point = [b[0] + (a[0] - b[0]) * radius / incoming, b[1] + (a[1] - b[1]) * radius / incoming];
    const end: Point = [b[0] + (c[0] - b[0]) * radius / outgoing, b[1] + (c[1] - b[1]) * radius / outgoing];
    return { start, end, command: `L ${start.join(' ')} Q ${b.join(' ')} ${end.join(' ')}` };
  };
  const parts = [`M ${(closed ? corner(0).end : simple[0]).join(' ')}`];
  for (let i = 1; i < simple.length - (closed ? 0 : 1); i++) parts.push(corner(i).command);
  parts.push(closed ? `${corner(0).command} Z` : `L ${simple[simple.length - 1].join(' ')}`);
  return parts.join(' ');
}

/** White alpha geometry; clipping is independent of the unchanged earth RGB. */
export function candidate01RoadCorridorGeometry(
  graph: ReturnType<typeof candidate01RoadGraph>,
  profile: CandidateRoadCorridorProfile,
  plain: (x: number, y: number) => boolean,
) {
  const broadRoad = profile.width === 32 && profile.maxTangentCut === 22 && profile.maxPlainOverhang === 2;
  const narrowTrail = profile.width === 20 && profile.maxTangentCut === 16 && profile.maxPlainOverhang === 0;
  if (!broadRoad && !narrowTrail) {
    throw new Error('candidate-01: road corridor requires a reviewed 32/22/2 or 20/16/0 profile');
  }
  const { width, height, cells, chains, occupied, neighbors, connected } = graph;
  const protectedCells = new Set(profile.protectedCells.map(key));
  if (cells.some(cell => protectedCells.has(key(cell)))) throw new Error('candidate-01: road overlaps a protected scene cell');
  const paths = chains.map(chain => roundedChain(chain, profile.maxTangentCut));
  const filled: string[] = [];
  const limits: string[] = [];
  const allowed: string[] = [];
  const rectangle = (x: number, y: number, w: number, h: number, extra = '') => `<rect x="${x}" y="${y}" width="${w}" height="${h}"${extra}/>`;
  for (const [x, y] of cells) {
    if (neighbors([x, y]).length === 0) filled.push(`<circle cx="${(x + .5) * TILE}" cy="${(y + .5) * TILE}" r="${profile.width / 2}"/>`);
    for (const [dx, dy] of directions) {
      const nx = x + dx, ny = y + dy;
      const outside = !inBounds(graph, nx, ny);
      // A bridge is a connection to the cell edge, never an earth-painted cell.
      if (connected(x, y) && (outside || !occupied.has(key([nx, ny])) && connected(nx, ny))) {
        const [cx, cy] = center([x, y]);
        paths.push(`M ${cx} ${cy} L ${cx + dx * TILE / 2} ${cy + dy * TILE / 2}`);
      }
    }
    if (broadRoad && [[x, y], [x + 1, y], [x, y + 1], [x + 1, y + 1]].every(p => occupied.has(key([p[0], p[1]])) && connected(p[0], p[1]))) {
      // Broad roads keep their filled aprons; narrow trails retain rounded junction paths.
      filled.push(rectangle((x + .5) * TILE, (y + .5) * TILE, TILE, TILE));
    }
    const overhang = profile.maxPlainOverhang;
    limits.push(rectangle(x * TILE - overhang, y * TILE - overhang, TILE + 2 * overhang, TILE + 2 * overhang, ` rx="${overhang}"`));
  }
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (!protectedCells.has(key([x, y])) && (occupied.has(key([x, y])) || plain(x, y))) {
      allowed.push(rectangle(x * TILE, y * TILE, TILE, TILE));
    }
  }
  return {
    alpha: `<g fill="none" stroke="white" stroke-width="${profile.width}" stroke-linecap="round" stroke-linejoin="round">${paths.map(d => `<path d="${d}"/>`).join('')}</g><g fill="white">${filled.join('')}</g>`,
    // Intersection: Euclidean 2px buffer AND actual road/plain cells, minus notch.
    limits: limits.join(''), allowed: allowed.join(''),
  };
}

import { TILE } from '@empire/game-ui';

const NESW = [[0, -1], [1, 0], [0, 1], [-1, 0]] as const;
export type CrossingCell = readonly [number, number];
export interface CandidateCrossingRun {
  readonly axis: 'horizontal' | 'vertical';
  readonly start: CrossingCell;
  readonly length: number;
  readonly pivot: CrossingCell;
}
export interface CandidateCrossingComponent {
  readonly cells: readonly { readonly cell: CrossingCell; readonly mask: number }[];
  readonly runs: readonly CandidateCrossingRun[];
}

/** Bridge terrain owns connectivity. These components only project that existing map. */
export function candidate01CrossingTopology(
  width: number,
  height: number,
  bridge: (x: number, y: number) => boolean,
  route: (x: number, y: number) => boolean,
): CandidateCrossingComponent[] {
  const inside = (x: number, y: number) => x >= 0 && x < width && y >= 0 && y < height && bridge(x, y);
  const visited = new Set<string>();
  const components: CandidateCrossingComponent[] = [];
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (!inside(x, y) || visited.has(`${x},${y}`)) continue;
    const queue: CrossingCell[] = [[x, y]];
    visited.add(`${x},${y}`);
    for (let index = 0; index < queue.length; index++) {
      const [cx, cy] = queue[index];
      for (const [dx, dy] of NESW) {
        const nx = cx + dx, ny = cy + dy, key = `${nx},${ny}`;
        if (!inside(nx, ny) || visited.has(key)) continue;
        visited.add(key);
        queue.push([nx, ny]);
      }
    }
    queue.sort(([ax, ay], [bx, by]) => ay - by || ax - bx);
    const cells = queue.map(([cx, cy]) => ({
      cell: [cx, cy] as CrossingCell,
      mask: NESW.reduce((mask, [dx, dy], bit) => mask | (inside(cx + dx, cy + dy) ? 1 << bit : 0), 0),
    }));
    const runs: CandidateCrossingRun[] = [];
    for (const [cx, cy] of queue) {
      // Keep the original route-first direction and near-end ownership for
      // unaccepted bridge families; a component now owns their intersections.
      const horizontal = route(cx - 1, cy) || route(cx + 1, cy)
        ? true : !(route(cx, cy - 1) || route(cx, cy + 1)) && (inside(cx - 1, cy) || inside(cx + 1, cy));
      if (horizontal ? inside(cx - 1, cy) : inside(cx, cy - 1)) continue;
      let length = 1;
      while (horizontal ? inside(cx + length, cy) : inside(cx, cy + length)) length++;
      runs.push({ axis: horizontal ? 'horizontal' : 'vertical', start: [cx, cy], length,
        pivot: horizontal ? [(cx + length / 2) * TILE, (cy + .5) * TILE]
          : [(cx + .5) * TILE, (cy + length / 2) * TILE] });
    }
    components.push({ cells, runs });
  }
  return components;
}

export interface CandidateCrossingJunction {
  readonly center: CrossingCell;
  readonly pivot: CrossingCell;
  readonly ports: readonly CrossingCell[];
}

/** Only the complete, reviewed three-by-three plus can use these four openings. */
export function candidate01CrossingJunction(component: CandidateCrossingComponent): CandidateCrossingJunction | null {
  if (component.cells.length !== 5 || component.runs.length !== 2) return null;
  const center = component.cells.find(({ mask }) => mask === 15)?.cell;
  if (!center) return null;
  const [x, y] = center;
  const expected = [[x, y, 15], [x, y - 1, 4], [x + 1, y, 8], [x, y + 1, 1], [x - 1, y, 2]];
  if (!expected.every(([cx, cy, mask]) => component.cells.some(({ cell, mask: actual }) =>
    cell[0] === cx && cell[1] === cy && actual === mask))) return null;
  const pivot: CrossingCell = [(x + .5) * TILE, (y + .5) * TILE];
  for (const axis of ['horizontal', 'vertical'] as const) {
    const run = component.runs.find(run => run.axis === axis);
    if (!run || run.length !== 3
      || run.start[0] !== x - (axis === 'horizontal' ? 1 : 0)
      || run.start[1] !== y - (axis === 'vertical' ? 1 : 0)
      || run.pivot[0] !== pivot[0] || run.pivot[1] !== pivot[1]) return null;
  }
  return { center, pivot, ports: NESW.map(([dx, dy]) => [pivot[0] + dx * TILE * 1.5, pivot[1] + dy * TILE * 1.5]) };
}

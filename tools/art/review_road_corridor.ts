/** Emit the actual runtime alpha (not a second implementation) for raster QA. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { content, engine } from '../board-harness';
import { candidate01Level } from '../../packages/story-candidate-01/src/levels';
import { candidate01EnvironmentScene } from '../../packages/story-candidate-01/src/presentation/candidate-01-environment';
import { candidate01RoadGraph } from '../../packages/story-candidate-01/src/presentation/candidate-01-road-corridor';
import { candidate01RoadMaterialPieces, candidate01IsAreaRoad } from '../../packages/story-candidate-01/src/presentation/candidate-01-road-material';
import { candidateMaterial } from '../../packages/story-candidate-01/src/presentation/candidate-01-terrain-materials';

const output = resolve(process.env.ROAD_CORRIDOR_OUTPUT ?? '/tmp/empire-road-corridor');
mkdirSync(output, { recursive: true });
const sceneProfile = candidate01EnvironmentScene('c01-01')?.roadCorridor;
if (!sceneProfile) throw new Error('c01-01 has no reviewed corridor profile');
const profile = sceneProfile;
type Cell = readonly [number, number];
const key = ([x, y]: Cell) => `${x},${y}`;
const neighbors: readonly Cell[] = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];
const svg = (w: number, h: number, body: string) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
function fixture(width: number, height: number, cells: readonly Cell[], excluded: readonly Cell[] = [], protectedCells: readonly Cell[] = [], bridges: readonly Cell[] = []) {
  const painted = new Set(cells.map(key)), forbidden = new Set(excluded.map(key)), connected = new Set([...cells, ...bridges].map(key));
  const graph = candidate01RoadGraph(width, height, (x, y) => painted.has(key([x, y])));
  const pieces = candidate01RoadMaterialPieces(width, height,
    (x, y) => painted.has(key([x, y])), (x, y) => connected.has(key([x, y])),
    (x, y) => !painted.has(key([x, y])) && !forbidden.has(key([x, y])), { ...profile, protectedCells });
  const body = pieces.map(piece => `<g transform="translate(${piece.x} ${piece.y})">${piece.markup.replace(/<image [^>]+mask="([^"]+)"\/>/g,
    '<rect width="1024" height="1024" fill="white" mask="$1"/>')}</g>`).join('');
  return { width, height, cells, excluded, protectedCells, bridges, chains: graph.chains, body };
}
const panels = [];
for (let mask = 0; mask < 256; mask++) {
  const cells: Cell[] = [[2, 2], ...neighbors.flatMap(([dx, dy], bit): Cell[] => mask & (1 << bit) ? [[2 + dx, 2 + dy]] : [])];
  panels.push(fixture(5, 5, cells));
}
writeFileSync(resolve(output, 'patterns.svg'), svg(2560, 2560, panels.map((panel, i) =>
  `<g transform="translate(${i % 16 * 160} ${Math.floor(i / 16) * 160})">${panel.body}</g>`).join('')));
writeFileSync(resolve(output, 'patterns.json'), JSON.stringify(panels.map(({ body: _body, ...data }) => data)));

const map = engine.createState(candidate01Level('c01-01')).map;
const cells: Cell[] = [], excluded: Cell[] = [];
map.tiles.forEach((id, i) => {
  const cell: Cell = [i % map.width, Math.floor(i / map.width)];
  if (candidate01IsAreaRoad(candidateMaterial(content, id))) cells.push(cell);
  else if (id !== 'plain') excluded.push(cell);
});
const actual = fixture(map.width, map.height, cells, excluded, profile.protectedCells);
const special = {
  actual,
  apron: fixture(5, 5, [[1, 1], [2, 1], [1, 2], [2, 2]]),
  ring: fixture(5, 5, [[1, 1], [2, 1], [3, 1], [1, 2], [3, 2], [1, 3], [2, 3], [3, 3]]),
  bridge: fixture(5, 5, [[1, 2], [2, 2], [1, 3]], [[3, 2], [2, 1], [3, 3]], [[2, 3]], [[3, 2]]),
  seam: fixture(34, 5, [[30, 1], [31, 1], [31, 2], [32, 2], [32, 3], [33, 3]]),
};
for (const [name, { body, ...data }] of Object.entries(special)) {
  writeFileSync(resolve(output, `${name}.svg`), svg(data.width * 32, data.height * 32, body));
  writeFileSync(resolve(output, `${name}.json`), JSON.stringify(data, null, 2) + '\n');
}
console.log(`Runtime alpha fixtures: ${output}`);

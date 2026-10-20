import { writeFileSync } from 'node:fs';
import { BoardView, emptyOverlay } from '../../packages/game-ui/src/ui/board';
import { GroundBoardDecorations, squareLayout } from '../../packages/game-ui/src/art/board-decorations';
import { CANDIDATE_01_BOARD_STYLE } from '../../packages/story-candidate-01/src/presentation/candidate-01-board-style';
import type { BoardPiece } from '../../packages/game-ui/src/art/board-surface';

// Execute the real source renderer over an inert surface. No alternative range
// renderer or hand-maintained palette is used as the export's authority.
const layers = new Map<string, readonly BoardPiece[]>();
const board = Object.create(BoardView.prototype) as any;
Object.assign(board, {
  state: { map: { width: 1, height: 1, tiles: ['plain'] } },

  origin: squareLayout.origin, contains: () => true,
  surface: { setLayer: (name: string, pieces: readonly BoardPiece[]) => layers.set(name, pieces) },
});
Object.defineProperty(board, "layout", { value: squareLayout });
Object.defineProperty(board, "decor", { value: GroundBoardDecorations });
const spots: Record<string, string> = {};
for (const role of ['threat', 'controlled', 'move', 'heal', 'attack', 'visible'] as const) {
  const overlay = emptyOverlay();
  if (role === 'visible') overlay.visible = new Set();
  else overlay[role].add(0);
  board.renderRanges(overlay);
  const pieces = layers.get('range')!;
  if (pieces.length !== 1) throw new Error(`Expected one source ${role} spot`);
  spots[role === 'visible' ? 'fog' : role] = pieces[0].markup;
}
const incoming = emptyOverlay(); incoming.incoming.set(0, 7); board.renderRanges(incoming);
spots.incoming = layers.get('range')![0].markup.replace(/<text[\s\S]*?<\/text>/, '').trim();
const rings = Object.fromEntries(['selection', 'cursor'].map(kind => [kind, GroundBoardDecorations.ring(squareLayout, kind as 'selection' | 'cursor')]));
const actionRule = CANDIDATE_01_BOARD_STYLE.match(/\.candidate-map \.candidate-action-spot[^{}]*\{([^}]+)\}/)?.[1];
const routeRule = CANDIDATE_01_BOARD_STYLE.match(/\.candidate-ground-route,[^{}]*\{([^}]+)\}/)?.[1];
const markerOpacity = Number(CANDIDATE_01_BOARD_STYLE.match(/\.candidate-battle-marker\s*\{\s*opacity:\s*([\d.]+)/)?.[1]);
if (!actionRule || !routeRule || !Number.isFinite(markerOpacity)) throw new Error('Source style declarations changed');
function drop(rule: string) {
  const match = rule.match(/drop-shadow\(([-\d.]+) ([-\d.]+)px ([-\d.]+)(?:px)? rgb\((\d+) (\d+) (\d+) \/ (\d+)%\)\)/);
  if (!match) throw new Error(`Unrecognized source drop shadow: ${rule}`);
  return { x: Number(match[1]), y: Number(match[2]), sigma: Number(match[3]), color: `#${match.slice(4, 7).map(n => Number(n).toString(16).padStart(2, '0')).join('')}`, opacity: Number(match[7]) / 100 };
}
const paths = [
  [{x:0,y:0},{x:1,y:0}], [{x:0,y:0},{x:1,y:0},{x:1,y:1}],
  [{x:2,y:3},{x:2,y:2},{x:1,y:2},{x:1,y:1}],
  [{x:-1,y:2},{x:0,y:1},{x:1,y:2},{x:2,y:1}],
  [{x:8,y:8},{x:7,y:8},{x:7,y:7},{x:6,y:7},{x:6,y:6}],
].map(points => {
  board.renderPath(points);
  return { points, d: GroundBoardDecorations.movePath(squareLayout, points), pieces: layers.get('path') };
});
const output = { schema: 1, tileSize: 32, source: ['packages/game-ui/src/art/board-decorations.ts', 'packages/game-ui/src/ui/board.ts', 'packages/story-candidate-01/src/presentation/candidate-01-board-style.ts'], spots, rings, actionDrop: drop(actionRule), routeDrop: drop(routeRule), markerOpacity, pathTemplate: paths[0].pieces![0].markup.replace(/d="[^"]+"/g, 'd="{path}"'), arrowTemplate: paths[0].pieces![1].markup.replace(/rotate\([^)]*\)/, 'rotate({angle})'), paths };
writeFileSync(process.argv[2] ?? 'godot/data/board-decorations.json', JSON.stringify(output, null, 2) + '\n');
console.log(`Exported ${Object.keys(spots).length} source spots, 2 rings, ${paths.length} paths and source filters.`);

/** Full shipped river board evidence; offline SVG, never a browser/gameplay claim. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay, mountBoard } from '../board-harness';
import { idx } from '@empire/battle-engine';
import { candidate01RoadGraph } from '../../packages/story-candidate-01/src/presentation/candidate-01-road-corridor';
import { candidate01IsAreaRoad } from '../../packages/story-candidate-01/src/presentation/candidate-01-road-material';
import { candidateMaterial } from '../../packages/story-candidate-01/src/presentation/candidate-01-terrain-materials';

const output = resolve(process.env.RIVER_ROAD_OUTPUT ?? '/tmp/empire-river-road');
const root = resolve(import.meta.dirname, '../..');
mkdirSync(output, { recursive: true });
const hash = (value: string) => createHash('sha256').update(value).digest('hex');
const state = engine.createState(shippedLevel('c01-02'));
const before = JSON.stringify(state);
const board = mountBoard(shippedLevel('c01-02'), CANDIDATE_01_ART, state);
board.render(emptyOverlay());
const initial = board.el.outerHTML;
const actors = () => [...board.el.querySelectorAll('.layer-units > .unit')].map(unit => unit.outerHTML).join('');
const actorHash = hash(actors());
board.render(emptyOverlay());
if (initial !== board.el.outerHTML) throw new Error('repeat redraw changed river board');
const overlay = emptyOverlay();
overlay.selected = { x: 10, y: 4 };
overlay.cursor = { x: 11, y: 4 };
overlay.path = [{ x: 10, y: 4 }, { x: 11, y: 4 }];
board.render(overlay);
if (actorHash !== hash(actors())) throw new Error('path overlay changed actors');
board.render(emptyOverlay());
if (initial !== board.el.outerHTML || before !== JSON.stringify(state)) throw new Error('overlay roundtrip mutated board/state');
const sources: Record<string, string> = {};
const full = board.el.cloneNode(true) as SVGSVGElement;
const stylesPath = 'packages/game-ui/src/styles/app.css';
const styles = readFileSync(resolve(root, stylesPath), 'utf8');
sources[stylesPath] = hash(styles);
const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
style.textContent = styles;
full.prepend(style);
const actorCount = full.querySelectorAll('.layer-units > .unit').length;
const buildingCount = full.querySelectorAll('[data-candidate-art="structure"]').length;
if (actorCount !== 20 || buildingCount !== 9) throw new Error('incomplete river-board capture');
const [, , width, height] = full.getAttribute('viewBox')!.split(' ').map(Number);
full.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
full.setAttribute('width', String(width));
full.setAttribute('height', String(height));
full.removeAttribute('style');
writeFileSync(resolve(output, 'full-board.svg'), full.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^\"]*)?"/g, (_, path: string) => {
  const file = resolve(root, `.${path}`);
  sources[path.slice(1)] = createHash('sha256').update(readFileSync(file)).digest('hex');
  return `href="${relative(output, file)}"`;
}));
const map = state.map;
const graph = candidate01RoadGraph(map.width, map.height, (x, y) => candidate01IsAreaRoad(candidateMaterial(content, map.tiles[idx(map, x, y)])));
writeFileSync(resolve(output, 'review.json'), JSON.stringify({
  kind: 'Full production BoardView SVG with all actors, scenery and targets; offline only',
  levelId: state.levelId, mapSize: [map.width, map.height], stateSha256: hash(before),
  actors: actorCount, buildings: buildingCount, actorsMarkupSha256: actorHash, structures: state.structures.length,
  roadCells: graph.cells, cardinalEdges: graph.chains.reduce((n, chain) => n + chain.length - 1, 0),
  redrawExact: true, overlayRoundtripExact: true, stateUnchanged: true, sources,
}, null, 2) + '\n');
board.dispose();
console.log(output);

/** Complete c01-12 diagnostic. Only four existing lying pictures are substituted. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { ArtDirection, BoardView, SvgBoardSurface } from '@empire/game-ui';
import { idx } from '@empire/battle-engine';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.VERTICAL_CROSSING_REVIEW_OUTPUT ?? '/tmp/empire-vertical-crossing-pilot');
const base = 'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/crossings-fortifications';
const hash = (data: string | Uint8Array) => createHash('sha256').update(data).digest('hex');
const sources = {
  wood: { path: `${base}/wood-bridge-vertical/long-variant/master.png`, foot: [627, 628], span: 1081, length: 5 },
  stone: { path: `${base}/stone-bridge-vertical/stone-bridge-vertical-master.png`, foot: [627, 594], span: 784, length: 3 },
};
const placements = [
  { x: 6, y: 2, kind: 'wood' as const }, { x: 17, y: 2, kind: 'wood' as const },
  { x: 6, y: 9, kind: 'stone' as const }, { x: 17, y: 9, kind: 'stone' as const },
].map(p => ({ ...p, world: [(p.x + .5) * 32, (p.y + sources[p.kind].length / 2) * 32] }));
const level = shippedLevel('c01-12');
const state = engine.createState(level);
const stateHash = hash(JSON.stringify(state));
for (const p of placements) {
  for (let i = 0; i < sources[p.kind].length; i++) {
    if (state.map.tiles[idx(state.map, p.x, p.y + i)] !== 'bridge') throw new Error(`pilot run ${p.x},${p.y+i} is ${state.map.tiles[idx(state.map,p.x,p.y+i)]}, not bridge`);
  }
}
const original = CANDIDATE_01_ART.presentation;
const replaced = new Set<string>();
const candidate = new ArtDirection(CANDIDATE_01_ART.providers, { ...original, sceneLayers(context) {
  const layers = original.sceneLayers(context);
  replaced.clear();
  return { ...layers, underUnits: layers.underUnits.map(piece => {
    const placement = placements.find(p => Math.abs(piece.x - p.world[0]) < 1e-8
      && Math.abs(piece.y - p.world[1]) < 1e-8 && piece.markup.includes('crossings-fortifications@2x.png'));
    if (!placement) return piece;
    const source = sources[placement.kind];
    const scale = source.length * 32 / source.span;
    const key = `${placement.x},${placement.y}`;
    if (replaced.has(key)) throw new Error('multiple pieces match one bridge midpoint');
    replaced.add(key);
    return { ...piece, markup: `<g class="candidate-environment-prop is-standing" transform="translate(${-source.foot[0] * scale} ${-source.foot[1] * scale})"><image href="/${source.path}" width="${1254 * scale}" height="${1254 * scale}"/></g>` };
  }) };
} });
let baselineActors = '';
let baselineBuildings = '';
for (const [name, art] of [['before', CANDIDATE_01_ART], ['candidate', candidate]] as const) {
  const dir = resolve(output, name); mkdirSync(dir, { recursive: true });
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art,
    renderer: scene => new SvgBoardSurface(scene) }, state,
    { onTileClick() {}, onTileEnter() {}, onLeave() {}, onSecondary() {}, onScale() {} });
  board.render(emptyOverlay());
  const svg = board.el as SVGSVGElement;
  const actors = [...svg.querySelectorAll('.layer-units > .unit')].map(x => x.outerHTML).join('');
  const buildings = [...svg.querySelectorAll('[data-candidate-art="structure"]')].map(x => x.outerHTML).join('');
  if (name === 'before') { baselineActors = actors; baselineBuildings = buildings; }
  else if (actors !== baselineActors || buildings !== baselineBuildings || replaced.size !== 4) throw new Error('pilot changed actors/buildings or failed exact four replacements');
  const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
  style.textContent = readFileSync(resolve(root, 'packages/game-ui/src/styles/app.css'), 'utf8'); svg.prepend(style);
  const [, , width, height] = svg.getAttribute('viewBox')!.split(' ').map(Number);
  svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg'); svg.setAttribute('width', String(width)); svg.setAttribute('height', String(height)); svg.removeAttribute('style');
  const hashes: Record<string, string> = {};
  const markup = svg.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
    const file = resolve(root, `.${path}`); hashes[path.slice(1)] = hash(readFileSync(file));
    return `href="${relative(dir, file)}"`;
  });
  writeFileSync(resolve(dir, 'runtime-standing-full.svg'), markup);
  writeFileSync(resolve(dir, 'runtime-scene-provenance.json'), JSON.stringify({
    kind: 'offline complete actual BoardView with explicitly proposed four bridge-picture substitutions',
    levelId: level.id, variant: name, gameplayChanged: false, browserPlayback: false, omissions: [],
    sources: hashes, placements, registration: sources, stateHash,
    actorCount: svg.querySelectorAll('.layer-units > .unit').length,
    structureDrawingCount: svg.querySelectorAll('[data-candidate-art="structure"]').length,
    actorsAndBuildingsByteIdentical: name === 'candidate',
    caveat: 'Along-channel walkway runs intersect horizontal crossings; no north/south bank landing is claimed. Candidate is not production-bound.',
  }, null, 2) + '\n');
  if (hash(JSON.stringify(state)) !== stateHash) throw new Error('pilot mutated battle state');
  board.dispose();
}
console.log(output);

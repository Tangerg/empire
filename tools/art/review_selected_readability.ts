/** Complete actual BoardView snapshots for the bounded selected-actor cue. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { BoardView, CELL_FOOT, SELECTION_CUE_OPACITY, SvgBoardSurface, TILE } from '@empire/game-ui';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.SELECTION_REVIEW_OUTPUT ?? 'docs/art-rebuild-20261019/approved-v5/selected-readability-review');
mkdirSync(output, { recursive: true });
const level = shippedLevel('c01-02');
const appStylesPath = 'packages/game-ui/src/styles/app.css';
const appStyles = readFileSync(resolve(root, appStylesPath), 'utf8');
const sources = new Map([[appStylesPath, createHash('sha256').update(appStyles).digest('hex')]]);
const renders = [];
for (const scenario of [
  { name: 'tree', key: 'bridge-cleric', hidden: false },
  { name: 'granary', key: 'bridge-guard-1', hidden: false },
  { name: 'unobstructed', key: 'bridge-knight', hidden: false },
  { name: 'hidden-selected', key: 'bridge-cleric', hidden: true },
]) {
  for (const cue of [false, true]) {
    const state = engine.createState(level);
    const before = JSON.stringify(state);
    const actor = state.units.find((unit) => unit.key === scenario.key)!;
    let origin = [0, 0];
    const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
      renderer: (scene) => { origin = [scene.originX, scene.originY]; return new SvgBoardSurface(scene); } }, state,
    { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} });
    const overlay = emptyOverlay();
    overlay.selected = { x: actor.x, y: actor.y };
    if (scenario.hidden) overlay.hiddenUnits.add(actor.id);
    board.render(overlay);
    const svg = board.el as SVGSVGElement;
    const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
    style.textContent = appStyles;
    svg.prepend(style);
    const [, , width, height] = svg.getAttribute('viewBox')!.split(' ').map(Number);
    svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    svg.setAttribute('width', String(width)); svg.setAttribute('height', String(height)); svg.removeAttribute('style');
    const originalCueCount = svg.querySelectorAll('.selection-cue').length;
    if (originalCueCount !== (scenario.hidden ? 0 : 1)) throw new Error('selected/hidden cue gate differs from expected state');
    // Diagnostic comparison removes only the new cue, retaining normal selected
    // glow/ring, every scene object and all actor placements and facings.
    if (!cue) svg.querySelectorAll('.selection-cue').forEach((element) => element.remove());
    const filename = `${scenario.name}-${cue ? 'cue' : 'baseline'}`;
    const markup = svg.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
      const file = resolve(root, `.${path}`);
      sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
      return `href="${relative(output, file)}"`;
    });
    writeFileSync(resolve(output, `${filename}.svg`), markup);
    const units = [...svg.querySelectorAll<SVGElement>('.layer-units > .unit')];
    if (units.length !== 20 || svg.querySelectorAll('[data-candidate-art="structure"]').length !== 9) throw new Error('incomplete scene');
    if (JSON.stringify(state) !== before) throw new Error('selected cue changed battle state');
    renders.push({ filename, scenario: scenario.name, cue, actorId: actor.id, key: actor.key, hidden: scenario.hidden,
      facing: actor.facing, cell: [actor.x, actor.y], worldFoot: [actor.x * TILE + CELL_FOOT.x, actor.y * TILE + CELL_FOOT.y],
      screenFoot: [actor.x * TILE + CELL_FOOT.x + origin[0], actor.y * TILE + CELL_FOOT.y + origin[1]],
      viewport: [0, 0, width, height], actors: units.length, visibleActors: units.filter((unit) => unit.style.display !== 'none').length,
      buildings: 9, cueCount: svg.querySelectorAll('.selection-cue').length,
      stateSha256: createHash('sha256').update(before).digest('hex') });
    board.dispose();
  }
}
writeFileSync(resolve(output, 'selection-provenance.json'), JSON.stringify({
  kind: 'offline actual BoardView/SvgBoardSurface selected-actor prototype; not live browser or GPU playback',
  levelId: level.id, opacity: SELECTION_CUE_OPACITY, renders, sources: Object.fromEntries(sources),
  scope: 'Only the already-visible selected actor gets this cue. No general occlusion detection, source depth change or second timeline.',
  omissions: [], gameplayChanged: false,
  limitations: ['Initial poses only', 'New/legacy mixed art at integration 5d72a1a',
    'The hidden-selected negative case deliberately hides one actor through the existing overlay contract; its selection ring remains existing UI behavior'],
}, null, 2) + '\n');
console.log(output);

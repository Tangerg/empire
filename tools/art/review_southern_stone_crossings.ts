/** Isolate the actual registered pieces, masks and four ports for offline raster QA. */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { content, engine, shippedLevel, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { BoardView, SvgBoardSurface, GROUND_TAGS, taggedGround } from '@empire/game-ui';
import { idx, inBounds } from '@empire/battle-engine';
import { CANDIDATE_01_HORIZONTAL_CROSSING as horizontal, CANDIDATE_01_VERTICAL_CROSSING as vertical,
  CANDIDATE_01_STONE_JUNCTION as windows, candidate01StoneJunctionMarkup } from '../../packages/story-candidate-01/src/presentation/candidate-01-crossing';
import { candidate01CrossingTopology, candidate01CrossingJunction } from '../../packages/story-candidate-01/src/presentation/candidate-01-crossing-topology';
import { candidateReviewViewport } from './candidate_review_viewport';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.SOUTHERN_CROSSING_REVIEW_OUTPUT ?? '/tmp/empire-southern-stone-crossings');
mkdirSync(output, { recursive: true });
const hash = (value: string) => createHash('sha256').update(value).digest('hex');
const state = engine.createState(shippedLevel('c01-12'));
const stateBefore = JSON.stringify(state);
const map = state.map;
const components = candidate01CrossingTopology(map.width, map.height,
  (x, y) => map.tiles[idx(map, x, y)] === 'bridge',
  (x, y) => inBounds(map, x, y) && taggedGround(content.terrains.get(map.tiles[idx(map, x, y)]), GROUND_TAGS.way, GROUND_TAGS.settlement))
  .filter(component => candidate01CrossingJunction(component));
if (components.length !== 2) throw new Error('expected exactly the two southern stone pluses');
const markup = candidate01StoneJunctionMarkup(components[0]);
const box = [-vertical.anchor[0], -vertical.anchor[1], ...vertical.logicalSize];
const sourceHashes: Record<string, string> = {};
function saveSvg(name: string, body: string) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="154" height="154" viewBox="${box.join(' ')}">${body}</svg>`
    .replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
      const file = resolve(root, `.${path}`);
      sourceHashes[path.slice(1)] = createHash('sha256').update(readFileSync(file)).digest('hex');
      return `href="${relative(output, file)}"`;
    });
  writeFileSync(resolve(output, `${name}.svg`), svg);
}
saveSvg('junction', markup);
const parsed = document.createElementNS('http://www.w3.org/2000/svg', 'g');
parsed.innerHTML = markup;
for (const [index, axis] of (['horizontal', 'vertical'] as const).entries()) {
  const image = parsed.querySelectorAll('image')[index].cloneNode(true) as SVGImageElement;
  const definitions = parsed.querySelectorAll('defs')[index];
  saveSvg(`${axis}-clipped`, definitions.outerHTML + image.outerHTML);
  image.removeAttribute('clip-path');
  saveSvg(`${axis}-complete`, image.outerHTML);
}
const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
  renderer: scene => new SvgBoardSurface(scene) }, state,
{ onTileClick() {}, onTileEnter() {}, onLeave() {}, onSecondary() {}, onScale() {} });
board.render(emptyOverlay());
const initial = board.el.outerHTML;
const units = () => [...board.el.querySelectorAll('.layer-units > .unit')].map(unit => unit.outerHTML).join('');
const actorHash = hash(units());
const structures = () => [...board.el.querySelectorAll('[data-candidate-art="structure"]')].map(item => item.outerHTML).join('');
const structureHash = hash(structures());
const north = () => [...board.el.querySelectorAll('.layer-scenery > g')].filter(item =>
  item.innerHTML.includes('crossings-fortifications@2x.png')).map(item => item.outerHTML).join('');
const woodHash = hash(north());
board.render(emptyOverlay());
if (initial !== board.el.outerHTML) throw new Error('repeated redraw changed the SVG');
const overlay = emptyOverlay();
overlay.selected = { x: 6, y: 10 };
overlay.cursor = { x: 17, y: 10 };
overlay.path = [{ x: 5, y: 10 }, { x: 6, y: 10 }, { x: 6, y: 11 }];
board.render(overlay);
if (hash(units()) !== actorHash || hash(structures()) !== structureHash || hash(north()) !== woodHash) {
  throw new Error('selection/path overlay changed actors, targets or deferred wood');
}
board.render(emptyOverlay());
if (initial !== board.el.outerHTML || JSON.stringify(state) !== stateBefore) throw new Error('redraw changed the initial board or state');
const targetVariants = [];
for (const variant of ['half-hp', 'disabled'] as const) {
  const probe = structuredClone(state);
  for (const target of probe.structures) {
    if (variant === 'half-hp') target.hp = content.structures.get(target.type).maxHp / 2;
    else target.disabled = true;
  }
  board.setState(probe);
  board.render(emptyOverlay());
  const targets = [...board.el.querySelectorAll('[data-candidate-art="structure"]')].filter(item =>
    item.innerHTML.includes('struct-sunken-bell-sluice@2x.png'));
  if (targets.length !== 3 || hash(units()) !== actorHash || hash(north()) !== woodHash) {
    throw new Error('target state probe hid a watergate or changed an actor/deferred bridge');
  }
  for (const target of targets) {
    if (target.querySelector('image')?.getAttribute('x') !== '-128'
      || target.parentElement?.querySelector('rect[y="28.2"]')?.getAttribute('width') !== (variant === 'half-hp' ? '10.40' : '20.80')) {
      throw new Error('target state probe lost its damaged frame or exact HP gauge');
    }
  }
  targetVariants.push({ variant, targets: targets.length, targetMarkupSha256: hash(targets.map(item => item.parentElement!.outerHTML).join('')),
    actorsUnchanged: true, deferredWoodUnchanged: true, hpGaugeWidth: variant === 'half-hp' ? 10.4 : 20.8 });
}
board.setState(state);
board.render(emptyOverlay());
if (initial !== board.el.outerHTML || JSON.stringify(state) !== stateBefore) throw new Error('target state probes did not restore the original board');
const context = { content, levelId: state.levelId, map, viewport: candidateReviewViewport(map) };
const runtimeProjection = JSON.stringify(CANDIDATE_01_ART.presentation.sceneLayers(context));
const editorProjection = JSON.stringify(CANDIDATE_01_ART.presentation.sceneLayers({ ...context, editing: true }));
if (editorProjection !== JSON.stringify(CANDIDATE_01_ART.presentation.sceneLayers({ ...context, editing: true }))
  || runtimeProjection !== JSON.stringify(CANDIDATE_01_ART.presentation.sceneLayers(context))
  || JSON.stringify(state) !== stateBefore) throw new Error('editor preview changed a projection or battle state');
writeFileSync(resolve(output, 'junction-review.json'), JSON.stringify({
  kind: 'Actual production component markup and offline SVG redraw checks; no GPU or gameplay claim',
  viewBox: box, rasterSize1x: [154, 154], registeredNativeSizes: [[154, 154], [307, 307]],
  horizontal, vertical, windows, components: components.map(component => ({ ...component, junction: candidate01CrossingJunction(component) })),
  nativeSources: sourceHashes, stateSha256: hash(stateBefore), actorsSha256: actorHash,
  terrainAndTargetStructuresSha256: structureHash, deferredWoodMarkupSha256: woodHash,
  repeatedRedrawExact: true, selectionCursorAndPathRoundTripExact: true,
  editorPreviewProjectionRoundTripExact: true,
  isolatedTargetStateProbes: targetVariants,
  noStandingFoot: true, dryTerrainPolicy: 'New strong vertical pixels stay inside the existing water channel; source alpha-1 residue is retained.',
}, null, 2) + '\n');
board.dispose();
console.log(output);

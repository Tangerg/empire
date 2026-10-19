/** Render a contact sheet from real scene-prop markup, without changing a battle. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { mapFromLevel, TacticalGrids } from '@empire/battle-engine';
import { boardPictureMarkup, boardPiecesMarkup, createSceneViewport } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '@empire/story-candidate-01';
import { CANDIDATE_01_ENVIRONMENT } from '../../packages/story-candidate-01/src/presentation/candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup, candidate01SceneProfile } from '../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';

const root = resolve(import.meta.dirname, '../..');
const secondSet = process.argv.includes('--second');
const output = resolve(process.env.FOREST_REVIEW_OUTPUT
  ?? `docs/art-rebuild-20261019/approved-v5/environment-runtime${secondSet ? '-second' : ''}`);
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const map = mapFromLevel(content, candidate01Level('c01-02'));
map.tiles.fill('forest');
const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
const context = { content, map, viewport, levelId: 'forest-registration-review' };
const field = boardPiecesMarkup(candidate01MapSceneryLayers(context).underUnits);
const frame = candidate01SceneFrameMarkup(context).foreground;
const props = (markup: string) => markup.match(/<g class="candidate-environment-prop [\s\S]*?<\/svg>\s*<\/g>/g) ?? [];
const columns = secondSet ? [
  { id: 'oak-grove-dense', x: 85, placement: [0.74, 0.86] },
  { id: 'mixed-forest-edge', x: 235, placement: [0.74, 0.86] },
  { id: 'stump-hollow', x: 385, placement: [0.58, 0.7] },
  { id: 'fallen-hollow-log', x: 515, placement: [0.9, 0.9] },
] : [
  { id: 'oak-ancient', x: 85, placement: [0.74, 0.86] },
  { id: 'mixed-forest-dense', x: 235, placement: [0.74, 0.86] },
  { id: 'fern-bed', x: 385, placement: [0.58, 0.7] },
  { id: 'boulder-large', x: 515, placement: [1.05, 1.05] },
];
const reports = [];
const pictures = [];
for (const { id, x, placement } of columns) {
  const { cell } = CANDIDATE_01_ENVIRONMENT.cell(id);
  const crop = `x="${-(cell.index % 4) * 96}" y="${-Math.floor(cell.index / 4) * 96}"`;
  const matches = props(id === 'boulder-large' || id === 'fallen-hollow-log' ? frame : field)
    .filter((markup) => markup.includes('forest-temperate@2x.png') && markup.includes(crop));
  const samples = matches.map((markup) => {
    const transform = /transform="translate\(([-\d.]+) ([-\d.]+)\) scale\(([-\d.]+)/.exec(markup);
    if (!transform) throw new Error(`missing prop transform: ${id}`);
    return { markup, scale: Math.abs(Number(transform[3])) };
  });
  const presentationScale = cell.presentationScale ?? 1;
  const middle = (placement[0] + placement[1]) / 2 * presentationScale;
  samples.sort((a, b) => Math.abs(a.scale - middle) - Math.abs(b.scale - middle));
  const sample = samples[0];
  if (!sample) throw new Error(`no actual scene placement found: ${id}`);
  reports.push({ id, anchor: cell.anchor, footprint: cell.footprint, presentationScale,
    placementScaleRange: placement, rendererScaleRange: placement.map((value) => value * presentationScale),
    sampledRendererScaleRange: [Math.min(...samples.map((entry) => entry.scale)), Math.max(...samples.map((entry) => entry.scale))],
    sampleScale: sample.scale, sampleCount: samples.length, reviewMarkup: sample.markup });
  pictures.push(`<g transform="translate(${x} 145)">${sample.markup}</g>`
    + `<path d="M${x - 5} 145h10 M${x} 140v10" stroke="#ff8070" stroke-width="0.7"/>`
    + `<text x="${x}" y="176" text-anchor="middle">${id}</text>`
    + `<text x="${x}" y="190" text-anchor="middle">scale ${sample.scale}</text>`);
}
const human = candidate01UnitPicture('c01.swordsman', '#ffffff');
if (!human) throw new Error('missing approved human reference');
pictures.push(`<g transform="translate(644 114)">${boardPictureMarkup(human)}</g>`
  + '<text x="660" y="176" text-anchor="middle">swordsman</text><text x="660" y="190" text-anchor="middle">44 px visible</text>');
const local = (markup: string) => markup.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g,
  (_, path: string) => `href="${pathToFileURL(resolve(root, `.${path}`)).href}"`);
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="740" height="210" viewBox="0 0 740 210">`
  + '<rect width="740" height="210" fill="#667064"/>'
  + '<g fill="#f5f1df" font-family="sans-serif" font-size="10">'
  + '<text x="15" y="18">Native 1× actual renderer markup; red crosses are authored ground contacts; no browser playback</text>'
  + '<path d="M15 145h710" stroke="#a6b2a0" stroke-width="0.5"/>'
  + pictures.join('') + '</g></svg>';
mkdirSync(output, { recursive: true });
writeFileSync(resolve(output, 'native-contact.svg'), local(svg));
writeFileSync(resolve(output, 'runtime-scale-report.json'), JSON.stringify({
  evidence: 'actual scene renderer markup rearranged onto a native-size diagnostic contact board',
  gameplayChanged: false, browserPlayback: false, assets: reports,
}, null, 2) + '\n');
console.log(output);

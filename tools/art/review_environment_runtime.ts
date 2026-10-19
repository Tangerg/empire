/** Render a contact sheet from real scene-prop markup, without changing a battle. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { mapFromLevel } from '@empire/battle-engine';
import { boardPictureMarkup, boardPiecesMarkup, runtimeAtlasCellMarkup } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '@empire/story-candidate-01';
import { CANDIDATE_01_ENVIRONMENT } from '../../packages/story-candidate-01/src/presentation/candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from '../../packages/story-candidate-01/src/presentation/candidate-01-map-scene';
import { candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';

import { candidateReviewViewport } from './candidate_review_viewport';

const root = resolve(import.meta.dirname, '../..');
const secondSet = process.argv.includes('--second');
const thirdSet = process.argv.includes('--third');
const fourthSet = process.argv.includes('--fourth');
const pineSet = process.argv.includes('--pine-first');
const brushSet = process.argv.includes('--pine-underbrush');
const matureSet = process.argv.includes('--pine-mature');
const lowSet = process.argv.includes('--pine-low');
const graveSet = process.argv.includes('--regional-graves');
const forgeSet = process.argv.includes('--regional-forge');
const snowSet = process.argv.includes('--regional-snow');
const extended = thirdSet || fourthSet || pineSet || brushSet || matureSet || lowSet || graveSet || forgeSet || snowSet;
if ([secondSet, thirdSet, fourthSet, pineSet, brushSet, matureSet, lowSet, graveSet, forgeSet, snowSet].filter(Boolean).length > 1) throw new Error('choose only one forest review set');
const output = resolve(process.env.FOREST_REVIEW_OUTPUT
  ?? `docs/art-rebuild-20261019/approved-v5/environment-runtime${forgeSet ? '-regional-forge' : snowSet ? '-regional-snow' : graveSet ? '-regional-graves' : lowSet ? '-pine-low' : matureSet ? '-pine-mature' : brushSet ? '-pine-underbrush' : pineSet ? '-pine-first' : fourthSet ? '-fourth' : thirdSet ? '-third' : secondSet ? '-second' : ''}`);
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const map = mapFromLevel(content, candidate01Level('c01-02'));
map.tiles.fill('forest');
const viewport = candidateReviewViewport(map);
const context = { content, map, viewport, levelId: 'forest-registration-review' };
const field = boardPiecesMarkup(candidate01MapSceneryLayers(context).underUnits);
const frame = candidate01SceneFrameMarkup(context).foreground;
const motherRootMap = mapFromLevel(content, candidate01Level('c01-02'));
motherRootMap.tiles.fill('c01.mother-root');
const motherRoot = boardPiecesMarkup(candidate01MapSceneryLayers({ ...context, map: motherRootMap }).underUnits);
const graveMap = mapFromLevel(content, candidate01Level('c01-02'));
graveMap.tiles.fill('c01.graveyard');
const graves = boardPiecesMarkup(candidate01MapSceneryLayers({ ...context, map: graveMap }).underUnits);
const forgeMap = mapFromLevel(content, candidate01Level('c01-02'));
forgeMap.tiles.fill('c01.forge');
const forges = boardPiecesMarkup(candidate01MapSceneryLayers({ ...context, map: forgeMap }).underUnits);
const props = (markup: string) => markup.match(/<g class="candidate-environment-prop [\s\S]*?<\/svg>\s*<\/g>/g) ?? [];
interface ReviewColumn {
  readonly id: string;
  readonly x: number;
  readonly placement: readonly [number, number];
  readonly catalogOnly?: boolean;
  readonly reference?: boolean;
  readonly reconstruction?: boolean;
}
const columns: readonly ReviewColumn[] = forgeSet ? [
  { id: 'forge-coal-pile', x: 85, placement: [0.4, 0.52] },
  { id: 'forge-coal-sacks', x: 235, placement: [0.4, 0.52] },
  { id: 'forge-ember-pile', x: 385, placement: [0.4, 0.52] },
  { id: 'forge-charcoal-kiln', x: 515, placement: [0.4, 0.52] },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : snowSet ? [
  { id: 'snow-pine-cluster', x: 85, placement: [1, 1], catalogOnly: true },
  { id: 'snow-bare-shrub', x: 235, placement: [1, 1], catalogOnly: true },
  { id: 'ice-boulder-cluster', x: 385, placement: [1, 1], catalogOnly: true },
  { id: 'snowdrift-rock', x: 515, placement: [1, 1], catalogOnly: true },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : graveSet ? [
  { id: 'grave-cross-cluster', x: 85, placement: [0.42, 0.42] },
  { id: 'grave-signboards', x: 235, placement: [0.42, 0.42] },
  { id: 'grave-dead-tree', x: 385, placement: [0.42, 0.42] },
  { id: 'grave-marker-lantern', x: 515, placement: [0.42, 0.42] },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : lowSet ? [
  { id: 'pine-low-shrub', x: 85, placement: [1, 1], catalogOnly: true },
  { id: 'pine-fallen-log', x: 235, placement: [1, 1], catalogOnly: true },
  { id: 'pine-stump', x: 385, placement: [1, 1], catalogOnly: true },
  { id: 'pine-mossy-rock', x: 515, placement: [1, 1], catalogOnly: true },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : matureSet ? [
  { id: 'pine-mature-a', x: 85, placement: [0.74, 0.86] },
  { id: 'pine-mature-b', x: 235, placement: [0.74, 0.86] },
  { id: 'pine-dead', x: 385, placement: [1, 1], catalogOnly: true },
  { id: 'pine-mature-c', x: 515, placement: [0.86, 1.06] },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : brushSet ? [
  { id: 'pine-underbrush-a', x: 85, placement: [0.58, 0.7] },
  { id: 'pine-underbrush-b', x: 235, placement: [1, 1], catalogOnly: true },
  { id: 'pine-underbrush-c', x: 385, placement: [0.58, 0.7] },
  { id: 'pine-underbrush-d', x: 515, placement: [1, 1], catalogOnly: true },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : pineSet ? [
  { id: 'pine-tall', x: 85, placement: [0.86, 1.06] },
  { id: 'pine-pair', x: 235, placement: [0.86, 1.06] },
  { id: 'pine-cluster', x: 385, placement: [0.74, 0.86] },
  { id: 'pine-sapling', x: 515, placement: [1, 1], catalogOnly: true },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : fourthSet ? [
  { id: 'mixed-forest-autumn', x: 85, placement: [0.74, 0.86] },
  { id: 'sapling-rock-cluster', x: 235, placement: [0.58, 0.7] },
  { id: 'stump-low', x: 385, placement: [0.58, 0.7] },
  { id: 'forest-floor-cluster', x: 515, placement: [0.58, 0.7], reconstruction: true },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : thirdSet ? [
  { id: 'bramble-dark', x: 85, placement: [0.58, 0.7] },
  { id: 'bramble-berries', x: 235, placement: [0.58, 0.7] },
  { id: 'leaf-pile', x: 385, placement: [1, 1], catalogOnly: true },
  { id: 'boulder-ferns', x: 515, placement: [0.8, 0.92] },
  { id: 'oak-ancient', x: 675, placement: [0.74, 0.86], reference: true },
] : secondSet ? [
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
for (const { id, x, placement, catalogOnly = false, reference = false, reconstruction = false } of columns) {
  const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(id);
  const crop = `x="${-(cell.index % 4) * 96}" y="${-Math.floor(cell.index / 4) * 96}"`;
  const source = id.startsWith('forge-') ? forges : id.startsWith('grave-') ? graves : id === 'boulder-ferns' ? motherRoot
    : ['boulder-large', 'fallen-hollow-log', 'pine-tall', 'pine-pair', 'pine-mature-c'].includes(id) ? frame : field;
  const matches = props(source)
    .filter((markup) => markup.includes(`${atlas.id}@2x.png`) && markup.includes(crop));
  const samples = matches.map((markup) => {
    const transform = /transform="translate\(([-\d.]+) ([-\d.]+)\) scale\(([-\d.]+)/.exec(markup);
    if (!transform) throw new Error(`missing prop transform: ${id}`);
    return { markup, scale: Math.abs(Number(transform[3])) };
  });
  const presentationScale = cell.presentationScale ?? 1;
  const middle = (placement[0] + placement[1]) / 2 * presentationScale;
  // Leaf pile has no shipped placement. Draw the catalog cell about its declared
  // contact for inspection, without adding it to a production scene selector.
  if (catalogOnly) {
    if (!cell.anchor) throw new Error(`missing catalog anchor: ${id}`);
    samples.push({ scale: middle, markup: `<g class="catalog-environment-probe"`
      + ` transform="translate(${-cell.anchor[0] * middle} ${-cell.anchor[1] * middle}) scale(${middle})">`
      + `${runtimeAtlasCellMarkup(atlas.raster, cell.index)}</g>` });
  }
  samples.sort((a, b) => Math.abs(a.scale - middle) - Math.abs(b.scale - middle));
  const sample = samples[0];
  if (!sample) throw new Error(`no actual scene placement found: ${id}`);
  reports.push({ id, anchor: cell.anchor, footprint: cell.footprint, presentationScale,
    evidence: catalogOnly ? 'catalog-only diagnostic; no shipped scene placement' : 'sampled scene renderer markup',
    reference, reconstruction,
    placementScaleRange: placement, rendererScaleRange: placement.map((value) => value * presentationScale),
    sampledRendererScaleRange: catalogOnly ? null
      : [Math.min(...samples.map((entry) => entry.scale)), Math.max(...samples.map((entry) => entry.scale))],
    sampleScale: sample.scale, sampleCount: catalogOnly ? 0 : samples.length, reviewMarkup: sample.markup });
  pictures.push(`<g transform="translate(${x} 145)">${sample.markup}</g>`
    + `<path d="M${x - 5} 145h10 M${x} 140v10" stroke="#ff8070" stroke-width="0.7"/>`
    + `<text x="${x}" y="176" text-anchor="middle">${id}</text>`
    + `<text x="${x}" y="190" text-anchor="middle">${reconstruction ? 'reconstructed; ' : ''}${catalogOnly ? 'catalog scale' : 'scale'} ${sample.scale}</text>`);
}
const human = candidate01UnitPicture('c01.swordsman', '#ffffff');
if (!human) throw new Error('missing approved human reference');
const humanX = extended ? 820 : 660;
pictures.push(`<g transform="translate(${humanX - 16} 114)">${boardPictureMarkup(human)}</g>`
  + `<text x="${humanX}" y="176" text-anchor="middle">swordsman</text>`
  + `<text x="${humanX}" y="190" text-anchor="middle">44 px visible</text>`);
const local = (markup: string) => markup.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g,
  (_, path: string) => `href="${pathToFileURL(resolve(root, `.${path}`)).href}"`);
const width = extended ? 900 : 740;
const title = thirdSet
  ? 'Native 1× scene samples; leaf-pile is a catalog-only probe; red crosses mark contacts; no browser playback'
  : 'Native 1× actual renderer markup; red crosses are authored ground contacts; no browser playback';
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="210" viewBox="0 0 ${width} 210">`
  + `<rect width="${width}" height="210" fill="#667064"/>`
  + '<g fill="#f5f1df" font-family="sans-serif" font-size="10">'
  + `<text x="15" y="18">${title}</text>`
  + `<path d="M15 145h${width - 30}" stroke="#a6b2a0" stroke-width="0.5"/>`
  + pictures.join('') + '</g></svg>';
mkdirSync(output, { recursive: true });
writeFileSync(resolve(output, 'native-contact.svg'), local(svg));
writeFileSync(resolve(output, 'runtime-scale-report.json'), JSON.stringify({
  evidence: thirdSet ? 'scene renderer samples and explicitly marked catalog-only leaf-pile probe'
    : 'actual scene renderer markup rearranged onto a native-size diagnostic contact board',
  gameplayChanged: false, browserPlayback: false, assets: reports,
}, null, 2) + '\n');
console.log(output);

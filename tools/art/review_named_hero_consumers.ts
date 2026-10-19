/** Read-only actual portrait and icon projection for a registered named hero. */
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { createTestCatalog } from '@empire/test-content';
import { portraitMarkup } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK } from '../../packages/story-candidate-01/src';
import { CANDIDATE_01_ART } from '../../packages/story-candidate-01/src/presentation';
import { CANDIDATE_01_ACTOR_ART, resolveCandidate01PresentationBinding } from '../../packages/story-candidate-01/src/presentation/candidate-01-actor-art';
import { candidate01UnitIcon, candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';
import { candidate01Asset } from '../../packages/story-candidate-01/src/presentation/candidate-01-assets';
const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.HERO_REVIEW_OUTPUT ?? '/tmp/empire-named-hero-consumers');
const type = process.env.HERO_UNIT_ID ?? 'c01.laiya';
const context = { actorKey: process.env.HERO_ACTOR_KEY };
const { unitArt: binding, characterTopicId: cardTopic } = resolveCandidate01PresentationBinding(CANDIDATE_01_ACTOR_ART, type, context);
if (!binding?.appearanceId || !cardTopic) throw new Error('review requires an explicit named appearance and character card');
const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const team = '#4d86b8';
const picture = candidate01UnitPicture(type, team, context)!;
const card = candidate01Asset(cardTopic);
const local = (svg: string) => svg.replace(/href="(\/packages\/[^"?]+)(?:\?[^\"]*)?"/g,
  (_all, path: string) => `href="${pathToFileURL(resolve(root, `.${path}`)).href}"`);
mkdirSync(output, { recursive: true });
const hud = `<svg xmlns="http://www.w3.org/2000/svg" width="192" height="224" viewBox="0 0 96 112">${portraitMarkup(CANDIDATE_01_ART, catalog.units.get(type), team, context)}</svg>`;
writeFileSync(resolve(output, 'actual-hud@2x.svg'), local(hud));
writeFileSync(resolve(output, 'actual-icon48.svg'), local(candidate01UnitIcon(type, team, 48, context)!));
const sources = [card.url, picture.strip!.href].map(href => {
  const path = resolve(root, `.${href.split('?')[0]}`);
  return { href, sha256: createHash('sha256').update(readFileSync(path)).digest('hex') };
});
writeFileSync(resolve(output, 'actual-consumers.json'), JSON.stringify({ type, context, binding, cardTopic,
  sources, picture, method: 'actual game-ui portraitMarkup and candidate01UnitIcon; offline SVG projection',
  liveBrowserVerified: false }, null, 2) + '\n');

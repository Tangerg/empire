/** Actual actor/type-only presentation equivalence at the empty actor-catalog checkpoint. */
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { content, engine, CANDIDATE_01_ART } from '../board-harness';
import { CANDIDATE_01_LEVELS } from '../../packages/story-candidate-01/src/levels';
import { CANDIDATE_01_ACTOR_ART, resolveCandidate01PresentationBinding } from '../../packages/story-candidate-01/src/presentation/candidate-01-actor-art';
import { portraitSvg, unitIcon, unitPicture } from '@empire/game-ui';
import { reviewFingerprint } from './review_fingerprint';

const root = resolve(import.meta.dirname, '../..');
const sources = new Map<string, string>();
const levels = CANDIDATE_01_LEVELS.map(level => {
  const state = engine.createState(level);
  const before = engine.saveBattle(state, '2026-10-19T00:00:00.000Z');
  const actors = state.units.map(actor => {
    const definition = content.units.get(actor.type);
    const team = state.players.find(player => player.id === actor.owner)?.color ?? '#ffffff';
    const context = { actorKey: actor.key };
    const binding = resolveCandidate01PresentationBinding(CANDIDATE_01_ACTOR_ART, actor.type, context);
    const picture = unitPicture(CANDIDATE_01_ART, definition, team, context);
    const icon = unitIcon(CANDIDATE_01_ART, definition, team, 32, context);
    const portrait = portraitSvg(CANDIDATE_01_ART, definition, team, 84, context);
    const generic = { picture: unitPicture(CANDIDATE_01_ART, definition, team),
      icon: unitIcon(CANDIDATE_01_ART, definition, team, 32), portrait: portraitSvg(CANDIDATE_01_ART, definition, team, 84) };
    for (const href of [picture.strip?.href, ...Array.from((icon + portrait).matchAll(/href="([^"?]+)(?:\?[^\"]*)?"/g), match => match[1])]) {
      if (!href) continue;
      const path = href.split('?')[0].replace(/^\//, '');
      if (!path.startsWith('packages/')) throw new Error(`unexpected runtime image path: ${href}`);
      sources.set(path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex'));
    }
    const sameAsTypeOnly = { picture: reviewFingerprint(picture) === reviewFingerprint(generic.picture),
      icon: icon === generic.icon, portrait: portrait === generic.portrait };
    if (CANDIDATE_01_ACTOR_ART.size === 0 && Object.values(sameAsTypeOnly).some(same => !same)) {
      throw new Error(`empty actor catalog changed ${level.id}/${actor.key}`);
    }
    return { key: actor.key, type: actor.type, owner: actor.owner, binding, sameAsTypeOnly,
      pictureSha256: reviewFingerprint(picture), iconSha256: reviewFingerprint(icon), portraitSha256: reviewFingerprint(portrait) };
  });
  const after = engine.saveBattle(state, '2026-10-19T00:00:00.000Z');
  if (reviewFingerprint(before) !== reviewFingerprint(after)) throw new Error(`presentation changed save: ${level.id}`);
  return { level: level.id, levelSha256: reviewFingerprint(level), saveSha256: reviewFingerprint(after), saveUnchanged: true, actors };
});
writeFileSync(process.env.ACTOR_CONTRACT_OUTPUT ?? '/tmp/empire-actor-art-contract.json', JSON.stringify({
  actorBindings: CANDIDATE_01_ACTOR_ART.size, sources: Object.fromEntries([...sources].sort()), levels,
}, null, 2) + '\n');

/** Snapshot every bound unit picture and icon, including authored team selection. */
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { CANDIDATE_01_UNIT_ART } from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
import { candidate01UnitIcon, candidate01UnitPicture } from '../../packages/story-candidate-01/src/presentation/candidate-01-runtime';
import { CANDIDATE_01_LEVELS } from '../../packages/story-candidate-01/src/levels';
import manifest from '../../packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import hdManifest from '../../packages/story-candidate-01/assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import teamManifest from '../../packages/story-candidate-01/assets/final-fantasy-v1/manifest-authored-team-variants.json';
import { reviewFingerprint as hash } from './review_fingerprint';
const root = resolve(import.meta.dirname, '../..');
const teams = [...new Set([...CANDIDATE_01_LEVELS.flatMap(level => level.players.map(player => player.color)),
  ...teamManifest.variants.map(variant => variant.teamColor)])].sort();
const pictures = Object.entries(CANDIDATE_01_UNIT_ART).map(([type, binding]) => {
  if (!binding) throw new Error(`undefined art binding: ${type}`);
  return { type, topicId: binding.topicId,
    ...(binding.appearanceId ? { appearanceId: binding.appearanceId } : {}),
    teams: teams.map(team => {
      const picture = candidate01UnitPicture(type, team);
      const href = picture?.strip?.href;
      if (!href) throw new Error(`bound unit has no sheet: ${type}`);
      const bytes = readFileSync(resolve(root, `.${href.split('?')[0]}`));
      return { team, pictureSha256: hash(picture), iconSha256: hash(candidate01UnitIcon(type, team, 48)),
        sourceSha256: createHash('sha256').update(bytes).digest('hex'), picture };
    }) };
});
writeFileSync(process.env.HERO_CONTRACT_OUTPUT ?? '/tmp/empire-hero-appearance-contract.json', JSON.stringify({
  canonicalCount: manifest.assetCount, canonicalSha256: hash(manifest), hdManifestSha256: hash(hdManifest),
  genericTeamManifestSha256: hash(teamManifest), teams, bindings: pictures,
}, null, 2) + '\n');

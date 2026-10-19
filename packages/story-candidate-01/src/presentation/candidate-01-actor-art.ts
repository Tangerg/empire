import type { UnitTypeId } from '@empire/battle-engine';
import type { UnitArtContext } from '@empire/game-ui';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_CHARACTER_ART, CANDIDATE_01_UNIT_ART, type Candidate01UnitArtBinding } from './candidate-01-bindings';
import { CANDIDATE_01_HERO_APPEARANCES, resolveCandidate01UnitArt, type Candidate01HeroAppearanceCatalog } from './candidate-01-hero-appearances';

export interface Candidate01ActorArtBinding {
  readonly expectedUnitType: UnitTypeId;
  readonly unitArt: Candidate01UnitArtBinding;
  readonly characterTopicId?: string;
}

export type Candidate01ActorArtCatalog = ReadonlyMap<string, Candidate01ActorArtBinding>;

/** Validate explicit actor declarations at startup, even for actors absent from the current mission. */
export function readCandidate01ActorArt(
  appearances: Candidate01HeroAppearanceCatalog,
  declarations: Readonly<Record<string, Candidate01ActorArtBinding>>,
): Candidate01ActorArtCatalog {
  const actors = new Map<string, Candidate01ActorArtBinding>();
  for (const [key, binding] of Object.entries(declarations)) {
    if (!key.trim() || key !== key.trim()) throw new Error('candidate-01 actor art: empty or padded actor key');
    const generic = CANDIDATE_01_UNIT_ART[binding.expectedUnitType];
    if (!generic || generic.topicId !== binding.unitArt.topicId) {
      throw new Error(`candidate-01 actor art ${key}: expected unit type/topic mismatch`);
    }
    if (!binding.unitArt.appearanceId) throw new Error(`candidate-01 actor art ${key}: explicit named appearance required`);
    resolveCandidate01UnitArt(appearances, binding.unitArt, '');
    if (binding.characterTopicId !== undefined && candidate01Asset(binding.characterTopicId).kind !== 'character-card') {
      throw new Error(`candidate-01 actor art ${key}: portrait must be a character-card topic`);
    }
    actors.set(key, binding);
  }
  return actors;
}

// Reviewed stage01 belongs only to this campaign actor. Stage02 has no progression owner.
export const CANDIDATE_01_ACTOR_ART = readCandidate01ActorArt(CANDIDATE_01_HERO_APPEARANCES, {
  'campaign-torren': { expectedUnitType: 'c01.swordsman',
    unitArt: { topicId: 'C01-UNIT-SWORDSMAN', appearanceId: 'torren-stage01' },
    characterTopicId: 'C01-CHAR-TORREN-01' },
});

/** Board, icon, portrait and diagnostics share this one explicit identity choice. */
export function resolveCandidate01PresentationBinding(
  actors: Candidate01ActorArtCatalog,
  type: UnitTypeId,
  context?: UnitArtContext,
): { readonly unitArt: Candidate01UnitArtBinding | undefined; readonly characterTopicId: string | undefined } {
  const key = context?.actorKey;
  const actor = key === undefined ? undefined : actors.get(key);
  if (actor) {
    if (actor.expectedUnitType !== type) {
      throw new Error(`candidate-01 actor art ${key}: expected ${actor.expectedUnitType}, received ${type}`);
    }
    // An actor without a declared card uses its own frame, never a generic face.
    return { unitArt: actor.unitArt, characterTopicId: actor.characterTopicId };
  }
  return { unitArt: CANDIDATE_01_UNIT_ART[type], characterTopicId: CANDIDATE_01_CHARACTER_ART[type] };
}

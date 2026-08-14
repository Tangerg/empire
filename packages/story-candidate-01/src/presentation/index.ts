import type { UnitTypeId } from '@empire/battle-engine';
import {
  ArtDirection,
  GroundBoardDecorations,
  type ArtProvider,
  type BattlePresentation,
  type StoryCampaignAdapter,
} from '@empire/game-ui';
import {
  applyCandidate01BattleContext,
  applyCandidate01BattleResultPolicy,
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
} from '../campaign';
import { CANDIDATE_01_LEVELS, candidate01Level } from '../levels';
import { CANDIDATE_01_SPEAKER_NAMES, candidate01Choice, candidate01Story } from '../story';
import {
  candidate01MapSceneryMarkup,
  candidate01SceneFrameMarkup,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import {
  candidate01CoverPropMarkup,
  candidate01FxMarkup,
  candidate01IconMarkup,
  candidate01TryIconMarkup,
  candidate01MarkerMarkup,
  candidate01StructureMarkup,
  candidate01TerrainMarkup,
  candidate01UnitIcon,
  candidate01UnitMarkup,
  candidate01WeaponFxTopic,
} from './candidate-01-runtime';
import { candidate01Asset, candidate01AssetUrl } from './candidate-01-assets';
import { CANDIDATE_01_PORTRAITS, candidate01StoryArt } from './candidate-01-story';
import {
  CANDIDATE_01_ABILITY_ART,
  CANDIDATE_01_CHARACTER_ART,
  CANDIDATE_01_STATUS_ART,
  CANDIDATE_01_UNIT_ART,
  CANDIDATE_01_WEAPON_ART,
} from './candidate-01-bindings';

let portraitSerial = 0;

const attr = (value: string): string =>
  value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');

function portraitMarkup(type: UnitTypeId, team: string): string | null {
  const characterTopic = CANDIDATE_01_CHARACTER_ART[type];
  const unitTopic = CANDIDATE_01_UNIT_ART[type];
  // A character portrait wins over the generic unit plate; without either there
  // is no portrait to draw. Stated as one lookup so the type follows the prose.
  const href = characterTopic
    ? candidate01AssetUrl(characterTopic)
    : unitTopic && candidate01Asset(unitTopic).url;
  if (!href) return null;
  const key = `candidate-${++portraitSerial}`;
  return `<defs><clipPath id="${key}"><rect width="96" height="112" rx="8"/></clipPath></defs>
    <g clip-path="url(#${key})"><rect width="96" height="112" fill="${attr(team)}" opacity="0.42"/>
    <image href="${attr(href)}" x="-8" y="0" width="112" height="112" preserveAspectRatio="xMidYMid slice" class="candidate-portrait-image"/>
    <path d="M0 96h96v16H0z" fill="${attr(team)}" opacity="0.72"/></g>
    <rect x="0.75" y="0.75" width="94.5" height="110.5" rx="8" fill="none" stroke="#201914" stroke-width="1.5"/>`;
}

export const CANDIDATE_01_BATTLE_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'candidate-01',
  boardClass: 'candidate-map',
  // Painted scenes want the tactical layer on the ground, not ruled over it.
  decorations: GroundBoardDecorations,
  matches: (levelId: string) => levelId.startsWith('c01-') || levelId.startsWith('experience-lab'),
  sceneProfile: candidate01SceneProfile,
  sceneFrame: candidate01SceneFrameMarkup,
  sceneLayers: candidate01MapSceneryMarkup,
  structure: candidate01StructureMarkup,
  marker: candidate01MarkerMarkup,
  weaponFx: candidate01WeaponFxTopic,
  effect: candidate01FxMarkup,
  healFx: 'C01-FX-17',
});

/**
 * Every drawing this pack answers for, as one provider.
 *
 * Composed rather than registered: it used to push itself into two module-level
 * arrays behind an idempotence flag, so the theme depended on which app had
 * imported which package first, and two packs answering for the same unit would
 * have resolved by installation order.
 */
export const CANDIDATE_01_ART_PROVIDER: ArtProvider = {
  id: 'candidate-01',
  unitMarkup: candidate01UnitMarkup,
  unitIcon: candidate01UnitIcon,
  terrainMarkup: candidate01TerrainMarkup,
  portraitMarkup,
  structureMarkup: () => null,
  iconMarkup: candidate01TryIconMarkup,
  abilityIcon: (ability) => CANDIDATE_01_ABILITY_ART[ability]
    ? candidate01IconMarkup(CANDIDATE_01_ABILITY_ART[ability]) : null,
  weaponIcon: (weapon) => {
    const art = CANDIDATE_01_WEAPON_ART[weapon];
    return art ? candidate01IconMarkup(art) : null;
  },
  statusIcon: (status) => CANDIDATE_01_STATUS_ART[status]
    ? candidate01IconMarkup(CANDIDATE_01_STATUS_ART[status]) : null,
  coverMarkup: candidate01CoverPropMarkup,
  markerMarkup: () => null,
  weaponFx: candidate01WeaponFxTopic,
  effectMarkup: candidate01FxMarkup,
};

/** This pack's art, ready for an application root to hand to a shell. */
export const CANDIDATE_01_ART = new ArtDirection(
  [CANDIDATE_01_ART_PROVIDER],
  [CANDIDATE_01_BATTLE_PRESENTATION],
);

const JOIN_AFTER: Readonly<Record<string, number>> = { mirelle: 3, bran: 4, tasha: 7, ivra: 9 };
const RELATION_LABELS: Readonly<Record<string, string>> = {
  cain: '凯恩', mirelle: '米蕾尔', tasha: '塔莎', refugees: '灰境难民', silverwood: '银林',
  'mountain-forge': '山炉氏族', 'named-dead': '归名者',
};

export function candidate01CampaignAdapter(): StoryCampaignAdapter {
  return {
    title: '断冠之誓',
    definition: CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
    levels: CANDIDATE_01_LEVELS,
    progressTotal: CANDIDATE_01_LEVELS.length,
    completionLabel: '完成前三章',
    portraits: CANDIDATE_01_PORTRAITS,
    joinAfter: JOIN_AFTER,
    relationLabels: RELATION_LABELS,
    chapterTitle: (chapter) => chapter === 1 ? '边境之火' : chapter === 2 ? '灰旗流亡' : '古老诸族',
    levelOrder: (level) => Number(level.extra?.order ?? level.id.slice(-2)),
    briefingId: (level) => `c01/brief-${String(Number(level.extra?.order ?? level.id.slice(-2))).padStart(2, '0')}`,
    storyArt: candidate01StoryArt,
    speakerNames: CANDIDATE_01_SPEAKER_NAMES,
    level: candidate01Level,
    story: candidate01Story,
    choice: candidate01Choice,
    applyBattleContext: applyCandidate01BattleContext,
    applyBattleResultPolicy: applyCandidate01BattleResultPolicy,
  };
}

export * from './candidate-01-assets';
export * from './candidate-01-story';

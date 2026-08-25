import type { LevelData, UnitTypeId } from '@empire/battle-engine';
import {
  ArtDirection,
  definitionKey,
  escapeAttr as attr,
  GroundBoardDecorations,
  type ArtProvider,
  type BattlePresentation,
  type StoryCampaignAdapter,
  boardPictureMarkup,} from '@empire/game-ui';
import {
  applyCandidate01BattleContext,
  applyCandidate01BattleResultPolicy,
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
} from '../campaign';
import { candidate01Level } from '../levels';
import { CANDIDATE_01_SPEAKER_NAMES, candidate01Choice, candidate01Story } from '../story';
import {
  candidate01MapSceneryLayers,
  candidate01SceneFrameMarkup,
  candidate01PaintsCells,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import {
  candidate01CoverPropMarkup,
  candidate01FxPicture,
  candidate01IconMarkup,
  candidate01TryIconMarkup,
  candidate01MarkerMarkup,
  candidate01StructureMarkup,
  candidate01TerrainMarkup,
  candidate01UnitIcon,
  candidate01UnitPicture,
  candidate01WeaponFxTopic,
} from './candidate-01-runtime';
import { candidate01AssetUrl } from './candidate-01-assets';
import { CANDIDATE_01_PORTRAITS, candidate01ChapterArt, candidate01StoryArt } from './candidate-01-story';
import {
  CANDIDATE_01_ABILITY_ART,
  CANDIDATE_01_CHARACTER_ART,
  CANDIDATE_01_STATUS_ART,
  CANDIDATE_01_WEAPON_ART,
} from './candidate-01-bindings';

/**
 * The plate a unit is shown on in the roster, the inspector and the codex.
 *
 * A named character has an authored card, which is one picture and goes in whole.
 * Everybody else has a *spritesheet*, and this used to put the sheet's PNG in the
 * plate — four frames of a 128x48 strip, scaled to cover a 96x112 box, so what a
 * player saw was a soldier and a half of the one behind him. At 84px in the HUD
 * that read as a crowded portrait; at 40px in the campaign roster it read as two
 * men in one frame.
 *
 * A sheet is not a picture. The figure is one *frame* of it — the same frame the
 * board stands the unit on — drawn about its own origin and scaled to the plate.
 */
function portraitMarkup(type: UnitTypeId, team: string): string | null {
  const key = `candidate-${definitionKey(type, team)}`;
  const plate = `<defs><clipPath id="${key}"><rect width="96" height="112" rx="8"/></clipPath></defs>`
    + `<g clip-path="url(#${key})"><rect width="96" height="112" fill="${attr(team)}" opacity="0.42"/>`;
  const frame = `<path d="M0 96h96v16H0z" fill="${attr(team)}" opacity="0.72"/></g>`
    + `<rect x="0.75" y="0.75" width="94.5" height="110.5" rx="8" fill="none" stroke="#201914" stroke-width="1.5"/>`;

  const characterTopic = CANDIDATE_01_CHARACTER_ART[type];
  if (characterTopic) {
    return `${plate}<image href="${attr(candidate01AssetUrl(characterTopic))}" x="-8" y="0"`
      + ` width="112" height="112" preserveAspectRatio="xMidYMid slice" class="candidate-portrait-image"/>${frame}`;
  }

  const picture = candidate01UnitPicture(type, team);
  if (!picture) return null;
  // The sprite is drawn in a 32-wide cell reaching 16 above it, so 48 tall fills
  // the plate at 2.333x, centred across it.
  const scale = 112 / 48;
  const inset = (96 - 32 * scale) / 2;
  return `${plate}<g transform="translate(${inset.toFixed(2)} ${(16 * scale).toFixed(2)}) scale(${scale.toFixed(3)})"`
    + ` class="candidate-portrait-image">${boardPictureMarkup(picture)}</g>${frame}`;
}

const CANDIDATE_01_BATTLE_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'candidate-01',
  boardClass: 'candidate-map',
  // Painted scenes want the tactical layer on the ground, not ruled over it.
  decorations: GroundBoardDecorations,
  paintsCells: candidate01PaintsCells,
  sceneProfile: candidate01SceneProfile,
  sceneFrame: candidate01SceneFrameMarkup,
  sceneLayers: candidate01MapSceneryLayers,
  structure: candidate01StructureMarkup,
  marker: candidate01MarkerMarkup,
  weaponFx: candidate01WeaponFxTopic,
  effect: candidate01FxPicture,
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
const CANDIDATE_01_ART_PROVIDER: ArtProvider = {
  id: 'candidate-01',
  unitPicture: candidate01UnitPicture,
  unitIcon: candidate01UnitIcon,
  terrainMarkup: candidate01TerrainMarkup,
  portraitMarkup,
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
};

/** This pack's art, ready for an application root to hand to a shell. */
export const CANDIDATE_01_ART = new ArtDirection(
  [CANDIDATE_01_ART_PROVIDER],
  CANDIDATE_01_BATTLE_PRESENTATION,
);

const JOIN_AFTER: Readonly<Record<string, number>> = { mirelle: 3, bran: 4, tasha: 7, ivra: 9 };
const RELATION_LABELS: Readonly<Record<string, string>> = {
  cain: '凯恩', mirelle: '米蕾尔', tasha: '塔莎', refugees: '灰境难民', silverwood: '银林',
  'mountain-forge': '山炉氏族', 'named-dead': '归名者',
};

/**
 * Where a level sits in the chapter, from its own `order` or from its id.
 *
 * Named because the briefing id is derived from it, and the two were the same
 * expression written twice: a change to how the order is resolved would have
 * left the briefing pointing at a different level than the one being played.
 */
const levelOrder = (level: LevelData): number =>
  Number(level.extra?.order ?? level.id.slice(-2));

export function candidate01CampaignAdapter(): StoryCampaignAdapter {
  return {
    title: '断冠之誓',
    definition: CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
    completionLabel: '完成前三章',
    portraits: CANDIDATE_01_PORTRAITS,
    joinAfter: JOIN_AFTER,
    relationLabels: RELATION_LABELS,
    resourceLabels: { supplies: '补给', treasury: '国库' },
    chapterTitle: (chapter) => chapter === 1 ? '边境之火' : chapter === 2 ? '灰旗流亡' : '古老诸族',
    chapterArt: candidate01ChapterArt,
    chapterOf: (level) => Number(level.extra?.chapter) || 1,
    levelOrder,
    briefingId: (level) => `c01/brief-${String(levelOrder(level)).padStart(2, '0')}`,
    storyArt: candidate01StoryArt,
    speakerNames: CANDIDATE_01_SPEAKER_NAMES,
    level: candidate01Level,
    story: candidate01Story,
    choice: candidate01Choice,
    applyBattleContext: applyCandidate01BattleContext,
    applyBattleResultPolicy: applyCandidate01BattleResultPolicy,
  };
}

export { CANDIDATE_01_MENU_ART } from './candidate-01-story';

import type { StructureTypeId, TerrainId, UnitTypeId, WeaponId } from '@empire/battle-engine';

export interface Candidate01UnitArtBinding {
  readonly topicId: string;
  readonly appearanceId?: string;
}

/** Engine ids stay story-agnostic; this table is the only unit-to-art translation. */
export const CANDIDATE_01_UNIT_ART: Readonly<Partial<Record<UnitTypeId, Candidate01UnitArtBinding>>> = {
  'c01.laiya': { topicId: 'C01-UNIT-BANNER-GUARD', appearanceId: 'laiya-stage01' },
  'c01.roderick': { topicId: 'C01-UNIT-HEAVY-KNIGHT' },
  'c01.cain': { topicId: 'C01-UNIT-LEGION-SHIELD' },
  'c01.bran': { topicId: 'C01-UNIT-RANGER' },
  'c01.mirelle': { topicId: 'C01-UNIT-GRAVEKEEPER' },
  'c01.tasha': { topicId: 'C01-UNIT-ENGINEER' },
  'c01.ivra': { topicId: 'C01-UNIT-IVRA-GROWTH' },
  'c01.swordsman': { topicId: 'C01-UNIT-SWORDSMAN' },
  'c01.banner-guard': { topicId: 'C01-UNIT-BANNER-GUARD' },
  'c01.archer': { topicId: 'C01-UNIT-ARCHER' },
  'c01.knight': { topicId: 'C01-UNIT-KNIGHT' },
  'c01.legion-shield': { topicId: 'C01-UNIT-LEGION-SHIELD' },
  'c01.gravekeeper': { topicId: 'C01-UNIT-GRAVEKEEPER' },
  'c01.engineer': { topicId: 'C01-UNIT-ENGINEER' },
  'c01.wolf-rider': { topicId: 'C01-UNIT-WOLF-RIDER' },
  'c01.skeleton-guard': { topicId: 'C01-UNIT-SKELETON-GUARD' },
  'c01.ghost': { topicId: 'C01-UNIT-GHOST' },
  'c01.inquisitor': { topicId: 'C01-UNIT-INQUISITOR' },
  'c01.templar': { topicId: 'C01-UNIT-TEMPLAR' },
  'c01.ballista': { topicId: 'C01-UNIT-BALLISTA' },
  'c01.battle-mage': { topicId: 'C01-UNIT-BATTLE-MAGE' },
  'c01.rune-shield': { topicId: 'C01-UNIT-RUNE-SHIELD' },
  'c01.rune-artificer': { topicId: 'C01-UNIT-RUNE-ARTIFICER' },
  'c01.stone-golem': { topicId: 'C01-UNIT-STONE-GOLEM' },
  'c01.silver-longbow': { topicId: 'C01-UNIT-SILVER-LONGBOW' },
  'c01.woodland-walker': { topicId: 'C01-UNIT-WOODLAND-WALKER' },
  'c01.druid': { topicId: 'C01-UNIT-DRUID' },
  'c01.cemetery-colossus': { topicId: 'C01-UNIT-CEMETERY-COLOSSUS' },
  'c01.refugee': { topicId: 'C01-MISSION-REFUGEE-ADULT' },
  'c01.laborer': { topicId: 'C01-MISSION-BRIDGE-LABORER' },

  /*
   * The nine unit types the built-in levels are fought with.
   *
   * The shipped game composes this pack's art for every level in its menu,
   * including the four built-in ones — so those were painted buildings and painted
   * ground with nine flat vector figures walking about on them, at half the visual
   * weight of a house. The sheets for all nine were already in the pack, matching
   * name for name (剑士, 弓箭手, 刺客, 牧师, 法师, 骑士, 巨魔, 弩车, 古龙), and
   * nothing referenced them.
   *
   * A binding table is exactly the place for this: it maps a domain id to this
   * pack's art, and it does not care which content pack declared the id. The
   * generated figures stay the answer for a type nobody has drawn.
   */
  soldier: { topicId: 'C01-UNIT-SWORDSMAN' },
  archer: { topicId: 'C01-UNIT-ARCHER' },
  rogue: { topicId: 'C01-UNIT-ASSASSIN' },
  cleric: { topicId: 'C01-UNIT-CLERIC' },
  mage: { topicId: 'C01-UNIT-MAGE' },
  knight: { topicId: 'C01-UNIT-KNIGHT' },
  ogre: { topicId: 'C01-UNIT-TROLL' },
  ballista: { topicId: 'C01-UNIT-BALLISTA' },
  dragon: { topicId: 'C01-UNIT-ANCIENT-DRAGON' },
};

/*
 * `CANDIDATE_01_TERRAIN_ART` stood here: sixteen terrains mapped to one
 * four-variant high-density tile each, stamped one per cell with no transition and
 * no connection. It was the second answer to "what does this cell's ground look
 * like", chosen over the environment builder's composition by a level-id
 * allowlist — so chapter one was painted and the other fifteen were stamped.
 *
 * `candidate-01-terrain-materials.ts` owns that question now, in the kit's own
 * vocabulary, for every level.
 */

export const CANDIDATE_01_MAP_STRUCTURE_ART: Readonly<Partial<Record<TerrainId, string>>> = {
  village: 'C01-STRUCT-JOINT-GRANARY',
  barracks: 'C01-STRUCT-MILITARY-REGISTRY',
  castle: 'C01-STRUCT-LORNE-KEEP',
  'c01.outpost': 'C01-STRUCT-LORNE-KEEP',
  'c01.field-post': 'C01-STRUCT-REFUGEE-SHELTER',
  'c01.keep': 'C01-STRUCT-LORNE-KEEP',
};

export const CANDIDATE_01_STRUCTURE_ART: Readonly<Partial<Record<StructureTypeId, string>>> = {
  gate: 'C01-STRUCT-REPAIRABLE-GATE',
  command_node: 'C01-STRUCT-OATH-TOWER-CONSOLE',
  depot: 'C01-STRUCT-MERCENARY-DEPOT',
  bulkhead: 'C01-STRUCT-WASTELAND-CHEVAUX',
  boss_part: 'C01-STRUCT-SUNKEN-BELL-SLUICE',
};

export const CANDIDATE_01_CHARACTER_ART: Readonly<Partial<Record<UnitTypeId, string>>> = {
  'c01.laiya': 'C01-CHAR-LEIA-01',
  'c01.roderick': 'C01-CHAR-RODERICK-01',
  'c01.cain': 'C01-CHAR-CAIN-01',
  'c01.bran': 'C01-CHAR-BRAN-01',
  'c01.mirelle': 'C01-CHAR-MIREL-01',
  'c01.tasha': 'C01-CHAR-TASHA-01',
  'c01.ivra': 'C01-CHAR-IVRA-01',
};

export const CANDIDATE_01_WEAPON_ART: Readonly<Partial<Record<WeaponId, string>>> = {
  'c01.border-blade': 'C01-EQUIP-SWORDSMAN',
  'c01.gray-oath': 'C01-EQUIP-BANNER-GUARD',
  'c01.guard-spear': 'C01-EQUIP-SPEARMAN',
  'c01.legion-spear': 'C01-EQUIP-LEGION-SHIELD',
  'c01.knight-lance': 'C01-EQUIP-KNIGHT',
  'c01.knight-charge': 'C01-EQUIP-LANCE-CAVALRY',
  'c01.longbow': 'C01-EQUIP-SILVER-LONGBOW',
  'c01.ranger-bow': 'C01-EQUIP-RANGER',
  'c01.lantern-staff': 'C01-EQUIP-GRAVEKEEPER',
  'c01.oath-dispel': 'C01-EQUIP-ANTI-OATH-GEAR',
  'c01.engineer-hammer': 'C01-EQUIP-ENGINEER',
  'c01.satchel-charge': 'C01-EQUIP-BRIDGE-TOWER-TOOLS',
  'c01.rune-hammer': 'C01-EQUIP-RUNE-ARTIFICER',
  'c01.forge-burst': 'C01-EQUIP-RUNE-ARTIFICER',
  'c01.wolf-spear': 'C01-EQUIP-WOLF-RIDER',
  'c01.undead-blade': 'C01-EQUIP-SKELETON-GUARD',
  'c01.ghost-touch': 'C01-EQUIP-GHOST',
  'c01.inquisitor-flame': 'C01-EQUIP-INQUISITOR',
  'c01.golem-slam': 'C01-EQUIP-STONE-GOLEM',
  'c01.ballista': 'C01-EQUIP-BALLISTA',
  'c01.oath-bolt': 'C01-EQUIP-BATTLE-MAGE',
  'c01.dragon-breath': 'C01-EQUIP-IVRA-GROWTH',
  'c01.colossus-sweep': 'C01-EQUIP-CEMETERY-COLOSSUS',
};

export const CANDIDATE_01_STATUS_ART: Readonly<Record<string, string>> = {
  poisoned: 'C01-STATUS-01',
  silenced: 'C01-STATUS-02',
  shaken: 'C01-STATUS-05',
  inspired: 'C01-STATUS-12',
  armor_down: 'C01-STATUS-08',
  'c01.oath-controlled': 'C01-STATUS-04',
};

export const CANDIDATE_01_ABILITY_ART: Readonly<Record<string, string>> = {
  capture: 'C01-SKILL-01',
  heal: 'C01-SKILL-07',
  attack: 'C01-SKILL-04',
  'c01.gray-rally': 'C01-SKILL-13',
  'c01.hold-the-line': 'C01-SKILL-02',
  'c01.name-the-dead': 'C01-SKILL-47',
};

export const CANDIDATE_01_WEAPON_FX: Readonly<Partial<Record<WeaponId, string>>> = {
  'c01.border-blade': 'C01-FX-01',
  'c01.gray-oath': 'C01-FX-06',
  'c01.guard-spear': 'C01-FX-02',
  'c01.legion-spear': 'C01-FX-02',
  'c01.knight-lance': 'C01-FX-02',
  'c01.knight-charge': 'C01-FX-07',
  'c01.longbow': 'C01-FX-03',
  'c01.ranger-bow': 'C01-FX-03',
  'c01.lantern-staff': 'C01-FX-04',
  'c01.oath-dispel': 'C01-FX-06',
  'c01.engineer-hammer': 'C01-FX-04',
  'c01.satchel-charge': 'C01-FX-07',
  'c01.rune-hammer': 'C01-FX-04',
  'c01.forge-burst': 'C01-FX-07',
  'c01.wolf-spear': 'C01-FX-02',
  'c01.undead-blade': 'C01-FX-01',
  'c01.ghost-touch': 'C01-FX-08',
  'c01.inquisitor-flame': 'C01-FX-06',
  'c01.golem-slam': 'C01-FX-04',
  'c01.ballista': 'C01-FX-03',
  'c01.oath-bolt': 'C01-FX-06',
  'c01.dragon-breath': 'C01-FX-05',
  'c01.colossus-sweep': 'C01-FX-08',
};

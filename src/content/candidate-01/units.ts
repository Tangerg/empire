import { defineUnit } from '../../core/content-builders';
import { FUNDS_RESOURCE } from '../../core/resources';
import type { UnitDef, WeaponDef } from '../../core/types';
import { CANDIDATE_01_WEAPONS } from './weapons';

const weaponCatalog = new Map<string, WeaponDef>(CANDIDATE_01_WEAPONS.map((entry) => [entry.id, entry]));

type UnitInput = Partial<UnitDef> & Pick<UnitDef, 'id' | 'name' | 'weapons'>;

const unit = (input: UnitInput): UnitDef => {
  const value = input.value ?? 180;
  return defineUnit({
    movementClass: 'foot',
    armorClass: 'light',
    value,
    recruitCosts: [{ resource: FUNDS_RESOURCE, amount: value }],
    morale: { maximum: 100, resilience: 0.1 },
    ...input,
  }, weaponCatalog);
};

/** Named characters and faction troops used by the first three chapters. */
export const CANDIDATE_01_UNITS: readonly UnitDef[] = [
  unit({
    id: 'c01.laiya', name: '莱娅', weapons: ['c01.border-blade', 'c01.gray-oath'], value: 420,
    maxHp: 116, defense: 0.15, movement: 4, abilities: ['attack', 'capture', 'wait'],
    defaultReaction: 'support', formations: ['formation-line', 'formation-loose'],
    morale: { maximum: 120, resilience: 0.35 },
    tags: ['hero', 'commander', 'infantry'], blurb: '从见习旗官开始学习让他人也能说“不”的年轻指挥官。',
  }),
  unit({
    id: 'c01.roderick', name: '罗德里克', weapons: ['c01.knight-lance', 'c01.knight-charge'], value: 560,
    maxHp: 135, defense: 0.2, movement: 6, movementClass: 'mounted', armorClass: 'heavy',
    abilities: ['attack', 'wait'], defaultReaction: 'support', formations: ['formation-line', 'formation-defensive'],
    tags: ['hero', 'commander', 'cavalry'], blurb: '可靠而强硬的王国骑士，莱娅的导师。',
  }),
  unit({
    id: 'c01.cain', name: '凯恩', weapons: ['c01.legion-spear', 'c01.knight-charge'], value: 520,
    maxHp: 125, defense: 0.2, movement: 4, armorClass: 'heavy', abilities: ['attack', 'capture', 'wait'],
    defaultReaction: 'guard', formations: ['formation-defensive', 'formation-line'],
    tags: ['hero', 'commander', 'infantry'], blurb: '相信程序与秩序可以减少任性伤亡的帝国军官。',
  }),
  unit({
    id: 'c01.bran', name: '布兰', weapons: ['c01.ranger-bow', 'c01.border-blade'], value: 360,
    maxHp: 88, movement: 5, armorClass: 'unarmored', vision: 5,
    tags: ['hero', 'ranged', 'scout', 'infantry'], blurb: '先记脚印、伤口和姓名，再谈旗帜的灰境猎人。',
  }),
  unit({
    id: 'c01.mirelle', name: '米蕾尔', weapons: ['c01.lantern-staff', 'c01.oath-dispel'], value: 390,
    maxHp: 78, movement: 3, armorClass: 'unarmored', abilities: ['heal', 'attack', 'capture', 'wait'],
    defaultReaction: 'conserve', tags: ['hero', 'support', 'arcane', 'gravekeeper', 'infantry'],
    blurb: '坚持让每一段亡者记忆保有姓名与修改痕迹的守墓人。',
  }),
  unit({
    id: 'c01.tasha', name: '塔莎', weapons: ['c01.engineer-hammer', 'c01.satchel-charge'], value: 400,
    maxHp: 92, defense: 0.08, movement: 4, tags: ['hero', 'engineer', 'infantry'],
    blurb: '用账目、契约和工程把理想变成可维护组织的佣兵首领。',
  }),
  unit({
    id: 'c01.ivra', name: '伊芙拉', weapons: ['c01.dragon-breath'], value: 680,
    maxHp: 145, defense: 0.18, movement: 6, movementClass: 'flying', armorClass: 'flying',
    abilities: ['attack', 'wait'], vision: 5, morale: { maximum: 120, resilience: 0.35 },
    tags: ['hero', 'dragon', 'monster', 'flying'], blurb: '只接受平等盟约、始终保有离开权的幼龙。',
  }),

  unit({ id: 'c01.swordsman', name: '边境剑士', weapons: ['c01.border-blade'], value: 120, maxHp: 98, defense: 0.08, movement: 3, formations: ['formation-line', 'formation-loose'], tags: ['infantry'], blurb: '可靠的近战与占领骨干。' }),
  unit({ id: 'c01.banner-guard', name: '灰旗卫士', weapons: ['c01.guard-spear'], value: 170, maxHp: 112, defense: 0.14, movement: 3, defaultReaction: 'guard', formations: ['formation-defensive', 'formation-line'], tags: ['infantry', 'guard'], blurb: '以援护和阵线保护灰旗同伴。' }),
  unit({ id: 'c01.archer', name: '边境弓手', weapons: ['c01.ranger-bow'], value: 160, maxHp: 78, movement: 3, armorClass: 'unarmored', tags: ['infantry', 'ranged'], blurb: '适应丘陵与林地的轻装射手。' }),
  unit({ id: 'c01.knight', name: '王国骑士', weapons: ['c01.knight-lance', 'c01.knight-charge'], value: 340, maxHp: 116, defense: 0.17, movement: 6, movementClass: 'mounted', armorClass: 'heavy', abilities: ['attack', 'wait'], tags: ['cavalry'], blurb: '长距离冲锋的重装骑兵。' }),
  unit({ id: 'c01.legion-shield', name: '军团盾卫', weapons: ['c01.legion-spear'], value: 230, maxHp: 120, defense: 0.2, movement: 3, armorClass: 'heavy', defaultReaction: 'guard', formations: ['formation-defensive', 'formation-line'], tags: ['infantry', 'guard'], blurb: '帝国军团的稳固盾墙。' }),
  unit({ id: 'c01.gravekeeper', name: '守墓人', weapons: ['c01.lantern-staff', 'c01.oath-dispel'], value: 260, maxHp: 72, movement: 3, armorClass: 'unarmored', abilities: ['heal', 'attack', 'capture', 'wait'], tags: ['infantry', 'support', 'arcane', 'gravekeeper'], blurb: '治疗盟友并压制誓文亡灵。' }),
  unit({ id: 'c01.engineer', name: '工兵', weapons: ['c01.engineer-hammer', 'c01.satchel-charge'], value: 250, maxHp: 86, movement: 4, tags: ['infantry', 'engineer'], blurb: '擅长拆除结构与打开战场缺口。' }),
  unit({ id: 'c01.wolf-rider', name: '荒原狼骑', weapons: ['c01.wolf-spear'], value: 300, maxHp: 102, movement: 7, movementClass: 'mounted', tags: ['cavalry', 'wasteland'], blurb: '高机动侧击单位，不适合正面硬拼。' }),
  unit({ id: 'c01.skeleton-guard', name: '骸骨守卫', weapons: ['c01.undead-blade'], value: 140, maxHp: 82, defense: 0.12, movement: 3, morale: { maximum: 70, resilience: 0.5 }, tags: ['undead', 'oathbound', 'infantry'], blurb: '被誓文反复拉回战场的亡者回声。' }),
  unit({ id: 'c01.ghost', name: '失名幽魂', weapons: ['c01.ghost-touch'], value: 200, maxHp: 68, movement: 5, movementClass: 'flying', armorClass: 'unarmored', abilities: ['attack', 'wait'], tags: ['undead', 'oathbound', 'flying'], blurb: '失去姓名后无法安息的记忆。' }),
  unit({ id: 'c01.inquisitor', name: '圣辉审判官', weapons: ['c01.inquisitor-flame'], value: 330, maxHp: 88, defense: 0.1, movement: 3, armorClass: 'heavy', tags: ['infantry', 'ranged', 'arcane', 'oathbound'], blurb: '用群体誓火压制异议的教廷军官。' }),
  unit({ id: 'c01.templar', name: '圣辉圣殿骑士', weapons: ['c01.legion-spear', 'c01.inquisitor-flame'], value: 360, maxHp: 122, defense: 0.2, movement: 3, armorClass: 'heavy', defaultReaction: 'guard', formations: ['formation-defensive'], tags: ['infantry', 'guard', 'oathbound'], blurb: '以重甲和誓火推进净化命令的教廷卫队。' }),
  unit({ id: 'c01.ballista', name: '城防弩车', weapons: ['c01.ballista'], value: 330, maxHp: 72, movement: 2, movementClass: 'heavy', abilities: ['attack', 'wait'], tags: ['siege', 'ranged'], blurb: '射程极远但无法移动后射击。' }),
  unit({ id: 'c01.battle-mage', name: '战斗法师', weapons: ['c01.oath-bolt'], value: 290, maxHp: 74, movement: 3, armorClass: 'unarmored', tags: ['infantry', 'ranged', 'arcane'], blurb: '以魔法穿透重甲的阵地单位。' }),
  unit({ id: 'c01.rune-shield', name: '符文盾卫', weapons: ['c01.rune-hammer'], value: 270, maxHp: 128, defense: 0.22, movement: 3, movementClass: 'heavy', armorClass: 'heavy', defaultReaction: 'guard', formations: ['formation-defensive'], tags: ['infantry', 'guard', 'forge'], blurb: '山炉氏族极难撼动的防守核心。' }),
  unit({ id: 'c01.rune-artificer', name: '符文工匠', weapons: ['c01.rune-hammer', 'c01.forge-burst'], value: 320, maxHp: 90, defense: 0.12, movement: 3, armorClass: 'heavy', tags: ['infantry', 'engineer', 'arcane', 'forge'], blurb: '用符文破甲并维护古代机械。' }),
  unit({ id: 'c01.stone-golem', name: '石魔像', weapons: ['c01.golem-slam'], value: 430, maxHp: 160, defense: 0.28, movement: 2, movementClass: 'heavy', armorClass: 'heavy', abilities: ['attack', 'wait'], morale: { maximum: 140, resilience: 0.7 }, tags: ['monster', 'construct', 'forge'], blurb: '缓慢、坚固，能把敌人推出阵线。' }),
  unit({ id: 'c01.silver-longbow', name: '银林长弓', weapons: ['c01.longbow'], value: 250, maxHp: 76, movement: 4, armorClass: 'unarmored', vision: 5, tags: ['infantry', 'ranged', 'silverwood'], blurb: '远射和对空能力兼备的银林守军。' }),
  unit({ id: 'c01.woodland-walker', name: '林地行者', weapons: ['c01.ranger-bow', 'c01.border-blade'], value: 280, maxHp: 88, movement: 6, vision: 5, tags: ['infantry', 'ranged', 'scout', 'silverwood'], blurb: '借森林掩护快速转移的游击者。' }),
  unit({ id: 'c01.druid', name: '银林德鲁伊', weapons: ['c01.oath-bolt'], value: 300, maxHp: 80, movement: 3, armorClass: 'unarmored', abilities: ['heal', 'attack', 'capture', 'wait'], tags: ['infantry', 'support', 'arcane', 'silverwood'], blurb: '维持母根循环的森林施法者。' }),
  unit({ id: 'c01.cemetery-colossus', name: '墓园巨像', weapons: ['c01.colossus-sweep'], value: 620, maxHp: 210, defense: 0.25, movement: 2, movementClass: 'heavy', armorClass: 'heavy', abilities: ['attack', 'wait'], morale: { maximum: 160, resilience: 0.75 }, tags: ['monster', 'undead', 'oathbound'], blurb: '由无数无名记忆聚成的战争残响。' }),
  unit({ id: 'c01.refugee', name: '边境难民', weapons: ['c01.civilian-tool'], value: 20, recruitCosts: [], maxHp: 58, movement: 3, armorClass: 'unarmored', abilities: ['wait'], defaultReaction: 'conserve', morale: { maximum: 80, resilience: 0 }, tags: ['civilian', 'escort'], blurb: '必须安全带离战区的非战斗人员。' }),
  unit({ id: 'c01.laborer', name: '随军工匠', weapons: ['c01.civilian-tool'], value: 35, recruitCosts: [], maxHp: 70, movement: 3, armorClass: 'unarmored', abilities: ['wait'], defaultReaction: 'conserve', tags: ['civilian', 'engineer', 'escort'], blurb: '能把一座桥或炉心从崩溃边缘拉回来的普通工匠。' }),
];

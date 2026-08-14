import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '@empire/battle-engine';
import type {
  Coord,
  LevelData,
  LevelScenario,
  LevelStructure,
  LevelUnit,
  Objective,
  PlayerConfig,
} from '@empire/battle-engine';

type CampaignHero = 'laiya' | 'bran' | 'mirelle' | 'tasha' | 'ivra' | 'torren' | 'elin';

interface UnitSeed {
  key: string;
  unit: string;
  x?: number;
  y?: number;
  owner?: number;
  facing?: LevelUnit['facing'];
  formation?: string;
  directive?: LevelUnit['directive'];
}

interface BattleSpec {
  order: number;
  chapter: 1 | 2 | 3;
  title: string;
  subtitle: string;
  map: string[];
  player: UnitSeed[];
  enemy: UnitSeed[];
  neutral?: UnitSeed[];
  deployment: {
    player: Coord[];
    enemy: Coord[];
  };
  objective: Objective;
  scenario?: LevelScenario;
  structures?: LevelStructure[];
  composites?: LevelData['composites'];
  owners?: LevelData['owners'];
  turnLimit?: number;
  aggression?: number;
  enemyName?: string;
  extra?: Record<string, unknown>;
}

const TWIN_HILLS = [
  'TTTTTTTTTTTTTTTTTTTTTTTTTTT',
  'TTT...T......TT......T..TTT',
  'TT...T.^^^^...T........T.TT',
  'Tq-----^^^^h..............T',
  'TT..T.-^^^^h.T..hhh...T..TT',
  'T.....-^^^^.......hh......T',
  'TT..T.-------..T........T.T',
  'T.....-.....-.............T',
  'T-----------v-v-----------T',
  'T..-..........-..........-T',
  'TT.-.hTh......--------...-T',
  'T..-......-.....h^^^^----qT',
  'TT.-...T..-hh.T.h^^^^h.T.TT',
  'T..--------h....h^^^^h....T',
  'TT...T........T..hhhh..T.TT',
  'TTT.....T......T.....T..TTT',
  'TTTTTTTTTTTTTTTTTTTTTTTTTTT',
];

const THREE_BRIDGES = [
  '...T...h..~~....T.....',
  '..v.......~~..h.....q.',
  '.....T....~~......T...',
  '.q..h.....==..........',
  '------....~~....------',
  '..T...v...~~..v....T..',
  '..........==..........',
  '.h....T...~~....h.....',
  '------....~~....------',
  '..T.......==.......T..',
  '....h.....~~..T.......',
  '.v....T...~~.....v....',
  '.....q....~~..h.....q.',
  '..T.......~~......T...',
];

const REDSTONE_SIEGE = [
  '...T......h...........',
  '..h...T.........######',
  '.......---.....#....K#',
  '.q-----...-----#.....#',
  '....h....T.....#..B..#',
  '..T.........####.....#',
  '.....v......-..-..v..#',
  '.h......T...#..-.....#',
  '....---.....#..#.....#',
  '..T.....h...----.....#',
  '.................#####',
  '.q...T.....v..........',
  '....h...---.....T.....',
  '..T.................q.',
  '......^....h..........',
];

const BLACK_FLAG_CAMP = [
  '..T....T......T.....',
  '.q...T....---.....q.',
  '....h...T.....T.....',
  '..T....BBBBB.....T..',
  '......-B...B-.......',
  '.T..h..B...B...h....',
  '.......B.B.B........',
  '..T....B...B....T...',
  '....v..BBBBB........',
  '.h.......---...h....',
  '..T...T.......T.....',
  '......v...T......q..',
  '.q..T......---......',
  '.....h....T.........',
];

const REED_VILLAGE = [
  '..T....s....T........',
  '.q..ssssss.....v.....',
  '...ss.v.ss..T.....q..',
  '.Tssssssss.....h.....',
  '..ss.v.sss..---......',
  '...ssssss.....T......',
  '.h...s....v......h...',
  '....---......ssss....',
  '..T....T...ss.v.ss...',
  '.v.....---ssssssss...',
  '...h.......ss.v.ss...',
  '..T.....q...ssss.....',
  '.....T..........T....',
  '.q.....h..---......q.',
  '...T...........h.....',
];

const WHITE_RIVER = [
  '..T...h...~~.....T......',
  '.q........~~..v......q..',
  '....T.....~~......h.....',
  '..v.......==....T.......',
  '----......~~......------',
  '...T..h...~~..v.........',
  '..........==............',
  '.h....T...~~......T.....',
  '------....~~....------..',
  '..T.......==.......h....',
  '....v.....~~..T.........',
  '.q....T...~~.....v......',
  '.....h....~~..q......q..',
  '..T.......~~......T.....',
];

const FREE_CITY = [
  'ppppppppppppppppppppppp',
  'pq---p--p---pp---p---qp',
  'pp#ppp#ppppppp#ppp#pppp',
  'pv#ppB#ppvpppp#Bpp#vppp',
  'pp#ppp#ppppppp#ppp#pppp',
  'ppppp---pppppp---pppppp',
  'pp###pppp#pp#pppp###ppp',
  'ppppppvpp#pp#pvpppppppp',
  'pp#ppppppppp#pppppp#ppp',
  'pv#ppBppp----ppBppp#vpp',
  'pp#pppppp#pp#pppppp#ppp',
  'ppppp---pppppp---pppppp',
  'pp#ppp#ppvpppp#ppp#pppp',
  'pq---p--p---pp---p---qp',
  'ppppppppppppppppppppppp',
];

const MERCENARY_QUARTER = [
  'pppppppppppppppppppppp',
  'pq--pp---pppp---pp--qp',
  'pp#pppp#ppvpp#pppp#ppp',
  'pv#pBpp#ppppp#pBpp#vpp',
  'pp#pppp#ppppp#pppp#ppp',
  'pppp---ppp#pppp---pppp',
  'pp#ppppppp#ppppppp#ppp',
  'pp#ppvppp---pppvpp#ppp',
  'pp#ppppppp#ppppppp#ppp',
  'pppp---ppp#pppp---pppp',
  'pv#pBpp#ppppp#pBpp#vpp',
  'pp#pppp#ppvpp#pppp#ppp',
  'pq--pp---pppp---pp--qp',
  'pppppppppppppppppppppp',
];

const OATH_PRISON = [
  '..T....o.....T.......',
  '.q....ooo......h..q..',
  '...T..o.o..---.......',
  '.---..o.o......T.....',
  '.....ooBoo...........',
  '.h...o###o...T...h...',
  '.....o#o#o...........',
  '..T..o###o..v....T...',
  '.....ooBoo...........',
  '.---..o.o....---.....',
  '..h...o.o..T.........',
  '....T.ooo.......h....',
  '.q.....o...---.....q.',
  '...T.........T.......',
  '......^..............',
];

const OLD_BANNER_PASS = [
  '...T..^^^^....h..........',
  '..h...^^^^..T......q.....',
  '.q..--^^^^------.........',
  '......hhhh......T........',
  '.T.........h.......T.....',
  '...v..---.....v..........',
  '......T....^^^^....h.....',
  '..h........^^^^..T.......',
  '.T...---...^^^^......q...',
  '....T.......hh...........',
  '..v.....T.......T...v....',
  '......h....---......h....',
  '.q..T............T.....q.',
  '.........^^^^............',
];

const SILENT_CEMETERY = [
  'gggTggggggogggggTggggg',
  'gqgggoggggooggggggggqg',
  'ggTggoooggogggTggggggg',
  'ggggoogoogoogggggTgggg',
  'gTggoqoooggoggTggggTgg',
  'ggggoogoogoogggggggggg',
  'ggTggooogoogggTggogggg',
  'ggggggogqggggggooogggg',
  'gqggTgoooggTgggoqogTgg',
  'gggggoogoogggggooogggg',
  'ggTggoooggogggTggogggg',
  'ggggoogoogoogggggTgggg',
  'gTgggoggggoogggggggTgg',
  'gqggTgggggogggTggggqgg',
  'ggggggTggggggggggTgggg',
  'ggTggggggggTgggggggggg',
];

const SUNKEN_BELL = [
  'rrr..~~~..rrrr..~~~..rr',
  'rqr..~~~..r..r..~~~.qrr',
  'rrr..~=~..r..r..~=~..rr',
  '....r~=~r.r..r.r~=~r...',
  'rrrrr===rrrrrrrr===rrrr',
  '....r~=~r..rr..r~=~r...',
  'rrr..~=~...rr...~=~..rr',
  'rvr..~~~..rrrr..~~~..vr',
  'rrr..~~~..r..r..~~~..rr',
  '....h~=~r.r..r.r~=~h...',
  'rrrrr===rrrrrrrr===rrrr',
  '....r~=~r..rr..r~=~r...',
  'rqr..~~~..r..r..~~~.qrr',
  'rrr..~~~..rrrr..~~~..rr',
  '....h~~~h......h~~~h...',
];

const HOLY_ARCHIVES = [
  'pppppppppppppppppppppppppp',
  'pq---p--p---pp---p--p---qp',
  'pp#ppp#ppppp##pppp#ppp#ppp',
  'pv#pBppppvpp##ppvpppB#vppp',
  'pp#ppp#ppppp##pppp#ppp#ppp',
  'pppp---ppppp##pppp---ppppp',
  'pp###ppp##pp##pp##ppp###pp',
  'pppppppp##pppppp##pppppppp',
  'pp#ppvpp##p----p##ppvpp#pp',
  'pp#ppppp##pppppp##ppppp#pp',
  'pp###pppp#pp##pp#pppp###pp',
  'pppp---ppppp##pppp---ppppp',
  'pv#pBppppvpp##ppvpppB#vppp',
  'pp#ppp#ppppp##pppp#ppp#ppp',
  'pq---p--p---pp---p--p---qp',
  'pppppppppppppppppppppppppp',
];

const MOUNTAIN_FORGE = [
  'fffffmmmmffffmmmmfffff',
  'fqfffmffmffffmffmfffqf',
  'fffffmffmffffmffmfffff',
  'fff##mffm####mffm##fff',
  'fffffmffmffffmffmfffff',
  'fffff====ffff====fffff',
  'ffvffmffmffffmffmffvff',
  'fffffmffm####mffmfffff',
  'fffff====ffff====fffff',
  'fffffmffmffffmffmfffff',
  'fff##mffm####mffm##fff',
  'fffffmffmffffmffmfffff',
  'fqfffmffmffffmffmfffqf',
  'fffffmmmmffffmmmmfffff',
  'ffvffffffffffffffffvff',
  'ffffffffffffffffffffff',
];

const SILVERWOOD = [
  'TRTTRRTTRRTTRRTTRRTTRRTT',
  'TqTTRRTTRRTTRRTTRRTTRTqT',
  'TRRTTRRTTRRTTRRTTRRTTRRT',
  'TTRRThhTRRTTRRTTRhhTRRTT',
  'TRRTTRRTTRRTTRRTTRRTTRRT',
  'TTRRTvTRRRTTRRTvTRRTTRRT',
  'TRRTTRRTTRRRRRRTTRRTTRRT',
  'TTRRThhTRRRRRRTTRhhTRRTT',
  'TRRTTRRTTRRRRRRTTRRTTRRT',
  'TTRRTvTRRRTTRRTvTRRTTRRT',
  'TRRTTRRTTRRTTRRTTRRTTRRT',
  'TTRRThhTRRTTRRTTRhhTRRTT',
  'TRRTTRRTTRRTTRRTTRRTTRRT',
  'TqTTRRTTRRTTRRTTRRTTRTqT',
  'TRTTRRTTRRTTRRTTRRTTRRTT',
  'TTRRTTRRTTRRTTRRTTRRTTRR',
];

const UNFLAGGED_MEMORY = [
  'ggTggggggogggggTggggogggg',
  'gqggoggggooggggggggoooggg',
  'ggggoooggogggTgggooogTggg',
  'gTggoogoogoogggggoogooggg',
  'ggggoqoooggoggTggoqoooggg',
  'ggggoogoogoogggggoogooggg',
  'ggTgooogoogggTgggooogoogg',
  'gggggogqgggggggggogqggggg',
  'gqgTgoooggTgggTgoooggTqgg',
  'ggggoogoogggggggoogoogggg',
  'ggTgoooggogggTgggooogoogg',
  'ggggoogoogoogggggoogooggg',
  'gTgggoggggooggggggogTgggg',
  'gqggTggggogggTggggoggqggg',
  'ggggggTggggggggTggggggggg',
  'ggTggggggggTggggggggTgggg',
  'gggggTggggggggggTgggggggg',
];

const hero = (id: CampaignHero, unit: string): UnitSeed => ({ key: `campaign-${id}`, unit });
const ally = (key: string, unit: string): UnitSeed => ({ key, unit });
const foe = (key: string, unit: string): UnitSeed => ({ key, unit, owner: 2, facing: 'west' });

const PARTY = {
  laiya: hero('laiya', 'c01.laiya'),
  torren: hero('torren', 'c01.swordsman'),
  elin: hero('elin', 'c01.archer'),
  bran: hero('bran', 'c01.bran'),
  mirelle: hero('mirelle', 'c01.mirelle'),
  tasha: { ...hero('tasha', 'c01.tasha'), formation: 'formation-defensive' },
  ivra: hero('ivra', 'c01.ivra'),
} as const;

const route = (label = '击退敌军'): Objective => ({ id: 'route', type: 'routEnemies', label });
const eliminateKey = (key: string, label: string): Objective => ({ id: `eliminate-${key}`, type: 'eliminate', selector: { keys: [key] }, label });
const score = (variable: string, count: number, label: string): Objective => ({ id: `score-${variable}`, type: 'score', variable, atLeast: count, label });
const escort = (keys: string[], zone: string, count: number, label: string): Objective => ({ id: `escort-${zone}`, type: 'escort', selector: { keys }, zone, count, label });

function protectLaiya(objective: Objective, label: string): Objective {
  return {
    id: 'primary',
    type: 'failOn',
    label,
    condition: { type: 'unitCount', selector: { keys: ['campaign-laiya'] }, op: 'lte', value: 0 },
    objective,
  };
}

const zone = (id: string, ...cells: Coord[]): NonNullable<LevelScenario['zones']>[number] => ({ id, cells });

/** Compact, per-battle deployment notation: `x,y x,y ...`. */
function coordinates(layout: string): Coord[] {
  return layout.trim().split(/\s+/).filter(Boolean).map((token) => {
    const [x, y, extra] = token.split(',').map(Number);
    if (!Number.isInteger(x) || !Number.isInteger(y) || extra !== undefined) {
      throw new Error(`invalid deployment coordinate "${token}"`);
    }
    return { x, y };
  });
}

const deployment = (player: string, enemy: string): BattleSpec['deployment'] => ({
  player: coordinates(player),
  enemy: coordinates(enemy),
});

function visitTrigger(id: string, zoneId: string, variable: string, signal: string): NonNullable<LevelScenario['triggers']>[number] {
  return {
    id,
    timing: 'afterAction',
    condition: { type: 'unitInZone', zone: zoneId, owner: 1 },
    effects: [
      { type: 'addVariable', key: variable, amount: 1 },
      { type: 'emitSignal', signal },
    ],
  };
}

const baseParty = (...extra: UnitSeed[]): UnitSeed[] => [
  { ...PARTY.laiya, formation: 'formation-defensive' },
  PARTY.torren,
  PARTY.elin,
  ...extra,
];

const BATTLES: readonly BattleSpec[] = [
  {
    order: 1, chapter: 1, title: '双子丘陵', subtitle: '第一次指挥：夺取高地，也承担冒进的后果。', map: TWIN_HILLS,
    player: baseParty(
      ally('field-cleric', 'c01.gravekeeper'), ally('roderick-reserve', 'c01.roderick'),
      ally('ridge-scout', 'c01.archer'), ally('ridge-guard', 'c01.banner-guard'),
      ally('west-spear', 'c01.banner-guard'), ally('village-runner', 'c01.archer'),
      ally('camp-guard', 'c01.legion-shield'), ally('field-knight', 'c01.knight'),
    ),
    enemy: [
      foe('vanguard-1', 'c01.legion-shield'), foe('vanguard-2', 'c01.swordsman'), foe('vanguard-archer', 'c01.archer'), foe('vanguard-cain', 'c01.cain'),
      foe('north-shield', 'c01.legion-shield'), foe('south-sword', 'c01.swordsman'), foe('south-bow', 'c01.archer'),
      foe('camp-bow', 'c01.archer'),
    ],
    deployment: deployment(
      '2,9 3,8 3,10 2,8 3,11 6,14 1,9 6,12 5,6 4,12 7,11',
      '15,4 19,5 22,3 24,5 20,9 24,12 18,13 23,8',
    ),
    objective: protectLaiya(route('击退维尔萨前锋'), '莱娅存活并击退维尔萨前锋'),
    owners: [{ x: 1, y: 3, owner: 1 }, { x: 25, y: 11, owner: 2 }, { x: 12, y: 8, owner: 0 }, { x: 14, y: 8, owner: 0 }],
    scenario: {
      zones: [zone(
        'north-hill',
        { x: 7, y: 2 }, { x: 8, y: 2 }, { x: 9, y: 2 }, { x: 10, y: 2 },
        { x: 7, y: 3 }, { x: 8, y: 3 }, { x: 9, y: 3 }, { x: 10, y: 3 },
        { x: 7, y: 4 }, { x: 8, y: 4 }, { x: 9, y: 4 }, { x: 10, y: 4 },
        { x: 7, y: 5 }, { x: 8, y: 5 }, { x: 9, y: 5 }, { x: 10, y: 5 },
      )],
      triggers: [{
        id: 'matching-orders', timing: 'afterAction',
        condition: { type: 'eventCount', event: 'death', op: 'gte', value: 1 },
        effects: [{ type: 'emitSignal', signal: 'found_matching_orders' }],
      }],
    },
    extra: { tacticalTheme: '双高地、中央村落、三线推进与分离侦察组', recommendedTurns: 11, battleScale: 'company', fronts: 3 },
  },
  {
    order: 2, chapter: 1, title: '三桥河谷', subtitle: '短暂停火，把六军之争让给过桥的平民。', map: THREE_BRIDGES,
    player: baseParty(ally('bridge-knight', 'c01.knight'), ally('bridge-guard-1', 'c01.banner-guard'), ally('bridge-guard-2', 'c01.archer'), ally('refugee-1', 'c01.refugee'), ally('refugee-2', 'c01.refugee'), ally('refugee-3', 'c01.refugee'), ally('river-scout', 'c01.swordsman'), ally('bridge-cleric', 'c01.gravekeeper'), ally('bridge-reserve', 'c01.banner-guard')),
    enemy: [
      foe('cain', 'c01.cain'), foe('shield-1', 'c01.legion-shield'), foe('shield-2', 'c01.legion-shield'), foe('bow-1', 'c01.archer'), foe('unmarked-ballista', 'c01.ballista'),
      foe('north-bank-guard', 'c01.swordsman'), foe('south-bank-bow', 'c01.archer'), foe('battery-screen', 'c01.legion-shield'),
    ],
    deployment: deployment('1,9 2,8 2,10 3,7 1,10 3,10 1,11 2,12 3,12 5,12 2,9 1,8', '20,4 19,3 19,5 18,8 20,10 15,2 16,11 20,12'),
    objective: protectLaiya({
      id: 'bridge-all', type: 'all', label: '护送平民并摧毁无标记弩车', objectives: [
        escort(['refugee-1', 'refugee-2', 'refugee-3'], 'east-safety', 3, '护送三名平民抵达东岸'),
        eliminateKey('unmarked-ballista', '摧毁向平民开火的无标记弩车'),
      ],
    }, '护送平民并阻止无标记弩车'),
    owners: [{ x: 1, y: 3, owner: 1 }, { x: 20, y: 1, owner: 2 }, { x: 5, y: 12, owner: 0 }, { x: 20, y: 12, owner: 0 }],
    scenario: {
      zones: [zone('middle-bridge', { x: 10, y: 6 }, { x: 11, y: 6 }), zone('east-safety', { x: 20, y: 0 }, { x: 21, y: 0 }, { x: 20, y: 1 }, { x: 21, y: 1 }, { x: 20, y: 2 }, { x: 21, y: 2 })],
      engagementRules: [{ id: 'bridge-truce', zone: 'middle-bridge', mode: 'no-attacks', players: [1, 2] }],
      triggers: [{ id: 'truce-kept', timing: 'turnEnd', condition: { type: 'turnAtLeast', turn: 3 }, effects: [{ type: 'removeEngagementRule', id: 'bridge-truce' }, { type: 'emitSignal', signal: 'accepted_bridge_truce' }] }],
    },
    extra: { tacticalTheme: '三条渡河路线、护送纵队与远程封锁', recommendedTurns: 12, battleScale: 'company', fronts: 3 },
  },
  {
    order: 3, chapter: 1, title: '赤石围城', subtitle: '城堡可以夺下，被控制的名字却未必救得回来。', map: REDSTONE_SIEGE,
    player: baseParty(ally('roderick', 'c01.roderick'), ally('siege-mage', 'c01.battle-mage'), ally('siege-engineer', 'c01.engineer'), ally('siege-guard', 'c01.banner-guard'), ally('field-cleric', 'c01.gravekeeper'), ally('north-sapper', 'c01.engineer'), ally('south-spear', 'c01.banner-guard')),
    enemy: [
      foe('oath-guard-1', 'c01.skeleton-guard'), foe('oath-guard-2', 'c01.skeleton-guard'), foe('redstone-ballista', 'c01.ballista'), foe('redstone-shield', 'c01.legion-shield'), foe('redstone-inquisitor', 'c01.inquisitor'),
      foe('outer-screen', 'c01.swordsman'), foe('south-tower-bow', 'c01.archer'), foe('keep-reserve', 'c01.templar'), foe('node-mage', 'c01.battle-mage'),
    ],
    deployment: deployment('1,7 2,6 2,8 3,5 3,9 4,4 4,10 6,12 2,12 6,2', '18,2 18,4 20,6 16,7 20,2 13,3 14,8 16,10 19,7'),
    objective: protectLaiya({
      id: 'redstone-path', type: 'any', label: '夺城或关闭誓文节点', objectives: [
        { id: 'disable-node', type: 'escort', selector: { keys: ['campaign-laiya', 'siege-engineer'] }, zone: 'oath-node', count: 1, label: '深入后方关闭控制节点' },
        { id: 'capture-redstone', type: 'captureHQ', label: '强攻并占领赤石城堡' },
        score('gate_breached', 1, '击溃门卫并切断城门誓文链'),
      ],
    }, '在十八回合内夺取赤石或切断控制链'),
    owners: [{ x: 1, y: 3, owner: 1 }, { x: 20, y: 2, owner: 2 }, { x: 18, y: 4, owner: 2 }, { x: 1, y: 11, owner: 0 }, { x: 20, y: 13, owner: 0 }],
    structures: [{ id: 'redstone-node', type: 'command_node', owner: 2, x: 18, y: 7 }],
    scenario: {
      variables: { gate_breached: 0 },
      zones: [zone('oath-node', { x: 18, y: 7 })],
      triggers: [{
        id: 'disable-redstone', timing: 'afterAction', condition: { type: 'unitInZone', zone: 'oath-node', owner: 1 },
        effects: [
          { type: 'changeUnitOwner', selector: { anyTags: ['oathbound'] }, owner: 1 },
          { type: 'damageStructure', id: 'redstone-node', amount: 100 },
          { type: 'emitSignal', signal: 'redstone_node_disabled' },
        ],
      }, {
        id: 'redstone-second-wave', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 4 },
        effects: [{ type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
          { key: 'redstone-wave-guard', x: 0, y: 13, unit: 'c01.banner-guard', owner: 1, facing: 'east' },
          { key: 'redstone-wave-engineer', x: 1, y: 14, unit: 'c01.engineer', owner: 1, facing: 'east' },
          { key: 'redstone-wave-bow', x: 2, y: 14, unit: 'c01.archer', owner: 1, facing: 'east' },
        ] }],
      }, {
        id: 'redstone-gate-breached', timing: 'afterAction',
        condition: { type: 'unitCount', selector: { keys: ['redstone-shield'] }, op: 'lte', value: 0 },
        effects: [
          { type: 'setVariable', key: 'gate_breached', value: 1 },
          { type: 'changeUnitOwner', selector: { anyTags: ['oathbound'] }, owner: 1 },
          { type: 'damageStructure', id: 'redstone-node', amount: 100 },
          { type: 'emitSignal', signal: 'redstone_gate_chain_broken' },
        ],
      }, {
        id: 'redstone-sappers-complete', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 15 },
        effects: [
          { type: 'setVariable', key: 'gate_breached', value: 1 },
          { type: 'changeUnitOwner', selector: { anyTags: ['oathbound'] }, owner: 1 },
          { type: 'damageStructure', id: 'redstone-node', amount: 100 },
          { type: 'emitSignal', signal: 'redstone_sappers_cut_chain' },
        ],
      }],
    },
    turnLimit: 18, aggression: 0.32,
    extra: { tacticalTheme: '两翼攻城、城外屏障与纵深守备', recommendedTurns: 14, battleScale: 'siege', fronts: 3 },
  },
  {
    order: 4, chapter: 1, title: '黑旗营地', subtitle: '侦察命令结束之处，是救人和取证必须开始的地方。', map: BLACK_FLAG_CAMP,
    player: baseParty(PARTY.mirelle, ally('scout-1', 'c01.swordsman'), ally('scout-2', 'c01.banner-guard'), ally('scout-3', 'c01.archer'), ally('scout-4', 'c01.swordsman'), ally('scout-cleric', 'c01.gravekeeper'), ally('scout-reserve', 'c01.banner-guard')),
    enemy: [
      foe('camp-guard-1', 'c01.swordsman'), foe('camp-guard-2', 'c01.swordsman'), foe('camp-inquisitor', 'c01.inquisitor'), foe('camp-bow', 'c01.archer'), foe('camp-mage', 'c01.battle-mage'),
      foe('north-patrol', 'c01.swordsman'), foe('east-patrol', 'c01.swordsman'), foe('south-patrol', 'c01.archer'), foe('gate-reserve', 'c01.swordsman'),
    ],
    deployment: deployment('3,10 3,9 4,10 4,9 5,11 3,11 5,9 1,13 4,11 2,10', '8,3 11,3 10,5 8,7 12,7 15,4 16,8 14,10 18,12'),
    objective: protectLaiya({
      id: 'black-camp-sequence', type: 'sequence', label: '调查营帐后带证据撤离', objectives: [
        score('evidence', 3, '调查三处营帐'),
        escort(['campaign-laiya', 'campaign-mirelle'], 'west-exit', 1, '至少一名证据携带者撤离'),
      ],
    }, '调查黑旗营地并带证人撤离'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 18, y: 1, owner: 2 }, { x: 7, y: 3, owner: 2 }, { x: 11, y: 3, owner: 2 }, { x: 9, y: 6, owner: 2 }],
    scenario: {
      variables: { evidence: 0 },
      zones: [zone('tent-a', { x: 7, y: 4 }), zone('tent-b', { x: 9, y: 6 }), zone('tent-c', { x: 11, y: 8 }), zone('west-exit', { x: 0, y: 11 }, { x: 0, y: 12 }, { x: 0, y: 13 })],
      triggers: [
        visitTrigger('inspect-a', 'tent-a', 'evidence', 'found_prisoners'),
        visitTrigger('inspect-b', 'tent-b', 'evidence', 'found_oath_template'),
        visitTrigger('inspect-c', 'tent-c', 'evidence', 'saved_witnesses'),
        {
          id: 'withdraw-with-evidence', timing: 'afterAction',
          condition: { type: 'variable', key: 'evidence', op: 'gte', value: 3 },
          effects: [{ type: 'setUnitDirective', selector: { keys: ['campaign-laiya', 'campaign-mirelle'] }, directive: { mode: 'retreat', waypoints: [{ x: 0, y: 12 }] } }],
        },
      ],
    },
    aggression: 0.42,
    extra: { tacticalTheme: '环形巡逻、营区渗透与反向撤离', recommendedTurns: 12, battleScale: 'raid', fronts: 3 },
  } as BattleSpec,
  {
    order: 5, chapter: 1, title: '焚村令', subtitle: '保护苇草村；灰旗第一次不是军令，而是一项选择。', map: REED_VILLAGE,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('village-guard', 'c01.banner-guard'), ally('village-guard-2', 'c01.banner-guard'), ally('villager-1', 'c01.refugee'), ally('villager-2', 'c01.refugee'), ally('villager-3', 'c01.refugee'), ally('village-cleric', 'c01.gravekeeper'), ally('west-militia', 'c01.swordsman'), ally('east-militia', 'c01.archer')),
    enemy: [
      foe('purifier-1', 'c01.inquisitor'), foe('purifier-2', 'c01.templar'), foe('purifier-3', 'c01.legion-shield'), foe('purifier-4', 'c01.archer'), foe('purifier-5', 'c01.knight'),
      foe('north-purifier', 'c01.swordsman'), foe('south-purifier', 'c01.templar'), foe('fire-support', 'c01.battle-mage'),
    ],
    deployment: deployment('6,7 7,6 7,8 8,5 8,9 9,6 10,8 5,3 12,10 11,9 9,8 5,9 12,7', '18,3 19,5 16,6 18,9 15,11 12,1 19,12 14,13'),
    objective: protectLaiya({
      id: 'save-village', type: 'all', label: '保护村民直至净化军撤退', objectives: [
        { id: 'protect-villagers', type: 'protect', selector: { anyTags: ['civilian'] }, minimumAlive: 2, untilTurn: 7, label: '至少两名村民存活' },
        { id: 'hold-seven', type: 'surviveTurns', turns: 7, label: '坚守七回合' },
      ],
    }, '拒绝焚村令并保护苇草村'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 18, y: 2, owner: 2 }, { x: 8, y: 11, owner: 1 }, { x: 1, y: 13, owner: 1 }, { x: 19, y: 13, owner: 0 }],
    scenario: {
      zones: [zone('west-road', { x: 0, y: 13 }, { x: 0, y: 14 }, { x: 1, y: 14 }, { x: 2, y: 14 })],
      triggers: [{
        id: 'purifier-reinforcement', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 4 },
        effects: [
          { type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
            { key: 'purifier-reinforcement', x: 0, y: 13, unit: 'c01.inquisitor', owner: 2, facing: 'east' },
            { key: 'purifier-west-shield', x: 0, y: 14, unit: 'c01.legion-shield', owner: 2, facing: 'east' },
            { key: 'purifier-west-bow', x: 2, y: 14, unit: 'c01.archer', owner: 2, facing: 'east' },
          ] },
          { type: 'emitSignal', signal: 'gray_banner_raised' },
        ],
      }],
    },
    turnLimit: 11, aggression: 0.55,
    extra: { tacticalTheme: '村落纵深防御、平民分区与两波夹击', recommendedTurns: 10, chapterFinale: true, battleScale: 'defense', fronts: 3 },
  },
  {
    order: 6, chapter: 2, title: '白河夜渡', subtitle: '人、粮车和后卫，没有一项损失会在结算画面外消失。', map: WHITE_RIVER,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('rearguard', 'c01.banner-guard'), ally('rearguard-2', 'c01.banner-guard'), ally('refugee-1', 'c01.refugee'), ally('refugee-2', 'c01.refugee'), ally('refugee-3', 'c01.refugee'), ally('refugee-4', 'c01.laborer'), ally('mounted-rearguard', 'c01.knight'), ally('white-river-cleric', 'c01.gravekeeper')),
    enemy: [
      foe('pursuer-1', 'c01.knight'), foe('pursuer-2', 'c01.swordsman'), foe('pursuer-3', 'c01.archer'), foe('pursuer-4', 'c01.swordsman'), foe('pursuer-ballista', 'c01.ballista'),
      foe('north-pursuer', 'c01.archer'), foe('ford-shield', 'c01.legion-shield'), foe('river-mage', 'c01.battle-mage'),
    ],
    deployment: deployment('9,7 8,8 10,9 8,10 7,7 8,7 9,8 7,11 8,11 9,11 8,12 6,10 7,12', '1,4 2,6 3,8 1,10 1,12 4,2 4,9 3,12'),
    objective: protectLaiya(escort(['refugee-1', 'refugee-2', 'refugee-3', 'refugee-4'], 'north-bank', 4, '护送四名非战斗人员渡河'), '在八回合内完成白河撤离'),
    scenario: {
      zones: [zone('north-bank', { x: 21, y: 0 }, { x: 22, y: 0 }, { x: 23, y: 0 }, { x: 21, y: 1 }, { x: 22, y: 1 }, { x: 23, y: 1 })],
      triggers: [
        { id: 'pursuit-wave', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 4 }, effects: [
          { type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
            { key: 'pursuit-wave-knight', x: 0, y: 1, unit: 'c01.knight', owner: 2, facing: 'east' },
            { key: 'pursuit-wave-sword', x: 0, y: 2, unit: 'c01.swordsman', owner: 2, facing: 'east' },
            { key: 'pursuit-wave-bow', x: 1, y: 3, unit: 'c01.archer', owner: 2, facing: 'east' },
          ] },
          { type: 'emitSignal', signal: 'white_river_pursuit_arrived' },
        ] },
        { id: 'bridge-damaged', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 6 }, effects: [{ type: 'emitSignal', signal: 'white_river_bridge_damaged' }] },
      ],
    },
    turnLimit: 13, aggression: 0.65,
    extra: { tacticalTheme: '行军纵队、分层后卫与持续追击', recommendedTurns: 11, battleScale: 'pursuit', fronts: 2 },
  },
  {
    order: 7, chapter: 2, title: '无主之城', subtitle: '城墙属于谁，要由谁先得到保护来回答。', map: FREE_CITY,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('militia', 'c01.banner-guard'), ally('militia-2', 'c01.banner-guard'), ally('engineer', 'c01.engineer'), ally('west-militia', 'c01.swordsman'), ally('east-militia', 'c01.archer'), ally('city-cleric', 'c01.gravekeeper'), ally('north-militia', 'c01.banner-guard'), ally('south-militia', 'c01.swordsman')),
    enemy: [
      foe('raider-1', 'c01.wolf-rider'), foe('raider-2', 'c01.wolf-rider'), foe('raider-3', 'c01.swordsman'), foe('royal-1', 'c01.swordsman'), foe('royal-2', 'c01.archer'), foe('royal-3', 'c01.legion-shield'),
      foe('west-raider', 'c01.archer'), foe('north-raider', 'c01.wolf-rider'), foe('east-royal', 'c01.legion-shield'), foe('royal-mage', 'c01.battle-mage'),
    ],
    deployment: deployment('10,8 10,7 10,9 8,8 13,8 9,8 11,8 10,11 6,11 16,11 11,9 9,9 13,12', '1,3 5,6 4,9 20,3 20,6 18,9 1,12 8,2 21,12 15,2'),
    objective: protectLaiya({ id: 'city-control', type: 'all', label: '夺回粮仓与民兵营', objectives: [
      { id: 'granary', type: 'control', zone: 'granary', label: '控制联合粮仓' },
      { id: 'militia-barracks', type: 'control', zone: 'militia-barracks', label: '控制民兵营' },
    ] }, '在两路敌军夹击下稳定洛岬'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 21, y: 1, owner: 2 }, { x: 5, y: 3, owner: 0 }, { x: 15, y: 3, owner: 0 }, { x: 5, y: 9, owner: 0 }, { x: 15, y: 9, owner: 0 }, { x: 1, y: 13, owner: 0 }, { x: 21, y: 13, owner: 0 }],
    scenario: { zones: [zone('granary', { x: 5, y: 3 }), zone('militia-barracks', { x: 15, y: 3 })] },
    turnLimit: 15, aggression: 0.38,
    extra: { tacticalTheme: '四向巷战、中央守备与分区占领', recommendedTurns: 12, battleScale: 'urban', fronts: 4 },
  },
  {
    order: 8, chapter: 2, title: '佣兵之价', subtitle: '名字、数目、日期——先结清死人留下的账。', map: MERCENARY_QUARTER,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, ally('mercenary-guard', 'c01.banner-guard'), ally('ledger-guard', 'c01.banner-guard'), ally('warehouse-bow', 'c01.archer'), ally('mercenary-cleric', 'c01.gravekeeper'), ally('mercenary-spear', 'c01.banner-guard'), ally('mercenary-scout', 'c01.swordsman')),
    enemy: [
      foe('thief-chief', 'c01.wolf-rider'), foe('thief-1', 'c01.swordsman'), foe('thief-2', 'c01.archer'), foe('thief-3', 'c01.wolf-rider'), foe('fraud-enforcer', 'c01.inquisitor'),
      foe('north-thief', 'c01.swordsman'), foe('east-thief', 'c01.archer'), foe('south-thief', 'c01.wolf-rider'), foe('fraud-shield', 'c01.legion-shield'),
    ],
    deployment: deployment('1,7 3,6 3,8 5,7 6,6 5,8 8,10 4,8 11,11 5,10 6,8 6,9', '10,2 12,3 14,2 19,5 19,8 8,1 20,11 12,12 16,10'),
    objective: protectLaiya({
      id: 'protect-tasha', type: 'failOn', label: '保护塔莎并恢复粮道',
      condition: { type: 'unitCount', selector: { keys: ['campaign-tasha'] }, op: 'lte', value: 0 },
      objective: route('击退抢粮者并保护三座粮仓'),
    }, '承认欠款并与塔莎共同保护粮道'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 20, y: 1, owner: 2 }, { x: 4, y: 3, owner: 1 }, { x: 15, y: 3, owner: 1 }, { x: 4, y: 10, owner: 1 }, { x: 15, y: 10, owner: 0 }],
    scenario: { zones: [zone('ledger', { x: 10, y: 7 }, { x: 11, y: 7 })], triggers: [visitTrigger('recover-ledger', 'ledger', 'ledgers', 'mercenary_dead_paid')] , variables: { ledgers: 0 } },
    turnLimit: 14,
    extra: { tacticalTheme: '三角接敌、分仓防御与契约同盟', recommendedTurns: 11, battleScale: 'urban', fronts: 3 },
  },
  {
    order: 9, chapter: 2, title: '笼中之火', subtitle: '拆掉锁链，不用新的命令替代旧的命令。', map: OATH_PRISON,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, ally('chain-breaker', 'c01.engineer'), ally('prison-guard', 'c01.banner-guard'), ally('prison-scout', 'c01.archer'), ally('prison-cleric', 'c01.gravekeeper'), ally('west-breach-guard', 'c01.banner-guard'), ally('east-breach-sword', 'c01.swordsman')),
    enemy: [
      foe('escort-1', 'c01.inquisitor'), foe('escort-2', 'c01.legion-shield'), foe('escort-3', 'c01.knight'), foe('escort-4', 'c01.archer'), foe('escort-mage', 'c01.battle-mage'),
      foe('north-chain-guard', 'c01.templar'), foe('east-chain-guard', 'c01.legion-shield'), foe('south-chain-guard', 'c01.swordsman'), foe('west-chain-bow', 'c01.archer'), foe('inner-jailer', 'c01.swordsman'),
    ],
    neutral: [{ key: 'caged-ivra', unit: 'c01.ivra', owner: 3, x: 7, y: 6, directive: { mode: 'guard' } }],
    deployment: deployment('1,10 2,9 2,11 3,8 3,12 4,10 5,12 1,13 6,13 4,12 7,13 8,12', '7,2 10,4 12,7 10,10 9,11 14,3 16,7 14,11 4,5 12,6'),
    objective: protectLaiya({ id: 'break-chains', type: 'neutralizeComposite', composite: 'ivra-chains', minimumNeutralized: 3, label: '破坏三处锁链节点' }, '不用控制誓文释放伊芙拉'),
    structures: [
      { id: 'chain-north', type: 'command_node', owner: 2, x: 7, y: 4 }, { id: 'chain-east', type: 'command_node', owner: 2, x: 9, y: 6 },
      { id: 'chain-south', type: 'command_node', owner: 2, x: 7, y: 8 }, { id: 'chain-west', type: 'command_node', owner: 2, x: 5, y: 6 },
    ],
    composites: [{ id: 'ivra-chains', parts: ['chain-north', 'chain-east', 'chain-south', 'chain-west'], minimumNeutralized: 3, tags: ['prison', 'oathbound'] }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 18, y: 1, owner: 2 }, { x: 1, y: 12, owner: 1 }, { x: 19, y: 12, owner: 2 }],
    turnLimit: 15,
    extra: { tacticalTheme: '四向锁链、环形守备与多路破门', recommendedTurns: 12, battleScale: 'assault', fronts: 4 },
  },
  {
    order: 10, chapter: 2, title: '旧旗下的追兵', subtitle: '导师站在旧旗之下；活着离开，比赢下争论更重要。', map: OLD_BANNER_PASS,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('gray-rearguard', 'c01.banner-guard'), ally('gray-scout', 'c01.archer'), ally('old-banner-cleric', 'c01.gravekeeper'), ally('old-banner-guard', 'c01.banner-guard')),
    enemy: [
      foe('roderick', 'c01.roderick'), foe('royal-guard-1', 'c01.knight'), foe('royal-guard-2', 'c01.legion-shield'), foe('royal-bow', 'c01.archer'), foe('royal-mage', 'c01.battle-mage'),
      foe('pass-shield', 'c01.legion-shield'), foe('north-rider', 'c01.knight'), foe('south-rider', 'c01.knight'), foe('pass-bow', 'c01.archer'), foe('banner-sword', 'c01.swordsman'),
    ],
    deployment: deployment('10,7 9,6 9,8 10,5 10,9 11,6 11,8 9,7 8,10 10,8 10,6', '18,6 20,4 19,8 21,6 17,3 16,7 15,2 16,11 20,10 22,8'),
    objective: protectLaiya(escort(['campaign-laiya'], 'east-pass', 1, '莱娅抵达东部山口'), '不与旧部死战，带灰旗军脱离包围'),
    scenario: {
      zones: [zone('east-pass', { x: 23, y: 0 }, { x: 24, y: 0 }, { x: 23, y: 1 }, { x: 24, y: 1 }, { x: 23, y: 2 }, { x: 24, y: 2 })],
      triggers: [
        { id: 'old-banner-rearguard', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 6 }, effects: [{ type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
          { key: 'old-banner-west-knight', x: 0, y: 4, unit: 'c01.knight', owner: 2, facing: 'east' },
          { key: 'old-banner-west-shield', x: 0, y: 5, unit: 'c01.legion-shield', owner: 2, facing: 'east' },
          { key: 'old-banner-west-bow', x: 1, y: 6, unit: 'c01.archer', owner: 2, facing: 'east' },
        ] }] },
        { id: 'ivra-intervenes', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 4 }, effects: [{ type: 'changeMorale', selector: { owner: 2 }, amount: -18, reason: '幼龙截断追兵' }, { type: 'emitSignal', signal: 'ivra_chose_to_help' }] },
      ],
    },
    aggression: 0.35,
    turnLimit: 14,
    extra: { tacticalTheme: '山口突破、前后追兵与有限交战', recommendedTurns: 11, chapterFinale: true, battleScale: 'breakthrough', fronts: 3 },
  },
  {
    order: 11, chapter: 3, title: '无声墓园', subtitle: '同一名士兵反复死亡，直到有人读出墓碑上的名字。', map: SILENT_CEMETERY,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('gravekeeper-escort', 'c01.gravekeeper'), ally('cemetery-shield', 'c01.banner-guard'), ally('cemetery-bow', 'c01.archer')),
    enemy: [
      foe('colossus', 'c01.cemetery-colossus'), foe('echo-1', 'c01.skeleton-guard'), foe('echo-2', 'c01.skeleton-guard'), foe('echo-3', 'c01.ghost'), foe('echo-4', 'c01.ghost'),
      foe('north-echo', 'c01.skeleton-guard'), foe('south-echo', 'c01.skeleton-guard'), foe('east-ghost', 'c01.ghost'), foe('west-ghost', 'c01.ghost'), foe('grave-inquisitor', 'c01.inquisitor'), foe('ossuary-guard', 'c01.skeleton-guard'),
    ],
    deployment: deployment('1,8 2,7 2,9 3,6 3,10 4,8 5,11 1,13 4,14 7,14', '11,7 9,5 13,5 10,9 14,9 7,2 8,12 18,6 6,10 19,3 17,12'),
    objective: protectLaiya({ id: 'grave-sequence', type: 'sequence', label: '归还三段真名后击败巨像', objectives: [score('names', 3, '读取三块拒绝净化的墓碑'), eliminateKey('colossus', '让墓园巨像安息')] }, '记录真名并终止墓园循环'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 20, y: 1, owner: 2 }, { x: 1, y: 8, owner: 1 }, { x: 16, y: 8, owner: 2 }, { x: 1, y: 13, owner: 0 }, { x: 19, y: 13, owner: 0 }],
    scenario: { variables: { names: 0 }, zones: [zone('name-a', { x: 5, y: 3 }), zone('name-b', { x: 14, y: 7 }), zone('name-c', { x: 8, y: 12 })], triggers: [visitTrigger('name-a', 'name-a', 'names', 'named_old_soldier'), visitTrigger('name-b', 'name-b', 'names', 'named_border_child'), visitTrigger('name-c', 'name-c', 'names', 'named_royal_deserter')] },
    turnLimit: 16,
    extra: { tacticalTheme: '分散墓碑、亡灵包围圈与中央巨像', recommendedTurns: 13, battleScale: 'expedition', fronts: 3 },
  },
  {
    order: 12, chapter: 3, title: '沉没钟塔', subtitle: '河水上涨时，暂时共同控制比抢走碎片更难。', map: SUNKEN_BELL,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('tower-engineer', 'c01.engineer'), ally('tower-guard', 'c01.banner-guard')),
    enemy: [
      foe('imperial-cain', 'c01.cain'), foe('imperial-1', 'c01.legion-shield'), foe('imperial-2', 'c01.battle-mage'), foe('oath-echo-1', 'c01.skeleton-guard'), foe('oath-echo-2', 'c01.ghost'),
      foe('east-imperial', 'c01.knight'), foe('north-echo', 'c01.skeleton-guard'), foe('south-echo', 'c01.ghost'), foe('tower-bow', 'c01.archer'), foe('mechanism-guard', 'c01.templar'),
    ],
    deployment: deployment('1,7 2,6 2,8 3,5 3,9 4,7 5,11 1,12 4,13', '20,7 19,6 19,8 11,4 11,10 21,5 10,2 15,12 20,12 16,4'),
    objective: protectLaiya({ id: 'bell-mechanism', type: 'neutralizeComposite', composite: 'bell-controls', minimumNeutralized: 2, label: '关闭两处失控钟塔机构' }, '在塔底崩塌前关闭钟塔机构'),
    structures: [
      { id: 'bell-a', type: 'boss_part', owner: 2, x: 6, y: 4 }, { id: 'bell-b', type: 'boss_part', owner: 2, x: 16, y: 4 }, { id: 'bell-c', type: 'boss_part', owner: 2, x: 11, y: 10 },
    ],
    composites: [{ id: 'bell-controls', parts: ['bell-a', 'bell-b', 'bell-c'], minimumNeutralized: 2, tags: ['tower', 'flood-control'] }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 20, y: 1, owner: 2 }, { x: 1, y: 12, owner: 0 }, { x: 20, y: 12, owner: 0 }],
    scenario: { triggers: [
      { id: 'water-rises', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 6 }, effects: [{ type: 'emitSignal', signal: 'bell_tower_flood_rising' }] },
      { id: 'drowned-echoes', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 5 }, effects: [{ type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
        { key: 'drowned-echo-west', x: 6, y: 0, unit: 'c01.ghost', owner: 2, facing: 'south' },
        { key: 'drowned-echo-east', x: 16, y: 0, unit: 'c01.ghost', owner: 2, facing: 'south' },
      ] }] },
    ] },
    turnLimit: 15,
    extra: { tacticalTheme: '双塔狭道、水上增援与分路关停', recommendedTurns: 12, battleScale: 'ruin', fronts: 3 },
  },
  {
    order: 13, chapter: 3, title: '圣城档案', subtitle: '把被删除的战争带出白塔，让档案仍然留下修改痕迹。', map: HOLY_ARCHIVES,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('archive-guard', 'c01.banner-guard'), ally('archive-engineer', 'c01.engineer'), ally('archive-bow', 'c01.archer'), ally('archive-sword', 'c01.swordsman'), ally('archive-cleric', 'c01.gravekeeper'), ally('archive-reserve', 'c01.banner-guard')),
    enemy: [
      foe('archive-inquisitor', 'c01.inquisitor'), foe('archive-guard-1', 'c01.legion-shield'), foe('archive-guard-2', 'c01.legion-shield'), foe('archive-mage', 'c01.battle-mage'), foe('archive-knight', 'c01.knight'),
      foe('west-archive-guard', 'c01.templar'), foe('north-archive-bow', 'c01.archer'), foe('central-archive-mage', 'c01.battle-mage'), foe('east-archive-guard', 'c01.legion-shield'), foe('south-archive-knight', 'c01.knight'), foe('exit-inquisitor', 'c01.inquisitor'),
    ],
    deployment: deployment('1,9 3,8 5,10 3,7 3,11 4,9 5,12 1,13 4,14 7,13 8,14 6,14 10,14', '10,3 14,5 12,7 15,8 17,4 8,4 10,1 14,10 20,7 18,12 23,13'),
    objective: protectLaiya({ id: 'archive-sequence', type: 'sequence', label: '取得三份档案并护送米蕾尔离开', objectives: [score('archives', 3, '读取三组被删除的档案'), escort(['campaign-mirelle'], 'archive-exit', 1, '米蕾尔携档案撤离')] }, '让米蕾尔把战争档案带出圣城'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 24, y: 1, owner: 2 }, { x: 4, y: 3, owner: 2 }, { x: 20, y: 3, owner: 2 }, { x: 4, y: 12, owner: 0 }, { x: 20, y: 12, owner: 2 }],
    scenario: { variables: { archives: 0 }, zones: [zone('archive-a', { x: 4, y: 3 }), zone('archive-b', { x: 20, y: 3 }), zone('archive-c', { x: 20, y: 12 }), zone('archive-exit', { x: 24, y: 14 }, { x: 25, y: 14 }, { x: 24, y: 15 }, { x: 25, y: 15 })], triggers: [visitTrigger('archive-a', 'archive-a', 'archives', 'found_deleted_war'), visitTrigger('archive-b', 'archive-b', 'archives', 'found_matching_orders'), visitTrigger('archive-c', 'archive-c', 'archives', 'mirelle_kept_revision_marks')] },
    turnLimit: 21,
    extra: { tacticalTheme: '多翼档案馆、纵深警戒与长距离撤离', recommendedTurns: 15, battleScale: 'infiltration', fronts: 3 },
  },
  {
    order: 14, chapter: 3, title: '山炉余烬', subtitle: '技术不是无罪的借口，修好炉心也不是替祖先赎罪。', map: MOUNTAIN_FORGE,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('forge-laborer', 'c01.laborer'), ally('forge-guard', 'c01.banner-guard'), ally('forge-engineer', 'c01.engineer'), ally('forge-bow', 'c01.archer')),
    enemy: [
      foe('golem-1', 'c01.stone-golem'), foe('golem-2', 'c01.stone-golem'), foe('rune-rebel-1', 'c01.rune-shield'), foe('rune-rebel-2', 'c01.rune-artificer'), foe('forge-mage', 'c01.battle-mage'),
      foe('north-golem', 'c01.stone-golem'), foe('east-rune-shield', 'c01.rune-shield'), foe('south-artificer', 'c01.rune-artificer'), foe('forge-guard-1', 'c01.templar'), foe('forge-guard-2', 'c01.rune-shield'), foe('forge-bow-guard', 'c01.archer'),
    ],
    deployment: deployment('1,8 2,7 2,9 3,6 2,10 4,8 5,12 1,13 4,14 7,14 9,14', '14,7 14,5 14,3 15,7 18,4 6,2 19,8 15,12 7,10 18,11 20,13'),
    objective: protectLaiya({ id: 'forge-all', type: 'all', label: '关闭熔流阀并平息失控守卫', objectives: [score('valves', 2, '关闭两处熔流阀'), route('平息失控的山炉守卫')] }, '阻止炉心爆炸并留下完整技术记录'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 20, y: 1, owner: 2 }, { x: 1, y: 12, owner: 0 }, { x: 20, y: 12, owner: 0 }],
    scenario: { variables: { valves: 0 }, zones: [zone('valve-a', { x: 5, y: 5 }, { x: 6, y: 5 }), zone('valve-b', { x: 14, y: 8 }, { x: 15, y: 8 })], triggers: [visitTrigger('valve-a', 'valve-a', 'valves', 'forge_valve_a_closed'), visitTrigger('valve-b', 'valve-b', 'valves', 'forge_valve_b_closed')] },
    turnLimit: 21,
    extra: { tacticalTheme: '双炉区、熔流通道与重装纵深', recommendedTurns: 15, battleScale: 'industrial', fronts: 3 },
  },
  {
    order: 15, chapter: 3, title: '银林长梦', subtitle: '森林延续的代价，不能继续转嫁给林外城市。', map: SILVERWOOD,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('silverwood-guard', 'c01.banner-guard'), ally('silverwood-scout', 'c01.woodland-walker'), ally('silverwood-cleric', 'c01.gravekeeper')),
    enemy: [
      foe('controlled-druid', 'c01.druid'), foe('controlled-longbow-1', 'c01.silver-longbow'), foe('controlled-longbow-2', 'c01.silver-longbow'), foe('controlled-walker', 'c01.woodland-walker'), foe('inquisitor-handler', 'c01.inquisitor'),
      foe('north-longbow', 'c01.silver-longbow'), foe('south-longbow', 'c01.silver-longbow'), foe('east-walker', 'c01.woodland-walker'), foe('root-guard-1', 'c01.templar'), foe('root-guard-2', 'c01.legion-shield'), foe('oath-druid', 'c01.druid'),
    ],
    neutral: [{ key: 'silverwood-witness', unit: 'c01.druid', owner: 3, x: 3, y: 13, directive: { mode: 'guard' } }],
    deployment: deployment('1,8 2,7 2,9 3,6 3,10 4,8 5,12 1,13 4,14 7,14', '12,7 14,5 14,9 17,7 20,7 8,2 8,13 21,11 11,5 11,9 17,12'),
    objective: protectLaiya({
      id: 'protect-root',
      type: 'failOn',
      label: '保护母根并解除誓文守军',
      condition: { type: 'structure', id: 'mother-root', state: 'destroyed' },
      objective: {
        id: 'eliminate-oath-forces',
        type: 'eliminate',
        selector: { owner: 2 },
        label: '击退控制母根的誓文部队',
      },
    }, '保住银林母根，同时结束代价转移'),
    structures: [{ id: 'mother-root', type: 'c01.mother-root', owner: 3, x: 11, y: 7, hp: 500 }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 22, y: 1, owner: 2 }, { x: 1, y: 13, owner: 0 }, { x: 22, y: 13, owner: 0 }],
    turnLimit: 17,
    extra: { tacticalTheme: '森林远射、母根环防与多路渗透', recommendedTurns: 14, battleScale: 'forest', fronts: 3 },
  },
  {
    order: 16, chapter: 3, title: '记住我的名字', subtitle: '无旗者不是需要被消灭的怪物，而是需要被听见的新意识。', map: UNFLAGGED_MEMORY,
    player: baseParty(
      PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra,
      ally('memory-gravekeeper', 'c01.gravekeeper'), ally('memory-guard', 'c01.banner-guard'),
      { ...ally('memory-bow', 'c01.archer'), directive: { mode: 'patrol', waypoints: [{ x: 6, y: 3 }] } },
      { ...ally('memory-sword', 'c01.swordsman'), directive: { mode: 'patrol', waypoints: [{ x: 16, y: 8 }] } },
      ally('memory-shield', 'c01.banner-guard'),
      { ...ally('memory-engineer', 'c01.engineer'), directive: { mode: 'patrol', waypoints: [{ x: 8, y: 13 }] } },
    ),
    enemy: [
      foe('unflagged', 'c01.cemetery-colossus'), foe('memory-1', 'c01.ghost'), foe('memory-2', 'c01.ghost'), foe('memory-3', 'c01.skeleton-guard'), foe('memory-inquisitor', 'c01.inquisitor'),
      foe('memory-4', 'c01.ghost'), foe('memory-5', 'c01.skeleton-guard'), foe('memory-6', 'c01.ghost'), foe('inquisitor-guard-1', 'c01.templar'), foe('inquisitor-guard-2', 'c01.legion-shield'), foe('memory-mage', 'c01.battle-mage'), foe('memory-bow-guard', 'c01.archer'),
    ],
    deployment: deployment('1,9 2,8 2,10 3,7 3,11 4,9 5,13 1,14 4,15 7,15 6,14 8,15 10,15', '12,8 9,5 15,5 10,11 22,8 16,10 7,12 18,3 20,7 20,9 17,13 22,12'),
    objective: protectLaiya({ id: 'true-name-all', type: 'all', label: '确认三个真名并终止聚忆暴走', objectives: [score('true_names', 3, '让三个真名得到回应'), eliminateKey('memory-inquisitor', '击退继续抽取记忆的审判官')] }, '让无旗者保有名字与选择'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 18, y: 4, owner: 2 }, { x: 1, y: 8, owner: 1 }, { x: 22, y: 8, owner: 2 }, { x: 1, y: 13, owner: 0 }, { x: 21, y: 13, owner: 2 }],
    scenario: { variables: { true_names: 0 }, zones: [zone('truth-a', { x: 6, y: 3 }), zone('truth-b', { x: 16, y: 8 }), zone('truth-c', { x: 8, y: 13 })], triggers: [visitTrigger('truth-a', 'truth-a', 'true_names', 'unflagged_name_one'), visitTrigger('truth-b', 'truth-b', 'true_names', 'unflagged_name_two'), visitTrigger('truth-c', 'truth-c', 'true_names', 'unflagged_name_three')] },
    turnLimit: 30,
    extra: { tacticalTheme: '中央聚忆体、三处真名与审判阵地', recommendedTurns: 16, chapterFinale: true, battleScale: 'finale', fronts: 4 },
  },
];

function resources(funds: number) {
  return {
    [FUNDS_RESOURCE]: { current: funds, capacity: null },
    [COMMAND_POINTS_RESOURCE]: { current: 1, capacity: 6 },
  };
}

function playerConfig(spec: BattleSpec): PlayerConfig[] {
  const players: PlayerConfig[] = [
    { id: 1, name: '灰旗军', team: 1, color: '#4d86b8', controller: 'human', resources: resources(300), objectives: [spec.objective] },
    { id: 2, name: spec.enemyName ?? '敌对部队', team: 2, color: '#a8493f', controller: 'ai', resources: resources(250), ai: { aggression: spec.aggression ?? 0.55 }, objectives: [route()] },
  ];
  if (spec.neutral?.length || spec.structures?.some((structure) => structure.owner === 3)) {
    // Story neutrals keep independent ownership, colour and AI, but share the
    // player's team: caged allies and witnesses are protected actors, not a
    // third hostile faction that the player must silently exterminate.
    players.push({ id: 3, name: '中立方', team: 1, color: '#b8a57a', controller: 'ai', resources: resources(0), ai: { aggression: 0 }, objectives: [structuredClone(spec.objective)] });
  }
  return players;
}

function place(seeds: readonly UnitSeed[], slots: readonly Coord[], owner: number): LevelUnit[] {
  if (seeds.some((seed) => seed.x === undefined || seed.y === undefined) && slots.length < seeds.length) {
    throw new Error(`deployment has ${slots.length} slots for ${seeds.length} units`);
  }
  return seeds.map((seed, index) => ({
    key: seed.key,
    x: seed.x ?? slots[index].x,
    y: seed.y ?? slots[index].y,
    unit: seed.unit,
    owner: seed.owner ?? owner,
    facing: seed.facing ?? (owner === 1 ? 'east' : 'west'),
    formation: seed.formation,
    directive: seed.directive,
  }));
}

function elevationFor(rows: readonly string[]): number[] {
  return rows.flatMap((row) => [...row].map((cell) => cell === '^' ? 2 : cell === 'h' ? 1 : 0));
}

function buildLevel(spec: BattleSpec): LevelData {
  const width = spec.map[0].length;
  const height = spec.map.length;
  const units = [
    ...place(spec.player, spec.deployment.player, 1),
    ...place(spec.enemy, spec.deployment.enemy, 2),
    ...place(spec.neutral ?? [], [], 3),
  ];
  const laiya = units.find((unit) => unit.key === 'campaign-laiya');
  const enemyCommander = units.find((unit) => unit.unit === 'c01.cain' || unit.unit === 'c01.roderick' || unit.unit === 'c01.inquisitor');
  return {
    schema: 2,
    id: `c01-${String(spec.order).padStart(2, '0')}`,
    name: `${String(spec.order).padStart(2, '0')} · ${spec.title}`,
    author: '《断冠之誓》战役',
    description: spec.subtitle,
    width,
    height,
    terrain: spec.map.slice(),
    elevation: elevationFor(spec.map),
    owners: spec.owners?.map((owner) => ({ ...owner })) ?? [],
    units,
    commanders: [
      ...(laiya ? [{ id: 'laiya-command', unitKey: 'campaign-laiya', radius: spec.chapter === 1 ? 1 : spec.chapter === 2 ? 2 : 3, turnGrants: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }], tactics: ['c01.gray-rally', 'c01.hold-the-line'] }] : []),
      ...(enemyCommander ? [{ id: 'enemy-command', unitKey: enemyCommander.key!, radius: 2, turnGrants: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }], tactics: ['rally'] }] : []),
    ],
    structures: spec.structures?.map((structure) => ({ ...structure })),
    composites: spec.composites?.map((composite) => ({ ...composite, parts: composite.parts.slice() })),
    players: playerConfig(spec),
    rules: {
      turnLimit: spec.turnLimit ?? null,
      moraleEnabled: spec.order >= 5,
      captureMode: 'progressive',
      captureThreshold: 100,
      baseResourceGrants: [],
    },
    victory: [spec.objective],
    scenario: spec.scenario ? structuredClone(spec.scenario) : undefined,
    extra: {
      campaign: 'candidate-01-gray-banner',
      chapter: spec.chapter,
      order: spec.order,
      subtitle: spec.subtitle,
      ...spec.extra,
    },
  };
}

export const CANDIDATE_01_LEVELS: readonly LevelData[] = BATTLES.map(buildLevel);

export function candidate01Level(id: string): LevelData {
  const level = CANDIDATE_01_LEVELS.find((entry) => entry.id === id);
  if (!level) throw new Error(`unknown candidate-01 level "${id}"`);
  return structuredClone(level);
}

export const CANDIDATE_01_ROSTER_BINDINGS: Readonly<Record<string, Array<{ campaignUnit: CampaignHero; levelUnitKey: string }>>> = Object.fromEntries(
  CANDIDATE_01_LEVELS.map((level) => [
    level.id,
    level.units.flatMap((unit) => unit.key?.startsWith('campaign-')
      ? [{ campaignUnit: unit.key.slice('campaign-'.length) as CampaignHero, levelUnitKey: unit.key }]
      : []),
  ]),
);

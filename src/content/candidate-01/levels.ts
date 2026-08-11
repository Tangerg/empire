import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../../core/resources';
import type {
  Coord,
  LevelData,
  LevelScenario,
  LevelStructure,
  LevelUnit,
  Objective,
  PlayerConfig,
} from '../../core/types';

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

const HILLS = [
  '....h....h....',
  '..T...v...T...',
  '.q..---....^..',
  '....h..T......',
  '...T.....h....',
  '......v.......',
  '..h.......T...',
  '....T..---.q..',
  '..^.....h.....',
  '....T.........',
];

const RIVER = [
  '....T..~~..h....',
  '..v....~~....T..',
  '....h..~~.......',
  '..T....~~..v....',
  '------====------',
  '....T..~~....h..',
  '..h....~~..T....',
  '.......~~.......',
  '.q..T..~~...T.q.',
  '....^..~~..^....',
];

const FORTRESS = [
  '....T..........^',
  '..h.......######',
  '......T..#....K#',
  '.q--------.....#',
  '....h....#..B..#',
  '..T......#.....#',
  '.....v...-..v..#',
  '..h......#.....#',
  '.........#######',
  '....T...........',
];

const BLACK_CAMP = [
  '...T....T.....',
  '.q...T......T.',
  '..T..---..T...',
  '.....v........',
  '.T......T...h.',
  '....B.........',
  '..h...T..v....',
  '....T.....T...',
  '..T....---..q.',
  '......^.......',
];

const BURNED_VILLAGE = [
  '...T..s.......',
  '.q..ssss..T...',
  '..s..v.s......',
  '.Tsssssss..h..',
  '..s.v.s.......',
  '...ssss..T....',
  '.h...s....v...',
  '...T....---...',
  '..s......T.q..',
  '....^.........',
];

const CITY = [
  'pppppppppppppp',
  'pq---p--p---qp',
  'pp#ppppppp#ppp',
  'pvpppBpppp#vpp',
  'pp#ppppppp#ppp',
  'ppppp---pppppp',
  'pp#ppppppp#ppp',
  'pvpppvpppp#Bpp',
  'ppp---pp---ppp',
  'pppppppppppppp',
];

const OATH_PRISON = [
  '...T...o...T..',
  '.q....ooo.....',
  '...T..o.o..h..',
  '.---ooqoo---..',
  '.....o.o......',
  '.h...ooo..T...',
  '......o.......',
  '..T......v....',
  '....---.....q.',
  '..^...........',
];

const GRAVEYARD = [
  'gggTggggggTggg',
  'gqgggoggggggqg',
  'ggTggooogTgggg',
  'ggggoogooggggg',
  'gTggoqoooggTgg',
  'ggggoogooggggg',
  'ggTggooogTgggg',
  'ggggggoggggggg',
  'gqggTggggTggqg',
  'gggggggggggggg',
];

const BELL_TOWER = [
  'rrr..~~~..rrr..',
  'rqr..~~~..rrrqr',
  'rrr..~=~..rrr..',
  '....r~=~r......',
  'rrrrr===rrrrrrr',
  '....r~=~r......',
  'rrr..~=~..rrr..',
  'rvr..~~~..rrrvv',
  'rrr..~~~..rrr..',
  '....h~~~h......',
];

const FORGE = [
  'ffffmmmmffffff',
  'fqffmffmffffqf',
  'ffffmffmffffff',
  'fff#mffm##ffff',
  'ffffmffmffffff',
  'ffff====ffffff',
  'ffffmffmffffff',
  'ffvfmffmffvfff',
  'ffffmmmmffffff',
  'ffffffffffffff',
];

const SILVERWOOD = [
  'TRTTRRTTRRTTRT',
  'TqTTRRTTRRTTqT',
  'TRRTTRRTTRRTTR',
  'TTRRThhTRRTTRT',
  'TRRTTRRTTRRTTR',
  'TTRRTvTRRTTRRT',
  'TRRTTRRTTRRTTR',
  'TTRRThhTRRTTRT',
  'TqTTRRTTRRTTqT',
  'TRTTRRTTRRTTRT',
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
  tasha: hero('tasha', 'c01.tasha'),
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

const baseParty = (...extra: UnitSeed[]): UnitSeed[] => [PARTY.laiya, PARTY.torren, PARTY.elin, ...extra];

const BATTLES: readonly BattleSpec[] = [
  {
    order: 1, chapter: 1, title: '双子丘陵', subtitle: '第一次指挥：夺取高地，也承担冒进的后果。', map: HILLS,
    player: baseParty(ally('field-cleric', 'c01.gravekeeper'), ally('roderick-reserve', 'c01.roderick')),
    enemy: [foe('vanguard-1', 'c01.legion-shield'), foe('vanguard-2', 'c01.swordsman'), foe('vanguard-archer', 'c01.archer'), foe('vanguard-cain', 'c01.cain')],
    objective: protectLaiya(route('击退维尔萨前锋'), '莱娅存活并击退维尔萨前锋'),
    owners: [{ x: 1, y: 2, owner: 1 }, { x: 11, y: 7, owner: 2 }, { x: 6, y: 1, owner: 0 }, { x: 6, y: 5, owner: 0 }],
    scenario: {
      zones: [zone('north-hill', { x: 4, y: 0 }, { x: 4, y: 3 })],
      triggers: [{
        id: 'matching-orders', timing: 'afterAction',
        condition: { type: 'eventCount', event: 'death', op: 'gte', value: 1 },
        effects: [{ type: 'emitSignal', signal: 'found_matching_orders' }],
      }],
    },
    extra: { tacticalTheme: '高地、支援与可信预测', recommendedTurns: 7 },
  },
  {
    order: 2, chapter: 1, title: '三桥河谷', subtitle: '短暂停火，把六军之争让给过桥的平民。', map: RIVER,
    player: baseParty(ally('bridge-knight', 'c01.knight'), ally('bridge-guard-1', 'c01.banner-guard'), ally('bridge-guard-2', 'c01.archer'), ally('refugee-1', 'c01.refugee'), ally('refugee-2', 'c01.refugee'), ally('refugee-3', 'c01.refugee')),
    enemy: [foe('cain', 'c01.cain'), foe('shield-1', 'c01.legion-shield'), foe('shield-2', 'c01.legion-shield'), foe('bow-1', 'c01.archer'), foe('unmarked-ballista', 'c01.ballista')],
    objective: protectLaiya({
      id: 'bridge-all', type: 'all', label: '护送平民并摧毁无标记弩车', objectives: [
        escort(['refugee-1', 'refugee-2', 'refugee-3'], 'east-safety', 3, '护送三名平民抵达东岸'),
        eliminateKey('unmarked-ballista', '摧毁向平民开火的无标记弩车'),
      ],
    }, '护送平民并阻止无标记弩车'),
    owners: [{ x: 1, y: 8, owner: 1 }, { x: 14, y: 8, owner: 2 }, { x: 2, y: 1, owner: 0 }, { x: 11, y: 3, owner: 0 }],
    scenario: {
      zones: [zone('middle-bridge', { x: 6, y: 4 }, { x: 7, y: 4 }, { x: 8, y: 4 }, { x: 9, y: 4 }), zone('east-safety', { x: 13, y: 1 }, { x: 14, y: 1 }, { x: 15, y: 1 })],
      engagementRules: [{ id: 'bridge-truce', zone: 'middle-bridge', mode: 'no-attacks', players: [1, 2] }],
      triggers: [{ id: 'truce-kept', timing: 'turnEnd', condition: { type: 'turnAtLeast', turn: 3 }, effects: [{ type: 'removeEngagementRule', id: 'bridge-truce' }, { type: 'emitSignal', signal: 'accepted_bridge_truce' }] }],
    },
    extra: { tacticalTheme: '护送、桥梁停火与远程封锁', recommendedTurns: 10 },
  },
  {
    order: 3, chapter: 1, title: '赤石围城', subtitle: '城堡可以夺下，被控制的名字却未必救得回来。', map: FORTRESS,
    player: baseParty(ally('roderick', 'c01.roderick'), ally('siege-mage', 'c01.battle-mage'), ally('siege-engineer', 'c01.engineer'), ally('siege-guard', 'c01.banner-guard'), ally('field-cleric', 'c01.gravekeeper')),
    enemy: [foe('oath-guard-1', 'c01.skeleton-guard'), foe('oath-guard-2', 'c01.skeleton-guard'), foe('redstone-ballista', 'c01.ballista'), foe('redstone-shield', 'c01.legion-shield'), foe('redstone-inquisitor', 'c01.inquisitor')],
    objective: protectLaiya({
      id: 'redstone-path', type: 'any', label: '夺城或关闭誓文节点', objectives: [
        { id: 'disable-node', type: 'escort', selector: { keys: ['campaign-laiya', 'siege-engineer'] }, zone: 'oath-node', count: 1, label: '深入后方关闭控制节点' },
        { id: 'capture-redstone', type: 'captureHQ', label: '强攻并占领赤石城堡' },
      ],
    }, '在十四回合内夺取赤石或关闭控制节点'),
    owners: [{ x: 1, y: 3, owner: 1 }, { x: 14, y: 2, owner: 2 }, { x: 12, y: 4, owner: 2 }, { x: 12, y: 6, owner: 2 }],
    structures: [{ id: 'redstone-node', type: 'command_node', owner: 2, x: 14, y: 6 }],
    scenario: {
      zones: [zone('oath-node', { x: 14, y: 6 })],
      triggers: [{
        id: 'disable-redstone', timing: 'afterAction', condition: { type: 'unitInZone', zone: 'oath-node', owner: 1 },
        effects: [
          { type: 'changeUnitOwner', selector: { anyTags: ['oathbound'] }, owner: 1 },
          { type: 'damageStructure', id: 'redstone-node', amount: 100 },
          { type: 'emitSignal', signal: 'redstone_node_disabled' },
        ],
      }],
    },
    turnLimit: 14, aggression: 0.32,
    extra: { tacticalTheme: '攻城、最小射程与非致命解法', recommendedTurns: 11 },
  },
  {
    order: 4, chapter: 1, title: '黑旗营地', subtitle: '侦察命令结束之处，是救人和取证必须开始的地方。', map: BLACK_CAMP,
    player: baseParty(PARTY.mirelle, ally('scout-1', 'c01.swordsman'), ally('scout-2', 'c01.banner-guard'), ally('scout-3', 'c01.archer')),
    enemy: [foe('camp-guard-1', 'c01.swordsman'), foe('camp-guard-2', 'c01.legion-shield'), foe('camp-inquisitor', 'c01.inquisitor'), foe('camp-bow', 'c01.archer'), foe('camp-mage', 'c01.battle-mage')],
    objective: protectLaiya({
      id: 'black-camp-sequence', type: 'sequence', label: '调查营帐后带证据撤离', objectives: [
        score('evidence', 3, '调查三处营帐'),
        escort(['campaign-laiya', 'campaign-mirelle'], 'west-exit', 2, '莱娅与米蕾尔携证据撤离'),
      ],
    }, '调查黑旗营地并带证人撤离'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 8, owner: 2 }, { x: 4, y: 5, owner: 2 }],
    scenario: {
      variables: { evidence: 0 },
      zones: [zone('tent-a', { x: 6, y: 3 }), zone('tent-b', { x: 5, y: 5 }), zone('tent-c', { x: 9, y: 6 }), zone('west-exit', { x: 0, y: 1 }, { x: 0, y: 2 }, { x: 0, y: 3 })],
      triggers: [
        visitTrigger('inspect-a', 'tent-a', 'evidence', 'found_prisoners'),
        visitTrigger('inspect-b', 'tent-b', 'evidence', 'found_oath_template'),
        visitTrigger('inspect-c', 'tent-c', 'evidence', 'saved_witnesses'),
        {
          id: 'withdraw-with-evidence', timing: 'afterAction',
          condition: { type: 'variable', key: 'evidence', op: 'gte', value: 3 },
          effects: [{ type: 'setUnitDirective', selector: { keys: ['campaign-laiya', 'campaign-mirelle'] }, directive: { mode: 'retreat', waypoints: [{ x: 0, y: 2 }] } }],
        },
      ],
    },
    rules: undefined,
    extra: { tacticalTheme: '有限视野、调查与有序撤离', recommendedTurns: 10 },
  } as BattleSpec,
  {
    order: 5, chapter: 1, title: '焚村令', subtitle: '保护苇草村；灰旗第一次不是军令，而是一项选择。', map: BURNED_VILLAGE,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('village-guard', 'c01.banner-guard'), ally('village-guard-2', 'c01.banner-guard'), ally('villager-1', 'c01.refugee'), ally('villager-2', 'c01.refugee')),
    enemy: [foe('purifier-1', 'c01.inquisitor'), foe('purifier-2', 'c01.templar'), foe('purifier-3', 'c01.legion-shield'), foe('purifier-4', 'c01.archer'), foe('purifier-5', 'c01.knight')],
    objective: protectLaiya({
      id: 'save-village', type: 'all', label: '保护村民直至净化军撤退', objectives: [
        { id: 'protect-villagers', type: 'protect', selector: { anyTags: ['civilian'] }, minimumAlive: 2, untilTurn: 7, label: '至少两名村民存活' },
        { id: 'hold-seven', type: 'surviveTurns', turns: 7, label: '坚守七回合' },
      ],
    }, '拒绝焚村令并保护苇草村'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 11, y: 8, owner: 2 }, { x: 5, y: 2, owner: 1 }, { x: 4, y: 4, owner: 1 }, { x: 10, y: 6, owner: 1 }],
    scenario: {
      zones: [zone('east-road', { x: 12, y: 7 }, { x: 13, y: 7 })],
      triggers: [{
        id: 'purifier-reinforcement', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 4 },
        effects: [
          { type: 'spawnUnits', reason: 'reinforcement', units: [{ key: 'purifier-reinforcement', x: 13, y: 7, unit: 'c01.inquisitor', owner: 2 }], ready: true },
          { type: 'emitSignal', signal: 'gray_banner_raised' },
        ],
      }],
    },
    turnLimit: 9, aggression: 0.68,
    extra: { tacticalTheme: '据点防守、平民保护与增援', recommendedTurns: 8, chapterFinale: true },
  },
  {
    order: 6, chapter: 2, title: '白河夜渡', subtitle: '人、粮车和后卫，没有一项损失会在结算画面外消失。', map: RIVER,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('rearguard', 'c01.banner-guard'), ally('rearguard-2', 'c01.banner-guard'), ally('refugee-1', 'c01.refugee'), ally('refugee-2', 'c01.refugee'), ally('refugee-3', 'c01.refugee'), ally('refugee-4', 'c01.laborer')),
    enemy: [foe('pursuer-1', 'c01.knight'), foe('pursuer-2', 'c01.knight'), foe('pursuer-3', 'c01.archer'), foe('pursuer-4', 'c01.swordsman'), foe('pursuer-ballista', 'c01.ballista')],
    objective: protectLaiya(escort(['refugee-1', 'refugee-2', 'refugee-3', 'refugee-4'], 'north-bank', 4, '护送四名非战斗人员渡河'), '在八回合内完成白河撤离'),
    scenario: { zones: [zone('north-bank', { x: 13, y: 0 }, { x: 14, y: 0 }, { x: 15, y: 0 })], triggers: [{ id: 'bridge-damaged', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 5 }, effects: [{ type: 'emitSignal', signal: 'white_river_bridge_damaged' }] }] },
    turnLimit: 9, aggression: 0.65,
    extra: { tacticalTheme: '多单位护送、后卫与时间压力', recommendedTurns: 8 },
  },
  {
    order: 7, chapter: 2, title: '无主之城', subtitle: '城墙属于谁，要由谁先得到保护来回答。', map: CITY,
    player: baseParty(PARTY.mirelle, PARTY.bran, ally('militia', 'c01.banner-guard'), ally('militia-2', 'c01.banner-guard'), ally('engineer', 'c01.engineer')),
    enemy: [foe('raider-1', 'c01.wolf-rider'), foe('raider-2', 'c01.wolf-rider'), foe('raider-3', 'c01.swordsman'), foe('royal-1', 'c01.knight'), foe('royal-2', 'c01.archer'), foe('royal-3', 'c01.legion-shield')],
    objective: protectLaiya({ id: 'city-control', type: 'all', label: '夺回粮仓与民兵营', objectives: [
      { id: 'granary', type: 'control', zone: 'granary', label: '控制联合粮仓' },
      { id: 'militia-barracks', type: 'control', zone: 'militia-barracks', label: '控制民兵营' },
    ] }, '在两路敌军夹击下稳定洛岬'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 1, y: 3, owner: 0 }, { x: 5, y: 3, owner: 0 }, { x: 11, y: 3, owner: 0 }, { x: 1, y: 7, owner: 0 }, { x: 5, y: 7, owner: 0 }, { x: 11, y: 7, owner: 0 }],
    scenario: { zones: [zone('granary', { x: 1, y: 3 }), zone('militia-barracks', { x: 5, y: 3 })] },
    extra: { tacticalTheme: '多方向防御、占领与城市走廊', recommendedTurns: 9 },
  },
  {
    order: 8, chapter: 2, title: '佣兵之价', subtitle: '名字、数目、日期——先结清死人留下的账。', map: CITY,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, ally('mercenary-guard', 'c01.banner-guard')),
    enemy: [foe('thief-chief', 'c01.wolf-rider'), foe('thief-1', 'c01.swordsman'), foe('thief-2', 'c01.archer'), foe('thief-3', 'c01.wolf-rider'), foe('fraud-enforcer', 'c01.inquisitor')],
    objective: protectLaiya({
      id: 'protect-tasha', type: 'failOn', label: '保护塔莎并恢复粮道',
      condition: { type: 'unitCount', selector: { keys: ['campaign-tasha'] }, op: 'lte', value: 0 },
      objective: route('击退抢粮者并保护三座粮仓'),
    }, '承认欠款并与塔莎共同保护粮道'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 1, y: 3, owner: 1 }, { x: 11, y: 3, owner: 1 }, { x: 1, y: 7, owner: 1 }],
    scenario: { zones: [zone('ledger', { x: 7, y: 5 })], triggers: [visitTrigger('recover-ledger', 'ledger', 'ledgers', 'mercenary_dead_paid')] , variables: { ledgers: 0 } },
    extra: { tacticalTheme: '三角接敌、工兵爆破与契约同盟', recommendedTurns: 8 },
  },
  {
    order: 9, chapter: 2, title: '笼中之火', subtitle: '拆掉锁链，不用新的命令替代旧的命令。', map: OATH_PRISON,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, ally('chain-breaker', 'c01.engineer')),
    enemy: [foe('escort-1', 'c01.inquisitor'), foe('escort-2', 'c01.legion-shield'), foe('escort-3', 'c01.knight'), foe('escort-4', 'c01.archer'), foe('escort-mage', 'c01.battle-mage')],
    neutral: [{ key: 'caged-ivra', unit: 'c01.ivra', owner: 3, x: 7, y: 3, directive: { mode: 'guard' } }],
    objective: protectLaiya({ id: 'break-chains', type: 'neutralizeComposite', composite: 'ivra-chains', minimumNeutralized: 3, label: '破坏三处锁链节点' }, '不用控制誓文释放伊芙拉'),
    structures: [
      { id: 'chain-north', type: 'command_node', owner: 2, x: 6, y: 2 }, { id: 'chain-east', type: 'command_node', owner: 2, x: 9, y: 3 },
      { id: 'chain-south', type: 'command_node', owner: 2, x: 7, y: 5 }, { id: 'chain-west', type: 'command_node', owner: 2, x: 5, y: 3 },
    ],
    composites: [{ id: 'ivra-chains', parts: ['chain-north', 'chain-east', 'chain-south', 'chain-west'], minimumNeutralized: 3, tags: ['prison', 'oathbound'] }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 8, owner: 2 }],
    extra: { tacticalTheme: '复合目标、路径分兵与非控制解法', recommendedTurns: 9 },
  },
  {
    order: 10, chapter: 2, title: '旧旗下的追兵', subtitle: '导师站在旧旗之下；活着离开，比赢下争论更重要。', map: HILLS,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra),
    enemy: [foe('roderick', 'c01.roderick'), foe('royal-guard-1', 'c01.knight'), foe('royal-guard-2', 'c01.legion-shield'), foe('royal-bow', 'c01.archer'), foe('royal-mage', 'c01.battle-mage')],
    objective: protectLaiya(escort(['campaign-laiya'], 'east-pass', 1, '莱娅抵达东部山口'), '不与旧部死战，带灰旗军脱离包围'),
    scenario: { zones: [zone('east-pass', { x: 12, y: 0 }, { x: 13, y: 0 }, { x: 13, y: 1 })], triggers: [{ id: 'ivra-intervenes', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 5 }, effects: [{ type: 'changeMorale', selector: { owner: 2 }, amount: -18, reason: '幼龙截断追兵' }, { type: 'emitSignal', signal: 'ivra_chose_to_help' }] }] },
    aggression: 0.48,
    extra: { tacticalTheme: '突破包围、士气与有限交战', recommendedTurns: 8, chapterFinale: true },
  },
  {
    order: 11, chapter: 3, title: '无声墓园', subtitle: '同一名士兵反复死亡，直到有人读出墓碑上的名字。', map: GRAVEYARD,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra),
    enemy: [foe('colossus', 'c01.cemetery-colossus'), foe('echo-1', 'c01.skeleton-guard'), foe('echo-2', 'c01.skeleton-guard'), foe('echo-3', 'c01.ghost'), foe('echo-4', 'c01.ghost')],
    objective: protectLaiya({ id: 'grave-sequence', type: 'sequence', label: '归还三段真名后击败巨像', objectives: [score('names', 3, '读取三块拒绝净化的墓碑'), eliminateKey('colossus', '让墓园巨像安息')] }, '记录真名并终止墓园循环'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 1, y: 8, owner: 1 }, { x: 12, y: 8, owner: 2 }, { x: 5, y: 4, owner: 2 }],
    scenario: { variables: { names: 0 }, zones: [zone('name-a', { x: 4, y: 2 }), zone('name-b', { x: 9, y: 4 }), zone('name-c', { x: 4, y: 7 })], triggers: [visitTrigger('name-a', 'name-a', 'names', 'named_old_soldier'), visitTrigger('name-b', 'name-b', 'names', 'named_border_child'), visitTrigger('name-c', 'name-c', 'names', 'named_royal_deserter')] },
    extra: { tacticalTheme: '阶段目标、亡灵克制与大型单位', recommendedTurns: 10 },
  },
  {
    order: 12, chapter: 3, title: '沉没钟塔', subtitle: '河水上涨时，暂时共同控制比抢走碎片更难。', map: BELL_TOWER,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra),
    enemy: [foe('imperial-cain', 'c01.cain'), foe('imperial-1', 'c01.legion-shield'), foe('imperial-2', 'c01.battle-mage'), foe('oath-echo-1', 'c01.skeleton-guard'), foe('oath-echo-2', 'c01.ghost')],
    objective: protectLaiya({ id: 'bell-mechanism', type: 'neutralizeComposite', composite: 'bell-controls', minimumNeutralized: 2, label: '关闭两处失控钟塔机构' }, '在塔底崩塌前关闭钟塔机构'),
    structures: [
      { id: 'bell-a', type: 'boss_part', owner: 2, x: 6, y: 2 }, { id: 'bell-b', type: 'boss_part', owner: 2, x: 9, y: 4 }, { id: 'bell-c', type: 'boss_part', owner: 2, x: 6, y: 6 },
    ],
    composites: [{ id: 'bell-controls', parts: ['bell-a', 'bell-b', 'bell-c'], minimumNeutralized: 2, tags: ['tower', 'flood-control'] }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 13, y: 1, owner: 2 }, { x: 1, y: 7, owner: 0 }, { x: 13, y: 7, owner: 0 }],
    scenario: { triggers: [{ id: 'water-rises', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 6 }, effects: [{ type: 'emitSignal', signal: 'bell_tower_flood_rising' }] }] },
    turnLimit: 11,
    extra: { tacticalTheme: '复合设施、狭道与临时共同目标', recommendedTurns: 9 },
  },
  {
    order: 13, chapter: 3, title: '圣城档案', subtitle: '把被删除的战争带出白塔，让档案仍然留下修改痕迹。', map: CITY,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('archive-guard', 'c01.banner-guard')),
    enemy: [foe('archive-inquisitor', 'c01.inquisitor'), foe('archive-guard-1', 'c01.legion-shield'), foe('archive-guard-2', 'c01.legion-shield'), foe('archive-mage', 'c01.battle-mage'), foe('archive-knight', 'c01.knight')],
    objective: protectLaiya({ id: 'archive-sequence', type: 'sequence', label: '取得三份档案并护送米蕾尔离开', objectives: [score('archives', 3, '读取三组被删除的档案'), escort(['campaign-mirelle'], 'archive-exit', 1, '米蕾尔携档案撤离')] }, '让米蕾尔把战争档案带出圣城'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 5, y: 3, owner: 2 }, { x: 11, y: 7, owner: 2 }],
    scenario: { variables: { archives: 0 }, zones: [zone('archive-a', { x: 5, y: 3 }), zone('archive-b', { x: 5, y: 7 }), zone('archive-c', { x: 11, y: 7 }), zone('archive-exit', { x: 13, y: 8 }, { x: 13, y: 9 })], triggers: [visitTrigger('archive-a', 'archive-a', 'archives', 'found_deleted_war'), visitTrigger('archive-b', 'archive-b', 'archives', 'found_matching_orders'), visitTrigger('archive-c', 'archive-c', 'archives', 'mirelle_kept_revision_marks')] },
    extra: { tacticalTheme: '资料点、护送与警戒爆发', recommendedTurns: 10 },
  },
  {
    order: 14, chapter: 3, title: '山炉余烬', subtitle: '技术不是无罪的借口，修好炉心也不是替祖先赎罪。', map: FORGE,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('forge-laborer', 'c01.laborer'), ally('forge-guard', 'c01.banner-guard')),
    enemy: [foe('golem-1', 'c01.stone-golem'), foe('golem-2', 'c01.stone-golem'), foe('rune-rebel-1', 'c01.rune-shield'), foe('rune-rebel-2', 'c01.rune-artificer'), foe('forge-mage', 'c01.battle-mage')],
    objective: protectLaiya({ id: 'forge-all', type: 'all', label: '关闭熔流阀并平息失控守卫', objectives: [score('valves', 2, '关闭两处熔流阀'), route('平息失控的山炉守卫')] }, '阻止炉心爆炸并留下完整技术记录'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 2, y: 7, owner: 0 }, { x: 10, y: 7, owner: 0 }],
    scenario: { variables: { valves: 0 }, zones: [zone('valve-a', { x: 4, y: 5 }), zone('valve-b', { x: 7, y: 5 })], triggers: [visitTrigger('valve-a', 'valve-a', 'valves', 'forge_valve_a_closed'), visitTrigger('valve-b', 'valve-b', 'valves', 'forge_valve_b_closed')] },
    extra: { tacticalTheme: '熔流障碍、强制位移与符文重甲', recommendedTurns: 10 },
  },
  {
    order: 15, chapter: 3, title: '银林长梦', subtitle: '森林延续的代价，不能继续转嫁给林外城市。', map: SILVERWOOD,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra, ally('silverwood-guard', 'c01.banner-guard')),
    enemy: [foe('controlled-druid', 'c01.druid'), foe('controlled-longbow-1', 'c01.silver-longbow'), foe('controlled-longbow-2', 'c01.silver-longbow'), foe('controlled-walker', 'c01.woodland-walker'), foe('inquisitor-handler', 'c01.inquisitor')],
    neutral: [{ key: 'silverwood-witness', unit: 'c01.druid', owner: 3, x: 5, y: 5, directive: { mode: 'guard' } }],
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
    structures: [{ id: 'mother-root', type: 'c01.mother-root', owner: 3, x: 6, y: 5, hp: 500 }],
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 1, y: 8, owner: 0 }, { x: 12, y: 8, owner: 0 }, { x: 5, y: 5, owner: 0 }],
    extra: { tacticalTheme: '森林视线、远射对空与目标保护', recommendedTurns: 9 },
  },
  {
    order: 16, chapter: 3, title: '记住我的名字', subtitle: '无旗者不是需要被消灭的怪物，而是需要被听见的新意识。', map: GRAVEYARD,
    player: baseParty(PARTY.mirelle, PARTY.bran, PARTY.tasha, PARTY.ivra),
    enemy: [foe('unflagged', 'c01.cemetery-colossus'), foe('memory-1', 'c01.ghost'), foe('memory-2', 'c01.ghost'), foe('memory-3', 'c01.skeleton-guard'), foe('memory-inquisitor', 'c01.inquisitor')],
    objective: protectLaiya({ id: 'true-name-all', type: 'all', label: '确认三个真名并终止聚忆暴走', objectives: [score('true_names', 3, '让三个真名得到回应'), eliminateKey('memory-inquisitor', '击退继续抽取记忆的审判官')] }, '让无旗者保有名字与选择'),
    owners: [{ x: 1, y: 1, owner: 1 }, { x: 12, y: 1, owner: 2 }, { x: 1, y: 8, owner: 1 }, { x: 12, y: 8, owner: 2 }, { x: 5, y: 4, owner: 2 }],
    scenario: { variables: { true_names: 0 }, zones: [zone('truth-a', { x: 4, y: 2 }), zone('truth-b', { x: 9, y: 4 }), zone('truth-c', { x: 4, y: 7 })], triggers: [visitTrigger('truth-a', 'truth-a', 'true_names', 'unflagged_name_one'), visitTrigger('truth-b', 'truth-b', 'true_names', 'unflagged_name_two'), visitTrigger('truth-c', 'truth-c', 'true_names', 'unflagged_name_three')] },
    extra: { tacticalTheme: '非歼灭终局、真名互动与多阵营压力', recommendedTurns: 11, chapterFinale: true },
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
    players.push({ id: 3, name: '中立方', team: 1, color: '#b8a57a', controller: 'ai', resources: resources(0), ai: { aggression: 0 }, objectives: [{ id: 'neutral-survival', type: 'surviveTurns', turns: spec.turnLimit ?? 30 }] });
  }
  return players;
}

const playerSlots = (height: number): Coord[] => [
  { x: 1, y: Math.min(4, height - 2) }, { x: 2, y: 3 }, { x: 2, y: 5 }, { x: 1, y: 2 },
  { x: 1, y: 6 }, { x: 2, y: 1 }, { x: 2, y: 7 }, { x: 1, y: 8 }, { x: 3, y: 4 }, { x: 3, y: 6 },
  { x: 3, y: 2 }, { x: 3, y: 7 },
];

const enemySlots = (width: number, height: number): Coord[] => [
  { x: width - 2, y: Math.min(4, height - 2) }, { x: width - 3, y: 3 }, { x: width - 3, y: 5 }, { x: width - 2, y: 2 },
  { x: width - 2, y: 6 }, { x: width - 3, y: 1 }, { x: width - 3, y: 7 }, { x: width - 2, y: 8 }, { x: width - 4, y: 4 }, { x: width - 4, y: 6 },
];

function place(seeds: readonly UnitSeed[], slots: readonly Coord[], owner: number): LevelUnit[] {
  return seeds.map((seed, index) => ({
    key: seed.key,
    x: seed.x ?? slots[index % slots.length].x,
    y: seed.y ?? slots[index % slots.length].y,
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
    ...place(spec.player, playerSlots(height), 1),
    ...place(spec.enemy, enemySlots(width, height), 2),
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
    level.units
      .filter((unit) => unit.key?.startsWith('campaign-'))
      .map((unit) => ({ campaignUnit: unit.key!.slice('campaign-'.length) as CampaignHero, levelUnitKey: unit.key! })),
  ]),
);

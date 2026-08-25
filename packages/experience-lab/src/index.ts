import {
  type Coord,
  type LevelData,
  type LevelUnit,
  type Objective,
  type PlayerConfig,
  COMMAND_POINTS_RESOURCE,
  FUNDS_RESOURCE,
} from '@empire/battle-engine';

const TERRAIN = [
  'TTTTTTTTTTTTTTTTTTTTTTTTTTTTT',
  'TTT...T......TT......T..TTTTT',
  'TT...T.^^^^...T........T..TTT',
  'Tq-----^^^^h...............TT',
  'TT..T.-^^^^h.T..hhh...T...TTT',
  'T.....-^^^^.......hh.......TT',
  'TT..T.-------..T........T..TT',
  'T.....-.....-..............TT',
  'T-----------v-v------------TT',
  'T..-..........-..........-.TT',
  'TT.-.hTh......--------...-.TT',
  'T..-......-.....h^^^^----q.TT',
  'TT.-...T..-hh.T.h^^^^h.T..TTT',
  'T..--------h....h^^^^h.....TT',
  'TT...T........T..hhhh..T...TT',
  'TTT.....T......T.....T..TT.TT',
  'TTTTTTTTTTTTTTTTTTTTTTTTTTTTT',
] as const;

const elevation = TERRAIN.flatMap((row) => [...row].map((cell) => cell === '^' ? 2 : cell === 'h' ? 1 : 0));
const resources = (funds: number) => ({
  [FUNDS_RESOURCE]: { current: funds, capacity: null },
  [COMMAND_POINTS_RESOURCE]: { current: 2, capacity: 7 },
});
const unit = (key: string, unitType: string, owner: number, x: number, y: number, facing: LevelUnit['facing']): LevelUnit => ({
  key, unit: unitType, owner, x, y, facing,
});
const zone = (id: string, cells: Coord[]) => ({ id, cells });

const mission: Objective = {
  id: 'trial-mission',
  type: 'all',
  label: '守住莱娅、夺取中枢并击退敌军统帅',
  objectives: [
    {
      id: 'protect-laiya', type: 'protect', label: '莱娅必须存活',
      selector: { keys: ['campaign-laiya'] }, minimumAlive: 1, untilTurn: 14,
    },
    { id: 'hold-crossroads', type: 'control', label: '控制中央誓文中枢', zone: 'central-objective' },
    { id: 'defeat-cain', type: 'eliminate', label: '击退凯恩', selector: { keys: ['enemy-cain'] } },
  ],
};

const players: PlayerConfig[] = [
  { id: 1, name: '灰旗联队', team: 1, color: '#4d86b8', controller: 'human', resources: resources(480), objectives: [mission] },
  { id: 2, name: '王冠试炼军', team: 2, color: '#a8493f', controller: 'ai', resources: resources(360), ai: { aggression: 0.58 }, objectives: [{ type: 'routEnemies', label: '击溃灰旗联队' }] },
  { id: 3, name: '银林观察团', team: 1, color: '#86a96b', controller: 'ai', resources: resources(0), ai: { aggression: 0.18 }, objectives: [structuredClone(mission)] },
];

/**
 * A deliberately broad, authored vertical slice. It combines mechanics rather
 * than introducing lab-only rules, so every result feeds back into production.
 */
const SUPER_EXPERIENCE_LEVEL: LevelData = {
  schema: 2,
  id: 'experience-lab-gray-banner-trial',
  name: '灰旗试炼 · 三线合围',
  author: 'Empire SRPG Experience Lab',
  description: '在开阔丘陵、村道与中央誓文阵地之间调度三支小队。高地、掩体、援护、指挥、士气、增援与多目标会在一场战斗中逐步展开。',
  width: TERRAIN[0].length,
  height: TERRAIN.length,
  terrain: [...TERRAIN],
  elevation,
  cliffs: [
    { from: { x: 7, y: 6 }, to: { x: 7, y: 5 } },
    { from: { x: 8, y: 6 }, to: { x: 8, y: 5 } },
    { from: { x: 17, y: 10 }, to: { x: 17, y: 11 } },
    { from: { x: 18, y: 10 }, to: { x: 18, y: 11 } },
  ],
  directionalCover: [
    { at: { x: 12, y: 7 }, sides: { east: 'half', south: 'half' } },
    { at: { x: 14, y: 7 }, sides: { west: 'half', south: 'half' } },
    { at: { x: 19, y: 9 }, sides: { west: 'full' } },
    { at: { x: 6, y: 10 }, sides: { east: 'half' } },
  ],
  owners: [
    { x: 1, y: 3, owner: 1 }, { x: 25, y: 11, owner: 2 },
    { x: 12, y: 8, owner: 0 }, { x: 14, y: 8, owner: 0 },
  ],
  units: [
    unit('campaign-laiya', 'c01.laiya', 1, 2, 9, 'east'),
    unit('gray-guard', 'c01.banner-guard', 1, 3, 8, 'east'),
    unit('gray-sword', 'c01.swordsman', 1, 3, 10, 'east'),
    unit('gray-archer', 'c01.archer', 1, 5, 6, 'east'),
    unit('gray-mirelle', 'c01.mirelle', 1, 4, 11, 'east'),
    unit('gray-tasha', 'c01.tasha', 1, 6, 12, 'east'),
    unit('gray-knight', 'c01.knight', 1, 4, 13, 'east'),
    unit('gray-wolf', 'c01.wolf-rider', 1, 7, 14, 'east'),
    unit('gray-ivra', 'c01.ivra', 1, 8, 12, 'east'),
    unit('silver-longbow', 'c01.silver-longbow', 3, 8, 3, 'south'),
    unit('silver-druid', 'c01.druid', 3, 9, 4, 'south'),
    unit('enemy-cain', 'c01.cain', 2, 23, 4, 'west'),
    unit('enemy-shield-a', 'c01.legion-shield', 2, 21, 5, 'west'),
    unit('enemy-shield-b', 'c01.legion-shield', 2, 21, 9, 'west'),
    unit('enemy-templar', 'c01.templar', 2, 24, 8, 'west'),
    unit('enemy-inquisitor', 'c01.inquisitor', 2, 19, 7, 'west'),
    unit('enemy-ballista', 'c01.ballista', 2, 23, 12, 'west'),
    unit('enemy-mage', 'c01.battle-mage', 2, 18, 13, 'west'),
    unit('enemy-golem', 'c01.stone-golem', 2, 16, 8, 'west'),
    unit('enemy-ghost', 'c01.ghost', 2, 17, 4, 'west'),
    unit('enemy-bow', 'c01.archer', 2, 22, 3, 'west'),
  ],
  commanders: [
    { id: 'gray-command', unitKey: 'campaign-laiya', radius: 3, aura: { defenseDelta: 0.04 }, turnGrants: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }], tactics: ['c01.gray-rally', 'c01.hold-the-line'] },
    { id: 'crown-command', unitKey: 'enemy-cain', radius: 2, aura: { attackMultiplier: 1.05 }, turnGrants: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }], tactics: ['rally'] },
  ],
  structures: [
    { id: 'central-node', type: 'command_node', owner: 2, x: 13, y: 8 },
    { id: 'east-gate', type: 'gate', owner: 2, x: 20, y: 8 },
    { id: 'supply-depot', type: 'depot', owner: 2, x: 23, y: 10 },
    { id: 'silver-root', type: 'c01.mother-root', owner: 3, x: 8, y: 4, hp: 500 },
  ],
  players,
  rules: {
    turnLimit: 18,
    moraleEnabled: true,
    captureMode: 'progressive',
    captureThreshold: 100,
    highGroundDamageMultiplier: 1.14,
    flankAttackMultiplier: 1.18,
    baseResourceGrants: [],
  },
  victory: [mission],
  scenario: {
    variables: { shrine_claimed: 0 },
    zones: [
      zone('central-objective', [{ x: 12, y: 8 }, { x: 13, y: 8 }, { x: 14, y: 8 }]),
      zone('oath-shrine', [{ x: 9, y: 3 }]),
      zone('east-courtyard', [{ x: 20, y: 7 }, { x: 20, y: 8 }, { x: 20, y: 9 }, { x: 21, y: 8 }]),
      zone('west-deployment', [
        { x: 2, y: 9 }, { x: 3, y: 8 }, { x: 3, y: 10 }, { x: 4, y: 11 },
        { x: 5, y: 6 }, { x: 6, y: 12 }, { x: 4, y: 13 }, { x: 7, y: 14 }, { x: 8, y: 12 },
      ]),
    ],
    overlays: [{ id: 'opening-smoke', type: 'signal_storm', zone: 'east-courtyard', remainingRounds: 3 }],
    triggers: [
      {
        id: 'claim-oath-shrine', timing: 'afterAction',
        condition: { type: 'unitInZone', zone: 'oath-shrine', owner: 1 },
        effects: [
          { type: 'setVariable', key: 'shrine_claimed', value: 1 },
          { type: 'addStatus', selector: { owner: 1 }, status: 'inspired', duration: 2 },
          { type: 'emitSignal', signal: 'experience_shrine_claimed' },
        ],
      },
      {
        id: 'crown-reinforcements', timing: 'turnStart',
        condition: { type: 'turnAtLeast', turn: 3 },
        effects: [{ type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
          unit('crown-wave-spear', 'c01.legion-shield', 2, 25, 6, 'west'),
          unit('crown-wave-bow', 'c01.archer', 2, 25, 8, 'west'),
          unit('crown-wave-rider', 'c01.knight', 2, 24, 14, 'west'),
        ] }],
      },
      {
        id: 'gray-reinforcements', timing: 'turnStart',
        condition: { type: 'turnAtLeast', turn: 4 },
        effects: [{ type: 'spawnUnits', reason: 'reinforcement', ready: true, units: [
          unit('gray-wave-engineer', 'c01.engineer', 1, 1, 12, 'east'),
          unit('gray-wave-gravekeeper', 'c01.gravekeeper', 1, 2, 13, 'east'),
        ] }],
      },
      {
        id: 'central-node-falls', timing: 'afterAction',
        condition: { type: 'structure', id: 'central-node', state: 'destroyed' },
        effects: [
          { type: 'addOverlay', id: 'broken-oath-fire', overlay: 'fire_field', zone: 'east-courtyard', rounds: 3 },
          { type: 'changeMorale', selector: { owner: 2 }, amount: -18, reason: 'central-node-destroyed' },
          { type: 'emitSignal', signal: 'experience_central_node_destroyed' },
        ],
      },
    ],
  },
  deployment: { zones: [{ player: 1, zone: 'west-deployment', unitKeys: ['campaign-laiya', 'gray-guard', 'gray-sword', 'gray-archer', 'gray-mirelle', 'gray-tasha', 'gray-knight', 'gray-wolf', 'gray-ivra'] }] },
  extra: {
    package: '@empire/experience-lab',
    purpose: 'seed-player-vertical-slice',
    aestheticPromise: '在压力下组织一支彼此依赖、仍保有选择的队伍',
    fronts: 3,
    recommendedTurns: 14,
  },
};

export function experienceLevel(): LevelData {
  return structuredClone(SUPER_EXPERIENCE_LEVEL);
}

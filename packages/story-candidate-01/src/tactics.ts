import { COMMAND_POINTS_RESOURCE, defineStatus, type StatusDef, type TacticDef } from '@empire/battle-engine';

export const CANDIDATE_01_STATUSES: readonly StatusDef[] = [
  defineStatus({
    id: 'c01.oath-controlled',
    name: '誓文控制',
    modifiers: { attackMultiplier: 0.9, defenseDelta: -0.05 },
    blockedAbilityTags: ['command'],
    tags: ['control', 'debuff', 'oathbound'],
  }),
];

export const CANDIDATE_01_TACTICS: readonly TacticDef[] = [
  {
    id: 'c01.gray-rally',
    name: '灰旗集结',
    costs: [{ resource: COMMAND_POINTS_RESOURCE, amount: 2 }],
    range: 3,
    radius: 2,
    target: 'tile',
    effects: [
      { type: 'removeStatus', status: 'shaken' },
      { type: 'addStatus', status: 'inspired', duration: 2 },
    ],
    tags: ['morale', 'command', 'gray-banner'],
  },
  {
    id: 'c01.hold-the-line',
    name: '守住退路',
    costs: [{ resource: COMMAND_POINTS_RESOURCE, amount: 2 }],
    range: 2,
    radius: 1,
    target: 'tile',
    effects: [{ type: 'removeStatus', status: 'shaken' }],
    tags: ['morale', 'defensive'],
  },
  {
    id: 'c01.name-the-dead',
    name: '归还真名',
    costs: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }],
    range: 3,
    radius: 1,
    target: 'tile',
    effects: [{ type: 'removeStatus', status: 'c01.oath-controlled' }],
    tags: ['arcane', 'recovery'],
  },
];

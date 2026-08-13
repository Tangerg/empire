import { COMMAND_POINTS_RESOURCE, type TacticDef } from '@empire/battle-engine';

export const COMMON_TACTICS: readonly TacticDef[] = [
  {
    id: 'rally',
    name: '集结号令',
    costs: [{ resource: COMMAND_POINTS_RESOURCE, amount: 2 }],
    range: 3,
    radius: 1,
    target: 'tile',
    effects: [{ type: 'addStatus', status: 'inspired', duration: 2 }],
    tags: ['morale', 'formation'],
  },
  {
    id: 'steady',
    name: '稳住阵线',
    costs: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }],
    range: 2,
    radius: 1,
    target: 'tile',
    effects: [{ type: 'removeStatus', status: 'shaken' }],
    tags: ['morale', 'recovery'],
  },
];

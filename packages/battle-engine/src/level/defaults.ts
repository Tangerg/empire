import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../resources';
import { DEFAULT_RULES } from '../types';
import type { LevelData, Objective, PlayerConfig, RuleSet } from '../types';

/** Falls back to "rout them or take their keep" when a level states no goal. */
export const DEFAULT_VICTORY: Objective[] = [{ type: 'routEnemies' }, { type: 'captureHQ' }];

function defaultPlayer(
  id: number,
  name: string,
  color: string,
  controller: 'human' | 'ai',
): PlayerConfig {
  return {
    id,
    name,
    team: id,
    color,
    controller,
    resources: {
      [FUNDS_RESOURCE]: { current: 0, capacity: null },
      [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
    },
    ai: { aggression: 0.5 },
  };
}

/** A blank, valid level: two sides, flat ground, the default win conditions. */
export function emptyLevel(width = 20, height = 14): LevelData {
  const row = '.'.repeat(width);
  return {
    schema: 2,
    id: 'untitled',
    name: '未命名关卡',
    description: '',
    width,
    height,
    terrain: new Array(height).fill(row),
    elevation: new Array(width * height).fill(0),
    cliffs: [],
    directionalCover: [],
    owners: [],
    units: [],
    players: [
      defaultPlayer(1, '蓝军', '#3f7fd8', 'human'),
      defaultPlayer(2, '红军', '#d8483f', 'ai'),
    ],
    rules: {},
    victory: [{ type: 'routEnemies' }, { type: 'captureHQ' }],
  };
}

/** The ruleset a level plays under: the engine defaults, patched by the level. */
export const resolveRules = (level: LevelData): RuleSet => ({ ...DEFAULT_RULES, ...(level.rules ?? {}) });

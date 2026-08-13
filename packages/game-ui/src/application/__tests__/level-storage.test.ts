// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { migrateLevel, normaliseLevel } from '@empire/battle-engine/mapio';
import {
  CUSTOM_LEVELS_KEY,
  loadCustomLevels,
  readCustomLevels,
  saveCustomLevel,
} from '../level-storage';
import type { LevelData } from '@empire/battle-engine';

const level = (id: string): LevelData =>
  normaliseLevel({
    schema: 2,
    id,
    name: id,
    width: 2,
    height: 1,
    terrain: ['..'],
    owners: [],
    units: [{ x: 0, y: 0, unit: 'soldier', owner: 1 }],
    players: [
      {
        id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human',
        resources: { funds: { current: 0, capacity: null } },
      },
      {
        id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai',
        resources: { funds: { current: 0, capacity: null } },
      },
    ],
    rules: {},
    victory: [{ type: 'routEnemies' }],
  });

describe('custom level storage', () => {
  beforeEach(() => localStorage.clear());

  it('round-trips saved levels newest first', () => {
    saveCustomLevel(level('one'));
    saveCustomLevel(level('two'));
    expect(loadCustomLevels().map((stored) => stored.level.id)).toEqual(['two', 'one']);
  });

  it('drops only the unreadable entry, never the whole collection', () => {
    saveCustomLevel(level('good'));
    const raw = JSON.parse(localStorage.getItem(CUSTOM_LEVELS_KEY)!);
    raw.push({ savedAt: 1, level: { schema: 99, id: 'from-the-future' } });
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(raw));

    const result = readCustomLevels();
    expect(result.levels.map((stored) => stored.level.id)).toEqual(['good']);
    expect(result.rejected).toEqual([
      { id: 'from-the-future', reason: expect.stringContaining('schema') },
    ]);
  });

  it('keeps an unreadable entry on disk so a later migration can rescue it', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify([
      { savedAt: 1, level: { schema: 99, id: 'from-the-future' } },
    ]));
    saveCustomLevel(level('new'));

    const stored = JSON.parse(localStorage.getItem(CUSTOM_LEVELS_KEY)!);
    expect(stored.map((entry: { level: { id: string } }) => entry.level.id))
      .toEqual(['new', 'from-the-future']);
  });

  it('survives a corrupted store instead of throwing', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, '{not json');
    const result = readCustomLevels();
    expect(result.levels).toEqual([]);
    expect(result.rejected).toHaveLength(1);
  });
});

describe('level schema migration', () => {
  it('upgrades a schema-1 level into the current resource model', () => {
    const legacy = {
      schema: 1,
      id: 'legacy',
      name: '旧关卡',
      width: 2,
      height: 1,
      terrain: ['..'],
      owners: [],
      units: [{ x: 0, y: 0, unit: 'soldier', owner: 1 }],
      players: [
        { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', funds: 300 },
        { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai', funds: 250 },
      ],
      rules: { baseIncome: 50, incomeOverride: 120 },
      victory: [{ type: 'routEnemies' }],
    };

    const migrated = migrateLevel(legacy) as Record<string, unknown>;
    expect(migrated.schema).toBe(2);

    const level = normaliseLevel(migrated);
    expect(level.players[0].resources.funds).toEqual({ current: 300, capacity: null });
    expect(level.players[1].resources.funds).toEqual({ current: 250, capacity: null });
    expect(level.rules.baseResourceGrants).toEqual([{ resource: 'funds', amount: 50 }]);
    expect(level.rules.siteResourceOverrides).toEqual({ funds: 120 });
    expect('baseIncome' in level.rules).toBe(false);
  });

  it('still refuses a schema with no upgrade path', () => {
    expect(() => normaliseLevel({ schema: 99, terrain: ['..'] })).toThrow(/schema/);
  });

  it('leaves a current-schema level untouched', () => {
    const current = level('unchanged');
    expect(migrateLevel(current)).toBe(current);
  });
});

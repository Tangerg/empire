import { testApply, testChooseAction, testMap, testState, testValidate, TEST_CONTENT } from './fixtures';
import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { defaultRules, defaultVictory, normaliseLevel, resolveRules, terrainRows } from '../level/index';
import { LevelFormatError } from '../level/schema';
import type { LevelData } from '../types';

const dir = join(import.meta.dirname, '../../../content-ancient-empires/src/levels');
const files = readdirSync(dir).filter((f) => f.endsWith('.json'));

const levels: { file: string; level: LevelData }[] = files.map((file) => ({
  file,
  level: normaliseLevel(JSON.parse(readFileSync(join(dir, file), 'utf8'))),
}));

describe('built-in levels', () => {
  it('ships at least three levels', () => {
    expect(levels.length).toBeGreaterThanOrEqual(3);
  });

  for (const { file, level } of levels) {
    describe(file, () => {
      it('passes validation with no errors', () => {
        const issues = testValidate(level);
        const errors = issues.filter((i) => i.severity === 'error');
        expect(errors, errors.map((e) => e.message).join('\n')).toEqual([]);
      });

      it('has rows matching the declared size', () => {
        expect(level.terrain.length).toBe(level.height);
        for (const row of level.terrain) expect(row.length).toBe(level.width);
      });

      it('round-trips through the terrain serialiser', () => {
        expect(terrainRows(TEST_CONTENT, testMap(level))).toEqual(level.terrain);
      });

      it('is playable: 12 AI-driven turns without an illegal action', () => {
        const s = testState(level);
        for (const p of s.players) p.controller = 'ai';
        for (let turn = 0; turn < 12 && s.phase === 'playing'; turn++) {
          for (let guard = 0; guard < 300; guard++) {
            const action = testChooseAction(s);
            testApply(s, action);
            if (action.kind === 'endTurn' || s.phase !== 'playing') break;
          }
        }
        expect(s.units.length).toBeGreaterThan(0);
      });
    });
  }
});

describe('terrain serialization', () => {
  it('refuses an unencoded terrain instead of replacing it with the default', () => {
    const map = testMap(levels[0].level);
    map.tiles[0] = 'unencoded';

    expect(() => terrainRows(TEST_CONTENT, map))
      .toThrow(new LevelFormatError('terrain "unencoded" has no serialized character'));
  });
});

describe('level defaults', () => {
  it('creates fresh nested rule and objective data for every caller', () => {
    const first = defaultRules();
    const second = defaultRules();
    first.baseResourceGrants.push({ resource: 'funds', amount: 99 });
    first.siteResourceOverrides.funds = 77;

    expect(second.baseResourceGrants).toEqual([]);
    expect(second.siteResourceOverrides).toEqual({});

    const firstVictory = defaultVictory();
    const secondVictory = defaultVictory();
    firstVictory.push({ type: 'surviveTurns', turns: 3 });
    expect(secondVictory).toHaveLength(2);
  });

  it('copies nested level overrides into the resolved battle rules', () => {
    const level = structuredClone(levels[0].level);
    level.rules = {
      ...level.rules,
      baseResourceGrants: [{ resource: 'funds', amount: 50 }],
      siteResourceOverrides: { funds: 25 },
    };
    const resolved = resolveRules(level);
    resolved.baseResourceGrants[0].amount = 500;
    resolved.siteResourceOverrides.funds = 250;

    expect(level.rules.baseResourceGrants?.[0].amount).toBe(50);
    expect(level.rules.siteResourceOverrides?.funds).toBe(25);
  });
});

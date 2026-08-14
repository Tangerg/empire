import { describe, expect, it } from 'vitest';
import { createBattleEngine, createState, validateLevel } from '@empire/battle-engine';
import { experienceLevel } from './index';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_RULES = createBattleEngine({ content: TEST_CATALOG }).rules;

describe('super experience level', () => {
  it('is a valid three-front vertical slice with production mechanics', () => {
    const level = experienceLevel();
    expect(validateLevel(TEST_RULES, level).filter((issue) => issue.severity === 'error')).toEqual([]);
    expect(level.width).toBeGreaterThanOrEqual(27);
    expect(level.units.length).toBeGreaterThanOrEqual(20);
    expect(level.scenario?.triggers?.length).toBeGreaterThanOrEqual(4);
    const state = createState(TEST_CATALOG, level);
    expect(state.deployment).not.toBeNull();
    expect(state.structures.length).toBe(4);
  });
});

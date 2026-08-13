import { describe, expect, it } from 'vitest';
import { createState, validateLevel } from '@empire/battle-engine';
import { experienceLevel } from './index';

import { cloneContentCatalog, GlobalContentCatalog } from '@empire/battle-engine';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = cloneContentCatalog(GlobalContentCatalog);

describe('super experience level', () => {
  it('is a valid three-front vertical slice with production mechanics', () => {
    const level = experienceLevel();
    expect(validateLevel(level, TEST_CATALOG).filter((issue) => issue.severity === 'error')).toEqual([]);
    expect(level.width).toBeGreaterThanOrEqual(27);
    expect(level.units.length).toBeGreaterThanOrEqual(20);
    expect(level.scenario?.triggers?.length).toBeGreaterThanOrEqual(4);
    const state = createState(level, TEST_CATALOG);
    expect(state.deployment).not.toBeNull();
    expect(state.structures.length).toBe(4);
  });
});

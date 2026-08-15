import { describe, expect, it } from 'vitest';
import { createBattleEngine, type TacticalSpace } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from './index';
import { CANDIDATE_01_LEVELS } from './levels';

/**
 * A regression gate for the AI's spatial work, counted rather than timed.
 *
 * `AGENTS.md` asks for a benchmark or behavioural guard where regression risk is
 * meaningful, and `__bench__/engine.bench.ts` reports numbers without ever
 * failing. A wall-clock threshold would fail on a loaded machine and pass on a
 * fast one, so this counts calls instead: given a level and a seed the engine is
 * deterministic, so the counts are exact and identical everywhere.
 *
 * What it protects is the shape of the search, not its constant factor. Move
 * fields and threat maps are the expensive queries, and the way they get out of
 * hand is structural — a candidate loop that recomputes per target instead of
 * per actor, a scorer that asks for a field it was already handed. That shows up
 * as a jump in calls per action, which is what this measures.
 */

/** Composed per suite, exactly like an application composition root. */
const CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

/** The heaviest battle the campaign ships: 25 units on a 25×17 board. */
const WORST_CASE = CANDIDATE_01_LEVELS.find((level) => level.id === 'c01-16')!;
const ACTIONS = 60;

type Counts = Record<'moveField' | 'threatOf' | 'pathTo', number>;

/**
 * A `TacticalSpace` that tallies the queries it forwards.
 *
 * Installed as a capability override rather than monkey-patched onto a shared
 * engine, which is both cleaner and a second piece of evidence: the spatial port
 * really is substitutable at composition time.
 *
 * Delegation is through the prototype, not a spread. `DefaultTacticalSpace` is a
 * class, and `{ ...inner }` copies own enumerable properties only — the seven
 * methods this decorator does not name would simply vanish.
 */
function countingSpace(inner: TacticalSpace, counts: Counts): TacticalSpace {
  return Object.assign(Object.create(inner) as TacticalSpace, {
    moveField: (...args: Parameters<TacticalSpace['moveField']>) => {
      counts.moveField++;
      return inner.moveField(...args);
    },
    threatOf: (...args: Parameters<TacticalSpace['threatOf']>) => {
      counts.threatOf++;
      return inner.threatOf(...args);
    },
    pathTo: (...args: Parameters<TacticalSpace['pathTo']>) => {
      counts.pathTo++;
      return inner.pathTo(...args);
    },
  });
}

describe('what the AI costs to run', () => {
  it('keeps its spatial work per action inside a bounded budget', () => {
    const counts: Counts = { moveField: 0, threatOf: 0, pathTo: 0 };
    const reference = createBattleEngine({ content: CATALOG });
    const engine = createBattleEngine({
      content: CATALOG,
      space: countingSpace(reference.rules.space, counts),
    });

    const state = engine.createState(WORST_CASE);
    for (const player of state.players) {
      player.controller = 'ai';
      player.ai = { aggression: 0.58 };
    }

    let actions = 0;
    while (state.phase !== 'over' && actions < ACTIONS) {
      engine.dispatch(state, engine.chooseAiAction(state));
      actions++;
    }
    expect(actions).toBe(ACTIONS);

    // Roughly 7 move fields and 10 threat maps per action today. The ceilings
    // are deliberately loose — this catches an order of magnitude, not a tuning
    // change, and a gate that fights every refactor gets deleted.
    expect(counts.moveField / actions).toBeLessThan(20);
    expect(counts.threatOf / actions).toBeLessThan(30);
    expect(counts.pathTo / actions).toBeLessThan(10);

    // And the counter was really wired in: a budget met by measuring nothing is
    // the failure mode this whole test is exposed to.
    expect(counts.moveField).toBeGreaterThan(actions);
  });
});

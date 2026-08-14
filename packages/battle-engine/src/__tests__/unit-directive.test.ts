import { describe, expect, it } from 'vitest';
import { createDefaultBattleRuleServices } from '../action-system';
import { applyAction } from '../actions';
import { UnitDirectives, directiveOf, directivePull } from '../unit-directive';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';
import type { GameState } from '../types';

/**
 * A standing order is content.
 *
 * The four orders were read by four scattered branches — what ground to want,
 * how close to stand to the enemy, whether to stop and fight, and when a patrol
 * advances its route. Each order answers for itself now.
 */

const field = (directive: GameState['units'][number]['directive'] | undefined): GameState =>
  testState(makeLevel(['.....', '.....'], {
    units: [{ ...u(0, 0, 'soldier', 1), directive }, u(4, 1, 'soldier', 2)],
    scenario: { zones: [{ id: 'rally', cells: [{ x: 4, y: 0 }] }] },
  }));

describe('standing orders', () => {
  it('gives each order its own answers, without a fall-through between them', () => {
    const guarding = field({ mode: 'guard', zone: 'rally', waypoints: [], cursor: 0 });
    const unit = guarding.units[0];

    expect(directivePull(TEST_RULES, guarding, unit, { x: 4, y: 0 })).toBe(260);
    expect(directivePull(TEST_RULES, guarding, unit, { x: 3, y: 0 })).toBe(-55);

    // Extraction pulls an order of magnitude harder than a guard post: getting
    // out is the mission, holding a spot is a preference.
    const leaving = field({ mode: 'retreat', zone: 'rally', waypoints: [], cursor: 0 });
    expect(directivePull(TEST_RULES, leaving, leaving.units[0], { x: 4, y: 0 })).toBe(1_200);
    // And an order to leave with nowhere to go is still an order to leave.
    const cornered = field({ mode: 'retreat', waypoints: [], cursor: 0 });
    expect(directivePull(TEST_RULES, cornered, cornered.units[0], { x: 0, y: 0 })).toBe(-100);

    // An assault wants no particular ground; the rest of the appraisal decides.
    const charging = field({ mode: 'assault', zone: 'rally', waypoints: [], cursor: 0 });
    expect(directivePull(TEST_RULES, charging, charging.units[0], { x: 0, y: 0 })).toBe(0);
  });

  it('lets the order itself advance a patrol that arrived', () => {
    const state = field({
      mode: 'patrol',
      waypoints: [{ x: 1, y: 0 }, { x: 3, y: 0 }],
      cursor: 0,
    });
    const unit = state.units[0];

    applyAction(state, {
      kind: 'command',
      unit: unit.id,
      path: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
      command: { ability: 'wait' },
    }, TEST_RULES);

    expect(unit.directive.cursor).toBe(1);
  });

  it('accepts an order the engine has never heard of', () => {
    const directives = UnitDirectives.clone();
    directives.define({
      id: 'test.forage',
      pull: ({ at }) => at.y * 40,
      engagement: 0.5,
      fightPenalty: -20,
    });
    const rules = createDefaultBattleRuleServices({ content: TEST_CONTENT, directives });
    const state = field({ mode: 'test.forage', waypoints: [], cursor: 0 });

    expect(directivePull(rules, state, state.units[0], { x: 0, y: 1 })).toBe(40);
    expect(directiveOf(rules, state.units[0]).fightPenalty).toBe(-20);
  });
});

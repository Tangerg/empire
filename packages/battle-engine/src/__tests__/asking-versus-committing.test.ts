import { describe, expect, it } from 'vitest';
import { readyWeapon, requireReadyWeapon } from '../combat';
import { canUseAbility, abilityDef } from '../abilities';
import { forecastCombatPlan } from '../combat-plan';
import { DomainInvariantError } from '../domain/errors';
import { cloneContentCatalog } from '../content-pack';
import { createBattleRules } from '../plugins/default';
import { TEST_CONTENT, TEST_RULES, makeLevel, testAbilityQuery, testState, u } from './fixtures';
import type { GameState } from '../types';

/**
 * Asking whether something is allowed and committing to it are different acts,
 * so they are different functions. One used to be written as a `try/catch`
 * around the other, which made "on cooldown" and "the content is broken" come
 * back as the same quiet no.
 */

const duel = (): GameState => testState(makeLevel(['....'], {
  units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)],
}));

describe('asking about a weapon', () => {
  it('answers no without an exception', () => {
    const state = duel();
    const mage = state.units[0];
    expect(readyWeapon(TEST_RULES, mage, 'mage_bolt')).toMatchObject({ id: 'mage_bolt' });
    expect(readyWeapon(TEST_RULES, mage, 'knight_sword')).toBeNull();
  });

  it('answers no for a weapon the unit carries but cannot fire yet', () => {
    const state = duel();
    const mage = state.units[0];
    mage.weaponState.mage_overcharge.cooldownRemaining = 2;
    expect(readyWeapon(TEST_RULES, mage, 'mage_overcharge')).toBeNull();
  });

  it('does not hide a weapon the content never defined', () => {
    // The old `catch { return false }` turned a typo in a unit's weapon list
    // into "this unit can never attack" — findable only by bisecting content.
    const content = cloneContentCatalog(TEST_CONTENT);
    content.units.override('mage', { weapons: ['mage_bolt', 'mage_bolth'] });
    const rules = createBattleRules({ content });
    const state = testState(makeLevel(['....'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }));

    expect(() => readyWeapon(rules, state.units[0], 'mage_bolth')).toThrow(/unknown weapon "mage_bolth"/);
    // A weapon the unit does not claim at all is still an ordinary "no".
    expect(readyWeapon(rules, state.units[0], 'nothing_at_all')).toBeNull();
  });
});

describe('committing to a weapon', () => {
  it('names the caller as the one at fault', () => {
    const state = duel();
    expect(() => requireReadyWeapon(TEST_RULES, state.units[0], 'knight_sword'))
      .toThrow(DomainInvariantError);
  });

  it('is not what a legality check uses', () => {
    const state = duel();
    const mage = state.units[0];
    const attack = abilityDef(TEST_RULES, 'attack');

    // A weapon this unit does not carry is a plain no, not a thrown error.
    expect(canUseAbility(TEST_RULES, attack, {
      ...testAbilityQuery(state, mage, { x: 0, y: 0 }),
      weaponId: 'knight_sword',
    })).toBe(false);
  });
});

describe('aiming a strike', () => {
  it('treats an impossible aim as a defect, because every caller filters first', () => {
    const state = duel();
    expect(() => forecastCombatPlan(TEST_RULES, state, state.units[0], { x: 3, y: 0 }))
      .toThrow(DomainInvariantError);
    expect(() => forecastCombatPlan(TEST_RULES, state, state.units[0], { x: 0, y: 0 }))
      .toThrow(DomainInvariantError);
  });
});

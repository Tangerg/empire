import { describe, expect, it } from 'vitest';
import { createDefaultBattleRuleServices } from '../action-system';
import { resolveDamage } from '../damage';
import { UnitDepartureHandlers } from '../unit-departure';
import { changeMorale } from '../morale';
import { applyScenarioEffect } from '../scenario';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';
import type { GameEvent, GameState } from '../types';

/**
 * One blow, one settlement.
 *
 * These are the steps every damage site used to reproduce by hand. They are
 * asserted here once so the sites do not have to be trusted individually.
 */

const duel = (rules = {}): GameState => testState(makeLevel(['....'], {
  units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
  rules,
}));

const record = () => {
  const events: GameEvent[] = [];
  return { events, emit: (event: GameEvent) => events.push(event) };
};

describe('a blow and what follows from it', () => {
  it('reports the caller\'s account before any consequence of it', () => {
    const state = duel();
    const victim = state.units[1].id;
    const { events, emit } = record();
    const outcome = resolveDamage(TEST_RULES, state, {
      unit: victim,
      amount: 999,
      report: (blow) => ({ type: 'statusTick', unit: victim, status: 'burning', amount: blow.amount, hpAfter: blow.hpAfter }),
    }, emit);

    expect(outcome).toMatchObject({ landed: true, killed: true, leftField: true, hpAfter: 0 });
    expect(events.map((event) => event.type)).toEqual(['statusTick', 'death', 'markerAdded']);
  });

  it('treats damage aimed at a unit that already left as a blow that did not land', () => {
    const state = duel();
    const departed = state.units[1].id;
    state.units.splice(1, 1);
    const { events, emit } = record();
    const outcome = resolveDamage(TEST_RULES, state, {
      unit: departed,
      amount: 10,
      report: () => { throw new Error('nothing to report'); },
    }, emit);

    expect(outcome.landed).toBe(false);
    expect(events).toEqual([]);
  });

  it('lets nonlethal damage wound but never finish', () => {
    const state = duel();
    const victim = state.units[1];
    victim.hp = 1;
    const { events, emit } = record();
    const outcome = resolveDamage(TEST_RULES, state, {
      unit: victim.id,
      amount: 99,
      nonlethal: true,
      report: () => ({ type: 'statusTick', unit: victim.id, status: 'burning', amount: 0, hpAfter: 1 }),
    }, emit);

    expect(outcome).toMatchObject({ landed: true, amount: 0, killed: false, leftField: false });
    expect(state.units).toHaveLength(2);
    expect(events).toEqual([]);
  });

  it('counts a unit that broke and ran as gone, not as alive', () => {
    const state = duel({ moraleEnabled: true, moraleDamageFactor: 40 });
    const victim = state.units[1].id;
    const { emit } = record();
    const outcome = resolveDamage(TEST_RULES, state, {
      unit: victim,
      amount: 20,
      report: (blow) => ({ type: 'statusTick', unit: victim, status: 'burning', amount: blow.amount, hpAfter: blow.hpAfter }),
    }, emit);

    // Asking `killed` here is the mistake that keeps being made.
    expect(outcome.killed).toBe(false);
    expect(outcome.leftField).toBe(true);
    expect(state.units).toHaveLength(1);
  });
});

describe('every way off the field is a departure', () => {
  const witnessed = () => {
    const seen: number[] = [];
    const unitDepartures = UnitDepartureHandlers.clone().register({
      id: 'test.witness',
      handle: ({ unit }) => seen.push(unit.id),
    });
    return { seen, rules: createDefaultBattleRuleServices({ content: TEST_CONTENT, unitDepartures }) };
  };

  it('announces a rout caused by nothing but morale', () => {
    const state = duel({ moraleEnabled: true });
    const { seen, rules } = witnessed();
    const broken = state.units[1].id;

    changeMorale(rules, state, broken, -999, 'test', () => {});

    // The rout used to be invisible to every consequence of departure: a
    // commander could break and keep buffing the troops it had abandoned.
    expect(state.units.some((unit) => unit.id === broken)).toBe(false);
    expect(seen).toEqual([broken]);
  });

  it('announces a surrender driven by a scenario', () => {
    const state = duel({ moraleEnabled: true });
    const { seen, rules } = witnessed();
    const defector = state.units[1].id;

    applyScenarioEffect(rules, state, {
      type: 'surrenderUnits',
      selector: { owner: 2 },
      to: 1,
    }, () => {});

    expect(seen).toEqual([defector]);
  });
});

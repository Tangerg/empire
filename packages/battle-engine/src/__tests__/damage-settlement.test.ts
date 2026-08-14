import { describe, expect, it } from 'vitest';
import { type BattleRuleServices } from '../action-system';
import { createBattleRules } from '../plugins/default';
import { applyAction } from '../actions';
import { resolveDamage } from '../damage';
import { WeaponHitEffectHandlers } from '../hit-effects';
import { MOMENTUM_RESOURCE } from '../resources';
import { removeUnit } from '../state';
import { UnitDepartureHandlers } from '../unit-departure';
import { changeMorale } from '../morale';
import { applyScenarioEffect } from '../scenario';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';
import type { GameEvent, GameState, Unit } from '../types';

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

/**
 * The volley, the riposte and the ally's covering shot are one act. These are
 * the two things the copies had each stopped agreeing about.
 */
describe('every blow in combat is the same act', () => {
  /** A ruleset whose only weapon rider removes whoever the test points it at. */
  const vanishing = (pick: (context: { attacker: Unit; target: Unit }) => Unit | null) => {
    const hitEffects = WeaponHitEffectHandlers.clone().replace({
      kind: 'addStatus' as const,
      apply: (context) => {
        const doomed = pick(context);
        if (doomed) removeUnit(context.state, doomed.id);
      },
      describe: () => 'test.vanish',
    });
    return createBattleRules({ content: TEST_CONTENT, hitEffects });
  };

  const brawl = (): GameState => testState(makeLevel(['..'], {
    units: [
      { ...u(0, 0, 'rogue', 1), resources: { [MOMENTUM_RESOURCE]: { current: 0, capacity: 50 } } },
      { ...u(1, 0, 'rogue', 2), resources: { [MOMENTUM_RESOURCE]: { current: 0, capacity: 50 } } },
    ],
  }));

  const strike = (state: GameState, rules: BattleRuleServices): GameEvent[] => applyAction(state, {
    kind: 'command',
    unit: state.units[0].id,
    path: [{ x: 0, y: 0 }],
    command: { ability: 'attack', weapon: 'rogue_blades', target: { x: 1, y: 0 } },
  }, rules);

  it('gives no survivor\'s momentum to a unit its own rider just removed', () => {
    // The riposte lands, poisons — and the poison kills. Only the volley used
    // to look again before crediting the target with the dash of momentum a
    // survivor earns; the riposte announced it for a unit already gone.
    const state = brawl();
    const attacker = state.units[0].id;
    const events = strike(state, vanishing(({ attacker: striker, target }) =>
      striker.owner === 2 ? target : null));

    const riposte = events.findIndex((event) => event.type === 'counter');
    expect(riposte).toBeGreaterThan(-1);
    expect(state.units.some((unit) => unit.id === attacker)).toBe(false);
    // Its own momentum for striking is earned before the riposte; nothing is
    // credited to it afterwards.
    expect(events.slice(riposte).some((event) =>
      event.type === 'resourceChanged' &&
      event.resource === MOMENTUM_RESOURCE &&
      event.subject.kind === 'unit' &&
      event.subject.id === attacker)).toBe(false);
  });

  it('teaches nobody anything with a blow that found nobody', () => {
    // The volley's rider takes the attacker off the field, so the riposte that
    // follows hits empty ground. It used to award its owner rank progress all
    // the same, because only the volley checked whether the blow landed.
    const state = brawl();
    const defender = state.units[1].id;
    const events = strike(state, vanishing(({ attacker }) => attacker.owner === 1 ? attacker : null));

    expect(state.units.map((unit) => unit.id)).toEqual([defender]);
    expect(events.some((event) => event.type === 'counter')).toBe(false);
    expect(events).not.toContainEqual(expect.objectContaining({
      type: 'rankProgressChanged',
      unit: defender,
    }));
  });
});

describe('every way off the field is a departure', () => {
  const witnessed = () => {
    const seen: number[] = [];
    const unitDepartures = UnitDepartureHandlers.clone().register({
      id: 'test.witness',
      handle: ({ unit }) => seen.push(unit.id),
    });
    return { seen, rules: createBattleRules({ content: TEST_CONTENT, unitDepartures }) };
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

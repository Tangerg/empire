import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { TEST_RULES, makeLevel, testCombatPlan, testForecast, testState, u } from './fixtures';

describe('reaction stances', () => {
  it('guards one strike, consumes the reaction, and gives up counterattacks', () => {
    const state = testState(
      makeLevel(['...'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 1), { ...u(1, 0, 'ogre', 2), reaction: 'guard' }],
      }),
    );
    const first = testForecast(state, state.units[0], state.units[2]);
    expect(first.reaction).toMatchObject({ stance: 'guard', unit: state.units[2].id });
    expect(first.strike.factorOf('reaction.guard')).toBe(0.7);
    expect(first.counter).toBeNull();

    applyAction(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    }, TEST_RULES);
    const second = testForecast(state, state.units[1], state.units[2]);
    expect(second.reaction).toBeNull();
    expect(second.strike.familyFactor('reaction.')).toBe(1);
    expect(second.counter).toBeNull();
  });

  it('has an adjacent supporter take the real damage without hiding it from forecast', () => {
    const state = testState(
      makeLevel(['..', '..'], {
        units: [
          u(0, 0, 'soldier', 1),
          u(1, 0, 'soldier', 2),
          { ...u(1, 1, 'knight', 2), reaction: 'support' },
        ],
      }),
    );
    const attacker = state.units[0];
    const protectedUnit = state.units[1];
    const supporter = state.units[2];
    const exchange = testForecast(state, attacker, protectedUnit);
    expect(exchange.interceptor).toBe(supporter.id);
    expect(exchange.damageRecipient).toBe(supporter.id);
    expect(exchange.defenderHpAfter).toBe(protectedUnit.hp);
    expect(exchange.recipientHpAfter).toBeLessThan(supporter.hp);

    const events = applyAction(state, {
      kind: 'command',
      unit: attacker.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    }, TEST_RULES);
    expect(protectedUnit.hp).toBe(100);
    expect(supporter.hp).toBe(exchange.recipientHpAfter);
    expect(supporter.reactionUsedRound).toBe(state.turn);
    expect(events).toContainEqual(
      expect.objectContaining({
        type: 'reactionTriggered',
        unit: supporter.id,
        stance: 'support',
        protectedUnit: protectedUnit.id,
      }),
    );
  });

  it('conserves limited and cooldown weapons when selecting a counter', () => {
    const normal = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'mage', 2)] }),
    );
    expect(testForecast(normal, normal.units[0], normal.units[1]).counter?.weapon).toBe('mage_overcharge');

    const conserve = testState(
      makeLevel(['..'], {
        units: [u(0, 0, 'soldier', 1), { ...u(1, 0, 'mage', 2), reaction: 'conserve' }],
      }),
    );
    expect(testForecast(conserve, conserve.units[0], conserve.units[1]).counter?.weapon).toBe('mage_bolt');
  });

  it('changes stance through a legal headless action', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const events = applyAction(state, { kind: 'reaction', unit: state.units[0].id, stance: 'support' }, TEST_RULES);
    expect(state.units[0].reaction).toBe('support');
    expect(events[0]).toEqual({ type: 'reactionChanged', unit: state.units[0].id, stance: 'support' });
  });

  it('forecasts and resolves one adjacent allied support attack after the counter', () => {
    const state = testState(
      makeLevel(['...', '...'], {
        units: [
          { ...u(0, 0, 'archer', 1), reaction: 'support' },
          u(0, 1, 'soldier', 1),
          u(1, 1, 'ogre', 2),
        ],
      }),
    );
    const supporter = state.units[0];
    const attacker = state.units[1];
    const defender = state.units[2];
    const plan = testCombatPlan(state, attacker, defender);

    expect(plan.supportAttack).toMatchObject({
      attacker: supporter.id,
      target: defender.id,
      weapon: 'archer_bow',
    });
    expect(plan.supportAttack!.damage.factorOf('reaction.support-attack')).toBe(0.6);
    expect(plan.supportAttack!.damage.modifiers).toContainEqual(
      expect.objectContaining({ id: 'reaction.support-attack', value: 0.6 }),
    );

    const events = applyAction(state, {
      kind: 'command',
      unit: attacker.id,
      path: [{ x: attacker.x, y: attacker.y }],
      command: { ability: 'attack', target: { x: defender.x, y: defender.y } },
    }, TEST_RULES);
    const counterIndex = events.findIndex((event) => event.type === 'counter');
    const supportIndex = events.findIndex((event) => event.type === 'supportAttack');
    expect(counterIndex).toBeGreaterThanOrEqual(0);
    expect(supportIndex).toBeGreaterThan(counterIndex);
    expect(events[supportIndex]).toEqual(expect.objectContaining({
      type: 'supportAttack',
      attacker: supporter.id,
      defender: defender.id,
      damage: plan.supportAttack!.damage.damage,
    }));
    expect(defender.hp).toBe(plan.supportAttack!.hpAfter);
    expect(supporter.reactionUsedRound).toBe(state.turn);
  });
});

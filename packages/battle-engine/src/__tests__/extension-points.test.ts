import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { createBattleRules } from '../plugins/default';
import { applyAction } from '../actions';
import { cloneContentCatalog } from '../content-pack';
import { Reactions } from '../reactions';
import { UnitDepartureHandlers, announceUnitDeparture } from '../unit-departure';
import { castOf } from '../casting';
import { TEST_CONTENT, makeLevel, testForecast, testState, u } from './fixtures';
import type { GameState, LevelData } from '../types';

/**
 * Two extension points added this round, exercised the way a content pack or a
 * rule plugin would use them: without editing the engine.
 */

const duel = (): LevelData =>
  makeLevel(['.....'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] });

describe('reaction stances are content', () => {
  it('ships four stances that combat asks about rather than knows', () => {
    expect(Reactions.ids()).toEqual(['counter', 'guard', 'support', 'conserve']);
    expect(Reactions.get('guard').incomingMultiplier).toBe(0.7);
    expect(Reactions.get('guard').retaliates).toBe(false);
    expect(Reactions.get('support').intercepts).toBe(true);
  });

  it('lets a pack add a stance without touching damage forecasting', () => {
    const reactions = Reactions.clone();
    reactions.define({
      id: 'dodge',
      name: '闪避',
      hint: '大幅减伤，但完全放弃还击。',
      intercepts: false,
      incomingMultiplier: 0.4,
      retaliates: false,
      conservesResources: false,
    });
    const rules = createBattleRules({ content: TEST_CONTENT, reactions });

    const state = testState(duel());
    const [attacker, defender] = state.units;
    const plain = testForecast(state, attacker, defender).strike.damage;

    defender.reaction = 'dodge';
    const dodged = createBattleEngine({ content: TEST_CONTENT, reactions })
      .forecast(state, attacker, defender);

    expect(dodged.strike.damage).toBeLessThan(plain);
    expect(dodged.reaction).toEqual({ unit: defender.id, stance: 'dodge' });
    expect(dodged.counter, 'a dodging unit gives up its riposte').toBeNull();
    expect(dodged.strike.modifiers.some((modifier) => modifier.id === 'reaction.dodge')).toBe(true);
    expect(rules.reactions.has('dodge')).toBe(true);
  });

  it("keeps one engine's added stance out of another", () => {
    const reactions = Reactions.clone();
    reactions.define({
      id: 'dodge', name: '闪避', hint: '',
      intercepts: false, incomingMultiplier: 0.4, retaliates: false, conservesResources: false,
    });
    createBattleEngine({ content: TEST_CONTENT, reactions });
    expect(Reactions.has('dodge')).toBe(false);
    expect(createBattleEngine({ content: TEST_CONTENT }).rules.reactions.has('dodge')).toBe(false);
  });
});

describe('a unit leaving the field is one announcement', () => {
  it('carries the unit itself, because it is already off the board', () => {
    const seen: Array<{ id: number; onBoard: boolean }> = [];
    const unitDepartures = UnitDepartureHandlers.clone().register({
      id: 'test.witness',
      handle: ({ state, unit }) => seen.push({
        id: unit.id,
        onBoard: state.units.some((candidate) => candidate.id === unit.id),
      }),
    });
    const rules = createBattleRules({ content: TEST_CONTENT, unitDepartures });
    const state = testState(makeLevel(['..'], {
      units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 5)],
    }));
    const victim = state.units[1].id;

    applyAction(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    }, rules);

    expect(seen).toEqual([{ id: victim, onBoard: false }]);
  });

  it("runs a plugin's own consequence beside the built-in ones", () => {
    const bounty: number[] = [];
    const unitDepartures = UnitDepartureHandlers.clone().register({
      id: 'test.bounty',
      handle: ({ unit }) => bounty.push(unit.owner),
    });
    const battle = createBattleEngine({ content: TEST_CONTENT, unitDepartures });
    const state = battle.createState(makeLevel(['..'], {
      units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 5)],
    }));

    battle.dispatch(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });

    expect(bounty).toEqual([2]);
    // The built-in consequences are registered the same way, not hard-wired.
    expect(battle.rules.unitDepartures.keys()).toEqual(
      expect.arrayContaining(['commander.defeat', 'casting.cancel', 'test.bounty']),
    );
  });

  it('refuses two consequences under one id', () => {
    const registry = UnitDepartureHandlers.clone();
    expect(() => registry.register({ id: 'commander.defeat', handle: () => {} }))
      .toThrow(/already registered/);
  });

  it('drops a charging strike the moment its caster falls', () => {
    const content = cloneContentCatalog(TEST_CONTENT);
    content.weapons.override('mage_meteor', { castTurns: 4 });
    const battle = createBattleEngine({ content });
    const state = battle.createState(makeLevel(['.....'], {
      units: [u(0, 0, 'mage', 1, 6), u(4, 0, 'soldier', 2), u(1, 0, 'knight', 2)],
    }));
    const mage = state.units[0];

    battle.dispatch(state, {
      kind: 'command',
      unit: mage.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'mage_meteor', target: { x: 4, y: 0 } },
    });
    expect(castOf(state, mage.id)).toBeTruthy();

    battle.dispatch(state, { kind: 'endTurn' });
    const events = battle.dispatch(state, {
      kind: 'command',
      unit: state.units.find((unit) => unit.type === 'knight')!.id,
      path: [{ x: 1, y: 0 }],
      command: { ability: 'attack', target: { x: 0, y: 0 } },
    });

    // Not "swept at the next boundary": gone with the caster, in the same events.
    expect(state.units.some((unit) => unit.id === mage.id)).toBe(false);
    expect(state.pendingCasts).toHaveLength(0);
    expect(events).toContainEqual(expect.objectContaining({ type: 'castCancelled', reason: 'casterLost' }));
  });

  it('announces a departure that was not a death', () => {
    const seen: number[] = [];
    const unitDepartures = UnitDepartureHandlers.clone().register({
      id: 'test.witness',
      handle: ({ unit }) => seen.push(unit.id),
    });
    const rules = createBattleRules({ content: TEST_CONTENT, unitDepartures });
    const state: GameState = testState(duel());
    announceUnitDeparture(rules, state, state.units[0], () => {});
    expect(seen).toEqual([state.units[0].id]);
  });
});

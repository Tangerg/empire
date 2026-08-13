import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../engine';
import { AiTurnContext, DefaultAiIntents, chooseAction } from '../ai';
import { TEST_CONTENT, makeLevel, testAiDependencies, testState, u } from './fixtures';
import type { Action, GameState } from '../types';

/**
 * What the AI considers doing is an open, ordered list.
 *
 * Executing a plugin's action kind was already possible; choosing one was not,
 * so half of every extension stopped at the human player.
 */

const skirmish = (): GameState => testState(makeLevel(['.....', '.....'], {
  units: [u(0, 0, 'soldier', 1), u(4, 0, 'soldier', 2), u(4, 1, 'archer', 2)],
}));

const aiTurn = (state: GameState): GameState => {
  state.currentPlayer = 2;
  return state;
};

/** Stances already settled, so the tests below are about the intent under test. */
const settled = () =>
  DefaultAiIntents.clone().replace({ id: 'reaction', priority: 30, propose: () => null });

describe('the ai intent list', () => {
  it('ships the original chain, in the original order', () => {
    expect(DefaultAiIntents.ids()).toEqual(['tactic', 'recruit', 'reaction', 'command']);
  });

  it('orders by priority, never by registration', () => {
    const intents = DefaultAiIntents.clone().register({
      id: 'early',
      priority: 5,
      propose: () => null,
    });
    expect(intents.ids()[0]).toBe('early');
  });

  it('lets a rule pack slot a decision between two built-ins', () => {
    const withdrawn: number[] = [];
    const intents = settled().register({
      id: 'test.retreat',
      priority: 35,
      propose: (context) => {
        const hurt = context.state.units.find((unit) =>
          unit.owner === context.player && !unit.done && unit.hp < 30);
        if (!hurt) return null;
        withdrawn.push(hurt.id);
        return { kind: 'face', unit: hurt.id, facing: 'west' } satisfies Action;
      },
    });

    const state = aiTurn(skirmish());
    state.units[1].hp = 10;

    // Priority 35 is ahead of the march at 40, so the retreat pre-empts it
    // exactly the way a tactic pre-empts everything.
    expect(chooseAction({ ...testAiDependencies(), intents }, state)).toEqual({
      kind: 'face',
      unit: state.units[1].id,
      facing: 'west',
    });
    expect(withdrawn).toEqual([state.units[1].id]);
    expect(chooseAction({ ...testAiDependencies(), intents: settled() }, state).kind).toBe('command');
  });

  it('lets a pack take a built-in decision over entirely', () => {
    const intents = settled().replace({ id: 'command', priority: 40, propose: () => null });
    const state = aiTurn(skirmish());

    // Nothing left to propose, so the turn ends instead of marching.
    expect(chooseAction({ ...testAiDependencies(), intents }, state)).toEqual({ kind: 'endTurn' });
  });

  it('keeps one engine\'s added intent out of another', () => {
    const intents = DefaultAiIntents.clone().register({ id: 'test.only', priority: 1, propose: () => null });
    createBattleEngine({ content: TEST_CONTENT, aiIntents: intents });

    expect(DefaultAiIntents.ids()).not.toContain('test.only');
    expect(createBattleEngine({ content: TEST_CONTENT }).aiIntents.ids()).not.toContain('test.only');
  });

  it('refuses two decisions under one id', () => {
    expect(() => DefaultAiIntents.clone().register({ id: 'command', priority: 1, propose: () => null }))
      .toThrow(/already registered/);
  });
});

describe('the analysis a decision shares', () => {
  it('derives the expensive readings once, and only when asked', () => {
    const state = aiTurn(skirmish());
    const context = new AiTurnContext(testAiDependencies(), state, 2, { aggression: 0.5 });

    expect(context.objectives).toBe(context.objectives);
    expect(context.danger).toBe(context.danger);
    expect(context.battlefield).toBe(context.battlefield);
    expect(context.actors().every((unit) => unit.owner === 2 && !unit.done)).toBe(true);
  });

  it('never offers a unit the ordering policy has not entitled to act', () => {
    const state = aiTurn(skirmish());
    state.units[1].done = true;
    const context = new AiTurnContext(testAiDependencies(), state, 2, { aggression: 0.5 });

    expect(context.actors().map((unit) => unit.id)).toEqual([state.units[2].id]);
  });
});

import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { careerOptions } from '../careers';
import { GameSession } from '../session';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';

describe('career tree and free career changes', () => {
  it('unlocks a branch from rank and current-career mastery while preserving entity identity', () => {
    const level = makeLevel(['..'], {
      units: [{ ...u(0, 0, 'soldier', 1, 50), rank: 1, rankProgress: 120 }, u(1, 0, 'soldier', 2)],
    });
    const session = new GameSession(level, createBattleEngine({ content: TEST_CONTENT }));
    const before = session.state.units[0];
    const id = before.id;
    expect(careerOptions(TEST_RULES, session.state, before).find((option) => option.career.id === 'ranger')).toMatchObject({ eligible: true });

    const events = session.dispatch({ kind: 'changeCareer', unit: id, career: 'ranger' });
    const after = session.state.units.find((unit) => unit.id === id)!;
    expect(after).toMatchObject({ id, type: 'archer', hp: 40, done: true });
    expect(after.career).toMatchObject({ current: 'ranger', unlocked: expect.arrayContaining(['militia', 'ranger']) });
    expect(events).toContainEqual(expect.objectContaining({ type: 'careerChanged', from: 'militia', to: 'ranger' }));
  });

  it('allows switching back to an unlocked career but rejects locked advanced branches', () => {
    const level = makeLevel(['..'], {
      units: [{ ...u(0, 0, 'soldier', 1), rank: 1, rankProgress: 120 }, u(1, 0, 'soldier', 2)],
    });
    const session = new GameSession(level, createBattleEngine({ content: TEST_CONTENT }));
    const id = session.state.units[0].id;
    session.dispatch({ kind: 'changeCareer', unit: id, career: 'ranger' });
    session.state.units[0].done = false;
    session.dispatch({ kind: 'changeCareer', unit: id, career: 'militia' });
    expect(session.state.units[0].type).toBe('soldier');

    session.state.units[0].done = false;
    expect(() => session.dispatch({ kind: 'changeCareer', unit: id, career: 'knight-order' })).toThrow();
    expect(session.state.units[0].type).toBe('soldier');
  });

  it('initialises deployed and recruited units with their matching root or branch career', () => {
    const state = testState(makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'soldier', 2)] }));
    expect(state.units[0].career.current).toBe('knight-order');
    expect(state.units[1].career.current).toBe('militia');
  });
});

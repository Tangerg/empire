import { describe, expect, it } from 'vitest';
import { chooseAction } from '../ai';
import { applyAction } from '../actions';
import { createState, player, unitsOf } from '../state';
import { makeLevel, u } from './fixtures';

/** A compact but non-trivial arena: two keeps, villages, mixed terrain. */
const arena = () =>
  makeLevel(
    [
      'C-.....v',
      '-.TT..h.',
      '.hT~~T..',
      '.v..TTh.',
      '.....-.C',
    ],
    {
      units: [
        u(1, 1, 'soldier', 1),
        u(0, 2, 'archer', 1),
        u(6, 3, 'soldier', 2),
        u(7, 3, 'knight', 2),
      ],
      owners: [
        { x: 0, y: 0, owner: 1 },
        { x: 7, y: 4, owner: 2 },
        { x: 7, y: 0, owner: 0 },
        { x: 1, y: 3, owner: 0 },
      ],
      funds: [400, 400],
    },
  );

describe('ai driver', () => {
  it('only ever emits legal actions and always reaches endTurn', () => {
    const s = createState(arena());
    for (const p of s.players) p.controller = 'ai';

    let actions = 0;
    for (let turn = 0; turn < 60 && s.phase === 'playing'; turn++) {
      let guard = 0;
      for (;;) {
        const action = chooseAction(s);
        // applyAction throws IllegalActionError on anything invalid.
        applyAction(s, action);
        actions++;
        if (action.kind === 'endTurn' || s.phase !== 'playing') break;
        expect(++guard).toBeLessThan(200); // must converge on endTurn
      }
    }
    expect(actions).toBeGreaterThan(20);
  });

  it('spends its gold on units', () => {
    const s = createState(arena());
    const before = player(s, 1).funds;
    const action = chooseAction(s);
    expect(action.kind).toBe('recruit');
    applyAction(s, action);
    expect(player(s, 1).funds).toBeLessThan(before);
    expect(unitsOf(s, 1).length).toBe(3);
  });

  it('takes a free village when one is in reach', () => {
    const s = createState(
      makeLevel(['.v.....', '.......'], {
        units: [u(0, 0, 'soldier', 1), u(6, 1, 'soldier', 2)],
      }),
    );
    const action = chooseAction(s);
    expect(action).toMatchObject({ kind: 'command', command: { ability: 'capture' } });
  });

  it('prefers a kill it can make without dying', () => {
    const s = createState(
      makeLevel(['....', '....'], {
        units: [u(0, 0, 'knight', 1), u(2, 0, 'mage', 2, 20), u(3, 1, 'soldier', 2)],
      }),
    );
    const action = chooseAction(s);
    expect(action).toMatchObject({
      kind: 'command',
      command: { ability: 'attack', target: { x: 2, y: 0 } },
    });
  });
});

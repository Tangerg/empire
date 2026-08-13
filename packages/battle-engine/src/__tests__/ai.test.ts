import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { player, unitsOf } from '../state';
import { TEST_RULES, makeLevel, testChooseAction, testState, u } from './fixtures';
import { FUNDS_RESOURCE } from '../resources';

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
    const s = testState(arena());
    for (const p of s.players) p.controller = 'ai';

    let actions = 0;
    for (let turn = 0; turn < 60 && s.phase === 'playing'; turn++) {
      let guard = 0;
      for (;;) {
        const action = testChooseAction(s);
        // applyAction throws IllegalActionError on anything invalid.
        applyAction(s, action, TEST_RULES);
        actions++;
        if (action.kind === 'endTurn' || s.phase !== 'playing') break;
        expect(++guard).toBeLessThan(200); // must converge on endTurn
      }
    }
    expect(actions).toBeGreaterThan(20);
  });

  it('spends its gold on units', () => {
    const s = testState(arena());
    const before = player(s, 1).resources[FUNDS_RESOURCE].current;
    const action = testChooseAction(s);
    expect(action.kind).toBe('recruit');
    applyAction(s, action, TEST_RULES);
    expect(player(s, 1).resources[FUNDS_RESOURCE].current).toBeLessThan(before);
    expect(unitsOf(s, 1).length).toBe(3);
  });

  it('takes a free village when one is in reach', () => {
    const s = testState(
      makeLevel(['.v.....', '.......'], {
        units: [u(0, 0, 'soldier', 1), u(6, 1, 'soldier', 2)],
      }),
    );
    const action = testChooseAction(s);
    expect(action).toMatchObject({ kind: 'command', command: { ability: 'capture' } });
  });

  it('prefers a kill it can make without dying', () => {
    const s = testState(
      makeLevel(['....', '....'], {
        units: [u(0, 0, 'knight', 1), u(2, 0, 'mage', 2, 20), u(3, 1, 'soldier', 2)],
      }),
    );
    const action = testChooseAction(s);
    expect(action).toMatchObject({
      kind: 'command',
      command: { ability: 'attack', target: { x: 2, y: 0 } },
    });
  });

  it('moves the selected escort toward its extraction zone instead of a decoy enemy', () => {
    const s = testState(
      makeLevel(['.........'], {
        units: [u(4, 0, 'soldier', 1), u(0, 0, 'soldier', 2)],
        scenario: { zones: [{ id: 'exit', cells: [{ x: 8, y: 0 }] }] },
        victory: [{ type: 'escort', selector: { ids: [1] }, zone: 'exit', count: 1 }],
      }),
    );
    let action = testChooseAction(s);
    if (action.kind === 'reaction') {
      applyAction(s, action, TEST_RULES);
      action = testChooseAction(s);
    }
    expect(action.kind).toBe('command');
    if (action.kind !== 'command') return;
    expect(action.unit).toBe(1);
    expect(action.path.at(-1)).toEqual({ x: 7, y: 0 });
  });

  it('prioritizes the current sequence stage and ignores a tempting later objective', () => {
    const level = makeLevel(['....'], {
      units: [u(1, 0, 'knight', 1), u(0, 0, 'mage', 2, 10), u(2, 0, 'ogre', 2)],
      structures: [{ id: 'gate', type: 'gate', x: 3, y: 0, owner: 2 }],
      victory: [{
        type: 'sequence',
        objectives: [
          { type: 'eliminate', selector: { ids: [3] } },
          { type: 'destroy', structures: ['gate'] },
        ],
      }],
    });
    const s = testState(level);
    const action = testChooseAction(s);
    expect(action).toMatchObject({
      kind: 'command',
      command: { ability: 'attack', target: { x: 2, y: 0 } },
    });
  });

  it('changes a threatened protected unit to guard before spending its action', () => {
    const s = testState(
      makeLevel(['.....'], {
        units: [u(1, 0, 'soldier', 1), u(2, 0, 'cleric', 1), u(4, 0, 'knight', 2)],
        victory: [{
          type: 'protect',
          selector: { ids: [1] },
          minimumAlive: 1,
          untilTurn: 5,
        }],
      }),
    );
    expect(testChooseAction(s)).toEqual({ kind: 'reaction', unit: 1, stance: 'guard' });
  });
});

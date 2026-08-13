import { describe, expect, it } from 'vitest';
import { applyAction, commandOptions, IllegalActionError } from '../actions';
import { idx } from '../grid';
import { GameSession } from '../session';
import { createState, player, unitAt } from '../state';
import { makeLevel, u } from './fixtures';
import { FUNDS_RESOURCE } from '../resources';

const wait = (unit: number, path: { x: number; y: number }[]) =>
  ({ kind: 'command', unit, path, command: { ability: 'wait' } }) as const;

describe('capture', () => {
  it('is instant by default and only available to infantry', () => {
    const s = createState(
      makeLevel(['v.'], { units: [u(1, 0, 'soldier', 1)], owners: [{ x: 0, y: 0, owner: 2 }] }),
    );
    applyAction(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 1, y: 0 }, { x: 0, y: 0 }],
      command: { ability: 'capture' },
    });
    expect(s.map.owners[idx(s.map, 0, 0)]).toBe(1);
  });

  it('is not offered to cavalry, constructs or beasts', () => {
    const s = createState(makeLevel(['v'], { units: [u(0, 0, 'knight', 1)] }));
    const abilities = commandOptions(s, s.units[0], { x: 0, y: 0 }).map((o) => o.ability);
    expect(abilities).not.toContain('capture');
  });

  it('takes multiple turns in progressive mode and scales with HP', () => {
    const s = createState(
      makeLevel(['v..'], {
        units: [u(0, 0, 'soldier', 1, 50), u(2, 0, 'soldier', 2)],
        rules: { captureMode: 'progressive', captureThreshold: 100 },
      }),
    );
    const capture = () =>
      applyAction(s, {
        kind: 'command',
        unit: s.units[0].id,
        path: [{ x: 0, y: 0 }],
        command: { ability: 'capture' },
      });
    capture();
    expect(s.map.owners[0]).toBe(0);
    expect(s.map.captureProgress[0]).toBe(50);
    s.units[0].done = false;
    capture();
    expect(s.map.owners[0]).toBe(1);
  });

  it('loses progress when the unit walks away', () => {
    const s = createState(
      makeLevel(['v....'], {
        units: [u(0, 0, 'soldier', 1, 40), u(4, 0, 'soldier', 2)],
        rules: { captureMode: 'progressive' },
      }),
    );
    applyAction(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'capture' },
    });
    expect(s.map.captureProgress[0]).toBeGreaterThan(0);
    s.units[0].done = false;
    applyAction(s, wait(s.units[0].id, [{ x: 0, y: 0 }, { x: 2, y: 0 }]));
    expect(s.map.captureProgress[0]).toBe(0);
  });
});

describe('economy and turn cycle', () => {
  it('pays building income at the start of each turn and heals on owned tiles', () => {
    const s = createState(
      makeLevel(['v.', 'C.'], {
        units: [u(0, 0, 'soldier', 1, 40), u(1, 1, 'soldier', 2)],
        owners: [
          { x: 0, y: 0, owner: 1 },
          { x: 0, y: 1, owner: 1 },
        ],
      }),
    );
    applyAction(s, { kind: 'endTurn' }); // -> P2
    applyAction(s, { kind: 'endTurn' }); // -> P1, turn 2
    expect(player(s, 1).resources[FUNDS_RESOURCE].current).toBe(200); // village 100 + castle 100
    expect(s.units[0].hp).toBe(60); // village heals 20
    expect(s.turn).toBe(2);
  });

  it('recruits from an owned castle, spending gold', () => {
    const s = createState(
      makeLevel(['C.', '..'], {
        units: [u(1, 1, 'soldier', 2)],
        owners: [{ x: 0, y: 0, owner: 1 }],
        funds: [400, 0],
      }),
    );
    const events = applyAction(s, { kind: 'recruit', at: { x: 0, y: 0 }, unit: 'knight' });
    expect(player(s, 1).resources[FUNDS_RESOURCE].current).toBe(50);
    expect(events).toContainEqual(expect.objectContaining({
      type: 'resourceChanged',
      resource: FUNDS_RESOURCE,
      subject: { kind: 'player', id: 1 },
      amount: -350,
      current: 50,
    }));
    expect(unitAt(s, 0, 0)!.type).toBe('knight');
    expect(unitAt(s, 0, 0)!.done).toBe(true); // recruits wait a turn by default
  });

  it('rejects unaffordable or misplaced recruitment', () => {
    const s = createState(
      makeLevel(['C.'], { units: [u(1, 0, 'soldier', 2)], owners: [{ x: 0, y: 0, owner: 1 }] }),
    );
    expect(() => applyAction(s, { kind: 'recruit', at: { x: 0, y: 0 }, unit: 'dragon' })).toThrow(
      IllegalActionError,
    );
    expect(() => applyAction(s, { kind: 'recruit', at: { x: 1, y: 0 }, unit: 'soldier' })).toThrow(
      IllegalActionError,
    );
  });
});

describe('legality', () => {
  it('refuses to move outside the movement field', () => {
    const s = createState(makeLevel(['.........'], { units: [u(0, 0, 'soldier', 1), u(8, 0, 'soldier', 2)] }));
    expect(() => applyAction(s, wait(s.units[0].id, [{ x: 0, y: 0 }, { x: 7, y: 0 }]))).toThrow(
      IllegalActionError,
    );
  });

  it('refuses to act twice with one unit', () => {
    const s = createState(makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] }));
    applyAction(s, wait(s.units[0].id, [{ x: 0, y: 0 }]));
    expect(() => applyAction(s, wait(s.units[0].id, [{ x: 0, y: 0 }]))).toThrow(IllegalActionError);
  });

  it('refuses to move an opponent unit', () => {
    const s = createState(makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] }));
    expect(() => applyAction(s, wait(s.units[1].id, [{ x: 2, y: 0 }]))).toThrow(IllegalActionError);
  });

  it('forbids siege units from firing after moving', () => {
    const s = createState(
      makeLevel(['.....'], { units: [u(0, 0, 'ballista', 1), u(3, 0, 'soldier', 2)] }),
    );
    // Standing still at range 3: in the firing arc.
    expect(commandOptions(s, s.units[0], { x: 0, y: 0 }).map((o) => o.ability)).toContain('attack');
    // Same target, but only reachable by moving first: no shot this turn.
    expect(commandOptions(s, s.units[0], { x: 1, y: 0 }).map((o) => o.ability)).not.toContain('attack');

    // Adjacent enemies sit inside the minimum range and cannot be hit at all.
    const adjacent = createState(
      makeLevel(['..'], { units: [u(0, 0, 'ballista', 1), u(1, 0, 'soldier', 2)] }),
    );
    expect(commandOptions(adjacent, adjacent.units[0], { x: 0, y: 0 }).map((o) => o.ability)).not.toContain(
      'attack',
    );
  });
});

describe('victory', () => {
  it('ends the game when one side is wiped out', () => {
    const s = createState(
      makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 5)] }),
    );
    const events = applyAction(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });
    expect(events.some((e) => e.type === 'gameOver')).toBe(true);
    expect(s.winnerTeam).toBe(1);
  });

  it('ends the game when the enemy keep falls', () => {
    const s = createState(
      makeLevel(['C.', '..'], {
        units: [u(1, 0, 'soldier', 1), u(1, 1, 'soldier', 2)],
        owners: [{ x: 0, y: 0, owner: 2 }],
        victory: [{ type: 'captureHQ' }],
      }),
    );
    applyAction(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 1, y: 0 }, { x: 0, y: 0 }],
      command: { ability: 'capture' },
    });
    expect(s.phase).toBe('over');
    expect(s.winnerTeam).toBe(1);
  });

  it('resolves surviveTurns without requiring a global turn limit', () => {
    const s = createState(
      makeLevel(['...'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
        victory: [{ type: 'surviveTurns', turns: 1 }],
      }),
    );
    applyAction(s, { kind: 'endTurn' });
    expect(s.phase).toBe('playing');
    applyAction(s, { kind: 'endTurn' });
    expect(s.phase).toBe('over');
    expect(s.winnerTeam).toBe(1);
    expect(s.endReason).toContain('坚守 1 回合');
  });
});

describe('session', () => {
  it('undoes a command but not a turn change', () => {
    const level = makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] });
    const session = new GameSession(level);
    const id = session.state.units[0].id;
    session.dispatch(wait(id, [{ x: 0, y: 0 }, { x: 1, y: 0 }]));
    expect(session.unit(id)!.x).toBe(1);
    expect(session.canUndo).toBe(true);
    session.undo();
    expect(session.unit(id)!.x).toBe(0);
    expect(session.unit(id)!.done).toBe(false);
    session.dispatch({ kind: 'endTurn' });
    expect(session.canUndo).toBe(false);
  });

  it('keeps state intact when an illegal action is rejected', () => {
    const level = makeLevel(['.........'], { units: [u(0, 0, 'soldier', 1), u(8, 0, 'soldier', 2)] });
    const session = new GameSession(level);
    const before = JSON.stringify(session.state);
    expect(session.tryDispatch(wait(session.state.units[0].id, [{ x: 0, y: 0 }, { x: 6, y: 0 }]))).toBeNull();
    expect(JSON.stringify(session.state)).toBe(before);
  });
});

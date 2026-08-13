import { describe, expect, it } from 'vitest';
import { applyAction, IllegalActionError } from '../actions';
import { makeLevel, testCommands, testForecast, testState, u } from './fixtures';
import { WEAPON_USES_RESOURCE } from '../resources';

describe('weapon actions', () => {
  it('offers and executes the weapon whose range covers the target', () => {
    const state = testState(
      makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] }),
    );
    const attacker = state.units[0];
    const option = testCommands(state, attacker, { x: 0, y: 0 }).find(
      (entry) => entry.weapon === 'soldier_javelin',
    );
    expect(option?.targets).toContainEqual({ x: 2, y: 0 });

    const expected = testForecast(state, attacker, state.units[1], { x: 0, y: 0 }, 'soldier_javelin');
    const events = applyAction(state, {
      kind: 'command',
      unit: attacker.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'soldier_javelin', target: { x: 2, y: 0 } },
    });

    expect(events.find((event) => event.type === 'attack')).toMatchObject({
      weapon: 'soldier_javelin',
      damage: expected.strike.damage,
    });
    expect(attacker.weaponState.soldier_javelin.resources[WEAPON_USES_RESOURCE].current).toBe(1);
  });

  it('rejects a mismatched weapon instead of silently using the default', () => {
    const state = testState(
      makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] }),
    );
    expect(() =>
      applyAction(state, {
        kind: 'command',
        unit: state.units[0].id,
        path: [{ x: 0, y: 0 }],
        command: { ability: 'attack', weapon: 'soldier_sword', target: { x: 2, y: 0 } },
      }),
    ).toThrow(IllegalActionError);
  });

  it('ticks owner-turn cooldowns and restores the weapon on schedule', () => {
    const state = testState(
      makeLevel(['...'], { units: [u(0, 0, 'mage', 1), u(2, 0, 'ogre', 2)] }),
    );
    const mage = state.units[0];
    applyAction(state, {
      kind: 'command',
      unit: mage.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'mage_overcharge', target: { x: 2, y: 0 } },
    });
    expect(mage.weaponState.mage_overcharge.cooldownRemaining).toBe(2);

    applyAction(state, { kind: 'endTurn' });
    applyAction(state, { kind: 'endTurn' });
    expect(mage.weaponState.mage_overcharge.cooldownRemaining).toBe(1);
    expect(testCommands(state, mage, { x: 0, y: 0 }).some((entry) => entry.weapon === 'mage_overcharge')).toBe(false);

    applyAction(state, { kind: 'endTurn' });
    applyAction(state, { kind: 'endTurn' });
    expect(mage.weaponState.mage_overcharge.cooldownRemaining).toBe(0);
    expect(testCommands(state, mage, { x: 0, y: 0 }).some((entry) => entry.weapon === 'mage_overcharge')).toBe(true);
  });

  it('lets a defender choose its strongest ready counter weapon', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'mage', 2)] }),
    );
    const exchange = testForecast(state, state.units[0], state.units[1]);
    expect(exchange.counter?.weapon).toBe('mage_overcharge');

    state.units[1].weaponState.mage_overcharge.cooldownRemaining = 1;
    const fallback = testForecast(state, state.units[0], state.units[1]);
    expect(fallback.counter?.weapon).toBe('mage_bolt');
  });

  it('enforces direct-fire line of sight while arcane and arcing profiles remain data choices', () => {
    const blocked = testState(
      makeLevel(['.#..'], { units: [u(0, 0, 'ballista', 1), u(3, 0, 'soldier', 2)] }),
    );
    expect(
      testCommands(blocked, blocked.units[0], { x: 0, y: 0 }).some(
        (entry) => entry.weapon === 'ballista_bolt',
      ),
    ).toBe(false);

    const clear = testState(
      makeLevel(['....'], { units: [u(0, 0, 'ballista', 1), u(3, 0, 'soldier', 2)] }),
    );
    expect(
      testCommands(clear, clear.units[0], { x: 0, y: 0 }).some(
        (entry) => entry.weapon === 'ballista_bolt',
      ),
    ).toBe(true);
  });
});

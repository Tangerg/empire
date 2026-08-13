import { describe, expect, it } from 'vitest';
import { applyAction, commandOptions, IllegalActionError } from '../actions';
import { computeDamage } from '../combat';
import { createBattleEngine } from '../engine';
import {
  awardRankProgress,
  ThresholdRankProgressionPolicy,
} from '../progression';
import { createState } from '../state';
import type { GameEvent } from '../types';
import { makeLevel, u } from './fixtures';
import { MOMENTUM_RESOURCE } from '../resources';

describe('battle-local rank and hero momentum', () => {
  it('promotes through the compact rank ladder and feeds the combat modifier pipeline', () => {
    const state = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'ogre', 2)] }),
    );
    const attacker = state.units[0];
    const defender = state.units[1];
    const rookieDamage = computeDamage(state, attacker, defender).damage;
    const events: GameEvent[] = [];

    awardRankProgress(attacker, 120, (event) => events.push(event));
    expect(attacker.rank).toBe(1);
    expect(computeDamage(state, attacker, defender).damage).toBeGreaterThan(rookieDamage);
    awardRankProgress(attacker, 200, (event) => events.push(event));
    expect(attacker.rank).toBe(2);
    expect(events.filter((event) => event.type === 'rankChanged')).toEqual([
      { type: 'rankChanged', unit: attacker.id, from: 0, to: 1 },
      { type: 'rankChanged', unit: attacker.id, from: 1, to: 2 },
    ]);
  });

  it('injects rank thresholds through BattleEngine and awards capture progress authoritatively', () => {
    const engine = createBattleEngine({
      progression: new ThresholdRankProgressionPolicy(10, 30),
    });
    const state = engine.createState(
      makeLevel(['.v.'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
      }),
    );
    const events = engine.dispatch(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
      command: { ability: 'capture' },
    });
    expect(state.units[0]).toMatchObject({ rank: 2, rankProgress: 60 });
    expect(events).toContainEqual({ type: 'rankChanged', unit: state.units[0].id, from: 0, to: 2 });
  });

  it('keeps a signature weapon hidden below its gate, then spends momentum on commit', () => {
    const level = makeLevel(['..'], {
      units: [
        {
          ...u(0, 0, 'knight', 1),
          resources: { [MOMENTUM_RESOURCE]: { current: 119, capacity: 150 } },
        },
        u(1, 0, 'ogre', 2),
      ],
    });
    const state = createState(level);
    const hero = state.units[0];
    const target = state.units[1];
    expect(commandOptions(state, hero, hero).some((option) => option.weapon === 'heroic_breakthrough')).toBe(false);
    expect(() => applyAction(state, {
      kind: 'command',
      unit: hero.id,
      path: [{ x: hero.x, y: hero.y }],
      command: { ability: 'attack', weapon: 'heroic_breakthrough', target },
    })).toThrow(IllegalActionError);

    hero.resources[MOMENTUM_RESOURCE].current = 120;
    expect(commandOptions(state, hero, hero).some((option) => option.weapon === 'heroic_breakthrough')).toBe(true);
    const events = applyAction(state, {
      kind: 'command',
      unit: hero.id,
      path: [{ x: hero.x, y: hero.y }],
      command: { ability: 'attack', weapon: 'heroic_breakthrough', target },
    });
    expect(hero.weaponState.heroic_breakthrough.cooldownRemaining).toBe(3);
    expect(events).toContainEqual({
      type: 'resourceChanged',
      resource: MOMENTUM_RESOURCE,
      subject: { kind: 'unit', id: hero.id },
      amount: -20,
      current: 100,
    });
    expect(events).toContainEqual(expect.objectContaining({
      type: 'resourceChanged', resource: MOMENTUM_RESOURCE, amount: 5,
    }));
    expect(events).toContainEqual(expect.objectContaining({
      type: 'resourceChanged', resource: MOMENTUM_RESOURCE, amount: 3,
    }));
    expect(hero.resources[MOMENTUM_RESOURCE].current).toBe(108);
  });

  it('does not expose hero-only weapons to an ordinary unit without a momentum pool', () => {
    const state = createState(
      makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'ogre', 2)] }),
    );
    expect(commandOptions(state, state.units[0], state.units[0])
      .some((option) => option.weapon === 'heroic_breakthrough')).toBe(false);
  });
});

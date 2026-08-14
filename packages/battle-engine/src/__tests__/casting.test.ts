import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { cloneContentCatalog } from '../content-pack';
import { activeCasts, castOf, isCharging } from '../casting';
import { SpellCastEntity } from '../domain/spell-cast';
import { hashState } from '../replay';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';
import type { ContentCatalog } from '../content-pack';
import type { BattleEngine } from '../engine';

/**
 * Charge time.
 *
 * A charged weapon locks a tile now and strikes it later. Delay is counted in
 * *actor turns*, so one content pack means the same thing whether turns belong
 * to whole sides or to single units, and everything else about the strike — the
 * damage pipeline, the area, the cost — stays the ordinary weapon path.
 */

const meteor = (castTurns = 2): ContentCatalog => {
  const catalog = cloneContentCatalog(TEST_CONTENT);
  catalog.weapons.override('mage_meteor', { castTurns });
  return catalog;
};

const duel = (rules: Record<string, unknown> = {}): LevelData =>
  makeLevel(['........'], {
    units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 1), u(4, 0, 'soldier', 2), u(7, 0, 'soldier', 2)],
    rules,
  });

const cast = (unit: number, at: { x: number; y: number }): Action => ({
  kind: 'command',
  unit,
  path: [{ x: 0, y: 0 }],
  command: { ability: 'attack', weapon: 'mage_meteor', target: at },
});

/** Runs actor turns until the predicate holds, so a test reads in turns not dispatches. */
function passTurns(battle: BattleEngine, state: GameState, turns: number): void {
  const target = state.actorTurns + turns;
  let guard = 0;
  while (state.actorTurns < target && state.phase === 'playing' && guard++ < 40) {
    battle.dispatch(state, { kind: 'endTurn' });
  }
}

describe('a charged weapon commits now and lands later', () => {
  it('marks the tile instead of striking it', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const [mage, , victim] = state.units;
    const hpBefore = victim.hp;

    const events = battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));

    expect(events.map((event) => event.type)).toContain('castBegan');
    expect(events.some((event) => event.type === 'attack')).toBe(false);
    expect(victim.hp).toBe(hpBefore);
    expect(activeCasts(state)[0].target).toEqual({ x: 4, y: 0 });
    expect(new SpellCastEntity(castOf(state, mage.id)!).remainingAt(state.actorTurns)).toBe(2);
  });

  it('strikes the tile once the charge completes', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const [mage, , victim] = state.units;
    const hpBefore = victim.hp;
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));

    passTurns(battle, state, 1);
    expect(victim.hp, 'must not land early').toBe(hpBefore);

    passTurns(battle, state, 1);
    expect(victim.hp).toBeLessThan(hpBefore);
    expect(state.pendingCasts).toHaveLength(0);
  });

  it('pays the weapon cost when it lands, not when it is committed', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const mage = state.units[0];
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));
    expect(mage.weaponState.mage_meteor.cooldownRemaining, 'a cast that never lands spends nothing').toBe(0);

    passTurns(battle, state, 2);
    expect(mage.weaponState.mage_meteor.cooldownRemaining).toBeGreaterThan(0);
  });

  it('hits the tile, so a target that walks away is missed', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const [mage, , victim] = state.units;
    const hpBefore = victim.hp;
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));

    // The defender's own turn is the window the charge opens for them.
    battle.dispatch(state, { kind: 'endTurn' });
    battle.dispatch(state, {
      kind: 'command',
      unit: victim.id,
      path: [{ x: 4, y: 0 }, { x: 5, y: 0 }, { x: 6, y: 0 }],
      command: { ability: 'wait' },
    });
    passTurns(battle, state, 1);

    expect(victim.hp, 'stepped clear of the marked tile').toBe(hpBefore);
    expect(state.pendingCasts).toHaveLength(0);
  });
});

describe('charging is a cost, not a free delay', () => {
  it('keeps the caster from acting while it charges', () => {
    const battle = createBattleEngine({ content: meteor(4) });
    const state = battle.createState(duel());
    const [mage, ally] = state.units;
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));

    passTurns(battle, state, 2); // back to the caster's side, charge still running
    expect(isCharging(state, mage)).toBe(true);
    expect(battle.canAct(state, mage), 'a charging unit is not refreshed').toBe(false);
    expect(battle.canAct(state, ally), 'its allies are unaffected').toBe(true);
    expect(() => battle.dispatch(state, { kind: 'face', unit: mage.id, facing: 'south' })).toThrow();
  });

  it('refuses a second cast from the same caster', () => {
    const battle = createBattleEngine({ content: meteor(4) });
    const state = battle.createState(duel());
    const mage = state.units[0];
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));
    expect(() => battle.dispatch(state, cast(mage.id, { x: 5, y: 0 }))).toThrow();
    expect(state.pendingCasts).toHaveLength(1);
  });
});

describe('a cast nobody sustains does not land', () => {
  it('fizzles when the caster falls, and stops counting as active at once', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const [mage, , victim] = state.units;
    const hpBefore = victim.hp;
    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));

    state.units = state.units.filter((unit) => unit.id !== mage.id);
    // Orphaned immediately for every reader, swept at the next boundary.
    expect(activeCasts(state)).toHaveLength(0);
    expect(state.pendingCasts).toHaveLength(1);

    const events = [
      ...battle.dispatch(state, { kind: 'endTurn' }),
      ...battle.dispatch(state, { kind: 'endTurn' }),
    ];
    expect(events).toContainEqual(expect.objectContaining({ type: 'castCancelled', reason: 'casterLost' }));
    expect(victim.hp).toBe(hpBefore);
    expect(state.pendingCasts).toHaveLength(0);
  });
});

describe('charge time works under either turn order', () => {
  it('counts actor turns, so a per-unit order lands it sooner in the round', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const mage = state.units.find((unit) => unit.type === 'mage')!;
    // Give the mage the floor whatever its charge happened to be.
    state.turnOrder.activeUnit = mage.id;
    state.currentPlayer = mage.owner;

    battle.dispatch(state, cast(mage.id, { x: 4, y: 0 }));
    const committed = castOf(state, mage.id)!;
    expect(committed.resolveAt - committed.declaredAt).toBe(2);

    passTurns(battle, state, 2);
    expect(state.pendingCasts).toHaveLength(0);
  });
});

describe('casts are part of the deterministic record', () => {
  it('survives a clone and changes the state hash', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const before = hashState(state);
    battle.dispatch(state, cast(state.units[0].id, { x: 4, y: 0 }));

    expect(hashState(state)).not.toBe(before);
    const copy = battle.cloneState(state);
    expect(hashState(copy)).toBe(hashState(state));
    copy.pendingCasts[0].target.x = 6;
    expect(state.pendingCasts[0].target.x, 'the clone must own its own casts').toBe(4);
  });

  it('rolls back with the action that committed it', () => {
    const battle = createBattleEngine({ content: meteor() });
    const state = battle.createState(duel());
    const mage = state.units[0];
    // An illegal aim point must leave no half-committed cast behind.
    expect(() => battle.dispatch(state, cast(mage.id, { x: 0, y: 0 }))).toThrow();
    expect(state.pendingCasts).toHaveLength(0);
  });
});

import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { IllegalActionError } from '../action-system';
import { INITIATIVE_THRESHOLD, InitiativeTurnOrder, SideTurnOrder, TurnOrders } from '../turn-order';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';

const engine = () => createBattleEngine({ content: TEST_CONTENT });

const duel = (rules: Record<string, unknown> = {}): LevelData =>
  makeLevel(['.......', '.......'], {
    units: [
      u(0, 0, 'soldier', 1),
      u(1, 0, 'rogue', 1),
      u(6, 0, 'soldier', 2),
      u(6, 1, 'ballista', 2),
    ],
    rules,
  });

const wait = (unit: number, x: number, y: number): Action =>
  ({ kind: 'command', unit, path: [{ x, y }], command: { ability: 'wait' } });

const actorIds = (state: GameState) => engine().actors(state).map((unit) => unit.id);

describe('turn order as a policy', () => {
  it('registers both families and defaults to side turns', () => {
    expect(TurnOrders.ids()).toEqual(expect.arrayContaining(['side', 'initiative']));
    const state = engine().createState(duel());
    expect(state.turnOrder.policy).toBe('side');
    expect(state.turnOrder.activeUnit).toBeNull();
  });

  it('keeps side turns entitling a whole army', () => {
    const battle = engine();
    const state = battle.createState(duel());
    const mine = state.units.filter((unit) => unit.owner === 1).map((unit) => unit.id);
    expect(battle.actors(state).map((unit) => unit.id).sort()).toEqual(mine.sort());
    for (const unit of state.units.filter((candidate) => candidate.owner === 1)) {
      expect(battle.canAct(state, unit)).toBe(true);
    }
    for (const unit of state.units.filter((candidate) => candidate.owner === 2)) {
      expect(battle.canAct(state, unit)).toBe(false);
    }
  });
});

describe('initiative turn order', () => {
  it('entitles exactly one unit at a time', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));

    expect(state.turnOrder.policy).toBe('initiative');
    expect(state.turnOrder.activeUnit).not.toBeNull();
    expect(actorIds(state)).toHaveLength(1);
    expect(state.currentPlayer).toBe(
      state.units.find((unit) => unit.id === state.turnOrder.activeUnit)!.owner,
    );
  });

  it('refuses a command from a unit that has no entitlement', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const active = state.turnOrder.activeUnit!;
    const other = state.units.find(
      (unit) => unit.owner === state.currentPlayer && unit.id !== active,
    );
    if (!other) return; // the active side may legitimately own a single unit
    expect(() => battle.dispatch(state, wait(other.id, other.x, other.y)))
      .toThrow(IllegalActionError);
  });

  it('hands the turn to the next charged unit and lets everyone act over time', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const seen: number[] = [];

    for (let step = 0; step < 24; step += 1) {
      const active = state.turnOrder.activeUnit;
      if (active === null) break;
      seen.push(active);
      const unit = state.units.find((candidate) => candidate.id === active)!;
      battle.dispatch(state, wait(unit.id, unit.x, unit.y));
      battle.dispatch(state, { kind: 'endTurn' });
    }

    expect(seen).toHaveLength(24);
    // Every unit gets turns, and faster units get more of them.
    expect(new Set(seen).size).toBe(4);
    const rogue = state.units.find((unit) => unit.type === 'rogue')!;
    const ballista = state.units.find((unit) => unit.type === 'ballista')!;
    const rogueTurns = seen.filter((id) => id === rogue.id).length;
    const ballistaTurns = seen.filter((id) => id === ballista.id).length;
    expect(rogueTurns).toBeGreaterThan(ballistaTurns);
  });

  it('is fully deterministic: the same battle replays the same order', () => {
    const order = () => {
      const battle = engine();
      const state = battle.createState(duel({ turnOrder: 'initiative' }));
      const seen: number[] = [];
      for (let step = 0; step < 10; step += 1) {
        const active = state.turnOrder.activeUnit!;
        seen.push(active);
        const unit = state.units.find((candidate) => candidate.id === active)!;
        battle.dispatch(state, wait(unit.id, unit.x, unit.y));
        battle.dispatch(state, { kind: 'endTurn' });
      }
      return seen;
    };
    expect(order()).toEqual(order());
  });

  it('previews the upcoming order without mutating state', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const before = JSON.stringify(state.turnOrder);
    const preview = battle.turnOrderPreview(state, 6);

    expect(preview).toHaveLength(6);
    expect(preview[0].id).toBe(state.turnOrder.activeUnit);
    expect(JSON.stringify(state.turnOrder)).toBe(before);
  });

  it('advances the battle clock once per full charge lap, not per actor turn', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const startTurn = state.turn;

    // Four units acting once each must not cost four rounds.
    for (let step = 0; step < 4; step += 1) {
      const unit = state.units.find((candidate) => candidate.id === state.turnOrder.activeUnit)!;
      battle.dispatch(state, wait(unit.id, unit.x, unit.y));
      battle.dispatch(state, { kind: 'endTurn' });
    }
    expect(state.turn).toBeLessThan(startTurn + 4);
  });

  it('keeps a departed unit from holding the turn hostage', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    const active = state.turnOrder.activeUnit!;

    state.units = state.units.filter((unit) => unit.id !== active);
    battle.dispatch(state, { kind: 'endTurn' });

    expect(state.turnOrder.activeUnit).not.toBe(active);
    expect(state.turnOrder.data[`charge.${active}`]).toBeUndefined();
  });

  it('lets the AI plan only for the entitled unit', () => {
    const battle = engine();
    const state = battle.createState(duel({ turnOrder: 'initiative' }));
    for (const player of state.players) player.controller = 'ai';

    for (let guard = 0; guard < 200 && state.phase === 'playing'; guard += 1) {
      const action = battle.chooseAiAction(state);
      // dispatch throws on an action the ordering policy forbids
      battle.dispatch(state, action);
    }
    expect(state.turn).toBeGreaterThan(1);
  });

  it('exposes its charge threshold and speed model as content-tunable', () => {
    expect(INITIATIVE_THRESHOLD).toBe(100);
    expect(SideTurnOrder.id).toBe('side');
    expect(InitiativeTurnOrder.id).toBe('initiative');
  });
});
